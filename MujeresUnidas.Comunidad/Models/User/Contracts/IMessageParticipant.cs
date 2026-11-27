using MujeresUnidas.Comunidad.Models.Messages;
namespace MujeresUnidas.Comunidad.Models.User.Contracts
{
    /// <summary>Client-side messaging capabilities; not a permission bypass.</summary>
    public interface IMessageParticipant
    {
        Task<(Guid? Id,string Error)> CreateConversationAsync(Conversation.CreateData data);
        Task<(bool Success,string Error)> SendMessageAsync(Guid conversationId,string body);
        Task<(bool Success,string Error)> EditMessageAsync(Guid messageId,string body);
        Task<(bool Success,string Error)> DeleteMessageAsync(Guid messageId);
        Task<(bool Success,string Message)> LeaveConversationAsync(Guid conversationId);
    }
}
