using System.Text.Json;
using MujeresUnidas.Comunidad.Connection;

namespace MujeresUnidas.Comunidad.Models.Messages
{
    public sealed class Message(ApiClient api)
    {
        #region Structs
        public readonly record struct MessageData(Guid MessageId,Guid ConversationId,Guid SenderUserId,
            string SenderName,string Body,DateTime CreatedUtc,DateTime? EditedUtc,bool IsDeleted);
        public readonly record struct SendData(string Body);
        public readonly record struct EditData(string Body);
        #endregion Structs
        #region Task
        public async Task<(IReadOnlyList<MessageData> Items,string Error)> GetMessagesAsync(Guid conversationId)
        {
            var reply=await api.RequestAsync(HttpMethod.Get,$"api/conversations/{conversationId}/messages");
            if(!reply.Success)return(Array.Empty<MessageData>(),reply.Error);
            try{return(JsonSerializer.Deserialize<List<MessageData>>(reply.Data.GetRawText(),
                new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??new(),string.Empty);}
            catch(JsonException){return(Array.Empty<MessageData>(),"No se pudo leer la conversación.");}
        }
        public async Task<(bool Success,string Error)> SendMessageAsync(Guid conversationId,string text)
        {
            var reply=await api.RequestAsync(HttpMethod.Post,$"api/conversations/{conversationId}/messages",new SendData(text));
            return(reply.Success,reply.Error);
        }
        public async Task<(bool Success,string Error)> EditMessageAsync(Guid id,string text)
        {
            var reply=await api.RequestAsync(HttpMethod.Put,$"api/conversations/messages/{id}",new EditData(text));
            return(reply.Success,reply.Error);
        }
        public async Task<(bool Success,string Error)> DeleteMessageAsync(Guid id)
        {
            var reply=await api.RequestAsync(HttpMethod.Delete,$"api/conversations/messages/{id}");
            return(reply.Success,reply.Error);
        }
        #endregion Task
    }
}
