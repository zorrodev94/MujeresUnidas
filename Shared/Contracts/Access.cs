namespace MujeresUnidas.Shared.Contracts;
/// <summary>Transport scopes; database and server authorization remain authoritative.</summary>
public enum ApplicationScope { Community, Management }
public enum AccountAccessState { Pending, Active, Declined, Suspended }
public enum UserRoleScope { Member, Staff, Professional, Owner }
public sealed record AccountAccessDto(Guid AccountId, string ApplicationCode, string RoleCode,string AccessStatus);
public sealed record ProfileDto(Guid AccountId,string ApplicationCode,string DisplayName,string? Biography,
    string? AvatarKey,string? CoverKey);
/// <summary>Session operations common to both clients; no SQL access from either client.</summary>
public interface IClientSession { Task SignOutAsync(); }
