using MujeresUnidas.Server.Models.Publications;
using MujeresUnidas.Server.Models.Publications.Repositories;
using MujeresUnidas.Server.Infrastructure.Database;
using MujeresUnidas.Server.Models.User.Services;
using MujeresUnidas.Server.Models.User.Repositories;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddScoped<Database>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<PostRepository>();
builder.Services.AddScoped<Post>();

var app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
// Allow HTTP only for the local Android emulator during Development.
// Do not send real participant or case data over this transport.
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.MapGet("/health", () => Results.Ok(new { status = "ready", app = "MujeresUnidas.Server" }));
app.MapControllers();
app.Run();
