namespace MujeresUnidas.Shared.Contracts;
public sealed record BadgeDefinitionDto(Guid Id,string Name,string? Description,string? ImageKey,
    string Scope,bool IsActive);
public sealed record AwardedBadgeDto(Guid BadgeId,string Name,string? Description,string Scope,DateTime AwardedUtc);
