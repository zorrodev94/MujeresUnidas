using System.ComponentModel.DataAnnotations;
namespace MujeresUnidas.Server.Models.User.Profiles
{
    /// <summary>Catalogue definition, independent of the User.UserBadgeData assignment.</summary>
    public sealed class Badge
    {
        #region Structs
        public readonly record struct BadgeData(Guid BadgeId,string Name,string Description,
            string IconKey,string BackgroundColor,string TextColor,bool IsProfessional);
        public struct CreateData
        {
            [Required,StringLength(60,MinimumLength=3)] public string Name { get; set; }
            [StringLength(200)] public string? Description { get; set; }
            [Required,StringLength(48)] public string IconKey { get; set; }
            [Required,RegularExpression("^#[0-9A-Fa-f]{6}$")] public string BackgroundColor { get; set; }
            [Required,RegularExpression("^#[0-9A-Fa-f]{6}$")] public string TextColor { get; set; }
        }
        #endregion Structs
    }
}
