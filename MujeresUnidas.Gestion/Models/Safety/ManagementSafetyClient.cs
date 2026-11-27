using MujeresUnidas.Shared.Contracts;
using MujeresUnidas.Gestion.Models.Api;
namespace MujeresUnidas.Gestion.Models.Safety;
/// <summary>Institutional client operations; permissions enforced in Server.</summary>
public sealed class ManagementSafetyClient(ManagementGateway api)
{
    private readonly ManagementGateway _api=api;
    public Task<(bool Success,List<IncidentReportDto>? Value,string Error)> IncidentsAsync() =>
        _api.GetAsync<List<IncidentReportDto>>("api/safety/management/incidents");

}
