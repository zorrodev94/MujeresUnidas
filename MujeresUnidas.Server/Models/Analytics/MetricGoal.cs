namespace MujeresUnidas.Server.Models.Analytics;

public enum GoalStatus { Planned, InProgress, Achieved, Evaluated, Rescheduled }
public enum GoalDirection { Increase, Decrease, Monitor }
public sealed record MetricDefinition(Guid Id,string Code,string Title,string Unit,GoalDirection Direction,string DataSource);
public sealed record MetricGoal(Guid Id,Guid MetricId,int GoalYear,Guid? ZoneId,Guid? SiteId,decimal TargetValue,GoalStatus Status,string? Notes);
public sealed record MetricObservation(Guid MetricId,int Year,int Month,decimal ObservedValue,string SourceNote,bool IsDemonstration,Guid? ZoneId=null,Guid? SiteId=null);
public sealed record GoalEvaluation(Guid GoalId,decimal ObservedValue,GoalStatus Status,string? Explanation);
public sealed record CommunityOutcomeSnapshot(Guid AccountId,int Year,int Month,bool? IsStudying,bool? IsWorking);
public sealed record GoalCreateRequest(Guid MetricId,int GoalYear,decimal TargetValue,Guid? ZoneId,Guid? SiteId,string? Notes);
public sealed record GoalEvaluationRequest(decimal ObservedValue,GoalStatus Status,string? Explanation);
public interface IGoalManager
{
    Task<IReadOnlyList<MetricDefinition>> GetDefinitionsAsync();
    Task<IReadOnlyList<MetricGoal>> GetGoalsAsync(int year);
    Task<Guid?> CreateGoalAsync(GoalCreateRequest input,Guid actorId);
    Task<bool> EvaluateGoalAsync(Guid goalId,GoalEvaluationRequest input,Guid actorId);
}
