using System.Data;
using Microsoft.Data.SqlClient;
using MujeresUnidas.Server.Infrastructure.Database;

namespace MujeresUnidas.Server.Models.Badges;

public sealed record BadgeSummary(Guid Id,string Name,string? Description,string? ImageKey,string Scope);
public sealed record BadgeAwardView(Guid BadgeId,string Name,string? Description,string Scope,DateTime AwardedUtc);
public sealed class BadgeRepository(Database database):IBadgeManager
{
    public async Task<IReadOnlyList<BadgeDefinition>> ListAsync()
    {
        var rows=new List<BadgeDefinition>();await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand("SELECT Id,Name,Description,COALESCE(ImageKey,IconKey),VisibilityScope,IsActive FROM dbo.Badges ORDER BY Name",db);
        await using var r=await cmd.ExecuteReaderAsync();
        while(await r.ReadAsync())rows.Add(new(r.GetGuid(0),r.GetString(1),r.IsDBNull(2)?null:r.GetString(2),r.IsDBNull(3)?null:r.GetString(3),r.GetString(4),r.GetBoolean(5)));
        return rows;
    }
    public async Task<IReadOnlyList<BadgeAwardView>> AwardsAsync(Guid account)
    {
        var rows=new List<BadgeAwardView>();await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"SELECT b.Id,b.Name,b.Description,b.VisibilityScope,u.ReviewedUtc
FROM dbo.UserBadges u JOIN dbo.Badges b ON b.Id=u.BadgeId
WHERE u.UserId=@id AND u.Status=N'Approved' AND b.IsActive=1 ORDER BY u.ReviewedUtc DESC",db);
        cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=account;
        await using var r=await cmd.ExecuteReaderAsync();
        while(await r.ReadAsync())rows.Add(new(r.GetGuid(0),r.GetString(1),r.IsDBNull(2)?null:r.GetString(2),r.GetString(3),r.IsDBNull(4)?DateTime.MinValue:r.GetDateTime(4)));
        return rows;
    }
    public async Task<bool> AwardAsync(Guid actorId,Guid accountId,Guid badgeId,string reason)
    {
        if(actorId==Guid.Empty||accountId==Guid.Empty||badgeId==Guid.Empty||string.IsNullOrWhiteSpace(reason)||reason.Length>300)return false;
        await using var db=await database.OpenAsync();
        await using var tx=(SqlTransaction)await db.BeginTransactionAsync();
        await using var cmd=new SqlCommand(@"IF EXISTS(SELECT 1 FROM dbo.Accounts WHERE Id=@account AND Status=1)
AND EXISTS(SELECT 1 FROM dbo.Badges WHERE Id=@badge AND IsActive=1)
BEGIN
 MERGE dbo.UserBadges WITH(HOLDLOCK) AS t
 USING(SELECT @account UserId,@badge BadgeId) AS s ON t.UserId=s.UserId AND t.BadgeId=s.BadgeId
 WHEN MATCHED THEN UPDATE SET Status=N'Approved',ReviewedBy=@actor,ReviewedUtc=SYSUTCDATETIME()
 WHEN NOT MATCHED THEN INSERT(UserId,BadgeId,RequestedBy,ReviewedBy,ReviewedUtc,Status)
 VALUES(@account,@badge,@actor,@actor,SYSUTCDATETIME(),N'Approved');
 INSERT dbo.BadgeAwardEvents(AccountId,BadgeId,ActorId,EventType,Reason)
 VALUES(@account,@badge,@actor,N'Award',@reason);
 SELECT 1;
END ELSE SELECT 0",db,tx);
        cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actorId;
        cmd.Parameters.Add("@account",SqlDbType.UniqueIdentifier).Value=accountId;
        cmd.Parameters.Add("@badge",SqlDbType.UniqueIdentifier).Value=badgeId;
        cmd.Parameters.Add("@reason",SqlDbType.NVarChar,300).Value=reason.Trim();
        var ok=Convert.ToInt32(await cmd.ExecuteScalarAsync())==1;
        await tx.CommitAsync();return ok;
    }
}
