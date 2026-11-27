using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using MujeresUnidas.Server.Infrastructure.Database;

namespace MujeresUnidas.Server.Models.Messages.Repositories
{
    /// <summary>SQL persistence and server-side membership checks for direct, group and public rooms.</summary>
    public sealed class ConversationRepository(Database database,MessageSecurity security)
    {
        #region Structs
        private const string AccessPredicate = @"(c.Type=N'Public' OR EXISTS
(SELECT 1 FROM dbo.ConversationParticipants AS cp WHERE cp.ConversationId=c.Id AND cp.UserId=@actor AND cp.LeftUtc IS NULL))";
        #endregion Structs
        #region Task
        public async Task<IReadOnlyList<Conversation.ConversationData>> ListAsync(Guid actor)
        {
            var items=new List<Conversation.ConversationData>();
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"SELECT TOP(80) c.Id,c.Title,c.Type,c.CreatedBy,c.ParticipantIdsJson,c.CreatedUtc
FROM dbo.Conversations c WHERE c.IsClosed=0 AND " + AccessPredicate + " ORDER BY c.CreatedUtc DESC",db);
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
            await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync())items.Add(Read(r));
            return items;
        }

        public async Task<Conversation.ConversationData?> FindVisibleAsync(Guid id,Guid actor)
        {
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"SELECT c.Id,c.Title,c.Type,c.CreatedBy,c.ParticipantIdsJson,c.CreatedUtc
FROM dbo.Conversations c WHERE c.Id=@id AND c.IsClosed=0 AND " + AccessPredicate,db);
            cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
            await using var r=await cmd.ExecuteReaderAsync();
            return await r.ReadAsync()?Read(r):null;
        }

        public async Task<Guid?> CreateAsync(Guid actor,Conversation.CreateData request,bool isStaff)
        {
            string type=request.Type;
            if(type is not ("Direct" or "Group" or "Public") || (type=="Public" && !isStaff))return null;
            var recipients=(request.UserIds??new List<Guid>()).Where(id=>id!=actor && id!=Guid.Empty).Distinct().ToArray();
            if(type=="Direct" && recipients.Length!=1 || type=="Group" && (recipients.Length<2 || recipients.Length>19) ||
               type=="Public" && recipients.Length!=0)return null;
            var all=type=="Public"?Array.Empty<Guid>():new[]{actor}.Concat(recipients).ToArray();
            var id=Guid.NewGuid();
            await using var db=await database.OpenAsync();
            await using var tx=(SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable);
            if(recipients.Length>0)
            {
                await using var validate=new SqlCommand(@"SELECT COUNT(*) FROM dbo.Accounts
WHERE Status=1 AND Id IN (SELECT TRY_CONVERT(uniqueidentifier,[value]) FROM OPENJSON(@ids))
AND EXISTS(SELECT 1 FROM dbo.ApplicationAccess x WHERE x.AccountId=dbo.Accounts.Id AND x.ApplicationCode=N'Community' AND x.AccessStatus=N'Active')",db,tx);
                validate.Parameters.Add("@ids",SqlDbType.NVarChar,-1).Value=JsonSerializer.Serialize(recipients);
                if(Convert.ToInt32(await validate.ExecuteScalarAsync())!=recipients.Length)
                {await tx.RollbackAsync();return null;}
            }
            // Never create a second direct thread for the same active pair.
            // This also avoids scattering their conversation over repeated profile clicks.
            if(type=="Direct")
            {
                await using var existing=new SqlCommand(@"SELECT TOP(1) c.Id FROM dbo.Conversations c WITH(UPDLOCK,HOLDLOCK)
WHERE c.Type=N'Direct' AND c.IsClosed=0
  AND (SELECT COUNT(*) FROM OPENJSON(c.ParticipantIdsJson))=2
  AND EXISTS(SELECT 1 FROM OPENJSON(c.ParticipantIdsJson) j
             WHERE TRY_CONVERT(uniqueidentifier,j.[value])=@actor)
  AND EXISTS(SELECT 1 FROM OPENJSON(c.ParticipantIdsJson) j
             WHERE TRY_CONVERT(uniqueidentifier,j.[value])=@recipient)
ORDER BY c.CreatedUtc",db,tx);
                existing.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
                existing.Parameters.Add("@recipient",SqlDbType.UniqueIdentifier).Value=recipients[0];
                var known=await existing.ExecuteScalarAsync();
                if(known is Guid prior)
                {
                    await tx.CommitAsync();
                    return prior;
                }
            }
            await using var cmd=new SqlCommand(@"INSERT dbo.Conversations(Id,Title,Type,CreatedBy,ParticipantIdsJson)
VALUES(@id,@title,@type,@actor,@ids)",db,tx);
            cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
            cmd.Parameters.Add("@title",SqlDbType.NVarChar,100).Value=request.Title.Trim();
            cmd.Parameters.Add("@type",SqlDbType.NVarChar,12).Value=type;
            cmd.Parameters.Add("@ids",SqlDbType.NVarChar,-1).Value=JsonSerializer.Serialize(all);
            await cmd.ExecuteNonQueryAsync();
            foreach(var participant in all)
            {
                await using var member=new SqlCommand(@"INSERT dbo.ConversationParticipants(ConversationId,UserId)
VALUES(@conversation,@member)",db,tx);
                member.Parameters.Add("@conversation",SqlDbType.UniqueIdentifier).Value=id;
                member.Parameters.Add("@member",SqlDbType.UniqueIdentifier).Value=participant;
                await member.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
            return id;
        }

        public async Task<bool> AddParticipantAsync(Guid id,Guid actor,Guid candidate)
        {
            if(candidate==Guid.Empty)return false;
            await using var db=await database.OpenAsync();
            await using var tx=(SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable);
            await using var cmd=new SqlCommand(@"SELECT c.Type,c.CreatedBy,c.ParticipantIdsJson FROM dbo.Conversations c WITH(UPDLOCK,HOLDLOCK)
WHERE c.Id=@id AND c.IsClosed=0",db,tx);
            cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
            string? type=null;Guid creator=default;List<Guid> members=new();
            await using(var r=await cmd.ExecuteReaderAsync())
            {
                if(await r.ReadAsync())
                {
                    type=r.GetString(0);creator=r.GetGuid(1);
                    members=JsonSerializer.Deserialize<List<Guid>>(r.GetString(2))??new();
                }
            }
            if(type is null or "Public" || !members.Contains(actor) || members.Contains(candidate) || members.Count>=20)
            {await tx.RollbackAsync();return false;}
            await using var check=new SqlCommand(@"SELECT COUNT(*) FROM dbo.Accounts WHERE Id=@candidate AND Status=1
AND EXISTS(SELECT 1 FROM dbo.ApplicationAccess x WHERE x.AccountId=dbo.Accounts.Id AND x.ApplicationCode=N'Community' AND x.AccessStatus=N'Active')",db,tx);
            check.Parameters.Add("@candidate",SqlDbType.UniqueIdentifier).Value=candidate;
            if(Convert.ToInt32(await check.ExecuteScalarAsync())!=1){await tx.RollbackAsync();return false;}
            members.Add(candidate);
            await using var update=new SqlCommand("UPDATE dbo.Conversations SET ParticipantIdsJson=@json WHERE Id=@id",db,tx);
            update.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
            update.Parameters.Add("@json",SqlDbType.NVarChar,-1).Value=JsonSerializer.Serialize(members);
            await update.ExecuteNonQueryAsync();
            await using var membership=new SqlCommand(@"IF EXISTS(SELECT 1 FROM dbo.ConversationParticipants
WHERE ConversationId=@id AND UserId=@candidate)
 UPDATE dbo.ConversationParticipants SET JoinedUtc=SYSUTCDATETIME(),LeftUtc=NULL
 WHERE ConversationId=@id AND UserId=@candidate
ELSE INSERT dbo.ConversationParticipants(ConversationId,UserId) VALUES(@id,@candidate)",db,tx);
            membership.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
            membership.Parameters.Add("@candidate",SqlDbType.UniqueIdentifier).Value=candidate;
            await membership.ExecuteNonQueryAsync();
            await tx.CommitAsync();return true;
        }

        public async Task<bool> LeaveAsync(Guid id,Guid actor)
        {
            await using var db=await database.OpenAsync();
            await using var tx=(SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable);
            await using var cmd=new SqlCommand(@"SELECT Type,ParticipantIdsJson FROM dbo.Conversations WITH(UPDLOCK,HOLDLOCK)
WHERE Id=@id AND IsClosed=0",db,tx);
            cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
            string? type=null;List<Guid> members=new();
            await using(var r=await cmd.ExecuteReaderAsync())
            {
                if(await r.ReadAsync()){type=r.GetString(0);members=JsonSerializer.Deserialize<List<Guid>>(r.GetString(1))??new();}
            }
            if(type=="Public" || !members.Remove(actor)){await tx.RollbackAsync();return false;}
            await using var update=new SqlCommand(@"UPDATE dbo.Conversations SET ParticipantIdsJson=@json,
IsClosed=CASE WHEN @count=0 THEN 1 ELSE 0 END WHERE Id=@id",db,tx);
            update.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
            update.Parameters.Add("@json",SqlDbType.NVarChar,-1).Value=JsonSerializer.Serialize(members);
            update.Parameters.Add("@count",SqlDbType.Int).Value=members.Count;
            await update.ExecuteNonQueryAsync();
            await using var membership=new SqlCommand(@"UPDATE dbo.ConversationParticipants
SET LeftUtc=SYSUTCDATETIME() WHERE ConversationId=@id AND UserId=@actor",db,tx);
            membership.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
            membership.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
            await membership.ExecuteNonQueryAsync();
            await tx.CommitAsync();return true;
        }

        public async Task<IReadOnlyList<Message.MessageData>?> MessagesAsync(Guid conversationId,Guid actor)
        {
            if(await FindVisibleAsync(conversationId,actor) is null)return null;
            var messages=new List<Message.MessageData>();
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"SELECT m.Id,m.ConversationId,m.SenderId,a.DisplayName,m.Body,m.CreatedUtc,m.EditedUtc,m.DeletedUtc
FROM (SELECT TOP(100) msg.* FROM dbo.Messages msg
JOIN dbo.Conversations c ON c.Id=msg.ConversationId
WHERE msg.ConversationId=@conversation AND
(c.Type=N'Public' OR EXISTS
 (SELECT 1 FROM dbo.ConversationParticipants cp WHERE cp.ConversationId=c.Id
 AND cp.UserId=@actor AND cp.LeftUtc IS NULL AND msg.CreatedUtc>=cp.JoinedUtc))
ORDER BY msg.CreatedUtc DESC) m
JOIN dbo.Accounts a ON a.Id=m.SenderId ORDER BY m.CreatedUtc ASC",db);
            cmd.Parameters.Add("@conversation",SqlDbType.UniqueIdentifier).Value=conversationId;
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
            await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync())messages.Add(new Message.MessageData(r.GetGuid(0),r.GetGuid(1),r.GetGuid(2),
                r.GetString(3),r.IsDBNull(7)?security.Decrypt(r.GetString(4)):"Mensaje eliminado",
                r.GetDateTime(5),r.IsDBNull(6)?null:r.GetDateTime(6),!r.IsDBNull(7)));
            return messages;
        }

        public async Task<Guid?> SendAsync(Guid conversationId,Guid sender,string body)
        {
            var id=Guid.NewGuid();
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"INSERT dbo.Messages(Id,ConversationId,SenderId,Body)
SELECT @id,c.Id,@sender,@body FROM dbo.Conversations c
WHERE c.Id=@conversation AND c.IsClosed=0 AND " + AccessPredicate,db);
            cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
            cmd.Parameters.Add("@conversation",SqlDbType.UniqueIdentifier).Value=conversationId;
            cmd.Parameters.Add("@sender",SqlDbType.UniqueIdentifier).Value=sender;
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=sender;
            cmd.Parameters.Add("@body",SqlDbType.NVarChar,-1).Value=security.Encrypt(body);
            return await cmd.ExecuteNonQueryAsync()==1?id:null;
        }

        public async Task<bool> EditAsync(Guid messageId,Guid actor,string body)
        {
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"UPDATE m SET Body=@body,EditedUtc=SYSUTCDATETIME() FROM dbo.Messages m
JOIN dbo.Conversations c ON c.Id=m.ConversationId WHERE m.Id=@id AND m.SenderId=@actor
AND m.DeletedUtc IS NULL AND c.IsClosed=0 AND " + AccessPredicate,db);
            cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=messageId;
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
            cmd.Parameters.Add("@body",SqlDbType.NVarChar,-1).Value=security.Encrypt(body);
            return await cmd.ExecuteNonQueryAsync()==1;
        }

        public async Task<bool> DeleteAsync(Guid messageId,Guid actor)
        {
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"UPDATE m SET Body=N'',DeletedUtc=SYSUTCDATETIME() FROM dbo.Messages m
JOIN dbo.Conversations c ON c.Id=m.ConversationId WHERE m.Id=@id AND m.SenderId=@actor
AND m.DeletedUtc IS NULL AND c.IsClosed=0 AND " + AccessPredicate,db);
            cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=messageId;
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
            return await cmd.ExecuteNonQueryAsync()==1;
        }
        #endregion Task

        private static Conversation.ConversationData Read(SqlDataReader r) => new(
            r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetGuid(3),
            JsonSerializer.Deserialize<List<Guid>>(r.GetString(4))??new List<Guid>(),r.GetDateTime(5));
    }
}
