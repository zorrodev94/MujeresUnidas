using System.Data;
using Microsoft.Data.SqlClient;
using MujeresUnidas.Server.Infrastructure.Database;

namespace MujeresUnidas.Server.Models.Media;

/// <summary>
/// Transactional quota reservation for future authenticated uploads.
/// Reserving disk space does not validate an uploaded file; the upload pipeline must
/// validate its signature, bytes, ownership and private storage before publication.
/// </summary>
public sealed class MediaQuotaService(Database database) : IMediaQuotaService
{
    public async Task<bool> TryReserveAsync(Guid ownerId,string application,long bytes)
    {
        if(ownerId==Guid.Empty||application is not ("Community" or "Management")||bytes<=0||bytes>15*1024*1024)
            return false;
        await using var db=await database.OpenAsync();
        await using var tx=(SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable);
        await using var command=new SqlCommand(@"IF NOT EXISTS (SELECT 1 FROM dbo.ApplicationAccess x
JOIN dbo.Accounts a ON a.Id=x.AccountId WHERE x.AccountId=@id AND x.ApplicationCode=@app
AND x.AccessStatus=N'Active' AND a.Status=1)
BEGIN SELECT CONVERT(int,0); END
ELSE BEGIN
 IF NOT EXISTS(SELECT 1 FROM dbo.StorageQuotas WITH(UPDLOCK,HOLDLOCK)
 WHERE AccountId=@id AND ApplicationCode=@app)
 INSERT dbo.StorageQuotas(AccountId,ApplicationCode,LimitBytes,UsedBytes)
 VALUES(@id,@app,CASE WHEN @app=N'Management' THEN 104857600 ELSE 52428800 END,0);
 UPDATE dbo.StorageQuotas SET UsedBytes=UsedBytes+@size
 WHERE AccountId=@id AND ApplicationCode=@app AND UsedBytes<=LimitBytes-@size;
 SELECT @@ROWCOUNT;
END",db,tx);
        command.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=ownerId;
        command.Parameters.Add("@app",SqlDbType.NVarChar,12).Value=application;
        command.Parameters.Add("@size",SqlDbType.BigInt).Value=bytes;
        var reserved=Convert.ToInt32(await command.ExecuteScalarAsync())==1;
        await tx.CommitAsync();return reserved;
    }
    public async Task ReleaseAsync(Guid ownerId,string application,long bytes)
    {
        if(ownerId==Guid.Empty||application is not ("Community" or "Management")||bytes<=0)return;
        await using var db=await database.OpenAsync();
        await using var command=new SqlCommand(@"UPDATE dbo.StorageQuotas
SET UsedBytes=CASE WHEN UsedBytes>=@size THEN UsedBytes-@size ELSE 0 END
WHERE AccountId=@id AND ApplicationCode=@app",db);
        command.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=ownerId;
        command.Parameters.Add("@app",SqlDbType.NVarChar,12).Value=application;
        command.Parameters.Add("@size",SqlDbType.BigInt).Value=bytes;
        await command.ExecuteNonQueryAsync();
    }
}
