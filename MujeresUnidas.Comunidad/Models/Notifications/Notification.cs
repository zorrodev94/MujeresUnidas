using System.Text.Json;
using MujeresUnidas.Comunidad.Connection;

namespace MujeresUnidas.Comunidad.Models.Notifications
{
    public sealed class Notification(ApiClient api)
    {
        #region Structs
        public readonly record struct NotificationData(Guid NotificationId,string Type,string Title,
            Guid? RelatedId,DateTime CreatedUtc,bool IsRead);
        #endregion Structs
        #region Task
        public async Task<(IReadOnlyList<NotificationData> Items,string Error)> ListAsync()
        {
            var response=await api.RequestAsync(HttpMethod.Get,"api/notifications");
            if(!response.Success)return(Array.Empty<NotificationData>(),response.Error);
            try{return(JsonSerializer.Deserialize<List<NotificationData>>(response.Data.GetRawText(),
                new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??new(),string.Empty);}
            catch(JsonException){return(Array.Empty<NotificationData>(),"No se pudieron cargar las notificaciones.");}
        }
        public async Task<bool> MarkReadAsync(Guid id)
            =>(await api.RequestAsync(HttpMethod.Post,$"api/notifications/{id}/read")).Success;
        #endregion Task
    }
}
