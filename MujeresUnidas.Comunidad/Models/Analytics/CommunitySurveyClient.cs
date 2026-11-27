using MujeresUnidas.Comunidad.Models.Api;
using MujeresUnidas.Shared.Contracts;

namespace MujeresUnidas.Comunidad.Models.Analytics;

/// <summary>Typed client adapter. No survey response is stored in local preferences.</summary>
public sealed class CommunitySurveyClient(CommunityGateway api)
{
    public Task<(bool Success,List<CommunitySurveyInfo>? Value,string Error)> ActiveAsync() =>
        api.GetAsync<List<CommunitySurveyInfo>>("api/community/surveys/active");
    public Task<(bool Success,CommunitySurveyReceipt? Value,string Error)> MineAsync(Guid surveyId) =>
        api.GetAsync<CommunitySurveyReceipt>($"api/community/surveys/{surveyId}/mine");
    public Task<(bool Success,string Error)> SaveAsync(Guid surveyId,CommunitySurveyAnswer response) =>
        api.ChangeAsync(HttpMethod.Put,$"api/community/surveys/{surveyId}/response",response);
    public Task<(bool Success,string Error)> WithdrawAsync(Guid surveyId) =>
        api.ChangeAsync(HttpMethod.Delete,$"api/community/surveys/{surveyId}/response");
}
