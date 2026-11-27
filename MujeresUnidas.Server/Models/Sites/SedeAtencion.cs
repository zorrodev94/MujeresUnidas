using System.ComponentModel.DataAnnotations;

namespace MujeresUnidas.Server.Models.Sites
{
    /// <summary>Institutional care site; demo locations do not represent real service addresses.</summary>
    public sealed class SedeAtencion
    {
        #region Data
        public Guid Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string SiteType { get; init; } = string.Empty;
        public string Municipality { get; init; } = "Ahome";
        public string StateName { get; init; } = "Sinaloa";
        public string AddressNote { get; init; } = "Ubicación por confirmar · DEMOSTRACIÓN";
        public bool IsDemo { get; init; } = true;
        public bool IsActive { get; init; } = true;
        public Guid? ZoneId { get; init; }
        public decimal? Latitude { get; init; }
        public decimal? Longitude { get; init; }
        #endregion

        #region Contracts
        /// <summary>Allowed service types; never infer a real address from a simulated site.</summary>
        public static readonly string[] Types = ["Administrative","Psychology","Legal","CommunityActivities","MultiService"];
        public sealed record CreateData(
            [Required, MinLength(3), MaxLength(24)] string Code,
            [Required, MinLength(4), MaxLength(120)] string DisplayName,
            [Required] string SiteType,
            [MaxLength(180)] string? AddressNote);
        public sealed record CaseSiteData(Guid SiteId, string? Reason = null);
        public sealed record StaffSiteData(Guid StaffId);
        public sealed record ZoneSiteData(Guid ZoneId);
        #endregion

        #region Tasks
        /// <summary>Only sites compatible with the care area may host the corresponding case.</summary>
        public static bool Supports(string area, string siteType) => siteType == "MultiService" ||
            (area == "Psychology" && siteType == "Psychology") ||
            (area == "Legal" && siteType == "Legal") ||
            (area == "SocialWork" && siteType == "Administrative") ||
            (area == "Other" && siteType == "CommunityActivities");
        #endregion
    }
}
