using MujeresUnidas.Shared.Contracts;
using MujeresUnidas.Gestion.Models.Api;
namespace MujeresUnidas.Gestion.Models.Sites;
/// <summary>Institutional client operations; permissions enforced in Server.</summary>
public sealed class ManagementSiteClient(ManagementGateway api)
{
    private readonly ManagementGateway _api=api;
    public Task<(bool Success,List<CareSiteDto>? Value,string Error)> SitesAsync() =>
        _api.GetAsync<List<CareSiteDto>>("api/sites");

}
