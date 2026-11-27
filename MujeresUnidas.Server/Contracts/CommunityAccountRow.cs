namespace MujeresUnidas.Server.Contracts
{
    /// <summary>Confidential registration row returned only to authorized institutional staff.</summary>
    public sealed record CommunityAccountRow(Guid Id, string Email, string DisplayName,
        string? Curp, string Status, string ActivationStatus, DateTime CreatedUtc, string CommunityRole,DateTime? ProposedVisitUtc=null,bool IdentityChecked=false,string? LocationLabel=null);
}
