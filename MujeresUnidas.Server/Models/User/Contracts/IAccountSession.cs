namespace MujeresUnidas.Server.Models.User.Contracts;

/// <summary>Server-side revocation and verification; client lock screens are separate policies.</summary>
public interface IAccountSession
{
    Task LogoutAsync(string? bearer);
    Task<MujeresUnidas.Server.Contracts.ApiUser?> GetUserByTokenAsync(string? bearer);
}
