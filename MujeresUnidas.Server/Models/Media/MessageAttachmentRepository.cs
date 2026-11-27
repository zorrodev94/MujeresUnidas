using System.Data;
using Microsoft.Data.SqlClient;
using MujeresUnidas.Server.Infrastructure.Database;
using MujeresUnidas.Server.Models.Messages;
using MujeresUnidas.Server.Models.Messages.Repositories;

namespace MujeresUnidas.Server.Models.Media;

public sealed record MessageAttachmentView(Guid AssetId,Guid MessageId,string MediaType,string MimeType,long SizeBytes);
/// <summary>Private encrypted media messages; quota + authorization are enforced on Server.</summary>
public sealed class MessageAttachmentRepository(Database database,IMediaQuotaService quotas,MessageSecurity security,
    ConversationRepository conversations,IConfiguration config)
{
    private string DirectoryPath => Path.GetFullPath(config["MU_MEDIA_ROOT"] is {Length:>0} path
        ?Path.Combine(path,"message-attachments")
        :Path.Combine(AppContext.BaseDirectory,"PrivateMedia","message-attachments"));
    private static (string? Type,string? Mime,long Limit) Classify(IFormFile file,ReadOnlySpan<byte> bytes)
    {
        var jpg=bytes.Length>3&&bytes[0]==0xff&&bytes[1]==0xd8&&bytes[2]==0xff;
        var png=bytes.Length>8&&bytes[0]==0x89&&bytes[1]==0x50&&bytes[2]==0x4e&&bytes[3]==0x47;
        var iso=bytes.Length>12&&bytes[4]==(byte)'f'&&bytes[5]==(byte)'t'&&bytes[6]==(byte)'y'&&bytes[7]==(byte)'p';
        var wav=bytes.Length>12&&bytes[0]==(byte)'R'&&bytes[1]==(byte)'I'&&bytes[2]==(byte)'F'&&bytes[3]==(byte)'F'&&
            bytes[8]==(byte)'W'&&bytes[9]==(byte)'A'&&bytes[10]==(byte)'V'&&bytes[11]==(byte)'E';
        var mp3=bytes.Length>3&&(bytes[..3].SequenceEqual("ID3"u8)||(bytes[0]==0xff&&(bytes[1]&0xe0)==0xe0));
        string mime=(file.ContentType??string.Empty).ToLowerInvariant().Trim();
        return mime switch
        {
            "image/jpeg" when jpg => ("Image","image/jpeg",5*1024*1024),
            "image/png" when png => ("Image","image/png",5*1024*1024),
            "audio/wav" or "audio/x-wav" when wav => ("Audio","audio/wav",8*1024*1024),
            "audio/mpeg" when mp3 => ("Audio","audio/mpeg",8*1024*1024),
            "audio/mp4" or "audio/x-m4a" when iso => ("Audio","audio/mp4",8*1024*1024),
            "video/mp4" when iso => ("Video","video/mp4",15*1024*1024),
            _ => (null,null,0)
        };
    }
    public async Task<(Guid? MessageId,string Error)> UploadAsync(Guid conversationId,Guid actor,IFormFile file)
    {
        if(!security.IsConfigured)return(null,"Falta la clave de mensajes del servidor.");
        if(file.Length is <1 or >15*1024*1024)return(null,"El archivo supera el tamaño permitido.");
        if(await conversations.FindVisibleAsync(conversationId,actor) is null)return(null,"Conversación no autorizada.");
        byte[] raw;
        await using(var input=file.OpenReadStream())
        {
            using var mem=new MemoryStream();await input.CopyToAsync(mem);
            if(mem.Length!=file.Length || mem.Length>15*1024*1024)return(null,"Archivo incompleto.");
            raw=mem.ToArray();
        }
        var (type,mime,limit)=Classify(file,raw);
        if(type is null||raw.Length>limit)return(null,"Formato de archivo no compatible o tamaño excesivo.");
        if(!await quotas.TryReserveAsync(actor,"Community",raw.Length))return(null,"Has alcanzado tu cuota personal de archivos.");
        string key=Guid.NewGuid().ToString("N")+".mue";string path=Path.Combine(DirectoryPath,key);
        var messageId=Guid.NewGuid();var assetId=Guid.NewGuid();bool success=false;
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            byte[] packed=security.EncryptMedia(raw);
            await File.WriteAllBytesAsync(path,packed);
            await using var db=await database.OpenAsync();
            await using var tx=(SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable);
            await using var cmd=new SqlCommand(@"INSERT dbo.Messages(Id,ConversationId,SenderId,Body)
SELECT @mid,c.Id,@owner,@body FROM dbo.Conversations c WHERE c.Id=@conv AND c.IsClosed=0 AND
(c.Type=N'Public' OR EXISTS(SELECT 1 FROM dbo.ConversationParticipants cp
WHERE cp.ConversationId=c.Id AND cp.UserId=@owner AND cp.LeftUtc IS NULL));
IF @@ROWCOUNT=1
BEGIN
 INSERT dbo.MediaAssets(Id,OwnerId,ApplicationCode,MediaType,StorageKey,MimeType,SizeBytes)
 VALUES(@aid,@owner,N'Community',@type,@key,@mime,@size);
 INSERT dbo.MessageAttachments(MessageId,AssetId) VALUES(@mid,@aid);
 SELECT 1;
END ELSE SELECT 0",db,tx);
            cmd.Parameters.Add("@mid",SqlDbType.UniqueIdentifier).Value=messageId;
            cmd.Parameters.Add("@aid",SqlDbType.UniqueIdentifier).Value=assetId;
            cmd.Parameters.Add("@conv",SqlDbType.UniqueIdentifier).Value=conversationId;
            cmd.Parameters.Add("@owner",SqlDbType.UniqueIdentifier).Value=actor;
            cmd.Parameters.Add("@body",SqlDbType.NVarChar,-1).Value=security.Encrypt("Archivo adjunto · "+type);
            cmd.Parameters.Add("@type",SqlDbType.NVarChar,12).Value=type;
            cmd.Parameters.Add("@key",SqlDbType.NVarChar,180).Value=key;
            cmd.Parameters.Add("@mime",SqlDbType.NVarChar,80).Value=mime!;
            cmd.Parameters.Add("@size",SqlDbType.BigInt).Value=raw.LongLength;
            if(Convert.ToInt32(await cmd.ExecuteScalarAsync())!=1)
            {await tx.RollbackAsync();return(null,"Conversación ya no disponible.");}
            await tx.CommitAsync();success=true;
            return(messageId,string.Empty);
        }
        catch(SqlException){return(null,"No se pudo guardar el adjunto. Verifica la migración 170.");}
        catch(IOException){return(null,"Almacenamiento privado no disponible.");}
        finally
        {
            if(!success)
            {
                try{if(File.Exists(path))File.Delete(path);}catch(IOException){}
                try{await quotas.ReleaseAsync(actor,"Community",raw.Length);}
                catch(SqlException){ /* Return the original upload error rather than masking it. */ }
            }
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(raw);
        }
    }
    public async Task<IReadOnlyList<MessageAttachmentView>?> ListAsync(Guid conversationId,Guid actor)
    {
        if(await conversations.FindVisibleAsync(conversationId,actor) is null)return null;
        var rows=new List<MessageAttachmentView>();
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"SELECT a.Id,m.Id,a.MediaType,a.MimeType,a.SizeBytes
FROM dbo.MessageAttachments x JOIN dbo.Messages m ON m.Id=x.MessageId
JOIN dbo.MediaAssets a ON a.Id=x.AssetId JOIN dbo.Conversations c ON c.Id=m.ConversationId
WHERE m.ConversationId=@conv AND m.DeletedUtc IS NULL AND
(c.Type=N'Public' OR EXISTS(SELECT 1 FROM dbo.ConversationParticipants cp
 WHERE cp.ConversationId=c.Id AND cp.UserId=@actor AND cp.LeftUtc IS NULL AND m.CreatedUtc>=cp.JoinedUtc))
ORDER BY m.CreatedUtc",db);
        cmd.Parameters.Add("@conv",SqlDbType.UniqueIdentifier).Value=conversationId;
        cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
        await using var r=await cmd.ExecuteReaderAsync();
        while(await r.ReadAsync())rows.Add(new(r.GetGuid(0),r.GetGuid(1),r.GetString(2),r.GetString(3),r.GetInt64(4)));
        return rows;
    }
    public async Task<(byte[]? Bytes,string Mime)> GetAsync(Guid conversationId,Guid assetId,Guid actor)
    {
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"SELECT a.StorageKey,a.MimeType
FROM dbo.MessageAttachments x JOIN dbo.Messages m ON x.MessageId=m.Id
JOIN dbo.MediaAssets a ON a.Id=x.AssetId JOIN dbo.Conversations c ON c.Id=m.ConversationId
WHERE m.ConversationId=@conv AND a.Id=@aid AND m.DeletedUtc IS NULL AND c.IsClosed=0 AND
(c.Type=N'Public' OR EXISTS(SELECT 1 FROM dbo.ConversationParticipants cp
 WHERE cp.ConversationId=c.Id AND cp.UserId=@actor AND cp.LeftUtc IS NULL AND m.CreatedUtc>=cp.JoinedUtc))",db);
        cmd.Parameters.Add("@conv",SqlDbType.UniqueIdentifier).Value=conversationId;
        cmd.Parameters.Add("@aid",SqlDbType.UniqueIdentifier).Value=assetId;
        cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
        await using var r=await cmd.ExecuteReaderAsync();
        if(!await r.ReadAsync())return(null,string.Empty);
        var key=r.GetString(0);var mime=r.GetString(1);
        if(Path.GetFileName(key)!=key)return(null,string.Empty);
        string path=Path.Combine(DirectoryPath,key);
        if(!File.Exists(path))return(null,string.Empty);
        byte[] packed=await File.ReadAllBytesAsync(path);
        return(security.DecryptMedia(packed),mime);
    }
}
