namespace MujeresUnidas.Server.Contracts
{
    /// <summary>Server-calculated counters and audit events for the institutional dashboard.</summary>
    public sealed record ManagementSummary(int PendingAccounts, int ActiveAccounts, int BlockedAccounts,
        int StaffAccounts, IReadOnlyList<ManagementEvent> RecentEvents);
}
