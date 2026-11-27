using MujeresUnidas.Shared.Contracts;
using MujeresUnidas.Comunidad.Models.Api;
namespace MujeresUnidas.Comunidad.Models.Badges;
/// <summary>Typed community client operations; private data are never fetched directly from SQL.</summary>
public sealed class CommunityBadgesClient(CommunityGateway api)
{
    private readonly CommunityGateway _api=api;
    public Task<(bool Success,List<AwardedBadgeDto>? Value,string Error)> AwardsAsync(Guid accountId) =>
        _api.GetAsync<List<AwardedBadgeDto>>($"api/badges/{accountId}");

}
