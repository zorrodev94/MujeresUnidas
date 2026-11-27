using MujeresUnidas.Comunidad.Connection;

namespace MujeresUnidas.Comunidad.Models.User.Subclasses;

/// <summary>UI behavior for authorized community staff. Server verifies every moderation request.</summary>
public sealed class CommunityStaff(ApiClient api):CommunityMember(api)
{
    public override string PublicRoleLabel=>"Staff de Comunidad";
    public override bool CanReviewBlogs=>true;
}
/// <summary>Institutional professional with community access. No automatic moderation privilege.</summary>
public sealed class CommunityProfessional(ApiClient api):CommunityMember(api)
{
    public override string PublicRoleLabel=>"Profesional institucional";
}
