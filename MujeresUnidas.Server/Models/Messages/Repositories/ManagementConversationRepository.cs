using Microsoft.Data.SqlClient;
using System.Data;
using MujeresUnidas.Server.Infrastructure.Database;
using MujeresUnidas.Server.Models.Messages.Management;

namespace MujeresUnidas.Server.Models.Messages.Repositories
{
    /// <summary>Persists staff-only conversations and enforces membership in SQL Server.</summary>
    public sealed class ManagementConversationRepository(Database database,MessageSecurity security)
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
        /// <summary>Returns active institutional accounts, never community identities.</summary>
        public async Task<IReadOnlyList<ManagementConversation.StaffData>> StaffAsync(Guid actor)
        {
            var people=new List<ManagementConversation.StaffData>();
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"SELECT Id,DisplayName,StaffRole FROM dbo.Accounts
WHERE Kind IN (1,2) AND Status=1 AND Id<>@actor ORDER BY DisplayName",db);
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
            await using var rows=await cmd.ExecuteReaderAsync();
            while(await rows.ReadAsync())people.Add(new(rows.GetGuid(0),rows.GetString(1),
                rows.IsDBNull(2)?null:rows.GetString(2)));
            return people;
        }

        /// <summary>Returns only conversations whose two participants are active institutional accounts.</summary>
        public async Task<IReadOnlyList<ManagementConversation.ConversationData>> ListAsync(Guid actor)
        {
            var chats=new List<ManagementConversation.ConversationData>();
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"SELECT c.Id,p.Id,p.DisplayName,p.StaffRole,
COALESCE(c.LastMessageUtc,c.CreatedUtc) FROM dbo.ManagementDirectConversations c
INNER JOIN dbo.Accounts p ON p.Id=CASE WHEN c.AccountA=@actor THEN c.AccountB ELSE c.AccountA END
WHERE (c.AccountA=@actor OR c.AccountB=@actor) AND p.Kind IN (1,2) AND p.Status=1
ORDER BY COALESCE(c.LastMessageUtc,c.CreatedUtc) DESC",db);
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
            await using var rows=await cmd.ExecuteReaderAsync();
            while(await rows.ReadAsync())chats.Add(new(rows.GetGuid(0),rows.GetGuid(1),
                rows.GetString(2),rows.IsDBNull(3)?null:rows.GetString(3),rows.GetDateTime(4)));
            return chats;
        }

        /// <summary>Finds or starts one staff-to-staff conversation without creating a community chat.</summary>
        public async Task<Guid?> StartAsync(Guid actor,Guid recipient)
        {
            if(actor==Guid.Empty||recipient==Guid.Empty||actor==recipient)return null;
            var a=actor.CompareTo(recipient)<0?actor:recipient;
            var b=actor.CompareTo(recipient)<0?recipient:actor;
            await using var db=await database.OpenAsync();
            await using var tx=(SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable);
            await using(var allowed=new SqlCommand(@"SELECT COUNT(*) FROM dbo.Accounts WITH(UPDLOCK,HOLDLOCK)
WHERE Id IN (@actor,@recipient) AND Kind IN (1,2) AND Status=1",db,tx))
            {
                allowed.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
                allowed.Parameters.Add("@recipient",SqlDbType.UniqueIdentifier).Value=recipient;
                if(Convert.ToInt32(await allowed.ExecuteScalarAsync())!=2)
                {await tx.RollbackAsync();return null;}
            }
            await using(var known=new SqlCommand(@"SELECT Id FROM dbo.ManagementDirectConversations WITH(UPDLOCK,HOLDLOCK)
WHERE AccountA=@a AND AccountB=@b",db,tx))
            {
                known.Parameters.Add("@a",SqlDbType.UniqueIdentifier).Value=a;
                known.Parameters.Add("@b",SqlDbType.UniqueIdentifier).Value=b;
                var found=await known.ExecuteScalarAsync();
                if(found is Guid id){await tx.CommitAsync();return id;}
            }
            var created=Guid.NewGuid();
            await using(var insert=new SqlCommand(@"INSERT dbo.ManagementDirectConversations(Id,AccountA,AccountB)
VALUES(@id,@a,@b)",db,tx))
            {
                insert.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=created;
                insert.Parameters.Add("@a",SqlDbType.UniqueIdentifier).Value=a;
                insert.Parameters.Add("@b",SqlDbType.UniqueIdentifier).Value=b;
                await insert.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();return created;
        }

        /// <summary>Decrypts only the 100 most recent messages for an authorized institutional participant.</summary>
        public async Task<IReadOnlyList<ManagementConversation.MessageData>?> MessagesAsync(Guid conversationId,Guid actor)
        {
            var messages=new List<ManagementConversation.MessageData>();
            await using var db=await database.OpenAsync();
            await using(var allowed=new SqlCommand(@"SELECT COUNT(*) FROM dbo.ManagementDirectConversations c
JOIN dbo.Accounts a ON a.Id=CASE WHEN c.AccountA=@actor THEN c.AccountB ELSE c.AccountA END
WHERE c.Id=@id AND (c.AccountA=@actor OR c.AccountB=@actor)
AND a.Kind IN (1,2) AND a.Status=1",db))
            {
                allowed.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=conversationId;
                allowed.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
                if(Convert.ToInt32(await allowed.ExecuteScalarAsync())!=1)return null;
            }
            await using var cmd=new SqlCommand(@"SELECT t.Id,t.SenderId,a.DisplayName,t.Body,t.CreatedUtc FROM
(SELECT TOP (100) m.* FROM dbo.ManagementDirectMessages m
WHERE m.ConversationId=@id ORDER BY m.CreatedUtc DESC,m.Id DESC) t
JOIN dbo.Accounts a ON a.Id=t.SenderId ORDER BY t.CreatedUtc,t.Id",db);
            cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=conversationId;
            await using var rows=await cmd.ExecuteReaderAsync();
            while(await rows.ReadAsync())messages.Add(new(rows.GetGuid(0),rows.GetGuid(1),
                rows.GetString(2),security.Decrypt(rows.GetString(3)),rows.GetDateTime(4)));
            return messages;
        }

        /// <summary>Encrypts message bodies before insertion; actor must belong to an active staff conversation.</summary>
        public async Task<Guid?> SendAsync(Guid conversationId,Guid actor,string body)
        {
            var id=Guid.NewGuid();
            await using var db=await database.OpenAsync();
            await using var tx=(SqlTransaction)await db.BeginTransactionAsync();
            await using var cmd=new SqlCommand(@"INSERT dbo.ManagementDirectMessages(Id,ConversationId,SenderId,Body)
SELECT @id,c.Id,@actor,@body FROM dbo.ManagementDirectConversations c
JOIN dbo.Accounts recipient ON recipient.Id=CASE WHEN c.AccountA=@actor THEN c.AccountB ELSE c.AccountA END
WHERE c.Id=@conversation AND (c.AccountA=@actor OR c.AccountB=@actor)
AND recipient.Kind IN (1,2) AND recipient.Status=1",db,tx);
            cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
            cmd.Parameters.Add("@conversation",SqlDbType.UniqueIdentifier).Value=conversationId;
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
            cmd.Parameters.Add("@body",SqlDbType.NVarChar,-1).Value=security.Encrypt(body);
            if(await cmd.ExecuteNonQueryAsync()!=1){await tx.RollbackAsync();return null;}
            await using var update=new SqlCommand(@"UPDATE dbo.ManagementDirectConversations
SET LastMessageUtc=SYSUTCDATETIME() WHERE Id=@conversation",db,tx);
            update.Parameters.Add("@conversation",SqlDbType.UniqueIdentifier).Value=conversationId;
            await update.ExecuteNonQueryAsync();
            await tx.CommitAsync();return id;
        }
        #endregion Task
    }
}
