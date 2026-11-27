using MujeresUnidas.Comunidad.Models.Api;
namespace MujeresUnidas.Comunidad.Models.Analytics;
public sealed record CommunityOutcomeInput(bool? IsStudying,bool? IsWorking,bool Consent);
/// <summary>Voluntary educational and employment status. Never reads others' private declarations.</summary>
public sealed class CommunityOutcomeClient(CommunityGateway api)
{
    public Task<(bool Success,string Error)> SaveAsync(bool? studying,bool? working) =>
        api.ChangeAsync(HttpMethod.Put,"api/outcomes/me",new CommunityOutcomeInput(studying,working,true));
    public Task<(bool Success,string Error)> WithdrawAsync() =>
        api.ChangeAsync(HttpMethod.Delete,"api/outcomes/me");
}
