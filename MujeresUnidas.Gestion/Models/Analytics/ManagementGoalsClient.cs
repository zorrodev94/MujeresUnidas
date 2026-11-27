using MujeresUnidas.Shared.Contracts;
using MujeresUnidas.Gestion.Models.Api;
namespace MujeresUnidas.Gestion.Models.Analytics;
/// <summary>Institutional client operations; permissions enforced in Server.</summary>
public sealed class ManagementGoalsClient(ManagementGateway api)
{
    private readonly ManagementGateway _api=api;
    public Task<(bool Success,List<MetricDefinitionDto>? Value,string Error)> DefinitionsAsync() =>
        _api.GetAsync<List<MetricDefinitionDto>>("api/management/goals/definitions");
    public Task<(bool Success,string Error)> EvaluateAsync(Guid goalId,object evaluation) =>
        _api.ChangeAsync(HttpMethod.Post,$"api/management/goals/{goalId}/evaluate",evaluation);

}
