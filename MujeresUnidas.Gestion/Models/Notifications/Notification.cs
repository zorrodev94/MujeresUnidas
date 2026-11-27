using System.Text.Json;
using MujeresUnidas.Gestion.Connection;

namespace MujeresUnidas.Gestion.Models.Notifications
{
    public sealed class Notification(ApiConnection api)
    {
        #region Structs
        public readonly record struct NotificationData(Guid NotificationId,string Type,string Title,
            Guid? RelatedId,DateTime CreatedUtc,bool IsRead);
        #endregion Structs
        #region Task
        public async Task<(IReadOnlyList<NotificationData> Items,string Error)> ListAsync()
        {
            var result=await api.SendAsync(HttpMethod.Get,"api/notifications");
            if(!result.Success)return(Array.Empty<NotificationData>(),result.Error);
            try{return(JsonSerializer.Deserialize<List<NotificationData>>(result.Data.GetRawText(),
                new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??new(),string.Empty);}
            catch(JsonException){return(Array.Empty<NotificationData>(),"No se pudieron leer las notificaciones.");}
        }
        public async Task<bool> ReadAsync(Guid id)=>(await api.SendAsync(HttpMethod.Post,$"api/notifications/{id}/read")).Success;
        #endregion Task
    }
}
