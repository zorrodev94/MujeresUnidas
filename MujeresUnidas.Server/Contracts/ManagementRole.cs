namespace MujeresUnidas.Server.Contracts
{
    /// <summary>Role option exposed to Gestión without granting permissions based on its display text.</summary>
    public sealed record ManagementRole(string Code, string DisplayName);
}
