using MujeresUnidas.Server.Models.User.Services;

namespace MujeresUnidas.Server.Models.User.Subclasses;

/// <summary>Community moderator policy. Its grants still require an authenticated, scoped token.</summary>
public sealed class CommunityStaff(AccountService accounts):CommunityMember(accounts)
{
    public override bool CanModerateBlogs=>true;
    public override string PublicRoleLabel=>"Staff de Comunidad";
}
/// <summary>Institutional professional may enter Community but is NOT a moderator by inheritance.</summary>
public sealed class CommunityProfessional(AccountService accounts):CommunityMember(accounts)
{
    public override string PublicRoleLabel=>"Profesional institucional";
}
