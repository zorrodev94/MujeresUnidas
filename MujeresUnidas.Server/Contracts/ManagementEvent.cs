namespace MujeresUnidas.Server.Contracts
{
    /// <summary>Recent audited event shown in the institutional dashboard.</summary>
    public sealed record ManagementEvent(string Action, string Account, DateTime CreatedUtc);
}
