using System.Data;
using Microsoft.Data.SqlClient;
using MujeresUnidas.Server.Infrastructure.Database;

namespace MujeresUnidas.Server.Models.Publications.Management;

public sealed record ManagementMoment(Guid Id,Guid AuthorId,string Author,string Body,DateTime CreatedUtc,int Likes,int Comments,bool Liked,bool Saved);
public sealed record ManagementProfile(Guid Id,string DisplayName,string? Role,int Followers,int Following,bool Followed);
public sealed record ManagementComment(Guid Id,Guid AuthorId,string Author,string Body,DateTime CreatedUtc);
public sealed record MomentCreate(string Body);
public sealed record CommentCreate(string Body);

/// <summary>Staff-only social context. Never shares records with CommunityPosts or UserFollows.</summary>
public sealed class ManagementSocialRepository(Database database)
{
    private const string ManagementActive=@"EXISTS(SELECT 1 FROM dbo.Accounts a
JOIN dbo.ApplicationAccess x ON x.AccountId=a.Id AND x.ApplicationCode=N'Management' AND x.AccessStatus=N'Active'
WHERE a.Id=@actor AND a.Kind IN(1,2) AND a.Status=1)";
    public async Task<IReadOnlyList<ManagementMoment>> MomentsAsync(Guid actor,bool savedOnly=false)
    {
        var list=new List<ManagementMoment>();
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"SELECT TOP(100) m.Id,m.AuthorId,a.DisplayName,m.Body,m.CreatedUtc,
(SELECT COUNT(*) FROM dbo.ManagementMomentReactions r WHERE r.MomentId=m.Id),
(SELECT COUNT(*) FROM dbo.ManagementMomentComments c WHERE c.MomentId=m.Id AND c.HiddenUtc IS NULL),
CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.ManagementMomentReactions r WHERE r.MomentId=m.Id AND r.AccountId=@actor) THEN 1 ELSE 0 END),
CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.ManagementSavedMoments s WHERE s.MomentId=m.Id AND s.AccountId=@actor) THEN 1 ELSE 0 END)
FROM dbo.ManagementMoments m JOIN dbo.Accounts a ON a.Id=m.AuthorId
WHERE m.HiddenUtc IS NULL AND ( @saved=0 OR EXISTS(SELECT 1 FROM dbo.ManagementSavedMoments s WHERE s.MomentId=m.Id AND s.AccountId=@actor))
ORDER BY m.CreatedUtc DESC",db);
        cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
        cmd.Parameters.Add("@saved",SqlDbType.Bit).Value=savedOnly;
        await using var r=await cmd.ExecuteReaderAsync();
        while(await r.ReadAsync())list.Add(new(r.GetGuid(0),r.GetGuid(1),r.GetString(2),r.GetString(3),r.GetDateTime(4),r.GetInt32(5),r.GetInt32(6),r.GetBoolean(7),r.GetBoolean(8)));
        return list;
    }
    public async Task<Guid?> CreateAsync(Guid actor,string body)
    {
        if(string.IsNullOrWhiteSpace(body)||body.Trim().Length>1200)return null;
        var id=Guid.NewGuid();await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"INSERT dbo.ManagementMoments(Id,AuthorId,Body)
SELECT @id,@actor,@body WHERE "+ManagementActive,db);
        cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
        cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
        cmd.Parameters.Add("@body",SqlDbType.NVarChar,1200).Value=body.Trim();
        return await cmd.ExecuteNonQueryAsync()==1?id:null;
    }
    public async Task<bool> SetLikeAsync(Guid actor,Guid id,bool value)
    {
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(value?@"IF EXISTS(SELECT 1 FROM dbo.ManagementMoments WHERE Id=@id AND HiddenUtc IS NULL)
AND NOT EXISTS(SELECT 1 FROM dbo.ManagementMomentReactions WHERE MomentId=@id AND AccountId=@actor)
INSERT dbo.ManagementMomentReactions(MomentId,AccountId) VALUES(@id,@actor)":@"DELETE FROM dbo.ManagementMomentReactions WHERE MomentId=@id AND AccountId=@actor",db);
        cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
        return await cmd.ExecuteNonQueryAsync()>0;
    }
    public async Task<bool> SetSavedAsync(Guid actor,Guid id,bool value)
    {
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(value?@"IF EXISTS(SELECT 1 FROM dbo.ManagementMoments WHERE Id=@id AND HiddenUtc IS NULL)
AND NOT EXISTS(SELECT 1 FROM dbo.ManagementSavedMoments WHERE MomentId=@id AND AccountId=@actor)
INSERT dbo.ManagementSavedMoments(MomentId,AccountId) VALUES(@id,@actor)":@"DELETE FROM dbo.ManagementSavedMoments WHERE MomentId=@id AND AccountId=@actor",db);
        cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
        return await cmd.ExecuteNonQueryAsync()>0;
    }
    public async Task<bool> CommentAsync(Guid actor,Guid id,string body)
    {
        if(string.IsNullOrWhiteSpace(body)||body.Trim().Length>600)return false;
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"INSERT dbo.ManagementMomentComments(Id,MomentId,AuthorId,Body)
SELECT NEWID(),@id,@actor,@body WHERE EXISTS(SELECT 1 FROM dbo.ManagementMoments WHERE Id=@id AND HiddenUtc IS NULL)",db);
        cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
        cmd.Parameters.Add("@body",SqlDbType.NVarChar,600).Value=body.Trim();return await cmd.ExecuteNonQueryAsync()==1;
    }
    public async Task<IReadOnlyList<ManagementComment>> CommentsAsync(Guid id)
    {
        var list=new List<ManagementComment>();await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"SELECT c.Id,c.AuthorId,a.DisplayName,c.Body,c.CreatedUtc
FROM dbo.ManagementMomentComments c JOIN dbo.Accounts a ON a.Id=c.AuthorId
JOIN dbo.ManagementMoments m ON m.Id=c.MomentId AND m.HiddenUtc IS NULL
WHERE c.MomentId=@id AND c.HiddenUtc IS NULL ORDER BY c.CreatedUtc",db);
        cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
        await using var r=await cmd.ExecuteReaderAsync();
        while(await r.ReadAsync())list.Add(new(r.GetGuid(0),r.GetGuid(1),r.GetString(2),r.GetString(3),r.GetDateTime(4)));
        return list;
    }
    public async Task<bool> HideAsync(Guid actor,Guid id,bool moderator)
    {
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"UPDATE dbo.ManagementMoments SET HiddenUtc=SYSUTCDATETIME()
WHERE Id=@id AND HiddenUtc IS NULL AND (AuthorId=@actor OR @moderator=1)",db);
        cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
        cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
        cmd.Parameters.Add("@moderator",SqlDbType.Bit).Value=moderator;
        return await cmd.ExecuteNonQueryAsync()==1;
    }
    public async Task<IReadOnlyList<ManagementProfile>> SearchAsync(Guid actor,string term)
    {
        var list=new List<ManagementProfile>();await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"SELECT TOP(100) a.Id,a.DisplayName,a.StaffRole,
(SELECT COUNT(*) FROM dbo.ManagementFollows WHERE FollowedId=a.Id),
(SELECT COUNT(*) FROM dbo.ManagementFollows WHERE FollowerId=a.Id),
CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.ManagementFollows f WHERE f.FollowerId=@actor AND f.FollowedId=a.Id) THEN 1 ELSE 0 END)
FROM dbo.Accounts a WHERE a.Kind IN(1,2) AND a.Status=1 AND a.DisplayName LIKE @term ORDER BY a.DisplayName",db);
        cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
        cmd.Parameters.Add("@term",SqlDbType.NVarChar,110).Value="%"+term.Replace("[","[[]").Replace("%","[%]").Replace("_","[_]")+"%";
        await using var r=await cmd.ExecuteReaderAsync();
        while(await r.ReadAsync())list.Add(new(r.GetGuid(0),r.GetString(1),r.IsDBNull(2)?"Owner":r.GetString(2),r.GetInt32(3),r.GetInt32(4),r.GetBoolean(5)));
        return list;
    }
    public async Task<bool> FollowAsync(Guid actor,Guid id,bool follow)
    {
        if(actor==id)return false;
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(follow?@"IF EXISTS(SELECT 1 FROM dbo.Accounts WHERE Id=@id AND Kind IN(1,2) AND Status=1)
AND NOT EXISTS(SELECT 1 FROM dbo.ManagementFollows WHERE FollowerId=@actor AND FollowedId=@id)
INSERT dbo.ManagementFollows(FollowerId,FollowedId) VALUES(@actor,@id)":@"DELETE FROM dbo.ManagementFollows WHERE FollowerId=@actor AND FollowedId=@id",db);
        cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
        cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
        return await cmd.ExecuteNonQueryAsync()>0;
    }
}
