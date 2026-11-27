using System.Data;
using Microsoft.Data.SqlClient;
using MujeresUnidas.Server.Infrastructure.Database;

namespace MujeresUnidas.Server.Models.Sites.Repositories
{
    /// <summary>SQL-backed demo sites: safe, scoped mutations; case and appointment site consistency.</summary>
    public sealed class CareSiteRepository(Database database)
    {
        #region Queries
        public async Task<IReadOnlyList<SedeAtencion>> ListAsync(bool activeOnly = true)
        {
            var sites = new List<SedeAtencion>();
            await using var db = await database.OpenAsync();
            await using var cmd = new SqlCommand(@"SELECT Id,Code,DisplayName,SiteType,Municipality,StateName,AddressNote,IsDemo,IsActive,ZoneId,Latitude,Longitude
FROM dbo.CareSites WHERE (@active=0 OR IsActive=1) ORDER BY Code",db);
            cmd.Parameters.Add("@active",SqlDbType.Bit).Value=activeOnly;
            await using var r = await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync())sites.Add(new SedeAtencion
            {
                Id=r.GetGuid(0),Code=r.GetString(1),DisplayName=r.GetString(2),SiteType=r.GetString(3),
                Municipality=r.GetString(4),StateName=r.GetString(5),AddressNote=r.GetString(6),
                IsDemo=r.GetBoolean(7),IsActive=r.GetBoolean(8),ZoneId=r.IsDBNull(9)?null:r.GetGuid(9),
                Latitude=r.IsDBNull(10)?null:r.GetDecimal(10),Longitude=r.IsDBNull(11)?null:r.GetDecimal(11)
            });
            return sites;
        }
        #endregion

        #region Mutations
        public async Task<bool> CreateAsync(SedeAtencion.CreateData data)
        {
            if(!SedeAtencion.Types.Contains(data.SiteType) || string.IsNullOrWhiteSpace(data.Code) ||
               !System.Text.RegularExpressions.Regex.IsMatch(data.Code,"^[A-Za-z0-9-]{3,24}$") ||
               string.IsNullOrWhiteSpace(data.DisplayName) || data.DisplayName.Trim().Length >120 ||
               (data.AddressNote?.Length??0)>180)return false;
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"INSERT dbo.CareSites(Id,Code,DisplayName,SiteType,AddressNote)
VALUES(NEWID(),@code,@name,@type,@address)",db);
            cmd.Parameters.Add("@code",SqlDbType.NVarChar,24).Value=data.Code.Trim().ToUpperInvariant();
            cmd.Parameters.Add("@name",SqlDbType.NVarChar,120).Value=data.DisplayName.Trim();
            cmd.Parameters.Add("@type",SqlDbType.NVarChar,24).Value=data.SiteType;
            cmd.Parameters.Add("@address",SqlDbType.NVarChar,180).Value=string.IsNullOrWhiteSpace(data.AddressNote)
                ?"Ubicación por confirmar · DEMOSTRACIÓN":data.AddressNote.Trim();
            try{return await cmd.ExecuteNonQueryAsync()==1;}
            catch(SqlException ex) when(ex.Number is 2601 or 2627){return false;}
        }

        /// <summary>Reassigns a case with an immutable audit of the previous site.
        /// Historical appointment SiteId values are never rewritten.</summary>
        public async Task<bool> AssignCaseAsync(Guid caseId,Guid siteId,Guid actorId,string? reason)
        {
            var rationale=string.IsNullOrWhiteSpace(reason)?"Asignación o traslado institucional de demostración":reason.Trim();
            if(caseId==Guid.Empty||siteId==Guid.Empty||actorId==Guid.Empty||rationale.Length>250)return false;
            await using var db=await database.OpenAsync();
            await using var tx=(SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable);
            await using var cmd=new SqlCommand(@"UPDATE c SET SiteId=@site
OUTPUT deleted.SiteId
FROM dbo.Cases c JOIN dbo.CareSites s ON s.Id=@site AND s.IsActive=1
WHERE c.Id=@case AND c.Status IN(N'UnderReview',N'InProgress')
AND (c.SiteId IS NULL OR c.SiteId<>@site)
AND (s.SiteType=N'MultiService' OR
 (c.Area=N'Psychology' AND s.SiteType=N'Psychology') OR
 (c.Area=N'Legal' AND s.SiteType=N'Legal') OR
 (c.Area=N'SocialWork' AND s.SiteType=N'Administrative') OR
 (c.Area=N'Other' AND s.SiteType=N'CommunityActivities'))
AND NOT EXISTS(SELECT 1 FROM dbo.Appointments a WHERE a.CaseId=c.Id AND a.Status IN(N'Scheduled',N'Confirmed'))",db,tx);
            cmd.Parameters.Add("@case",SqlDbType.UniqueIdentifier).Value=caseId;
            cmd.Parameters.Add("@site",SqlDbType.UniqueIdentifier).Value=siteId;
            object? old=await cmd.ExecuteScalarAsync();
            if(old is null){await tx.RollbackAsync();return false;}
            await using var audit=new SqlCommand(@"INSERT dbo.CaseSiteTransfers(CaseId,PreviousSiteId,NewSiteId,Reason,ChangedBy)
VALUES(@case,@old,@site,@reason,@actor)",db,tx);
            audit.Parameters.Add("@case",SqlDbType.UniqueIdentifier).Value=caseId;
            audit.Parameters.Add("@site",SqlDbType.UniqueIdentifier).Value=siteId;
            audit.Parameters.Add("@old",SqlDbType.UniqueIdentifier).Value=old is Guid g?g:DBNull.Value;
            audit.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actorId;
            audit.Parameters.Add("@reason",SqlDbType.NVarChar,250).Value=rationale;
            await audit.ExecuteNonQueryAsync();
            await tx.CommitAsync();return true;
        }

        public async Task<bool> SetZoneAsync(Guid siteId,Guid zoneId)
        {
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"UPDATE dbo.CareSites SET ZoneId=@zone
WHERE Id=@site AND IsActive=1 AND EXISTS(SELECT 1 FROM dbo.ServiceZones z WHERE z.Id=@zone AND z.IsActive=1)",db);
            cmd.Parameters.Add("@site",SqlDbType.UniqueIdentifier).Value=siteId;
            cmd.Parameters.Add("@zone",SqlDbType.UniqueIdentifier).Value=zoneId;
            return await cmd.ExecuteNonQueryAsync()==1;
        }

        public async Task<bool> AssignStaffAsync(Guid siteId,Guid staffId)
        {
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"MERGE dbo.CareSiteStaff WITH (HOLDLOCK) AS target
USING (SELECT @site SiteId,@staff StaffId) AS source
ON target.SiteId=source.SiteId AND target.StaffId=source.StaffId
WHEN MATCHED THEN UPDATE SET IsActive=1
WHEN NOT MATCHED BY TARGET THEN INSERT(SiteId,StaffId) VALUES(@site,@staff);",db);
            cmd.Parameters.Add("@site",SqlDbType.UniqueIdentifier).Value=siteId;
            cmd.Parameters.Add("@staff",SqlDbType.UniqueIdentifier).Value=staffId;
            // Avoid throwing for invalid staff/site by checking eligibility first.
            await using var eligible=new SqlCommand(@"SELECT COUNT(*) FROM dbo.CareSites s CROSS JOIN dbo.Accounts a
WHERE s.Id=@site AND s.IsActive=1 AND a.Id=@staff AND a.Kind IN(1,2) AND a.Status=1",db);
            eligible.Parameters.Add("@site",SqlDbType.UniqueIdentifier).Value=siteId;
            eligible.Parameters.Add("@staff",SqlDbType.UniqueIdentifier).Value=staffId;
            if(Convert.ToInt32(await eligible.ExecuteScalarAsync())!=1)return false;
            return await cmd.ExecuteNonQueryAsync()>0;
        }
        #endregion
    }
}
