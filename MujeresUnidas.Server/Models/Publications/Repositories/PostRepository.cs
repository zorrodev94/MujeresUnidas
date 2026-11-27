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
p.IsFeatured,p.IsExample,p.CreatedUtc,p.Subcategory,p.EventMode
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

        /// <summary>Shows approved posts by a particular author to Community visitors.</summary>
        public async Task<IReadOnlyList<Post.ViewData>> GetByAuthorAsync(Guid authorId)
        {
            await using var db=await _database.OpenAsync();
            await using var cmd=new SqlCommand(Fields+
                " WHERE p.AuthorId=@author AND p.Status=1 ORDER BY p.CreatedUtc DESC",db);
            cmd.Parameters.Add("@author",SqlDbType.UniqueIdentifier).Value=authorId;
            return await ReadAsync(cmd);
        }

        /// <summary>Returns approved publications bookmarked by the authenticated community member.</summary>
        public async Task<IReadOnlyList<Post.ViewData>> GetSavedAsync(Guid viewer)
        {
            await using var db=await _database.OpenAsync();
            await using var cmd=new SqlCommand(Fields+@"
INNER JOIN dbo.PostBookmarks b ON b.PostId=p.Id
WHERE b.AccountId=@viewer AND p.Status=1 ORDER BY b.CreatedUtc DESC",db);
            cmd.Parameters.Add("@viewer",SqlDbType.UniqueIdentifier).Value=viewer;
            return await ReadAsync(cmd);
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
                " WHERE p.Status=0 AND p.Category IN(N'Blog',N'Moment') ORDER BY p.CreatedUtc ASC", db);
            return await ReadAsync(cmd);
        }

        /// <summary>Institutional review queue for guides, jobs and events; not visible to Community Staff.</summary>
        public async Task<IReadOnlyList<Post.ViewData>> GetInstitutionalPendingAsync()
        {
            await using var db=await _database.OpenAsync();
            await using var cmd=new SqlCommand(Fields+
                " WHERE p.Status=0 AND p.Category IN (N'Guide',N'Job',N'Event') ORDER BY p.CreatedUtc ASC",db);
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
                    reader.GetBoolean(7), reader.GetBoolean(8), reader.GetDateTime(9),
                    reader.IsDBNull(10)?null:reader.GetString(10),reader.IsDBNull(11)?null:reader.GetString(11)));
            }
            return rows;
        }

        /// <summary>Persists a real member submission; approved Staff entries are immediately visible.</summary>
        public async Task<Guid> InsertAsync(Guid authorId, Post.CreateData data, bool autoApprove)
        {
            var id = Guid.NewGuid();
            await using var db = await _database.OpenAsync();
            await using var cmd = new SqlCommand(@"INSERT dbo.CommunityPosts
(Id,AuthorId,Category,Subcategory,EventMode,Title,Summary,Body,Status,IsFeatured,IsExample)
VALUES(@id,@author,@category,@subcategory,@mode,@title,@summary,@body,@status,0,0)", db);
            cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;
            cmd.Parameters.Add("@author", SqlDbType.UniqueIdentifier).Value = authorId;
            cmd.Parameters.Add("@category", SqlDbType.NVarChar, 12).Value = data.Category;
            cmd.Parameters.Add("@subcategory",SqlDbType.NVarChar,48).Value=(object?)data.Subcategory??DBNull.Value;
            cmd.Parameters.Add("@mode",SqlDbType.NVarChar,16).Value=(object?)data.Mode??DBNull.Value;
            cmd.Parameters.Add("@title", SqlDbType.NVarChar, 120).Value = data.Title.Trim();
            cmd.Parameters.Add("@summary", SqlDbType.NVarChar, 260).Value = data.Summary.Trim();
            cmd.Parameters.Add("@body", SqlDbType.NVarChar, -1).Value = data.Body.Trim();
            cmd.Parameters.Add("@status", SqlDbType.TinyInt).Value = (byte)(autoApprove ? 1 : 0);
            await cmd.ExecuteNonQueryAsync();
            if(!autoApprove)
            {
                await using var notify=new SqlCommand(@"INSERT dbo.Notifications(Id,RecipientId,ActorId,Type,Title,RelatedId)
SELECT NEWID(),Id,@author,N'PostPending',N'Nueva publicación pendiente de moderación.',@post
FROM dbo.Accounts WHERE Kind=0 AND Status=1 AND CommunityRole=N'Staff' AND @category=N'Blog'
UNION ALL
SELECT NEWID(),Id,@author,N'PostPending',N'Nueva publicación institucional pendiente.',@post
FROM dbo.Accounts WHERE Status=1 AND @category<>N'Blog' AND
(Kind=2 OR (Kind=1 AND StaffRole=N'Administrator'))",db);
                notify.Parameters.Add("@author",SqlDbType.UniqueIdentifier).Value=authorId;
                notify.Parameters.Add("@post",SqlDbType.UniqueIdentifier).Value=id;
                notify.Parameters.Add("@category",SqlDbType.NVarChar,12).Value=data.Category;
                await notify.ExecuteNonQueryAsync();
            }
            return id;
        }

        /// <summary>Retrieves the persisted type so edits use the same validation as creation.</summary>
        public async Task<string?> CategoryAsync(Guid postId)
        {
            await using var db=await _database.OpenAsync();
            await using var cmd=new SqlCommand("SELECT Category FROM dbo.CommunityPosts WHERE Id=@id AND Status<>3",db);
            cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=postId;
            return await cmd.ExecuteScalarAsync() as string;
        }
        /// <summary>Lists image identifiers for a post the actor may actually inspect.</summary>
        public async Task<IReadOnlyList<Guid>> MediaAsync(Guid postId)
        {
            await using var db=await _database.OpenAsync();
            await using var cmd=new SqlCommand("SELECT Id FROM dbo.PostMedia WHERE PostId=@post AND IsCover=0 ORDER BY CreatedUtc",db);
            cmd.Parameters.Add("@post",SqlDbType.UniqueIdentifier).Value=postId;
            var ids=new List<Guid>();await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync())ids.Add(r.GetGuid(0));
            return ids;
        }
        public async Task<string?> MediaKeyAsync(Guid postId,Guid mediaId)
        {
            await using var db=await _database.OpenAsync();
            await using var cmd=new SqlCommand("SELECT StorageKey FROM dbo.PostMedia WHERE PostId=@post AND Id=@id",db);
            cmd.Parameters.Add("@post",SqlDbType.UniqueIdentifier).Value=postId;
            cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=mediaId;
            return await cmd.ExecuteScalarAsync() as string;
        }
        /// <summary>Returns the one public-facing cover ID; gallery images remain independent.</summary>
        public async Task<Guid?> CoverIdAsync(Guid postId)
        {
            await using var db=await _database.OpenAsync();
            await using var cmd=new SqlCommand("SELECT TOP(1) Id FROM dbo.PostMedia WHERE PostId=@id AND IsCover=1",db);
            cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=postId;
            return await cmd.ExecuteScalarAsync() is Guid id?id:null;
        }
        /// <summary>Replaces the image designated as the cover, preserving the gallery count.</summary>
        public async Task<(bool Success,string? OldKey)> SaveCoverAsync(Guid postId,Guid author,string key,string mime,bool staff)
        {
            await using var db=await _database.OpenAsync();
            await using var tx=(SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable);
            await using var owner=new SqlCommand(@"SELECT COUNT(*) FROM dbo.CommunityPosts WITH(UPDLOCK,HOLDLOCK)
WHERE Id=@post AND AuthorId=@author AND Status<>3 AND IsExample=0",db,tx);
            owner.Parameters.Add("@post",SqlDbType.UniqueIdentifier).Value=postId;
            owner.Parameters.Add("@author",SqlDbType.UniqueIdentifier).Value=author;
            if(Convert.ToInt32(await owner.ExecuteScalarAsync())!=1){await tx.RollbackAsync();return(false,null);}
            await using var old=new SqlCommand("SELECT TOP(1) StorageKey FROM dbo.PostMedia WHERE PostId=@post AND IsCover=1",db,tx);
            old.Parameters.Add("@post",SqlDbType.UniqueIdentifier).Value=postId;
            var existing=await old.ExecuteScalarAsync() as string;
            if(existing is null)
            {
                await using var insert=new SqlCommand(@"INSERT dbo.PostMedia(Id,PostId,StorageKey,ContentType,IsCover)
VALUES(NEWID(),@post,@key,@mime,1)",db,tx);
                insert.Parameters.Add("@post",SqlDbType.UniqueIdentifier).Value=postId;
                insert.Parameters.Add("@key",SqlDbType.NVarChar,90).Value=key;
                insert.Parameters.Add("@mime",SqlDbType.NVarChar,20).Value=mime;
                await insert.ExecuteNonQueryAsync();
            }
            else
            {
                await using var update=new SqlCommand(@"UPDATE dbo.PostMedia SET StorageKey=@key,ContentType=@mime
WHERE PostId=@post AND IsCover=1",db,tx);
                update.Parameters.Add("@post",SqlDbType.UniqueIdentifier).Value=postId;
                update.Parameters.Add("@key",SqlDbType.NVarChar,90).Value=key;
                update.Parameters.Add("@mime",SqlDbType.NVarChar,20).Value=mime;
                await update.ExecuteNonQueryAsync();
            }
            await using var moderation=new SqlCommand(@"UPDATE dbo.CommunityPosts SET
Status=CASE WHEN @staff=1 AND Category IN(N'Blog',N'Moment') THEN Status ELSE 0 END,
IsFeatured=0,UpdatedUtc=SYSUTCDATETIME() WHERE Id=@post",db,tx);
            moderation.Parameters.Add("@post",SqlDbType.UniqueIdentifier).Value=postId;
            moderation.Parameters.Add("@staff",SqlDbType.Bit).Value=staff;
            await moderation.ExecuteNonQueryAsync();await tx.CommitAsync();return(true,existing);
        }
        /// <summary>Private and pending drafts are never available to unrelated readers.</summary>
        public async Task<bool> CanReadMediaAsync(Guid postId,Guid actorId,bool staff,bool institutional)
        {
            await using var db=await _database.OpenAsync();
            await using var cmd=new SqlCommand(@"SELECT COUNT_BIG(*) FROM dbo.CommunityPosts
WHERE Id=@post AND Status<>3 AND (Status=1 OR AuthorId=@actor OR
(@staff=1 AND Category IN(N'Blog',N'Moment')) OR
(@institutional=1 AND Category IN (N'Guide',N'Job',N'Event'))) ",db);
            cmd.Parameters.Add("@post",SqlDbType.UniqueIdentifier).Value=postId;
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actorId;
            cmd.Parameters.Add("@staff",SqlDbType.Bit).Value=staff;
            cmd.Parameters.Add("@institutional",SqlDbType.Bit).Value=institutional;
            return (long)(await cmd.ExecuteScalarAsync()??0L)>0;
        }
        /// <summary>Limits image count, checks actual authorship, then remoderates edits when needed.</summary>
        public async Task<Guid?> AddMediaAsync(Guid postId,Guid actorId,string key,string mime,bool staff)
        {
            Guid id=Guid.NewGuid();
            await using var db=await _database.OpenAsync();
            await using var transaction=(SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable);
            await using var cmd=new SqlCommand(@"INSERT dbo.PostMedia(Id,PostId,StorageKey,ContentType)
SELECT @id,@post,@key,@mime WHERE EXISTS
(SELECT 1 FROM dbo.CommunityPosts WITH (UPDLOCK,HOLDLOCK)
 WHERE Id=@post AND AuthorId=@actor AND Status<>3 AND IsExample=0)
AND (SELECT COUNT(*) FROM dbo.PostMedia WITH(UPDLOCK,HOLDLOCK) WHERE PostId=@post AND IsCover=0)<
 (CASE WHEN EXISTS(SELECT 1 FROM dbo.CommunityPosts WHERE Id=@post AND Category=N'Moment') THEN 1 ELSE 5 END)",db,transaction);
            cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
            cmd.Parameters.Add("@post",SqlDbType.UniqueIdentifier).Value=postId;
            cmd.Parameters.Add("@key",SqlDbType.NVarChar,90).Value=key;
            cmd.Parameters.Add("@mime",SqlDbType.NVarChar,20).Value=mime;
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actorId;
            if(await cmd.ExecuteNonQueryAsync()!=1)
            {await transaction.RollbackAsync();return null;}
            await using var update=new SqlCommand(@"UPDATE dbo.CommunityPosts SET
Status=CASE WHEN @staff=1 AND Category IN(N'Blog',N'Moment') THEN Status ELSE 0 END,
IsFeatured=CASE WHEN @staff=1 AND Category IN(N'Blog',N'Moment') THEN IsFeatured ELSE 0 END,
UpdatedUtc=SYSUTCDATETIME() WHERE Id=@post",db,transaction);
            update.Parameters.Add("@post",SqlDbType.UniqueIdentifier).Value=postId;
            update.Parameters.Add("@staff",SqlDbType.Bit).Value=staff;
            await update.ExecuteNonQueryAsync();await transaction.CommitAsync();return id;
        }
        /// <summary>Uses a server-side predicate for member ownership or staff authorization.</summary>
        public async Task<bool> UpdateAsync(Guid postId, Guid actorId, bool staff, Post.EditData data)
        {
            await using var db = await _database.OpenAsync();
            await using var cmd = new SqlCommand(@"UPDATE dbo.CommunityPosts
SET Title=@title,Summary=@summary,Body=@body,Subcategory=@subcategory,EventMode=@mode,UpdatedUtc=SYSUTCDATETIME(),
Status=CASE WHEN @staff=1 AND Category IN(N'Blog',N'Moment') THEN 1 ELSE 0 END,IsFeatured=0,ModeratedBy=NULL,ModeratedUtc=NULL
WHERE Id=@id AND Status<>3 AND (AuthorId=@actor OR (@staff=1 AND Category IN(N'Blog',N'Moment')))
AND LEN(@body) BETWEEN (CASE WHEN Category=N'Moment' THEN 8 ELSE 20 END) AND CASE Category
WHEN N'Moment' THEN 600 WHEN N'Blog' THEN 8000 WHEN N'Guide' THEN 12000 WHEN N'Job' THEN 5000 ELSE 4000 END", db);
            cmd.Parameters.Add("@title", SqlDbType.NVarChar, 120).Value = data.Title.Trim();
            cmd.Parameters.Add("@summary", SqlDbType.NVarChar, 260).Value = data.Summary.Trim();
            cmd.Parameters.Add("@body", SqlDbType.NVarChar, -1).Value = data.Body.Trim();
            cmd.Parameters.Add("@subcategory",SqlDbType.NVarChar,48).Value=(object?)data.Subcategory??DBNull.Value;
            cmd.Parameters.Add("@mode",SqlDbType.NVarChar,16).Value=(object?)data.Mode??DBNull.Value;
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
UpdatedUtc=SYSUTCDATETIME() WHERE Id=@id AND Status<>3 AND (AuthorId=@actor OR (@staff=1 AND Category IN(N'Blog',N'Moment')))",db);
            cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=postId;
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actorId;
            cmd.Parameters.Add("@staff",SqlDbType.Bit).Value=staff;
            return await cmd.ExecuteNonQueryAsync()==1;
        }

        /// <summary>Institutional reviewer soft-deletes a post after server-side role authorization.</summary>
        public async Task<bool> DeleteReviewedAsync(Guid postId)
        {
            await using var db=await _database.OpenAsync();
            await using var cmd=new SqlCommand(@"UPDATE dbo.CommunityPosts
SET Status=3,IsFeatured=0,UpdatedUtc=SYSUTCDATETIME() WHERE Id=@post AND Status<>3",db);
            cmd.Parameters.Add("@post",SqlDbType.UniqueIdentifier).Value=postId;
            return await cmd.ExecuteNonQueryAsync()==1;
        }
        /// <summary>Member authors request removal; an institutional reviewer must authorize it.</summary>
        public async Task<bool> RequestDeletionAsync(Guid postId,Guid requester)
        {
            await using var db=await _database.OpenAsync();
            await using var cmd=new SqlCommand(@"INSERT dbo.PostDeletionRequests(Id,PostId,RequestedBy)
SELECT NEWID(),Id,@actor FROM dbo.CommunityPosts p WHERE p.Id=@post AND p.AuthorId=@actor
AND p.Status<>3 AND NOT EXISTS(SELECT 1 FROM dbo.PostDeletionRequests d
WHERE d.PostId=@post AND d.Status=N'Pending')",db);
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=requester;
            cmd.Parameters.Add("@post",SqlDbType.UniqueIdentifier).Value=postId;
            return await cmd.ExecuteNonQueryAsync()==1;
        }
        /// <summary>Reviewers see only administrative metadata for deletion requests.</summary>
        public async Task<IReadOnlyList<object>> PendingDeletionAsync()
        {
            var rows=new List<object>();
            await using var db=await _database.OpenAsync();
            await using var cmd=new SqlCommand(@"SELECT TOP(100) d.Id,d.PostId,p.Title,p.Category,
 d.RequestedUtc FROM dbo.PostDeletionRequests d JOIN dbo.CommunityPosts p ON p.Id=d.PostId
 WHERE d.Status=N'Pending' ORDER BY d.RequestedUtc",db);
            await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync())rows.Add(new {id=r.GetGuid(0),postId=r.GetGuid(1),
                title=r.GetString(2),category=r.GetString(3),requestedUtc=r.GetDateTime(4)});
            return rows;
        }
        /// <summary>A reviewer either soft-deletes the requested post or rejects the removal request.</summary>
        public async Task<bool> ReviewDeletionAsync(Guid requestId,Guid reviewer,bool approve)
        {
            await using var db=await _database.OpenAsync();
            await using var tx=(SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable);
            await using var cmd=new SqlCommand(@"UPDATE dbo.PostDeletionRequests
SET Status=@status,ReviewedBy=@reviewer,ReviewedUtc=SYSUTCDATETIME()
OUTPUT inserted.PostId,inserted.RequestedBy WHERE Id=@id AND Status=N'Pending'",db,tx);
            cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=requestId;
            cmd.Parameters.Add("@reviewer",SqlDbType.UniqueIdentifier).Value=reviewer;
            cmd.Parameters.Add("@status",SqlDbType.NVarChar,12).Value=approve?"Approved":"Rejected";
            Guid postId;Guid owner;
            await using(var r=await cmd.ExecuteReaderAsync())
            {
                if(!await r.ReadAsync()){await r.CloseAsync();await tx.RollbackAsync();return false;}
                postId=r.GetGuid(0);owner=r.GetGuid(1);
            }
            if(approve)
            {
                await using var post=new SqlCommand(@"UPDATE dbo.CommunityPosts
SET Status=3,IsFeatured=0,UpdatedUtc=SYSUTCDATETIME() WHERE Id=@post AND Status<>3",db,tx);
                post.Parameters.Add("@post",SqlDbType.UniqueIdentifier).Value=postId;
                await post.ExecuteNonQueryAsync();
            }
            await using var notice=new SqlCommand(@"INSERT dbo.Notifications(Id,RecipientId,ActorId,Type,Title,RelatedId)
VALUES(NEWID(),@owner,@reviewer,N'PostReview',@title,@post)",db,tx);
            notice.Parameters.Add("@owner",SqlDbType.UniqueIdentifier).Value=owner;
            notice.Parameters.Add("@reviewer",SqlDbType.UniqueIdentifier).Value=reviewer;
            notice.Parameters.Add("@post",SqlDbType.UniqueIdentifier).Value=postId;
            notice.Parameters.Add("@title",SqlDbType.NVarChar,100).Value=approve?
                "La solicitud de retiro fue aprobada.":"La solicitud de retiro fue rechazada.";
            await notice.ExecuteNonQueryAsync();await tx.CommitAsync();return true;
        }

        /// <summary>Only pending requests may be approved or declined by a staff reviewer.</summary>
        public async Task<bool> ReviewAsync(Guid postId, Guid reviewerId, Post.ReviewData data, bool institutional)
        {
            await using var db = await _database.OpenAsync();
            await using var cmd = new SqlCommand(@"UPDATE dbo.CommunityPosts
SET Status=@status,IsFeatured=@featured,ModeratedBy=@reviewer,
ModeratedUtc=SYSUTCDATETIME() WHERE Id=@id AND Status=0 AND
((@institutional=1 AND Category IN (N'Guide',N'Job',N'Event')) OR
 (@institutional=0 AND Category IN(N'Blog',N'Moment')))",db);
            cmd.Parameters.Add("@status",SqlDbType.TinyInt).Value=(byte)(data.Approved ? 1 : 2);
            cmd.Parameters.Add("@featured",SqlDbType.Bit).Value=data.Approved && data.Featured;
            cmd.Parameters.Add("@reviewer",SqlDbType.UniqueIdentifier).Value=reviewerId;
            cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=postId;
            cmd.Parameters.Add("@institutional",SqlDbType.Bit).Value=institutional;
            bool changed=await cmd.ExecuteNonQueryAsync()==1;
            if(changed)
            {
                await using var notify=new SqlCommand(@"INSERT dbo.Notifications(Id,RecipientId,ActorId,Type,Title,RelatedId)
SELECT NEWID(),AuthorId,@reviewer,N'PostReview',@title,@post FROM dbo.CommunityPosts
WHERE Id=@post AND AuthorId IS NOT NULL AND AuthorId<>@reviewer",db);
                notify.Parameters.Add("@reviewer",SqlDbType.UniqueIdentifier).Value=reviewerId;
                notify.Parameters.Add("@post",SqlDbType.UniqueIdentifier).Value=postId;
                notify.Parameters.Add("@title",SqlDbType.NVarChar,100).Value=data.Approved?"Tu publicación fue aprobada.":"Tu publicación fue rechazada.";
                await notify.ExecuteNonQueryAsync();
            }
            return changed;
        }
        #endregion Task
    }
}
