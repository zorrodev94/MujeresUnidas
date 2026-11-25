namespace MujeresUnidas.Server.Contracts
{
    /// <summary>Authentication token and account summary after a successful login.</summary>
    public sealed record AuthResponse(string Token, DateTime ExpiresUtc, ApiUser User);
}
