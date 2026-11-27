namespace MujeresUnidas.Shared.Contracts;

/// <summary>Safe published survey card: no response or account identity.</summary>
public sealed record CommunitySurveyInfo(Guid Id,string Title,string Description);
/// <summary>Voluntary structured data; clients cannot assign a respondent or choose an administrator.</summary>
public sealed record CommunitySurveyAnswer(
    bool? IsStudying, bool? IsWorking, DateTime? StudyStartedMonth,
    DateTime? WorkStartedMonth, string Contribution, string HelpfulResource, bool Consent);
/// <summary>Self-only response; never exposed via institutional listing endpoints.</summary>
public sealed record CommunitySurveyReceipt(
    Guid SurveyId, bool? IsStudying, bool? IsWorking, DateTime? StudyStartedMonth,
    DateTime? WorkStartedMonth, string Contribution, string HelpfulResource, DateTime UpdatedUtc);
/// <summary>Aggregates are displayed only if the participant count meets the privacy threshold.</summary>
public sealed record CommunitySurveySummary(
    int Year, int Participants, int? Studying, int? Working, int? StudyingAndWorking,
    int? CommunityContributed, int? StudyingSinceYearStart, int? WorkingSinceYearStart);
