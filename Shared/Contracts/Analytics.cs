namespace MujeresUnidas.Shared.Contracts;
public enum GoalStatus { Planned, InProgress, Achieved, Evaluated, Rescheduled }
public enum MetricDirection { Increase, Decrease, Monitor }
public sealed record MetricDefinitionDto(Guid Id,string Code,string Title,string Unit,string Direction,string DataSource,bool IsActive);
public sealed record MetricGoalDto(Guid Id,Guid MetricId,short GoalYear,Guid? SiteId,Guid? ZoneId,
    decimal TargetValue,string Status,string? Notes);
public sealed record MetricObservationDto(Guid Id,Guid MetricId,short PeriodYear,byte PeriodMonth,
    decimal ObservedValue,string SourceNote);
