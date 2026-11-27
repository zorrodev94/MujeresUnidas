using MujeresUnidas.Shared.Contracts;
using MujeresUnidas.Gestion.Models.Api;
namespace MujeresUnidas.Gestion.Models.Publications;
/// <summary>Institutional client operations; permissions enforced in Server.</summary>
public sealed class ManagementPublishingClient(ManagementGateway api)
{
    private readonly ManagementGateway _api=api;
    public Task<(bool Success,List<PublicationDto>? Value,string Error)> PendingAsync() =>
        _api.GetAsync<List<PublicationDto>>("api/posts/institutional/pending");
    public Task<(bool Success,string Error)> ReviewAsync(Guid id,bool approved) =>
        _api.ChangeAsync(HttpMethod.Post,$"api/posts/institutional/{id}/review",new{approved,featured=false});
    public Task<(bool Success,string Error)> CreateMomentAsync(string body) =>
        _api.ChangeAsync(HttpMethod.Post,"api/management/social/moments",new{body});

}
