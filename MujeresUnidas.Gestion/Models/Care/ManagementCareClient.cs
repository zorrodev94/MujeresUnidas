using MujeresUnidas.Shared.Contracts;
using MujeresUnidas.Gestion.Models.Api;
namespace MujeresUnidas.Gestion.Models.Care;
/// <summary>Institutional client operations; permissions enforced in Server.</summary>
public sealed class ManagementCareClient(ManagementGateway api)
{
    private readonly ManagementGateway _api=api;
    public Task<(bool Success,List<CareCaseDto>? Value,string Error)> CasesAsync() =>
        _api.GetAsync<List<CareCaseDto>>("api/care/management/cases");
    public Task<(bool Success,List<AppointmentDto>? Value,string Error)> AppointmentsAsync() =>
        _api.GetAsync<List<AppointmentDto>>("api/care/management/appointments");

}
