namespace MujeresUnidas.Shared.Contracts;
public enum PublicationKind { Moment, Blog, Guide, Job, Event, ManagementMoment }
public enum PublicationState { Pending, Approved, Rejected, Hidden, Removed }
public sealed record PublicationDto(Guid Id,string Category,string Title,string Summary,string Body,
    string Author,string Status,DateTime CreatedUtc);
public sealed record PublicationInteractionDto(Guid PostId,int Likes,int Comments,bool Liked,bool Saved);
public sealed record ModerationDecisionDto(Guid PublicationId,string Action,string Reason);
