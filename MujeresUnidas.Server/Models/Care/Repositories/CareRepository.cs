using System.Data;
using Microsoft.Data.SqlClient;
using MujeresUnidas.Server.Infrastructure.Database;
using MujeresUnidas.Server.Models.Care.Base;
using CareCase=MujeresUnidas.Server.Models.Care.Base.Case;

namespace MujeresUnidas.Server.Models.Care.Repositories
{
    /// <summary>Parameterized, account-scoped persistence for confidential appointments and case requests.</summary>
    public sealed class CareRepository(Database database)
    {
        #region Operation Data
        private const string AccessCase="(c.ApplicantId=@actor OR EXISTS(SELECT 1 FROM dbo.CaseAssignments ca WHERE ca.CaseId=c.Id AND ca.ProfessionalId=@actor AND ca.IsActive=1))";
        #endregion Operation Data
        #region Initialization
        #endregion Initialization
        #region Arrays
        #endregion Arrays
        #region Structs
        #endregion Structs
        #region Task
        public async Task<Guid?> RequestAsync(Guid actor,CareCase.CreateRequestData data)
        {
            if(!CareCase.IsArea(data.Area)||string.IsNullOrWhiteSpace(data.Subject)||data.Subject.Trim().Length is < 4 or > 160)return null;
            var id=Guid.NewGuid();
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"INSERT dbo.CaseRequests(Id,ApplicantId,Area,Subject)
SELECT @id,@actor,@area,@subject WHERE EXISTS(SELECT 1 FROM dbo.Accounts WHERE Id=@actor AND Kind=0 AND Status=1)",db);
            cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
            cmd.Parameters.Add("@area",SqlDbType.NVarChar,20).Value=data.Area;
            cmd.Parameters.Add("@subject",SqlDbType.NVarChar,160).Value=data.Subject.Trim();
            return await cmd.ExecuteNonQueryAsync()==1?id:null;
        }
        public async Task<IReadOnlyList<CareCase.RequestData>> RequestsAsync(Guid actor,bool reviewer)
        {
            var result=new List<CareCase.RequestData>();
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"SELECT TOP (100) Id,ApplicantId,Area,Subject,Status,CreatedUtc
FROM dbo.CaseRequests WHERE (@reviewer=1 OR ApplicantId=@actor) ORDER BY CreatedUtc DESC",db);
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
            cmd.Parameters.Add("@reviewer",SqlDbType.Bit).Value=reviewer;
            await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync())result.Add(new(r.GetGuid(0),r.GetGuid(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetDateTime(5)));
            return result;
        }
        public async Task<Guid?> ReviewAsync(Guid requestId,Guid reviewer,bool approved)
        {
            await using var db=await database.OpenAsync();
            await using var tx=(SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable);
            var id=Guid.NewGuid();
            await using var cmd=new SqlCommand(@"UPDATE dbo.CaseRequests
SET Status=@status,ReviewedBy=@reviewer,ReviewedUtc=SYSUTCDATETIME()
OUTPUT inserted.ApplicantId,inserted.Area WHERE Id=@request AND Status=N'Pending'",db,tx);
            cmd.Parameters.Add("@status",SqlDbType.NVarChar,12).Value=approved?"Approved":"Rejected";
            cmd.Parameters.Add("@reviewer",SqlDbType.UniqueIdentifier).Value=reviewer;
            cmd.Parameters.Add("@request",SqlDbType.UniqueIdentifier).Value=requestId;
            Guid applicant;string area;
            await using(var r=await cmd.ExecuteReaderAsync())
            {
                if(!await r.ReadAsync()){await r.CloseAsync();await tx.RollbackAsync();return null;}
                applicant=r.GetGuid(0);area=r.GetString(1);
            }
            if(approved)
            {
                await using var insert=new SqlCommand(@"INSERT dbo.Cases(Id,RequestId,ApplicantId,Area)
VALUES(@id,@request,@applicant,@area)",db,tx);
                insert.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
                insert.Parameters.Add("@request",SqlDbType.UniqueIdentifier).Value=requestId;
                insert.Parameters.Add("@applicant",SqlDbType.UniqueIdentifier).Value=applicant;
                insert.Parameters.Add("@area",SqlDbType.NVarChar,20).Value=area;
                await insert.ExecuteNonQueryAsync();
            }
            await using var notice=new SqlCommand(@"INSERT dbo.Notifications(Id,RecipientId,ActorId,Type,Title,RelatedId)
VALUES(NEWID(),@applicant,@reviewer,N'Care',@title,@related)",db,tx);
            notice.Parameters.Add("@applicant",SqlDbType.UniqueIdentifier).Value=applicant;
            notice.Parameters.Add("@reviewer",SqlDbType.UniqueIdentifier).Value=reviewer;
            notice.Parameters.Add("@title",SqlDbType.NVarChar,160).Value=approved?"Tu solicitud de atención fue aceptada.":"Tu solicitud de atención fue revisada.";
            notice.Parameters.Add("@related",SqlDbType.UniqueIdentifier).Value=approved?id:requestId;
            await notice.ExecuteNonQueryAsync();await tx.CommitAsync();return approved?id:Guid.Empty;
        }
        public async Task<IReadOnlyList<CareCase.CaseData>> CasesAsync(Guid actor)
        {
            var result=new List<CareCase.CaseData>();
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"SELECT TOP(100) c.Id,c.ApplicantId,c.Area,c.Status,c.OpenedUtc,c.ClosedUtc,c.SiteId,s.DisplayName
FROM dbo.Cases c LEFT JOIN dbo.CareSites s ON s.Id=c.SiteId WHERE "+AccessCase+" ORDER BY c.OpenedUtc DESC",db);
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
            await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync())result.Add(new(r.GetGuid(0),r.GetGuid(1),r.GetString(2),r.GetString(3),r.GetDateTime(4),r.IsDBNull(5)?null:r.GetDateTime(5),r.IsDBNull(6)?null:r.GetGuid(6),r.IsDBNull(7)?null:r.GetString(7)));
            return result;
        }
        public async Task<bool> AssignAsync(Guid caseId,Guid professional,Guid manager)
        {
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"MERGE dbo.CaseAssignments WITH(HOLDLOCK) AS target
USING (SELECT @case AS CaseId,@professional AS ProfessionalId) AS source
ON target.CaseId=source.CaseId AND target.ProfessionalId=source.ProfessionalId
WHEN MATCHED THEN UPDATE SET IsActive=1,AssignedBy=@manager,AssignedUtc=SYSUTCDATETIME()
WHEN NOT MATCHED BY TARGET THEN INSERT(CaseId,ProfessionalId,AssignedBy) VALUES(@case,@professional,@manager);
",db);
            // Explicit assignment only: no automatic blanket access for Owner or Administrator.
            await using var eligibility=new SqlCommand(@"SELECT COUNT(*) FROM dbo.Accounts p JOIN dbo.Cases c ON c.Id=@case
WHERE p.Id=@professional AND p.Kind=1 AND p.Status=1
AND ((c.Area=N'Psychology' AND p.StaffRole=N'Psychology') OR
     (c.Area=N'Legal' AND p.StaffRole=N'Legal') OR
     (c.Area=N'SocialWork' AND p.StaffRole=N'SocialWork') OR
     (c.Area=N'Other' AND p.StaffRole IN(N'Psychology',N'Legal',N'SocialWork')))",db);
            eligibility.Parameters.Add("@professional",SqlDbType.UniqueIdentifier).Value=professional;
            eligibility.Parameters.Add("@case",SqlDbType.UniqueIdentifier).Value=caseId;
            if(Convert.ToInt32(await eligibility.ExecuteScalarAsync())!=1)return false;
            await using var exists=new SqlCommand("SELECT COUNT(*) FROM dbo.Cases WHERE Id=@case AND Status NOT IN(N'Closed',N'Withdrawn')",db);
            exists.Parameters.Add("@case",SqlDbType.UniqueIdentifier).Value=caseId;
            if(Convert.ToInt32(await exists.ExecuteScalarAsync())!=1)return false;
            cmd.Parameters.Add("@case",SqlDbType.UniqueIdentifier).Value=caseId;
            cmd.Parameters.Add("@professional",SqlDbType.UniqueIdentifier).Value=professional;
            cmd.Parameters.Add("@manager",SqlDbType.UniqueIdentifier).Value=manager;
            await cmd.ExecuteNonQueryAsync();return true;
        }
        public async Task<bool> StatusAsync(Guid caseId,Guid actor,string status)
        {
            if(status is not("UnderReview" or "InProgress" or "Resolved" or "Closed" or "Withdrawn"))return false;
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"UPDATE dbo.Cases SET Status=@status,
ClosedUtc=CASE WHEN @status IN(N'Resolved',N'Closed',N'Withdrawn') THEN SYSUTCDATETIME() ELSE NULL END
WHERE Id=@case AND Status NOT IN(N'Resolved',N'Closed',N'Withdrawn') AND EXISTS
(SELECT 1 FROM dbo.CaseAssignments WHERE CaseId=@case AND ProfessionalId=@actor AND IsActive=1)",db);
            cmd.Parameters.Add("@case",SqlDbType.UniqueIdentifier).Value=caseId;
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
            cmd.Parameters.Add("@status",SqlDbType.NVarChar,20).Value=status;
            return await cmd.ExecuteNonQueryAsync()==1;
        }
        public async Task<IReadOnlyList<(Guid Id,string DisplayName,string Role)>> ProfessionalsAsync()
        {
            var result=new List<(Guid,string,string)>();
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"SELECT Id,DisplayName,StaffRole FROM dbo.Accounts
WHERE Kind=1 AND Status=1 AND StaffRole IN(N'Psychology',N'Legal',N'SocialWork')
ORDER BY DisplayName",db);
            await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync())result.Add((r.GetGuid(0),r.GetString(1),r.GetString(2)));
            return result;
        }
        /// <summary>Administrative overview excludes identifying applicant details and confidential notes.</summary>
        public async Task<IReadOnlyList<CareCase.CaseData>> ManagementCasesAsync()
        {
            var result=new List<CareCase.CaseData>();
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"SELECT TOP(100) c.Id,c.Area,c.Status,c.OpenedUtc,c.ClosedUtc,c.SiteId,s.DisplayName
FROM dbo.Cases c LEFT JOIN dbo.CareSites s ON s.Id=c.SiteId ORDER BY c.OpenedUtc DESC",db);
            await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync())result.Add(new(r.GetGuid(0),Guid.Empty,r.GetString(1),r.GetString(2),r.GetDateTime(3),r.IsDBNull(4)?null:r.GetDateTime(4),r.IsDBNull(5)?null:r.GetGuid(5),r.IsDBNull(6)?null:r.GetString(6)));
            return result;
        }
        public async Task<IReadOnlyList<Appointment.AppointmentData>> ManagementAppointmentsAsync()
        {
            var result=new List<Appointment.AppointmentData>();
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"SELECT TOP(100) a.Id,a.CaseId,a.ProfessionalId,p.DisplayName,
a.ScheduledUtc,a.DurationMinutes,a.AppointmentType,a.Modality,a.LocationLabel,a.Status,a.SiteId
FROM dbo.Appointments a JOIN dbo.Accounts p ON p.Id=a.ProfessionalId
ORDER BY a.ScheduledUtc DESC",db);
            await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync())result.Add(new(r.GetGuid(0),r.GetGuid(1),r.GetGuid(2),r.GetString(3),r.GetDateTime(4),r.GetInt32(5),r.GetString(6),r.GetString(7),r.IsDBNull(8)?null:r.GetString(8),r.GetString(9),r.IsDBNull(10)?null:r.GetGuid(10)));
            return result;
        }
        public async Task<Guid?> ScheduleAsync(Guid scheduler,Appointment.ScheduleData data)
        {
            if(data.ScheduledUtc<=DateTime.UtcNow || data.ScheduledUtc>DateTime.UtcNow.AddYears(1) ||
               data.DurationMinutes is < 15 or > 180 || data.AppointmentType is not ("Initial" or "FollowUp" or "Interdisciplinary") ||
               data.Modality is not ("InPerson" or "Virtual" or "Telephone") || (data.LocationLabel?.Length??0)>160)return null;
            var id=Guid.NewGuid();
            await using var db=await database.OpenAsync();
            await using var tx=(SqlTransaction)await db.BeginTransactionAsync();
            await using var cmd=new SqlCommand(@"INSERT dbo.Appointments
(Id,CaseId,ProfessionalId,ScheduledUtc,DurationMinutes,AppointmentType,Modality,LocationLabel,CreatedBy,SiteId)
SELECT @id,c.Id,@professional,@when,@duration,@type,@modality,s.DisplayName,@actor,s.Id
FROM dbo.Cases c JOIN dbo.CareSites s ON s.Id=c.SiteId AND s.IsActive=1
WHERE c.Id=@case AND c.Status IN(N'UnderReview',N'InProgress')
AND (@site IS NULL OR @site=s.Id)
AND EXISTS(SELECT 1 FROM dbo.CaseAssignments ca WHERE ca.CaseId=c.Id AND ca.ProfessionalId=@professional AND ca.IsActive=1)
AND EXISTS(SELECT 1 FROM dbo.CareSiteStaff css WHERE css.SiteId=s.Id AND css.StaffId=@professional AND css.IsActive=1)
AND EXISTS(SELECT 1 FROM dbo.Accounts p WHERE p.Id=@professional AND p.Status=1 AND p.Kind=1
 AND ((c.Area=N'Psychology' AND p.StaffRole=N'Psychology') OR
      (c.Area=N'Legal' AND p.StaffRole=N'Legal') OR
      (c.Area=N'SocialWork' AND p.StaffRole=N'SocialWork') OR c.Area=N'Other'))",db,tx);
            cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
            cmd.Parameters.Add("@case",SqlDbType.UniqueIdentifier).Value=data.CaseId;
            cmd.Parameters.Add("@professional",SqlDbType.UniqueIdentifier).Value=data.ProfessionalId;
            cmd.Parameters.Add("@when",SqlDbType.DateTime2).Value=data.ScheduledUtc.ToUniversalTime();
            cmd.Parameters.Add("@duration",SqlDbType.Int).Value=data.DurationMinutes;
            cmd.Parameters.Add("@type",SqlDbType.NVarChar,22).Value=data.AppointmentType;
            cmd.Parameters.Add("@modality",SqlDbType.NVarChar,15).Value=data.Modality;
            cmd.Parameters.Add("@site",SqlDbType.UniqueIdentifier).Value=(object?)data.SiteId??DBNull.Value;
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=scheduler;
            if(await cmd.ExecuteNonQueryAsync()!=1){await tx.RollbackAsync();return null;}
            await using var notify=new SqlCommand(@"INSERT dbo.Notifications
(Id,RecipientId,ActorId,Type,Title,RelatedId)
SELECT NEWID(),c.ApplicantId,@actor,N'Appointment',
N'Cita programada. Consulta fecha, hora y lugar en Mis citas.',@appointment
FROM dbo.Cases c WHERE c.Id=@case",db,tx);
            notify.Parameters.Add("@case",SqlDbType.UniqueIdentifier).Value=data.CaseId;
            notify.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=scheduler;
            notify.Parameters.Add("@appointment",SqlDbType.UniqueIdentifier).Value=id;
            await notify.ExecuteNonQueryAsync();
            await tx.CommitAsync();return id;
        }
        public async Task<IReadOnlyList<Appointment.AppointmentData>> AppointmentsAsync(Guid actor)
        {
            var result=new List<Appointment.AppointmentData>();
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"SELECT TOP(100) a.Id,a.CaseId,a.ProfessionalId,u.DisplayName,a.ScheduledUtc,
a.DurationMinutes,a.AppointmentType,a.Modality,a.LocationLabel,a.Status,a.SiteId
FROM dbo.Appointments a JOIN dbo.Cases c ON c.Id=a.CaseId
JOIN dbo.Accounts u ON u.Id=a.ProfessionalId
WHERE "+AccessCase+" ORDER BY a.ScheduledUtc DESC",db);
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
            await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync())result.Add(new(r.GetGuid(0),r.GetGuid(1),r.GetGuid(2),r.GetString(3),r.GetDateTime(4),r.GetInt32(5),r.GetString(6),r.GetString(7),r.IsDBNull(8)?null:r.GetString(8),r.GetString(9),r.IsDBNull(10)?null:r.GetGuid(10)));
            return result;
        }
        public async Task<bool> UpdateAppointmentAsync(Guid appointmentId,Guid actor,string status,bool schedulingAuthority)
        {
            if(status is not("Confirmed" or "Cancelled" or "NoShow"))return false;
            await using var db=await database.OpenAsync();
            await using var tx=(SqlTransaction)await db.BeginTransactionAsync();
            await using var cmd=new SqlCommand(@"UPDATE a SET Status=@status FROM dbo.Appointments a
WHERE a.Id=@id AND a.Status IN(N'Scheduled',N'Confirmed') AND
((@scheduler=1 AND @status IN(N'Confirmed',N'Cancelled')) OR
 EXISTS(SELECT 1 FROM dbo.CaseAssignments ca WHERE ca.CaseId=a.CaseId AND ca.ProfessionalId=@actor AND ca.IsActive=1))",db);
            cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=appointmentId;
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
            cmd.Parameters.Add("@status",SqlDbType.NVarChar,16).Value=status;
            cmd.Parameters.Add("@scheduler",SqlDbType.Bit).Value=schedulingAuthority;
            if(await cmd.ExecuteNonQueryAsync()!=1){await tx.RollbackAsync();return false;}
            await using var notify=new SqlCommand(@"INSERT dbo.Notifications(Id,RecipientId,ActorId,Type,Title,RelatedId)
SELECT NEWID(),c.ApplicantId,@actor,N'Appointment',@title,a.Id
FROM dbo.Appointments a JOIN dbo.Cases c ON c.Id=a.CaseId WHERE a.Id=@appointment",db,tx);
            notify.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
            notify.Parameters.Add("@appointment",SqlDbType.UniqueIdentifier).Value=appointmentId;
            notify.Parameters.Add("@title",SqlDbType.NVarChar,100).Value=status switch
            {
                "Cancelled"=>"Tu cita fue cancelada. Consulta Mis citas para detalles.",
                "NoShow"=>"Tu cita se registró como no asistida. Consulta Mis citas.",
                _=>"Tu cita fue confirmada. Consulta fecha, hora y lugar en Mis citas."
            };
            await notify.ExecuteNonQueryAsync();
            await tx.CommitAsync();return true;
        }
        /// <summary>Records one completed professional session with the assigned specialist; no intimate notes in this demo.</summary>
        public async Task<bool> CompleteSessionAsync(Guid appointmentId,Guid actor,Appointment.SessionData data)
        {
            if(data.StartedUtc==default || data.EndedUtc==default || data.EndedUtc<data.StartedUtc ||
               data.EndedUtc>DateTime.UtcNow.AddMinutes(5) ||
               string.IsNullOrWhiteSpace(data.Outcome) || data.Outcome.Length>80)return false;
            await using var db=await database.OpenAsync();
            await using var tx=(SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable);
            await using var update=new SqlCommand(@"UPDATE a SET Status=N'Completed'
FROM dbo.Appointments a WHERE a.Id=@id AND a.Status=N'Confirmed' AND
EXISTS(SELECT 1 FROM dbo.CaseAssignments ca WHERE ca.CaseId=a.CaseId
AND ca.ProfessionalId=@actor AND ca.IsActive=1)",db,tx);
            update.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=appointmentId;
            update.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
            if(await update.ExecuteNonQueryAsync()!=1){await tx.RollbackAsync();return false;}
            await using var insert=new SqlCommand(@"INSERT dbo.AppointmentSessions
(Id,AppointmentId,StartedUtc,EndedUtc,Outcome,RecordedBy)
VALUES(NEWID(),@id,@started,@ended,@outcome,@actor)",db,tx);
            insert.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=appointmentId;
            insert.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
            insert.Parameters.Add("@started",SqlDbType.DateTime2).Value=data.StartedUtc.ToUniversalTime();
            insert.Parameters.Add("@ended",SqlDbType.DateTime2).Value=data.EndedUtc.ToUniversalTime();
            insert.Parameters.Add("@outcome",SqlDbType.NVarChar,80).Value=data.Outcome.Trim();
            await insert.ExecuteNonQueryAsync();await tx.CommitAsync();return true;
        }
        #endregion Task
    }
}
