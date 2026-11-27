using MujeresUnidas.Shared.Contracts;
using MujeresUnidas.Comunidad.Models.Api;
namespace MujeresUnidas.Comunidad.Models.Sites;
/// <summary>Typed community client operations; private data are never fetched directly from SQL.</summary>
public sealed class CommunitySiteClient(CommunityGateway api)
{
    private readonly CommunityGateway _api=api;
    public Task<(bool Success,List<CareSiteDto>? Value,string Error)> PublicSitesAsync() =>
        _api.GetAsync<List<CareSiteDto>>("api/sites/public");

}
