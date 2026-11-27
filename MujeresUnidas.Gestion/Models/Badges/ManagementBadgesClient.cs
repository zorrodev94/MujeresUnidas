using MujeresUnidas.Shared.Contracts;
using MujeresUnidas.Gestion.Models.Api;
namespace MujeresUnidas.Gestion.Models.Badges;
/// <summary>Institutional client operations; permissions enforced in Server.</summary>
public sealed class ManagementBadgesClient(ManagementGateway api)
{
    private readonly ManagementGateway _api=api;
    public Task<(bool Success,List<BadgeDefinitionDto>? Value,string Error)> DefinitionsAsync() =>
        _api.GetAsync<List<BadgeDefinitionDto>>("api/badges");
    public Task<(bool Success,string Error)> AwardAsync(Guid recipient,Guid badge,string reason) =>
        _api.ChangeAsync(HttpMethod.Post,"api/badges/grant",new {accountId=recipient,badgeId=badge,reason});

}
