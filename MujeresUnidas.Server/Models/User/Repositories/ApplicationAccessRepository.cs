using System.Data;
using Microsoft.Data.SqlClient;
using MujeresUnidas.Server.Infrastructure.Database;

namespace MujeresUnidas.Server.Models.User.Repositories;

/// <summary>One identity, separate approval per application; never duplicate passwords.</summary>
public sealed class ApplicationAccessRepository(Database database)
{
    public sealed record AccessRow(Guid AccountId,string DisplayName,string Email,string ApplicationCode,string RoleCode,string AccessStatus);
    public async Task<IReadOnlyList<AccessRow>> ListAsync()
    {
        var result=new List<AccessRow>();
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"SELECT x.AccountId,a.DisplayName,a.Email,x.ApplicationCode,x.RoleCode,x.AccessStatus
FROM dbo.ApplicationAccess x JOIN dbo.Accounts a ON a.Id=x.AccountId ORDER BY x.ApplicationCode,x.CreatedUtc DESC",db);
        await using var r=await cmd.ExecuteReaderAsync();
        while(await r.ReadAsync())result.Add(new(r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5)));
        return result;
    }
    /// <summary>Management professionals joining Community are explicitly authorized as Professional, never Staff.</summary>
    public async Task<bool> GrantCommunityAsync(Guid accountId,Guid actorId)
    {
        await using var db=await database.OpenAsync();
        await using var tx=(SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable);
        await using var cmd=new SqlCommand(@"IF EXISTS(SELECT 1 FROM dbo.Accounts a
WHERE a.Id=@account AND a.Kind IN(1,2) AND a.Status=1
AND (a.Kind=2 OR EXISTS(SELECT 1 FROM dbo.ActivationVisits v WHERE v.AccountId=a.Id AND v.IdentityChecked=1))
AND NOT EXISTS(SELECT 1 FROM dbo.ApplicationAccess x WHERE x.AccountId=a.Id AND x.ApplicationCode=N'Community' AND x.AccessStatus=N'Active'))
BEGIN
 MERGE dbo.ApplicationAccess WITH(HOLDLOCK) AS x
 USING(SELECT @account AccountId,N'Community' ApplicationCode) AS s
 ON x.AccountId=s.AccountId AND x.ApplicationCode=s.ApplicationCode
 WHEN MATCHED THEN UPDATE SET RoleCode=N'Professional',AccessStatus=N'Active',GrantedBy=@actor,GrantedUtc=SYSUTCDATETIME()
 WHEN NOT MATCHED THEN INSERT(AccountId,ApplicationCode,RoleCode,AccessStatus,GrantedBy,GrantedUtc)
 VALUES(@account,N'Community',N'Professional',N'Active',@actor,SYSUTCDATETIME());
 SELECT 1;
END ELSE SELECT 0;",db,tx);
        cmd.Parameters.Add("@account",SqlDbType.UniqueIdentifier).Value=accountId;
        cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actorId;
        var ok=Convert.ToInt32(await cmd.ExecuteScalarAsync())==1;
        if(ok)
        {
            await using var profile=new SqlCommand(@"INSERT dbo.ApplicationProfiles(AccountId,ApplicationCode,DisplayName)
SELECT a.Id,N'Community',a.DisplayName FROM dbo.Accounts a WHERE a.Id=@account
AND NOT EXISTS(SELECT 1 FROM dbo.ApplicationProfiles p WHERE p.AccountId=a.Id AND p.ApplicationCode=N'Community')",db,tx);
            profile.Parameters.Add("@account",SqlDbType.UniqueIdentifier).Value=accountId;
            await profile.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();return ok;
    }
    public async Task<bool> SuspendAsync(Guid accountId,string application,Guid actor)
    {
        if(application is not ("Community" or "Management"))return false;
        await using var db=await database.OpenAsync();
        await using var tx=(SqlTransaction)await db.BeginTransactionAsync();
        await using var cmd=new SqlCommand(@"UPDATE dbo.ApplicationAccess SET AccessStatus=N'Suspended'
WHERE AccountId=@id AND ApplicationCode=@app AND AccessStatus=N'Active' AND RoleCode<>N'Owner';
IF @@ROWCOUNT=1 BEGIN
 DELETE FROM dbo.Sessions WHERE AccountId=@id AND ApplicationCode=@app;
 SELECT 1;
END ELSE SELECT 0;",db,tx);
        cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=accountId;
        cmd.Parameters.Add("@app",SqlDbType.NVarChar,12).Value=application;
        var result=Convert.ToInt32(await cmd.ExecuteScalarAsync())==1;
        await tx.CommitAsync();return result;
    }
}
