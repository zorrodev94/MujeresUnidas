using MujeresUnidas.Shared.Contracts;
using MujeresUnidas.Comunidad.Models.Api;
namespace MujeresUnidas.Comunidad.Models.Care;
/// <summary>Typed community client operations; private data are never fetched directly from SQL.</summary>
public sealed class CommunityCareClient(CommunityGateway api)
{
    private readonly CommunityGateway _api=api;
    public Task<(bool Success,List<AppointmentDto>? Value,string Error)> MyAppointmentsAsync() =>
        _api.GetAsync<List<AppointmentDto>>("api/care/appointments");
    public Task<(bool Success,string Error)> RequestCareAsync(object request) =>
        _api.ChangeAsync(HttpMethod.Post,"api/care/requests",request);

}
