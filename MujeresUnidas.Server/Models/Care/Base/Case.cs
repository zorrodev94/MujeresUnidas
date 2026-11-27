using System.ComponentModel.DataAnnotations;

namespace MujeresUnidas.Server.Models.Care.Base
{
    /// <summary>Confidential care workflow; request, case and appointment permissions are enforced in Server.</summary>
    public sealed class Case
    {
        #region Operation Data
        public static readonly string[] Areas={"Psychology","Legal","SocialWork","Other"};
        #endregion Operation Data
        #region Initialization
        #endregion Initialization
        #region Arrays
        public static readonly string[] States={"UnderReview","InProgress","Resolved","Closed","Withdrawn"};
        #endregion Arrays
        #region Structs
        public readonly record struct RequestData(Guid Id,Guid ApplicantId,string Area,string Subject,string Status,DateTime CreatedUtc);
        public struct CreateRequestData
        {
            [Required,RegularExpression("^(Psychology|Legal|SocialWork|Other)$")] public string Area {get;set;}
            [Required,StringLength(160,MinimumLength=4)] public string Subject {get;set;}
        }
        public readonly record struct CaseData(Guid Id,Guid ApplicantId,string Area,string Status,DateTime OpenedUtc,DateTime? ClosedUtc,
            Guid? SiteId=null,string? SiteName=null);
        public readonly record struct ReviewData(bool Approve);
        public readonly record struct AssignmentData(Guid ProfessionalId);
        public readonly record struct StatusData(string Status);
        #endregion Structs
        #region Task
        public static bool IsArea(string? value)=>Areas.Contains(value);
        #endregion Task
    }
}
