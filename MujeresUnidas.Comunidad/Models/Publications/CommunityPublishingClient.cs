using MujeresUnidas.Shared.Contracts;
using MujeresUnidas.Comunidad.Models.Api;
namespace MujeresUnidas.Comunidad.Models.Publications;
/// <summary>Typed community client operations; private data are never fetched directly from SQL.</summary>
public sealed class CommunityPublishingClient(CommunityGateway api)
{
    private readonly CommunityGateway _api=api;
    public Task<(bool Success,List<PublicationDto>? Value,string Error)> FeedAsync() =>
        _api.GetAsync<List<PublicationDto>>("api/posts");
    public Task<(bool Success,string Error)> LikeAsync(Guid postId) =>
        _api.ChangeAsync(HttpMethod.Put,$"api/posts/{postId}/like");

}
