using System.Data;
using Microsoft.Data.SqlClient;
using MujeresUnidas.Server.Infrastructure.Database;
using MujeresUnidas.Server.Models.Publications.Engagement;

namespace MujeresUnidas.Server.Models.Publications.Repositories
{
    /// <summary>Owns SQL state for likes, bookmarks and comments on APPROVED community posts.</summary>
    public sealed class PostEngagementRepository(Database database)
    {
        #region Operation Data
        #endregion Operation Data
        #region Initialization
        #endregion Initialization
        #region Arrays
        #endregion Arrays
        #region Structs
        #endregion Structs
        #region Task
        public async Task<PostEngagement.SummaryData?> ReadAsync(Guid post,Guid viewer)
        {
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"SELECT
(SELECT COUNT(*) FROM dbo.PostReactions WHERE PostId=@post) AS Likes,
(SELECT COUNT(*) FROM dbo.PostComments WHERE PostId=@post AND DeletedUtc IS NULL) AS Comments,
CASE WHEN EXISTS(SELECT 1 FROM dbo.PostReactions WHERE PostId=@post AND AccountId=@viewer) THEN 1 ELSE 0 END AS Liked,
CASE WHEN EXISTS(SELECT 1 FROM dbo.PostBookmarks WHERE PostId=@post AND AccountId=@viewer) THEN 1 ELSE 0 END AS Saved
WHERE EXISTS(SELECT 1 FROM dbo.CommunityPosts WHERE Id=@post AND Status=1)",db);
            cmd.Parameters.Add("@post",SqlDbType.UniqueIdentifier).Value=post;
            cmd.Parameters.Add("@viewer",SqlDbType.UniqueIdentifier).Value=viewer;
            int likes,comments;bool liked,saved;
            await using(var reader=await cmd.ExecuteReaderAsync())
            {
                if(!await reader.ReadAsync())return null;
                likes=reader.GetInt32(0);comments=reader.GetInt32(1);
                liked=reader.GetInt32(2)==1;saved=reader.GetInt32(3)==1;
            }
            var recent=new List<PostEngagement.CommentData>();
            await using var latest=new SqlCommand(@"SELECT TOP(20) c.Id,c.AuthorId,a.DisplayName,c.Body,c.CreatedUtc
FROM dbo.PostComments c INNER JOIN dbo.Accounts a ON a.Id=c.AuthorId
WHERE c.PostId=@post AND c.DeletedUtc IS NULL
ORDER BY c.CreatedUtc DESC,c.Id DESC",db);
            latest.Parameters.Add("@post",SqlDbType.UniqueIdentifier).Value=post;
            await using var rows=await latest.ExecuteReaderAsync();
            while(await rows.ReadAsync())recent.Add(new(rows.GetGuid(0),rows.GetGuid(1),
                rows.GetString(2),rows.GetString(3),rows.GetDateTime(4),rows.GetGuid(1)==viewer));
            return new(likes,comments,liked,saved,recent);
        }
        public async Task<bool> SetReactionAsync(Guid post,Guid viewer,bool liked)
            =>await SetFlagAsync("PostReactions",post,viewer,liked);
        public async Task<bool> SetBookmarkAsync(Guid post,Guid viewer,bool saved)
            =>await SetFlagAsync("PostBookmarks",post,viewer,saved);

        /// <summary>Uses a hardcoded allowlist of table names and parameterized account/post keys.</summary>
        private async Task<bool> SetFlagAsync(string table,Guid post,Guid viewer,bool value)
        {
            if(table is not ("PostReactions" or "PostBookmarks"))return false;
            await using var db=await database.OpenAsync();
            string sql=value
                ?$@"IF EXISTS(SELECT 1 FROM dbo.CommunityPosts WHERE Id=@post AND Status=1)
BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.{table} WHERE PostId=@post AND AccountId=@actor)
    INSERT dbo.{table}(PostId,AccountId) VALUES(@post,@actor)
  SELECT CAST(1 AS int)
END ELSE SELECT CAST(0 AS int)"
                :$@"DELETE FROM dbo.{table} WHERE PostId=@post AND AccountId=@actor;
SELECT CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.CommunityPosts WHERE Id=@post AND Status=1) THEN 1 ELSE 0 END AS int)";
            await using var cmd=new SqlCommand(sql,db);
            cmd.Parameters.Add("@post",SqlDbType.UniqueIdentifier).Value=post;
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=viewer;
            try {return Convert.ToInt32(await cmd.ExecuteScalarAsync())==1;}
            catch(SqlException ex) when(ex.Number is 2601 or 2627){return true;}
        }
        public async Task<Guid?> AddCommentAsync(Guid post,Guid author,string body)
        {
            var id=Guid.NewGuid();
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"INSERT dbo.PostComments(Id,PostId,AuthorId,Body)
SELECT @id,p.Id,@author,@body FROM dbo.CommunityPosts p
WHERE p.Id=@post AND p.Status=1",db);
            cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
            cmd.Parameters.Add("@post",SqlDbType.UniqueIdentifier).Value=post;
            cmd.Parameters.Add("@author",SqlDbType.UniqueIdentifier).Value=author;
            cmd.Parameters.Add("@body",SqlDbType.NVarChar,600).Value=body;
            return await cmd.ExecuteNonQueryAsync()==1?id:null;
        }
        public async Task<bool> RemoveCommentAsync(Guid post,Guid comment,Guid actor,bool staff)
        {
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"UPDATE dbo.PostComments SET DeletedUtc=SYSUTCDATETIME()
WHERE PostId=@post AND Id=@comment AND DeletedUtc IS NULL
AND (AuthorId=@actor OR @moderator=1)",db);
            cmd.Parameters.Add("@post",SqlDbType.UniqueIdentifier).Value=post;
            cmd.Parameters.Add("@comment",SqlDbType.UniqueIdentifier).Value=comment;
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
            cmd.Parameters.Add("@moderator",SqlDbType.Bit).Value=staff;
            return await cmd.ExecuteNonQueryAsync()==1;
        }
        #endregion Task
    }
}
