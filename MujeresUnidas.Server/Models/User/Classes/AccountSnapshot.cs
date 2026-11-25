using MujeresUnidas.Server.Contracts;
using MujeresUnidas.Server.Models.User.Enums;

namespace MujeresUnidas.Server.Models.User.Classes
{
/// <summary>Private database snapshot used only for authentication inside Server.</summary>
internal sealed record AccountSnapshot(
    Guid Id, string Email, string DisplayName, string? Curp, string? Biography,
    byte Kind, byte Status, string? StaffRole, byte[] PasswordSalt, byte[] PasswordHash)
{
    /// <summary>Converts the snapshot to safe account information without hashes or salts.</summary>
    public ApiUser ToApiUser() => new(Id, Email, DisplayName,
        Status switch
        {
            (byte)AccountStatus.Pending => "Pending",
            (byte)AccountStatus.Active => "Active",
            _ => "Blocked"
        },
        Kind switch
        {
            (byte)AccountKind.Community => "Community",
            (byte)AccountKind.Staff => "Staff",
            _ => "Owner"
        },Curp,Biography,
        StaffRole);
}
}
