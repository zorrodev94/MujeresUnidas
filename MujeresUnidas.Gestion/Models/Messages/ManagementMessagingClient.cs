using MujeresUnidas.Shared.Contracts;
using MujeresUnidas.Gestion.Models.Api;
namespace MujeresUnidas.Gestion.Models.Messages;
/// <summary>Institutional client operations; permissions enforced in Server.</summary>
public sealed class ManagementMessagingClient(ManagementGateway api)
{
    private readonly ManagementGateway _api=api;
    public Task<(bool Success,List<ConversationDto>? Value,string Error)> ConversationsAsync() =>
        _api.GetAsync<List<ConversationDto>>("api/management/messaging/conversations");
    public Task<(bool Success,string Error)> SendAsync(Guid conversationId,string text) =>
        _api.ChangeAsync(HttpMethod.Post,$"api/management/messaging/conversations/{conversationId}/messages",new{body=text});

}
