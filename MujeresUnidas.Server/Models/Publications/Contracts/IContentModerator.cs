namespace MujeresUnidas.Server.Models.Publications.Contracts;

public enum ModerationActionType { Approve, Reject, Hide, Restore, Remove }
public sealed record ModerationDecision(Guid PostId,ModerationActionType Action,string Reason);
/// <summary>Moderation is separate from authors deleting their own content.</summary>
public interface IContentModerator
{
    Task<bool> ModerateAsync(Guid actorId,ModerationDecision decision);
}
