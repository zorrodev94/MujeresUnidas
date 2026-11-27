using MujeresUnidas.Shared.Contracts;
using MujeresUnidas.Gestion.Models.Api;
namespace MujeresUnidas.Gestion.Models.Geography;
/// <summary>Institutional client operations; permissions enforced in Server.</summary>
public sealed class ManagementMapClient(ManagementGateway api)
{
    private readonly ManagementGateway _api=api;
    public Task<(bool Success,List<ServiceZoneDto>? Value,string Error)> ZonesAsync() =>
        _api.GetAsync<List<ServiceZoneDto>>("api/management/zones");
    public Task<(bool Success,string Error)> AssignSiteAsync(Guid siteId,Guid zoneId) =>
        _api.ChangeAsync(HttpMethod.Put,$"api/sites/{siteId}/zone",new{zoneId});

}
