using MujeresUnidas.Gestion.Models.Api;
namespace MujeresUnidas.Gestion.Models.Analytics;
public sealed record CommunityOutcomeSummaryDto(int Year,int Participants,int? Studying,int? Working,int? StudyingAndWorking);
public sealed record OutcomeResult(bool Available,CommunityOutcomeSummaryDto? Data,string? Note);
/// <summary>Only annual aggregates; no staff client method can fetch individual responses.</summary>
public sealed class OutcomeSummaryClient(ManagementGateway api)
{
    public Task<(bool Success,OutcomeResult? Value,string Error)> SummaryAsync(int year) =>
        api.GetAsync<OutcomeResult>($"api/outcomes/summary/{year}");
}
