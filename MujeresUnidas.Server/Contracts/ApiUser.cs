namespace MujeresUnidas.Server.Contracts
{
    /// <summary>Account information returned to its authenticated owner; password and hash are never included.</summary>
    public sealed record ApiUser(Guid Id, string Email, string DisplayName, string Status,
        string Kind, string? Curp, string? Biography, string? Role = null);
}
