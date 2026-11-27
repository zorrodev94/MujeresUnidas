using System.Text.Json;
using MujeresUnidas.Comunidad.Connection;

namespace MujeresUnidas.Comunidad.Models.Messages
{
    /// <summary>Conversation operations remain in the model rather than the MAUI page.</summary>
    public sealed class Conversation(ApiClient api)
    {
        #region Structs
        public readonly record struct ConversationData(Guid ConversationId,string Title,string Type,
            Guid CreatedById,IReadOnlyList<Guid> UserIds,DateTime CreatedUtc);
        public struct CreateData
        {
            public string Title { get; set; }
            public string Type { get; set; }
            public List<Guid> UserIds { get; set; }
        }
        public readonly record struct AddParticipantData(Guid UserId);
        #endregion Structs
        #region Task
        public async Task<(IReadOnlyList<ConversationData> Items,string Error)> GetConversationsAsync()
        {
            var reply=await api.RequestAsync(HttpMethod.Get,"api/conversations");
            if(!reply.Success)return(Array.Empty<ConversationData>(),reply.Error);
            try
            {
                return (JsonSerializer.Deserialize<List<ConversationData>>(reply.Data.GetRawText(),JsonOptions())??new(),string.Empty);
            }
            catch(JsonException){return(Array.Empty<ConversationData>(),"La lista de conversaciones no pudo leerse.");}
        }
        public async Task<(Guid? Id,string Error)> CreateConversationAsync(CreateData data)
        {
            var reply=await api.RequestAsync(HttpMethod.Post,"api/conversations",data);
            if(!reply.Success)return(null,reply.Error);
            return reply.Data.TryGetProperty("conversationId",out var id)?(id.GetGuid(),string.Empty):
                (null,"Server no devolvió el identificador de conversación.");
        }
        public async Task<(bool Success,string Message)> AddParticipantAsync(Guid id,Guid user)
        {
            var reply=await api.RequestAsync(HttpMethod.Post,$"api/conversations/{id}/participants",new AddParticipantData(user));
            return(reply.Success,reply.Success?"Participante agregado.":reply.Error);
        }
        public async Task<(bool Success,string Message)> LeaveAsync(Guid id)
        {
            var reply=await api.RequestAsync(HttpMethod.Post,$"api/conversations/{id}/leave");
            return(reply.Success,reply.Success?"Saliste de la conversación.":reply.Error);
        }
        private static JsonSerializerOptions JsonOptions()=>new(){PropertyNameCaseInsensitive=true};
        #endregion Task
    }
}
