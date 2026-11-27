using System.Data;
using Microsoft.Data.SqlClient;
using MujeresUnidas.Server.Infrastructure.Database;

namespace MujeresUnidas.Server.Models.User.Profiles
{
    /// <summary>Public directory, social follows and references to private media.</summary>
    public sealed class ProfileRepository(Database database)
    {
        #region Task
        public async Task<IReadOnlyList<CommunityProfile.ProfileData>> SearchAsync(Guid actor,string query)
        {
            var people=new List<CommunityProfile.ProfileData>();
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"SELECT TOP (30) a.Id,COALESCE(ap.DisplayName,a.DisplayName),COALESCE(ap.Biography,N''),x.RoleCode,
(SELECT COUNT(*) FROM dbo.UserFollows WHERE FollowedId=a.Id),
(SELECT COUNT(*) FROM dbo.UserFollows WHERE FollowerId=a.Id),
CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.UserFollows WHERE FollowerId=@actor AND FollowedId=a.Id) THEN 1 ELSE 0 END),
CONVERT(bit,CASE WHEN a.Id=@actor THEN 1 ELSE 0 END),
CONVERT(bit,CASE WHEN p.AvatarFile IS NULL THEN 0 ELSE 1 END),
CONVERT(bit,CASE WHEN p.CoverFile IS NULL THEN 0 ELSE 1 END)
FROM dbo.Accounts a
JOIN dbo.ApplicationAccess x ON x.AccountId=a.Id AND x.ApplicationCode=N'Community' AND x.AccessStatus=N'Active'
LEFT JOIN dbo.ApplicationProfiles ap ON ap.AccountId=a.Id AND ap.ApplicationCode=N'Community'
LEFT JOIN dbo.ApplicationProfileMedia p ON p.AccountId=a.Id AND p.ApplicationCode=N'Community'
WHERE a.Status=1 AND COALESCE(ap.DisplayName,a.DisplayName) LIKE @q
ORDER BY a.DisplayName",db);
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
            cmd.Parameters.Add("@q",SqlDbType.NVarChar,110).Value="%"+query.Replace("[","[[]").Replace("%","[%]").Replace("_","[_]")+"%";
            await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync())people.Add(Read(r));
            return people;
        }
        public async Task<CommunityProfile.ProfileData?> GetAsync(Guid actor,Guid target)
        {
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"SELECT a.Id,COALESCE(ap.DisplayName,a.DisplayName),COALESCE(ap.Biography,N''),x.RoleCode,
(SELECT COUNT(*) FROM dbo.UserFollows WHERE FollowedId=a.Id),
(SELECT COUNT(*) FROM dbo.UserFollows WHERE FollowerId=a.Id),
CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.UserFollows WHERE FollowerId=@actor AND FollowedId=a.Id) THEN 1 ELSE 0 END),
CONVERT(bit,CASE WHEN a.Id=@actor THEN 1 ELSE 0 END),
CONVERT(bit,CASE WHEN p.AvatarFile IS NULL THEN 0 ELSE 1 END),
CONVERT(bit,CASE WHEN p.CoverFile IS NULL THEN 0 ELSE 1 END)
FROM dbo.Accounts a
JOIN dbo.ApplicationAccess x ON x.AccountId=a.Id AND x.ApplicationCode=N'Community' AND x.AccessStatus=N'Active'
LEFT JOIN dbo.ApplicationProfiles ap ON ap.AccountId=a.Id AND ap.ApplicationCode=N'Community'
LEFT JOIN dbo.ApplicationProfileMedia p ON p.AccountId=a.Id AND p.ApplicationCode=N'Community'
WHERE a.Status=1 AND a.Id=@target",db);
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
            cmd.Parameters.Add("@target",SqlDbType.UniqueIdentifier).Value=target;
            await using var r=await cmd.ExecuteReaderAsync();
            return await r.ReadAsync()?Read(r):null;
        }
        public async Task<bool> FollowAsync(Guid actor,Guid target)
        {
            if(actor==target)return false;
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"BEGIN TRANSACTION;
IF EXISTS(SELECT 1 FROM dbo.Accounts a JOIN dbo.ApplicationAccess x ON x.AccountId=a.Id
 WHERE a.Id=@actor AND a.Status=1 AND x.ApplicationCode=N'Community' AND x.AccessStatus=N'Active')
AND EXISTS(SELECT 1 FROM dbo.Accounts a JOIN dbo.ApplicationAccess x ON x.AccountId=a.Id
 WHERE a.Id=@target AND a.Status=1 AND x.ApplicationCode=N'Community' AND x.AccessStatus=N'Active')
AND NOT EXISTS(SELECT 1 FROM dbo.UserFollows WITH(UPDLOCK,HOLDLOCK) WHERE FollowerId=@actor AND FollowedId=@target)
BEGIN
 INSERT dbo.UserFollows(FollowerId,FollowedId) VALUES(@actor,@target);
 INSERT dbo.Notifications(Id,RecipientId,ActorId,Type,Title,RelatedId)
 SELECT NEWID(),@target,@actor,N'Follower',N'Una persona comenzó a seguirte.',@actor;
 SELECT CAST(1 AS bit);
END
ELSE SELECT CAST(0 AS bit);
COMMIT TRANSACTION;",db);
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
            cmd.Parameters.Add("@target",SqlDbType.UniqueIdentifier).Value=target;
            return Convert.ToBoolean(await cmd.ExecuteScalarAsync());
        }
        public async Task<bool> UnfollowAsync(Guid actor,Guid target)
        {
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand("DELETE FROM dbo.UserFollows WHERE FollowerId=@actor AND FollowedId=@target",db);
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
            cmd.Parameters.Add("@target",SqlDbType.UniqueIdentifier).Value=target;
            return await cmd.ExecuteNonQueryAsync()==1;
        }
        public async Task<IReadOnlyList<CommunityProfile.ProfileData>> ConnectionsAsync(Guid actor,Guid target,bool followers)
        {
            var people=new List<CommunityProfile.ProfileData>();
            await using var db=await database.OpenAsync();
            string join=followers?"f.FollowerId=a.Id AND f.FollowedId=@target":"f.FollowedId=a.Id AND f.FollowerId=@target";
            await using var cmd=new SqlCommand(@"SELECT TOP(80) a.Id,COALESCE(ap.DisplayName,a.DisplayName),COALESCE(ap.Biography,N''),x.RoleCode,
(SELECT COUNT(*) FROM dbo.UserFollows WHERE FollowedId=a.Id),
(SELECT COUNT(*) FROM dbo.UserFollows WHERE FollowerId=a.Id),
CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.UserFollows WHERE FollowerId=@actor AND FollowedId=a.Id) THEN 1 ELSE 0 END),
CONVERT(bit,CASE WHEN a.Id=@actor THEN 1 ELSE 0 END),
CONVERT(bit,CASE WHEN p.AvatarFile IS NULL THEN 0 ELSE 1 END),
CONVERT(bit,CASE WHEN p.CoverFile IS NULL THEN 0 ELSE 1 END)
FROM dbo.UserFollows f JOIN dbo.Accounts a ON " + join + @"
JOIN dbo.ApplicationAccess x ON x.AccountId=a.Id AND x.ApplicationCode=N'Community' AND x.AccessStatus=N'Active'
LEFT JOIN dbo.ApplicationProfiles ap ON ap.AccountId=a.Id AND ap.ApplicationCode=N'Community'
LEFT JOIN dbo.ApplicationProfileMedia p ON p.AccountId=a.Id AND p.ApplicationCode=N'Community'
WHERE a.Status=1 ORDER BY a.DisplayName",db);
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
            cmd.Parameters.Add("@target",SqlDbType.UniqueIdentifier).Value=target;
            await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync())people.Add(Read(r));
            return people;
        }
        public async Task<string?> MediaFileAsync(Guid id,bool avatar,string application="Community")
        {
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand("SELECT "+(avatar?"AvatarFile":"CoverFile")+" FROM dbo.ApplicationProfileMedia WHERE AccountId=@id AND ApplicationCode=@app",db);
            cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
            cmd.Parameters.Add("@app",SqlDbType.NVarChar,12).Value=application;
            var value=await cmd.ExecuteScalarAsync();
            return value is null or DBNull?null:Convert.ToString(value);
        }
        public async Task<string?> SetMediaAsync(Guid id,bool avatar,string key,string application="Community")
        {
            await using var db=await database.OpenAsync();
            await using var tx=(SqlTransaction)await db.BeginTransactionAsync();
            await using var read=new SqlCommand("SELECT "+(avatar?"AvatarFile":"CoverFile")+" FROM dbo.ApplicationProfileMedia WITH(UPDLOCK,HOLDLOCK) WHERE AccountId=@id AND ApplicationCode=@app",db,tx);
            read.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
            read.Parameters.Add("@app",SqlDbType.NVarChar,12).Value=application;
            var old=await read.ExecuteScalarAsync();
            await using var upsert=new SqlCommand(@"IF EXISTS(SELECT 1 FROM dbo.ApplicationProfileMedia WHERE AccountId=@id AND ApplicationCode=@app)
 UPDATE dbo.ApplicationProfileMedia SET "+(avatar?"AvatarFile":"CoverFile")+@"=@key,UpdatedUtc=SYSUTCDATETIME() WHERE AccountId=@id AND ApplicationCode=@app
ELSE INSERT dbo.ApplicationProfileMedia(AccountId,ApplicationCode,"+(avatar?"AvatarFile":"CoverFile")+") VALUES(@id,@app,@key)",db,tx);
            upsert.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
            upsert.Parameters.Add("@app",SqlDbType.NVarChar,12).Value=application;
            upsert.Parameters.Add("@key",SqlDbType.NVarChar,90).Value=key;
            await upsert.ExecuteNonQueryAsync();
            await tx.CommitAsync();
            return old is null or DBNull?null:Convert.ToString(old);
        }
        private static CommunityProfile.ProfileData Read(SqlDataReader r) => new(
            r.GetGuid(0),r.GetString(1),r.GetString(2),r.IsDBNull(3)?"Member":r.GetString(3),
            r.GetInt32(4),r.GetInt32(5),r.GetBoolean(6),r.GetBoolean(7),r.GetBoolean(8),r.GetBoolean(9));
        #endregion Task
    }
}
