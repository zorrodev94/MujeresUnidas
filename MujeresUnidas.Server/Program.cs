using MujeresUnidas.Server.Infrastructure.Database;
using MujeresUnidas.Server.Models.User.Services;
using MujeresUnidas.Server.Models.User.Repositories;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddScoped<Database>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<AccountService>();

var app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.UseHttpsRedirection();
app.MapGet("/health", () => Results.Ok(new { status = "ready", app = "MujeresUnidas.Server" }));
app.MapControllers();
app.Run();
