using MujeresUnidas.Shared.Contracts;
using MujeresUnidas.Comunidad.Models.Api;
namespace MujeresUnidas.Comunidad.Models.Safety;
/// <summary>Typed community client operations; private data are never fetched directly from SQL.</summary>
public sealed class CommunitySafetyClient(CommunityGateway api)
{
    private readonly CommunityGateway _api=api;
    public Task<(bool Success,List<EmergencyContactDto>? Value,string Error)> MyContactsAsync() =>
        _api.GetAsync<List<EmergencyContactDto>>("api/safety/contacts");
    public Task<(bool Success,string Error)> AddContactAsync(object contact) =>
        _api.ChangeAsync(HttpMethod.Post,"api/safety/contacts",contact);

}
