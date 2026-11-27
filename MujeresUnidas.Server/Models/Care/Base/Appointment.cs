using System.ComponentModel.DataAnnotations;

namespace MujeresUnidas.Server.Models.Care.Base
{
    /// <summary>Scheduled care session with a case and a specifically assigned professional.</summary>
    public sealed class Appointment
    {
        #region Operation Data
        #endregion Operation Data
        #region Initialization
        #endregion Initialization
        #region Arrays
        #endregion Arrays
        #region Structs
        public readonly record struct AppointmentData(Guid Id,Guid CaseId,Guid ProfessionalId,string ProfessionalName,
            DateTime ScheduledUtc,int DurationMinutes,string AppointmentType,string Modality,
            string? LocationLabel,string Status,Guid? SiteId=null);
        public struct ScheduleData
        {
            public Guid CaseId {get;set;}
            public Guid ProfessionalId {get;set;}
            public Guid? SiteId {get;set;}
            public DateTime ScheduledUtc {get;set;}
            [Range(15,180)] public int DurationMinutes {get;set;}
            [RegularExpression("^(Initial|FollowUp|Interdisciplinary)$")] public string AppointmentType {get;set;}
            [RegularExpression("^(InPerson|Virtual|Telephone)$")] public string Modality {get;set;}
            [StringLength(160)] public string? LocationLabel {get;set;}
        }
        public readonly record struct StatusData(string Status);
        public readonly record struct SessionData(DateTime StartedUtc,DateTime EndedUtc,string Outcome);
        #endregion Structs
        #region Task
        #endregion Task
    }
}
