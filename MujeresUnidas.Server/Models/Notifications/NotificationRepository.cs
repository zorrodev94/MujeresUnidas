using System.Data;
using Microsoft.Data.SqlClient;
using MujeresUnidas.Server.Infrastructure.Database;

namespace MujeresUnidas.Server.Models.Notifications
{
    public sealed class NotificationRepository(Database database)
    {
        #region Task
        public async Task<IReadOnlyList<Notification.NotificationData>> GetAsync(Guid userId)
        {
            var rows=new List<Notification.NotificationData>();
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"SELECT TOP(50) Id,Type,Title,RelatedId,CreatedUtc,ReadUtc
FROM dbo.Notifications WHERE RecipientId=@id ORDER BY CreatedUtc DESC",db);
            cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=userId;
            await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync())rows.Add(new Notification.NotificationData(r.GetGuid(0),r.GetString(1),
                r.GetString(2),r.IsDBNull(3)?null:r.GetGuid(3),r.GetDateTime(4),!r.IsDBNull(5)));
            return rows;
        }
        public async Task<bool> ReadAsync(Guid id,Guid userId)
        {
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"UPDATE dbo.Notifications SET ReadUtc=COALESCE(ReadUtc,SYSUTCDATETIME())
WHERE Id=@id AND RecipientId=@user",db);
            cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
            cmd.Parameters.Add("@user",SqlDbType.UniqueIdentifier).Value=userId;
            return await cmd.ExecuteNonQueryAsync()==1;
        }
        #endregion Task
    }
}
