-- EarlyAccess R4: additive schema only. NEVER drop a user table or reset the database.
-- Run after 000, 030 and 050 against the intended MujeresUnidas demo database.
USE MujeresUnidas;
GO
-- Extended private profile. Public API never returns this table indiscriminately.
IF COL_LENGTH(N'dbo.Accounts',N'FirstName') IS NULL ALTER TABLE dbo.Accounts ADD FirstName nvarchar(70) NULL;
IF COL_LENGTH(N'dbo.Accounts',N'PaternalLastName') IS NULL ALTER TABLE dbo.Accounts ADD PaternalLastName nvarchar(70) NULL;
IF COL_LENGTH(N'dbo.Accounts',N'MaternalLastName') IS NULL ALTER TABLE dbo.Accounts ADD MaternalLastName nvarchar(70) NULL;
IF COL_LENGTH(N'dbo.Accounts',N'PhoneNumber') IS NULL ALTER TABLE dbo.Accounts ADD PhoneNumber nvarchar(24) NULL;
IF COL_LENGTH(N'dbo.Accounts',N'AlternatePhoneNumber') IS NULL ALTER TABLE dbo.Accounts ADD AlternatePhoneNumber nvarchar(24) NULL;
IF COL_LENGTH(N'dbo.Accounts',N'DateOfBirth') IS NULL ALTER TABLE dbo.Accounts ADD DateOfBirth date NULL;
IF COL_LENGTH(N'dbo.Accounts',N'Address') IS NULL ALTER TABLE dbo.Accounts ADD Address nvarchar(250) NULL;
IF COL_LENGTH(N'dbo.Accounts',N'City') IS NULL ALTER TABLE dbo.Accounts ADD City nvarchar(90) NULL;
IF COL_LENGTH(N'dbo.Accounts',N'State') IS NULL ALTER TABLE dbo.Accounts ADD State nvarchar(90) NULL;
IF COL_LENGTH(N'dbo.Accounts',N'PostalCode') IS NULL ALTER TABLE dbo.Accounts ADD PostalCode nvarchar(12) NULL;
IF COL_LENGTH(N'dbo.Accounts',N'Rfc') IS NULL ALTER TABLE dbo.Accounts ADD Rfc nvarchar(13) NULL;
IF COL_LENGTH(N'dbo.Accounts',N'MustChangePassword') IS NULL ALTER TABLE dbo.Accounts ADD MustChangePassword bit NOT NULL CONSTRAINT DF_Accounts_MustChangePassword DEFAULT(0);
GO
-- Review of a person's demo visit is separate from opening a confidential support Case.
IF OBJECT_ID(N'dbo.ActivationVisits',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.ActivationVisits
 (
  AccountId uniqueidentifier NOT NULL PRIMARY KEY REFERENCES dbo.Accounts(Id),
  ProposedUtc datetime2(3) NULL,
  LocationLabel nvarchar(160) NULL,
  IdentityChecked bit NOT NULL DEFAULT 0,
  CheckedBy uniqueidentifier NULL REFERENCES dbo.Accounts(Id),
  CheckedUtc datetime2(3) NULL,
  Notes nvarchar(200) NULL
 );
END;
GO
-- User badge proposals do not confer permissions.
IF OBJECT_ID(N'dbo.Badges',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.Badges
 (Id uniqueidentifier NOT NULL PRIMARY KEY,
  Name nvarchar(70) NOT NULL UNIQUE,
  Description nvarchar(250) NULL,
  IconKey nvarchar(70) NULL,
  BackgroundColor nvarchar(7) NULL,
  TextColor nvarchar(7) NULL,
  IsProfessional bit NOT NULL DEFAULT(0),
  IsActive bit NOT NULL DEFAULT(1));
END;
IF COL_LENGTH(N'dbo.Badges',N'BackgroundColor') IS NULL ALTER TABLE dbo.Badges ADD BackgroundColor nvarchar(7) NULL;
IF COL_LENGTH(N'dbo.Badges',N'TextColor') IS NULL ALTER TABLE dbo.Badges ADD TextColor nvarchar(7) NULL;
IF COL_LENGTH(N'dbo.Badges',N'IsProfessional') IS NULL ALTER TABLE dbo.Badges ADD IsProfessional bit NOT NULL DEFAULT(0);
IF OBJECT_ID(N'dbo.UserBadges',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.UserBadges
 (UserId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  BadgeId uniqueidentifier NOT NULL REFERENCES dbo.Badges(Id),
  RequestedBy uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  RequestedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  ReviewedBy uniqueidentifier NULL REFERENCES dbo.Accounts(Id),
  ReviewedUtc datetime2(3) NULL,
  Status nvarchar(12) NOT NULL DEFAULT N'Pending',
  CONSTRAINT PK_UserBadges PRIMARY KEY(UserId,BadgeId),
  CONSTRAINT CK_UserBadges_Status CHECK(Status IN (N'Pending',N'Approved',N'Rejected')));
END;
GO
-- One independent cover plus up to five gallery images, enforced transactionally in PostRepository.
IF COL_LENGTH(N'dbo.PostMedia',N'IsCover') IS NULL
 ALTER TABLE dbo.PostMedia ADD IsCover bit NOT NULL CONSTRAINT DF_PostMedia_IsCover DEFAULT(0);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'UX_PostMedia_OneCover' AND object_id=OBJECT_ID(N'dbo.PostMedia'))
 CREATE UNIQUE INDEX UX_PostMedia_OneCover ON dbo.PostMedia(PostId) WHERE IsCover=1;
GO
IF OBJECT_ID(N'dbo.PostDeletionRequests',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.PostDeletionRequests
 (Id uniqueidentifier NOT NULL PRIMARY KEY,
  PostId uniqueidentifier NOT NULL REFERENCES dbo.CommunityPosts(Id),
  RequestedBy uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  RequestedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  Status nvarchar(12) NOT NULL DEFAULT N'Pending',
  ReviewedBy uniqueidentifier NULL REFERENCES dbo.Accounts(Id),
  ReviewedUtc datetime2(3) NULL,
  CONSTRAINT CK_PostDeletion_Status CHECK(Status IN(N'Pending',N'Approved',N'Rejected')));
 CREATE INDEX IX_PostDeletion_Pending ON dbo.PostDeletionRequests(Status,RequestedUtc);
END;
GO
-- Old Conversation.Type kept only for backward compatibility; logical model uses IsPublic.
IF OBJECT_ID(N'dbo.ConversationParticipants',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.ConversationParticipants
 (ConversationId uniqueidentifier NOT NULL REFERENCES dbo.Conversations(Id),
  UserId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  JoinedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  LeftUtc datetime2(3) NULL,
  CONSTRAINT PK_ConversationParticipants PRIMARY KEY(ConversationId,UserId));
 CREATE INDEX IX_ConversationParticipants_User ON dbo.ConversationParticipants(UserId,LeftUtc);
END;
GO
INSERT dbo.ConversationParticipants(ConversationId,UserId,JoinedUtc)
SELECT c.Id,ids.UserId,c.CreatedUtc
FROM dbo.Conversations c
CROSS APPLY (SELECT DISTINCT TRY_CONVERT(uniqueidentifier,[value]) AS UserId FROM OPENJSON(c.ParticipantIdsJson)) ids
JOIN dbo.Accounts a ON a.Id=ids.UserId
WHERE c.Type<>N'Public' AND ids.UserId IS NOT NULL
AND NOT EXISTS(SELECT 1 FROM dbo.ConversationParticipants p WHERE p.ConversationId=c.Id AND p.UserId=ids.UserId);
GO
-- A CaseRequest contains minimum data; detailed case notes are NOT publicly accessible.
IF OBJECT_ID(N'dbo.CaseRequests',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.CaseRequests
 (Id uniqueidentifier NOT NULL PRIMARY KEY,
  ApplicantId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  Area nvarchar(20) NOT NULL,
  Subject nvarchar(160) NOT NULL,
  Status nvarchar(12) NOT NULL DEFAULT N'Pending',
  CreatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  ReviewedBy uniqueidentifier NULL REFERENCES dbo.Accounts(Id),
  ReviewedUtc datetime2(3) NULL,
  CONSTRAINT CK_CaseRequests_Area CHECK(Area IN(N'Psychology',N'Legal',N'SocialWork',N'Other')),
  CONSTRAINT CK_CaseRequests_Status CHECK(Status IN(N'Pending',N'Approved',N'Rejected')));
 CREATE INDEX IX_CaseRequests_Applicant ON dbo.CaseRequests(ApplicantId,CreatedUtc DESC);
END;
GO
IF OBJECT_ID(N'dbo.Cases',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.Cases
 (Id uniqueidentifier NOT NULL PRIMARY KEY,
  RequestId uniqueidentifier NOT NULL UNIQUE REFERENCES dbo.CaseRequests(Id),
  ApplicantId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  Area nvarchar(20) NOT NULL,
  Status nvarchar(20) NOT NULL DEFAULT N'UnderReview',
  OpenedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  ClosedUtc datetime2(3) NULL,
  ClosedReason nvarchar(160) NULL,
  CONSTRAINT CK_Cases_Status CHECK(Status IN(N'UnderReview',N'InProgress',N'Resolved',N'Closed',N'Withdrawn')));
 CREATE INDEX IX_Cases_Applicant ON dbo.Cases(ApplicantId,OpenedUtc DESC);
END;
GO
IF OBJECT_ID(N'dbo.CaseAssignments',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.CaseAssignments
 (CaseId uniqueidentifier NOT NULL REFERENCES dbo.Cases(Id),
  ProfessionalId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  AssignedBy uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  AssignedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  IsActive bit NOT NULL DEFAULT(1),
  CONSTRAINT PK_CaseAssignments PRIMARY KEY(CaseId,ProfessionalId));
END;
GO
IF OBJECT_ID(N'dbo.Appointments',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.Appointments
 (Id uniqueidentifier NOT NULL PRIMARY KEY,
  CaseId uniqueidentifier NOT NULL REFERENCES dbo.Cases(Id),
  ProfessionalId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  ScheduledUtc datetime2(3) NOT NULL,
  DurationMinutes int NOT NULL,
  AppointmentType nvarchar(22) NOT NULL,
  Modality nvarchar(15) NOT NULL,
  LocationLabel nvarchar(160) NULL,
  Status nvarchar(16) NOT NULL DEFAULT N'Scheduled',
  CreatedBy uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  CreatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  CONSTRAINT CK_Appointments_Duration CHECK(DurationMinutes BETWEEN 15 AND 180),
  CONSTRAINT CK_Appointments_Type CHECK(AppointmentType IN(N'Initial',N'FollowUp',N'Interdisciplinary')),
  CONSTRAINT CK_Appointments_Mode CHECK(Modality IN(N'InPerson',N'Virtual',N'Telephone')),
  CONSTRAINT CK_Appointments_Status CHECK(Status IN(N'Scheduled',N'Confirmed',N'Completed',N'Cancelled',N'NoShow')));
 CREATE INDEX IX_Appointments_Case ON dbo.Appointments(CaseId,ScheduledUtc);
END;
GO
IF OBJECT_ID(N'dbo.AppointmentSessions',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.AppointmentSessions
 (Id uniqueidentifier NOT NULL PRIMARY KEY,
  AppointmentId uniqueidentifier NOT NULL UNIQUE REFERENCES dbo.Appointments(Id),
  StartedUtc datetime2(3) NOT NULL,
  EndedUtc datetime2(3) NOT NULL,
  Outcome nvarchar(80) NOT NULL,
  RecordedBy uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  CreatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  CONSTRAINT CK_AppointmentSessions_Time CHECK(EndedUtc>=StartedUtc));
END;
GO
PRINT N'R4 migrations applied. Existing data preserved. No case/appointment demo records seeded.';
