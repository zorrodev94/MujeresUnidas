using MujeresUnidas.Shared.Contracts;
using MujeresUnidas.Comunidad.Models.Api;
namespace MujeresUnidas.Comunidad.Models.Messages;
/// <summary>Typed community client operations; private data are never fetched directly from SQL.</summary>
public sealed class CommunityMessagingClient(CommunityGateway api)
{
    private readonly CommunityGateway _api=api;
    public Task<(bool Success,string Error)> SendAsync(Guid conversationId,string body) =>
        _api.ChangeAsync(HttpMethod.Post,$"api/conversations/{conversationId}/messages",new MessageDraftDto(body));

}
