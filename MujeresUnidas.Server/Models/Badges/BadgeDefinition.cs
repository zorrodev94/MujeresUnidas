namespace MujeresUnidas.Server.Models.Badges;

public sealed record BadgeDefinition(Guid Id,string Name,string? Description,string? ImageKey,string VisibilityScope,bool IsActive);
public sealed record BadgeAward(Guid AccountId,Guid BadgeId,Guid AwardedBy,DateTime AwardedUtc,string? Reason);
public interface IBadgeManager
{
    Task<IReadOnlyList<BadgeDefinition>> ListAsync();
    Task<bool> AwardAsync(Guid actorId,Guid accountId,Guid badgeId,string reason);
}
