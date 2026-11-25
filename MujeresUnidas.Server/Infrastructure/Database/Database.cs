using Microsoft.Data.SqlClient;
namespace MujeresUnidas.Server.Infrastructure.Database
{
/// <summary>Opens parameterized SQL Server connections; no credentials are shipped to clients.</summary>
public sealed class Database(IConfiguration configuration)
{
    public async Task<SqlConnection> OpenAsync(CancellationToken ct = default)
    {
        string value = configuration.GetConnectionString("Main")
            ?? throw new InvalidOperationException("ConnectionStrings:Main is not configured. See README.md.");
        var connection = new SqlConnection(value);
        await connection.OpenAsync(ct);
        return connection;
    }
}
}
