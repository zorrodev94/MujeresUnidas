using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using MujeresUnidas.Server.Models.Publications;
using MujeresUnidas.Server.Models.Safety;
using MujeresUnidas.Server.Models.Messages.Repositories;
using MujeresUnidas.Server.Models.Messages;
using MujeresUnidas.Server.Models.User.Profiles;
using MujeresUnidas.Server.Models.Notifications;
using MujeresUnidas.Server.Models.Publications.Repositories;
using MujeresUnidas.Server.Infrastructure.Database;
using MujeresUnidas.Server.Models.User.Services;
using MujeresUnidas.Server.Models.User.Repositories;
using MujeresUnidas.Server.Models.Care.Repositories;
using MujeresUnidas.Server.Models.Sites.Repositories;
using MujeresUnidas.Server.Models.Reports;
using MujeresUnidas.Server.Models.Analytics;
using MujeresUnidas.Server.Models.Geography;
using MujeresUnidas.Server.Models.Publications.Management;
using MujeresUnidas.Server.Models.Badges;
using MujeresUnidas.Server.Models.Media;
using MujeresUnidas.Server.Contracts;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // cloudflared is expected to connect over loopback. Never trust forwarded
    // headers from arbitrary public clients or remote networks.
    options.ForwardedHeaders=ForwardedHeaders.XForwardedFor|ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(IPAddress.Loopback);
    options.KnownProxies.Add(IPAddress.IPv6Loopback);
    options.ForwardLimit=1;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode=StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter=PartitionedRateLimiter.Create<HttpContext,string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",_ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit=600,Window=TimeSpan.FromMinutes(1),QueueLimit=0,AutoReplenishment=true
            }));
});
builder.Services.AddOpenApi();
builder.Services.AddScoped<Database>();
builder.Services.AddScoped<EmergencyContactSecurity>();
builder.Services.AddScoped<EmergencyContactRepository>();
builder.Services.AddScoped<IncidentRepository>();
builder.Services.AddScoped<CareRepository>();
builder.Services.AddScoped<CareSiteRepository>();
builder.Services.AddScoped<CareAnalyticsRepository>();
builder.Services.AddScoped<MujeresUnidas.Server.Models.Analytics.CommunityOutcomeRepository>();
builder.Services.AddScoped<MetricsRepository>();
builder.Services.AddScoped<CommunitySurveyRepository>();
builder.Services.AddScoped<ZoneRepository>();
builder.Services.AddScoped<ManagementSocialRepository>();
builder.Services.AddScoped<BadgeRepository>();
builder.Services.AddScoped<IMediaQuotaService,MediaQuotaService>();
builder.Services.AddScoped<MessageAttachmentRepository>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<ApplicationAccessRepository>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<PostRepository>();
builder.Services.AddScoped<PostEngagementRepository>();
builder.Services.AddScoped<ConversationRepository>();
builder.Services.AddScoped<ManagementConversationRepository>();
builder.Services.AddScoped<MessageSecurity>();
builder.Services.AddScoped<ProfileRepository>();
builder.Services.AddScoped<NotificationRepository>();
builder.Services.AddScoped<PrivateMediaStore>();
builder.Services.AddScoped<Post>();
builder.Services.AddScoped<PostMediaStore>();

var app = builder.Build();
app.UseForwardedHeaders();
app.UseRateLimiter();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
// Allow HTTP only for the local Android emulator during Development.
// Do not send real participant or case data over this transport.
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.MapGet("/health", () => Results.Ok(new { status = "alive", app = "MujeresUnidas.Server" }));
app.MapGet("/health/ready", async (AccountService accounts) =>
{
    try
    {
        return await accounts.SchemaCurrentAsync() ?
            Results.Ok(new {status="ready",schema="beta10",version="1.0.0"}):Results.StatusCode(503);
    }
    catch(Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException or InvalidOperationException)
    {
        return Results.StatusCode(503);
    }
});
// A staff invitation uses a temporary password: no privileged API calls until rotated.
app.Use(async (context,next) =>
{
    var path=context.Request.Path;
    if(!path.StartsWithSegments("/api/auth/change-password") &&
       !path.StartsWithSegments("/api/auth/logout") &&
       !path.StartsWithSegments("/api/auth/me") &&
       context.Request.Headers.Authorization.ToString().StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase))
    {
        var accounts=context.RequestServices.GetRequiredService<AccountService>();
        var user=await accounts.GetUserByTokenAsync(context.Request.Headers.Authorization.ToString());
        if(user?.MustChangePassword==true)
        {
            context.Response.StatusCode=428;
            await context.Response.WriteAsJsonAsync(new OperationResult("Debes cambiar tu contraseña temporal antes de continuar."));
            return;
        }
    }
    await next();
});
app.MapControllers();
app.Run();
