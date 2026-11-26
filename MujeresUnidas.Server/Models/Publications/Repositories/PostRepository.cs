using System.Data;
using Microsoft.Data.SqlClient;
using MujeresUnidas.Server.Infrastructure.Database;

namespace MujeresUnidas.Server.Models.Publications.Repositories
{
    /// <summary>SQL Server repository for moderated community publications and their audit states.</summary>
    public sealed class PostRepository
    {
        #region Operation Data
        private readonly Database _database;
        private const string Fields = @"SELECT TOP(60) p.Id,p.Category,p.Title,p.Summary,p.Body,
COALESCE(a.DisplayName,N'Equipo EarlyAccess') AS AuthorName,p.Status,
p.IsFeatured,p.IsExample,p.CreatedUtc
FROM dbo.CommunityPosts p LEFT JOIN dbo.Accounts a ON a.Id=p.AuthorId ";
        #endregion Operation Data

        #region Initialization
        public PostRepository(Database database) { _database = database; }
        #endregion Initialization

        #region Task
        /// <summary>Returns approved content, bounded by a maximum row count.</summary>
        public async Task<IReadOnlyList<Post.ViewData>> GetFeedAsync(string? category, bool featured)
        {
            await using var db = await _database.OpenAsync();
            string sql = Fields + @" WHERE p.Status=1 AND (@category IS NULL OR p.Category=@category)
AND (@featured=0 OR p.IsFeatured=1) ORDER BY p.CreatedUtc DESC";
            await using var cmd = new SqlCommand(sql, db);
            cmd.Parameters.Add("@category", SqlDbType.NVarChar, 12).Value = (object?)category ?? DBNull.Value;
            cmd.Parameters.Add("@featured", SqlDbType.Bit).Value = featured;
            var rows = await ReadAsync(cmd);
            return featured ? rows.Take(3).ToArray() : rows;
        }

        /// <summary>Returns the current member's non-removed posts and statuses.</summary>
        public async Task<IReadOnlyList<Post.ViewData>> GetMineAsync(Guid authorId)
        {
            await using var db = await _database.OpenAsync();
            await using var cmd = new SqlCommand(Fields +
                " WHERE p.AuthorId=@author AND p.Status<>3 ORDER BY p.CreatedUtc DESC", db);
            cmd.Parameters.Add("@author", SqlDbType.UniqueIdentifier).Value = authorId;
            return await ReadAsync(cmd);
        }

        /// <summary>Returns unreviewed entries for the Community Staff queue.</summary>
        public async Task<IReadOnlyList<Post.ViewData>> GetPendingAsync()
        {
            await using var db = await _database.OpenAsync();
            await using var cmd = new SqlCommand(Fields +
                " WHERE p.Status=0 ORDER BY p.CreatedUtc ASC", db);
            return await ReadAsync(cmd);
        }

        private static async Task<IReadOnlyList<Post.ViewData>> ReadAsync(SqlCommand cmd)
        {
            var rows = new List<Post.ViewData>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                byte status = reader.GetByte(6);
                rows.Add(new Post.ViewData(reader.GetGuid(0), reader.GetString(1),
                    reader.GetString(2), reader.GetString(3), reader.GetString(4),
                    reader.GetString(5), status switch { 0=>"Pendiente", 1=>"Aprobada", 2=>"Rechazada", _=>"Retirada" },
                    reader.GetBoolean(7), reader.GetBoolean(8), reader.GetDateTime(9)));
            }
            return rows;
        }

        /// <summary>Persists a real member submission; approved Staff entries are immediately visible.</summary>
        public async Task<Guid> InsertAsync(Guid authorId, Post.CreateData data, bool staff)
        {
            var id = Guid.NewGuid();
            await using var db = await _database.OpenAsync();
            await using var cmd = new SqlCommand(@"INSERT dbo.CommunityPosts
(Id,AuthorId,Category,Title,Summary,Body,Status,IsFeatured,IsExample)
VALUES(@id,@author,@category,@title,@summary,@body,@status,0,0)", db);
            cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;
            cmd.Parameters.Add("@author", SqlDbType.UniqueIdentifier).Value = authorId;
            cmd.Parameters.Add("@category", SqlDbType.NVarChar, 12).Value = data.Category;
            cmd.Parameters.Add("@title", SqlDbType.NVarChar, 120).Value = data.Title.Trim();
            cmd.Parameters.Add("@summary", SqlDbType.NVarChar, 260).Value = data.Summary.Trim();
            cmd.Parameters.Add("@body", SqlDbType.NVarChar, 2500).Value = data.Body.Trim();
            cmd.Parameters.Add("@status", SqlDbType.TinyInt).Value = (byte)(staff ? 1 : 0);
            await cmd.ExecuteNonQueryAsync();
            return id;
        }

        /// <summary>Uses a server-side predicate for member ownership or staff authorization.</summary>
        public async Task<bool> UpdateAsync(Guid postId, Guid actorId, bool staff, Post.EditData data)
        {
            await using var db = await _database.OpenAsync();
            await using var cmd = new SqlCommand(@"UPDATE dbo.CommunityPosts
SET Title=@title,Summary=@summary,Body=@body,UpdatedUtc=SYSUTCDATETIME(),
Status=@status,IsFeatured=0,ModeratedBy=NULL,ModeratedUtc=NULL
WHERE Id=@id AND Status<>3 AND (@staff=1 OR AuthorId=@actor)", db);
            cmd.Parameters.Add("@title", SqlDbType.NVarChar, 120).Value = data.Title.Trim();
            cmd.Parameters.Add("@summary", SqlDbType.NVarChar, 260).Value = data.Summary.Trim();
            cmd.Parameters.Add("@body", SqlDbType.NVarChar, 2500).Value = data.Body.Trim();
            cmd.Parameters.Add("@status", SqlDbType.TinyInt).Value = (byte)(staff ? 1 : 0);
            cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = postId;
            cmd.Parameters.Add("@actor", SqlDbType.UniqueIdentifier).Value = actorId;
            cmd.Parameters.Add("@staff", SqlDbType.Bit).Value = staff;
            return await cmd.ExecuteNonQueryAsync() == 1;
        }

        /// <summary>Soft-deletes a publication rather than physically erasing its trace.</summary>
        public async Task<bool> DeleteAsync(Guid postId, Guid actorId, bool staff)
        {
            await using var db = await _database.OpenAsync();
            await using var cmd = new SqlCommand(@"UPDATE dbo.CommunityPosts SET Status=3,IsFeatured=0,
UpdatedUtc=SYSUTCDATETIME() WHERE Id=@id AND Status<>3 AND (@staff=1 OR AuthorId=@actor)",db);
            cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=postId;
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actorId;
            cmd.Parameters.Add("@staff",SqlDbType.Bit).Value=staff;
            return await cmd.ExecuteNonQueryAsync()==1;
        }

        /// <summary>Only pending requests may be approved or declined by a staff reviewer.</summary>
        public async Task<bool> ReviewAsync(Guid postId, Guid reviewerId, Post.ReviewData data)
        {
            await using var db = await _database.OpenAsync();
            await using var cmd = new SqlCommand(@"UPDATE dbo.CommunityPosts
SET Status=@status,IsFeatured=@featured,ModeratedBy=@reviewer,
ModeratedUtc=SYSUTCDATETIME() WHERE Id=@id AND Status=0",db);
            cmd.Parameters.Add("@status",SqlDbType.TinyInt).Value=(byte)(data.Approved ? 1 : 2);
            cmd.Parameters.Add("@featured",SqlDbType.Bit).Value=data.Approved && data.Featured;
            cmd.Parameters.Add("@reviewer",SqlDbType.UniqueIdentifier).Value=reviewerId;
            cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=postId;
            return await cmd.ExecuteNonQueryAsync()==1;
        }
        #endregion Task
    }
}
