namespace MujeresUnidas.Shared.Contracts;
public enum CareStatus { UnderReview, InProgress, Resolved, Closed, Withdrawn }
public enum AppointmentState { Scheduled, Confirmed, Completed, Cancelled, NoShow }
public sealed record CareCaseDto(Guid Id,Guid? SiteId,string Area,string Status);
public sealed record CareRequestDto(Guid Id,string Area,string Status,DateTime CreatedUtc);
public sealed record AppointmentDto(Guid Id,Guid CaseId,Guid? SiteId,DateTime StartsUtc,string Status);
