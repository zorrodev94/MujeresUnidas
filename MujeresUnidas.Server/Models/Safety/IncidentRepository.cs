using System.Data;
using Microsoft.Data.SqlClient;
using MujeresUnidas.Server.Infrastructure.Database;

namespace MujeresUnidas.Server.Models.Safety
{
    /// <summary>Non-emergency incident records for the supervised educational demo; not a police report channel.</summary>
    public sealed class IncidentRepository(Database database)
    {
        #region DTO
        public sealed record CreateIncidentData(string Category, string Summary);
        public sealed record IncidentData(Guid Id, string Category, string Summary, string Status, DateTime CreatedUtc);
        public sealed record StatusData(string Status);
        #endregion

        #region Repository
        private static readonly string[] Categories = ["Violence", "Theft", "Harassment", "Other"];
        private static readonly string[] Statuses = ["Received", "UnderReview", "Closed"];

        public async Task<Guid?> AddAsync(Guid ownerId, CreateIncidentData data)
        {
            string category = (data.Category ?? string.Empty).Trim();
            string summary = (data.Summary ?? string.Empty).Trim();
            if (!Categories.Contains(category) || summary.Length is < 15 or > 900) return null;
            Guid id = Guid.NewGuid();
            await using var db = await database.OpenAsync();
            await using var cmd = new SqlCommand(@"INSERT dbo.IncidentReports(Id,OwnerId,Category,Summary,Status)
VALUES(@id,@owner,@category,@summary,N'Received')", db);
            cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;
            cmd.Parameters.Add("@owner", SqlDbType.UniqueIdentifier).Value = ownerId;
            cmd.Parameters.Add("@category", SqlDbType.NVarChar, 25).Value = category;
            cmd.Parameters.Add("@summary", SqlDbType.NVarChar, 900).Value = summary;
            await cmd.ExecuteNonQueryAsync();
            return id;
        }

        public async Task<IReadOnlyList<IncidentData>> MineAsync(Guid ownerId)
        {
            var rows = new List<IncidentData>();
            await using var db = await database.OpenAsync();
            await using var cmd = new SqlCommand(@"SELECT TOP(30) Id,Category,Summary,Status,CreatedUtc
FROM dbo.IncidentReports WHERE OwnerId=@owner ORDER BY CreatedUtc DESC", db);
            cmd.Parameters.Add("@owner", SqlDbType.UniqueIdentifier).Value = ownerId;
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) rows.Add(new IncidentData(reader.GetGuid(0), reader.GetString(1),
                reader.GetString(2), reader.GetString(3), reader.GetDateTime(4)));
            return rows;
        }

        public async Task<IReadOnlyList<IncidentData>> ManagementListAsync()
        {
            // Deliberately excludes account identification for the hackathon overview.
            var rows = new List<IncidentData>();
            await using var db = await database.OpenAsync();
            await using var cmd = new SqlCommand(@"SELECT TOP(50) Id,Category,Summary,Status,CreatedUtc
FROM dbo.IncidentReports ORDER BY CreatedUtc DESC", db);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) rows.Add(new IncidentData(reader.GetGuid(0), reader.GetString(1),
                reader.GetString(2), reader.GetString(3), reader.GetDateTime(4)));
            return rows;
        }

        public async Task<bool> UpdateStatusAsync(Guid id, string status)
        {
            if (!Statuses.Contains(status)) return false;
            await using var db = await database.OpenAsync();
            await using var cmd = new SqlCommand(@"UPDATE dbo.IncidentReports SET Status=@status,
UpdatedUtc=SYSUTCDATETIME() WHERE Id=@id", db);
            cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;
            cmd.Parameters.Add("@status", SqlDbType.NVarChar, 18).Value = status;
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        #endregion
    }
}
