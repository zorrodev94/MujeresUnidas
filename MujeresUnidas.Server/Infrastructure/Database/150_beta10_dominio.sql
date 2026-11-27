-- Mujeres Unidas · Beta 1.0 · Expansion incremental (Ahome)
-- Requires 000,030,050,070,080,100,120,140. Non-destructive: NEVER drops existing data.
USE MujeresUnidas;
GO
-- Identity is not the same as access to an application; Accounts.Kind remains for compatibility.
IF OBJECT_ID(N'dbo.ApplicationAccess',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.ApplicationAccess
 (
  AccountId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  ApplicationCode nvarchar(12) NOT NULL,
  RoleCode nvarchar(64) NOT NULL,
  AccessStatus nvarchar(12) NOT NULL CONSTRAINT DF_AppAccess_Status DEFAULT N'Pending',
  GrantedBy uniqueidentifier NULL REFERENCES dbo.Accounts(Id),
  GrantedUtc datetime2(3) NULL,
  CreatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  CONSTRAINT PK_ApplicationAccess PRIMARY KEY(AccountId,ApplicationCode),
  CONSTRAINT CK_ApplicationAccess_App CHECK(ApplicationCode IN(N'Community',N'Management')),
  CONSTRAINT CK_ApplicationAccess_Status CHECK(AccessStatus IN(N'Pending',N'Active',N'Declined',N'Suspended')),
  CONSTRAINT CK_ApplicationAccess_Role CHECK (
   (ApplicationCode=N'Community' AND RoleCode IN(N'Member',N'Staff',N'Professional')) OR
   (ApplicationCode=N'Management' AND LEN(RoleCode)>0))
 );
 CREATE INDEX IX_AppAccess_Inbox ON dbo.ApplicationAccess(ApplicationCode,AccessStatus,CreatedUtc);
END;
GO
-- Seed access only for existing accounts; no automatic cross-client permissions.
INSERT dbo.ApplicationAccess(AccountId,ApplicationCode,RoleCode,AccessStatus,GrantedBy,GrantedUtc)
SELECT a.Id,CASE WHEN a.Kind=0 THEN N'Community' ELSE N'Management' END,
 CASE WHEN a.Kind=0 THEN COALESCE(a.CommunityRole,N'Member')
      WHEN a.Kind=2 THEN N'Owner' ELSE COALESCE(a.StaffRole,N'Professional') END,
 CASE WHEN a.Status=1 THEN N'Active' WHEN a.Status=2 THEN N'Suspended' ELSE N'Pending' END,
 a.ActivatedBy,a.ActivatedUtc
FROM dbo.Accounts a WHERE NOT EXISTS(
 SELECT 1 FROM dbo.ApplicationAccess x WHERE x.AccountId=a.Id AND x.ApplicationCode=CASE WHEN a.Kind=0 THEN N'Community' ELSE N'Management' END);
GO
-- Separate DDL/DML into batches: SQL Server binds column names before the ALTER runs.
IF COL_LENGTH(N'dbo.Sessions',N'ApplicationCode') IS NULL
 ALTER TABLE dbo.Sessions ADD ApplicationCode nvarchar(12) NULL;
GO
UPDATE s SET ApplicationCode=CASE WHEN a.Kind=0 THEN N'Community' ELSE N'Management' END
FROM dbo.Sessions s JOIN dbo.Accounts a ON a.Id=s.AccountId WHERE s.ApplicationCode IS NULL;
GO
IF EXISTS(SELECT 1 FROM dbo.Sessions WHERE ApplicationCode IS NULL)
 THROW 51150,N'Sesiones sin aplicación. Revisa la base antes de continuar.',1;
IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Sessions') AND name=N'ApplicationCode' AND is_nullable=1)
 ALTER TABLE dbo.Sessions ALTER COLUMN ApplicationCode nvarchar(12) NOT NULL;
GO
-- Application-scoped profile attributes: never copy management media into Community.
IF OBJECT_ID(N'dbo.ApplicationProfiles',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.ApplicationProfiles
 (
  AccountId uniqueidentifier NOT NULL,
  ApplicationCode nvarchar(12) NOT NULL,
  DisplayName nvarchar(100) NOT NULL,
  Biography nvarchar(500) NULL,
  AvatarAssetId uniqueidentifier NULL,
  CoverAssetId uniqueidentifier NULL,
  UpdatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  CONSTRAINT PK_ApplicationProfiles PRIMARY KEY(AccountId,ApplicationCode),
  CONSTRAINT FK_AppProfiles_Access FOREIGN KEY(AccountId,ApplicationCode) REFERENCES dbo.ApplicationAccess(AccountId,ApplicationCode)
 );
END;
GO
INSERT dbo.ApplicationProfiles(AccountId,ApplicationCode,DisplayName,Biography)
SELECT x.AccountId,x.ApplicationCode,a.DisplayName,a.Biography
FROM dbo.ApplicationAccess x JOIN dbo.Accounts a ON a.Id=x.AccountId
WHERE NOT EXISTS(SELECT 1 FROM dbo.ApplicationProfiles p WHERE p.AccountId=x.AccountId AND p.ApplicationCode=x.ApplicationCode);
GO
-- Images belong to one application profile; management and community never share media.
IF OBJECT_ID(N'dbo.ApplicationProfileMedia',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.ApplicationProfileMedia
 (
  AccountId uniqueidentifier NOT NULL,
  ApplicationCode nvarchar(12) NOT NULL,
  AvatarFile nvarchar(90) NULL,
  CoverFile nvarchar(90) NULL,
  UpdatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  CONSTRAINT PK_ApplicationProfileMedia PRIMARY KEY(AccountId,ApplicationCode),
  CONSTRAINT FK_ApplicationProfileMedia_Profile FOREIGN KEY(AccountId,ApplicationCode) REFERENCES dbo.ApplicationProfiles(AccountId,ApplicationCode)
 );
END;
GO
-- Existing photos retained in their original application; no automatic copy across applications.
INSERT dbo.ApplicationProfileMedia(AccountId,ApplicationCode,AvatarFile,CoverFile)
SELECT p.AccountId,CASE WHEN a.Kind=0 THEN N'Community' ELSE N'Management' END,p.AvatarFile,p.CoverFile
FROM dbo.ProfileMedia p JOIN dbo.Accounts a ON a.Id=p.AccountId
WHERE NOT EXISTS(SELECT 1 FROM dbo.ApplicationProfileMedia x WHERE x.AccountId=p.AccountId
AND x.ApplicationCode=CASE WHEN a.Kind=0 THEN N'Community' ELSE N'Management' END);
GO
-- Portable, non-georeferenced conceptual sectors (NOT authoritative risk boundaries).
IF OBJECT_ID(N'dbo.ServiceZones',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.ServiceZones
 (
  Id uniqueidentifier NOT NULL CONSTRAINT PK_ServiceZones PRIMARY KEY DEFAULT NEWID(),
  Code nvarchar(12) NOT NULL CONSTRAINT UQ_ServiceZones_Code UNIQUE,
  DisplayName nvarchar(80) NOT NULL,
  Municipality nvarchar(65) NOT NULL DEFAULT N'Ahome',
  GeometryGeoJson nvarchar(max) NULL,
  GeometrySource nvarchar(200) NULL,
  IsConceptual bit NOT NULL DEFAULT 1,
  IsActive bit NOT NULL DEFAULT 1,
  CONSTRAINT CK_ServiceZones_Code CHECK(Code IN(N'N',N'S',N'E',N'O')),
  CONSTRAINT CK_ServiceZones_Geometry CHECK(GeometryGeoJson IS NULL OR ISJSON(GeometryGeoJson)=1)
 );
END;
GO
-- Only four conceptual directional categories. No fictional street addresses.
INSERT dbo.ServiceZones(Id,Code,DisplayName)
SELECT NEWID(),v.Code,v.DisplayName
FROM (VALUES (N'N',N'Norte'),(N'S',N'Sur'),(N'E',N'Este'),(N'O',N'Oeste'))v(Code,DisplayName)
WHERE NOT EXISTS(SELECT 1 FROM dbo.ServiceZones z WHERE z.Code=v.Code);
GO
IF COL_LENGTH(N'dbo.CareSites',N'ZoneId') IS NULL
 ALTER TABLE dbo.CareSites ADD ZoneId uniqueidentifier NULL REFERENCES dbo.ServiceZones(Id);
IF COL_LENGTH(N'dbo.CareSites',N'Latitude') IS NULL
 ALTER TABLE dbo.CareSites ADD Latitude decimal(9,6) NULL;
IF COL_LENGTH(N'dbo.CareSites',N'Longitude') IS NULL
 ALTER TABLE dbo.CareSites ADD Longitude decimal(9,6) NULL;
GO
IF OBJECT_ID(N'dbo.CaseSiteTransfers',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.CaseSiteTransfers
 (
  Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWID(),
  CaseId uniqueidentifier NOT NULL REFERENCES dbo.Cases(Id),
  PreviousSiteId uniqueidentifier NULL REFERENCES dbo.CareSites(Id),
  NewSiteId uniqueidentifier NOT NULL REFERENCES dbo.CareSites(Id),
  Reason nvarchar(250) NOT NULL,
  ChangedBy uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  ChangedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME()
 );
 CREATE INDEX IX_CaseSiteTransfers_Case ON dbo.CaseSiteTransfers(CaseId,ChangedUtc);
END;
GO
-- General indicators; observations are recorded independently of targets.
IF OBJECT_ID(N'dbo.MetricDefinitions',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.MetricDefinitions
 (
  Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWID(),
  Code nvarchar(40) NOT NULL UNIQUE,
  Title nvarchar(130) NOT NULL,
  Unit nvarchar(24) NOT NULL,
  Direction nvarchar(12) NOT NULL,
  DataSource nvarchar(120) NOT NULL,
  IsActive bit NOT NULL DEFAULT 1,
  CONSTRAINT CK_Metric_Direction CHECK(Direction IN(N'Increase',N'Decrease',N'Monitor'))
 );
END;
GO
INSERT dbo.MetricDefinitions(Code,Title,Unit,Direction,DataSource)
SELECT v.Code,v.Title,v.Unit,v.Direction,v.DataSource
FROM (VALUES
 (N'COMPLETED_SESSIONS',N'Atenciones completadas',N'Sesiones',N'Increase',N'AppointmentSessions'),
 (N'CASES_RESOLVED',N'Casos resueltos',N'Casos',N'Increase',N'Cases'),
 (N'ACTIVE_MEMBERS',N'Integrantes activas',N'Personas',N'Increase',N'ApplicationAccess'),
 (N'EDUCATION_RATE',N'Participación en estudios declarada',N'Porcentaje',N'Increase',N'CommunityOutcomeSnapshots'),
 (N'EMPLOYMENT_RATE',N'Participación laboral declarada',N'Porcentaje',N'Increase',N'CommunityOutcomeSnapshots'),
 (N'REPORTS_RECORDED',N'Reportes recibidos',N'Reportes',N'Monitor',N'IncidentReports')
 )v(Code,Title,Unit,Direction,DataSource)
WHERE NOT EXISTS(SELECT 1 FROM dbo.MetricDefinitions m WHERE m.Code=v.Code);
GO
IF OBJECT_ID(N'dbo.MetricGoals',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.MetricGoals
 (
  Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWID(),
  MetricId uniqueidentifier NOT NULL REFERENCES dbo.MetricDefinitions(Id),
  GoalYear smallint NOT NULL,
  ZoneId uniqueidentifier NULL REFERENCES dbo.ServiceZones(Id),
  SiteId uniqueidentifier NULL REFERENCES dbo.CareSites(Id),
  TargetValue decimal(18,4) NOT NULL,
  Status nvarchar(16) NOT NULL DEFAULT N'Planned',
  Notes nvarchar(400) NULL,
  CreatedBy uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  CreatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  UpdatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  CONSTRAINT CK_MetricGoals_Year CHECK(GoalYear BETWEEN 2020 AND 2100),
  CONSTRAINT CK_MetricGoals_Value CHECK(TargetValue>=0),
  CONSTRAINT CK_MetricGoals_Status CHECK(Status IN(N'Planned',N'InProgress',N'Achieved',N'Evaluated',N'Rescheduled')),
  CONSTRAINT CK_MetricGoals_Scope CHECK(ZoneId IS NULL OR SiteId IS NULL)
 );
 CREATE INDEX IX_MetricGoals_Year ON dbo.MetricGoals(GoalYear,Status);
END;
GO
IF OBJECT_ID(N'dbo.MetricObservations',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.MetricObservations
 (
  Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWID(),
  MetricId uniqueidentifier NOT NULL REFERENCES dbo.MetricDefinitions(Id),
  PeriodYear smallint NOT NULL,
  PeriodMonth tinyint NOT NULL,
  ZoneId uniqueidentifier NULL REFERENCES dbo.ServiceZones(Id),
  SiteId uniqueidentifier NULL REFERENCES dbo.CareSites(Id),
  ObservedValue decimal(18,4) NOT NULL,
  SourceNote nvarchar(300) NOT NULL,
  IsDemonstration bit NOT NULL DEFAULT 1,
  RecordedBy uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  RecordedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  CONSTRAINT CK_MetricObs_Month CHECK(PeriodMonth BETWEEN 1 AND 12)
 );
 CREATE INDEX IX_MetricObservations_Period ON dbo.MetricObservations(MetricId,PeriodYear,PeriodMonth);
END;
GO
IF OBJECT_ID(N'dbo.GoalEvaluations',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.GoalEvaluations
 (
  Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWID(),
  GoalId uniqueidentifier NOT NULL REFERENCES dbo.MetricGoals(Id),
  ObservedValue decimal(18,4) NOT NULL,
  Status nvarchar(16) NOT NULL,
  Explanation nvarchar(400) NULL,
  EvaluatedBy uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  EvaluatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  CONSTRAINT CK_GoalEvaluations_Status CHECK(Status IN(N'InProgress',N'Achieved',N'Evaluated',N'Rescheduled'))
 );
END;
GO
IF OBJECT_ID(N'dbo.CommunityOutcomeSnapshots',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.CommunityOutcomeSnapshots
 (
  Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWID(),
  AccountId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  PeriodYear smallint NOT NULL,
  PeriodMonth tinyint NOT NULL,
  IsStudying bit NULL,
  IsWorking bit NULL,
  ConsentedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  CONSTRAINT CK_Outcome_Month CHECK(PeriodMonth BETWEEN 1 AND 12),
  CONSTRAINT UQ_Outcome_AccountPeriod UNIQUE(AccountId,PeriodYear,PeriodMonth)
 );
END;
GO
IF OBJECT_ID(N'dbo.ZoneMetrics',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.ZoneMetrics
 (
  Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWID(),
  ZoneId uniqueidentifier NOT NULL REFERENCES dbo.ServiceZones(Id),
  MetricId uniqueidentifier NOT NULL REFERENCES dbo.MetricDefinitions(Id),
  PeriodYear smallint NOT NULL,
  PeriodMonth tinyint NOT NULL,
  ObservedValue decimal(18,4) NOT NULL,
  IsDemonstration bit NOT NULL DEFAULT 1,
  SourceNote nvarchar(300) NOT NULL,
  RecordedBy uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  RecordedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  CONSTRAINT CK_ZoneMetrics_Month CHECK(PeriodMonth BETWEEN 1 AND 12)
 );
END;
GO
-- Private social context for management; never use CommunityPosts as an institutional personal feed.
IF OBJECT_ID(N'dbo.ManagementMoments',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.ManagementMoments
 (
  Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWID(),
  AuthorId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  Body nvarchar(1200) NOT NULL,
  CreatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  EditedUtc datetime2(3) NULL,
  HiddenUtc datetime2(3) NULL,
  CONSTRAINT CK_ManagementMoments_Body CHECK(LEN(LTRIM(RTRIM(Body))) BETWEEN 1 AND 1200)
 );
 CREATE INDEX IX_ManagementMoments_Recent ON dbo.ManagementMoments(CreatedUtc DESC);
END;
GO
IF OBJECT_ID(N'dbo.ManagementFollows',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.ManagementFollows
 (
  FollowerId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  FollowedId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  CreatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  CONSTRAINT PK_ManagementFollows PRIMARY KEY(FollowerId,FollowedId),
  CONSTRAINT CK_ManagementFollows_Different CHECK(FollowerId<>FollowedId)
 );
END;
GO
IF OBJECT_ID(N'dbo.ManagementMomentReactions',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.ManagementMomentReactions
 (
  MomentId uniqueidentifier NOT NULL REFERENCES dbo.ManagementMoments(Id),
  AccountId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  CreatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  CONSTRAINT PK_ManagementMomentReactions PRIMARY KEY(MomentId,AccountId)
 );
END;
GO
IF OBJECT_ID(N'dbo.ManagementMomentComments',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.ManagementMomentComments
 (
  Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWID(),
  MomentId uniqueidentifier NOT NULL REFERENCES dbo.ManagementMoments(Id),
  AuthorId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  Body nvarchar(600) NOT NULL,
  CreatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  HiddenUtc datetime2(3) NULL,
  CONSTRAINT CK_ManagementComment_Length CHECK(LEN(LTRIM(RTRIM(Body))) BETWEEN 1 AND 600)
 );
END;
GO
IF OBJECT_ID(N'dbo.ManagementSavedMoments',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.ManagementSavedMoments
 (
  MomentId uniqueidentifier NOT NULL REFERENCES dbo.ManagementMoments(Id),
  AccountId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  CreatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  CONSTRAINT PK_ManagementSavedMoments PRIMARY KEY(MomentId,AccountId)
 );
END;
GO
-- Badge catalog metadata and award audit. Existing Badges/UserBadges retained unchanged.
IF COL_LENGTH(N'dbo.Badges',N'ImageKey') IS NULL ALTER TABLE dbo.Badges ADD ImageKey nvarchar(200) NULL;
-- Description already exists in dbo.Badges (R4); no duplicate column required.
IF COL_LENGTH(N'dbo.Badges',N'VisibilityScope') IS NULL ALTER TABLE dbo.Badges ADD VisibilityScope nvarchar(12) NOT NULL DEFAULT N'Global';
GO
-- Starter catalog; "Ganadora" is never assigned without institutional verification.
INSERT dbo.Badges(Id,Name,Description,IconKey,IsProfessional,IsActive,VisibilityScope)
SELECT NEWID(),v.Name,v.Description,v.IconKey,0,1,N'Global'
FROM (VALUES
 (N'Participante EarlyAccess',N'Participación académica demostrada',N'award'),
 (N'Colaboración',N'Contribución distinguida y revisada',N'handshake'),
 (N'Ganadora',N'Reconocimiento reservado a un resultado oficial',N'trophy')
)v(Name,Description,IconKey)
WHERE NOT EXISTS(SELECT 1 FROM dbo.Badges b WHERE b.Name=v.Name);
GO
IF OBJECT_ID(N'dbo.BadgeAwardEvents',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.BadgeAwardEvents
 (
  Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWID(),
  AccountId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  BadgeId uniqueidentifier NOT NULL REFERENCES dbo.Badges(Id),
  ActorId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  EventType nvarchar(12) NOT NULL,
  Reason nvarchar(300) NULL,
  CreatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  CONSTRAINT CK_BadgeAwardEvent_Type CHECK(EventType IN(N'Award',N'Revoke'))
 );
END;
GO
-- Moderation records are separate from safety incident reports.
IF OBJECT_ID(N'dbo.ContentModerationActions',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.ContentModerationActions
 (
  Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWID(),
  PostId uniqueidentifier NULL REFERENCES dbo.CommunityPosts(Id),
  ManagementMomentId uniqueidentifier NULL REFERENCES dbo.ManagementMoments(Id),
  ActorId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  ActionType nvarchar(12) NOT NULL,
  Reason nvarchar(300) NOT NULL,
  CreatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  CONSTRAINT CK_ContentModeration_ExactlyOne CHECK((CASE WHEN PostId IS NULL THEN 0 ELSE 1 END)+(CASE WHEN ManagementMomentId IS NULL THEN 0 ELSE 1 END)=1),
  CONSTRAINT CK_ContentModeration_Action CHECK(ActionType IN(N'Approve',N'Reject',N'Hide',N'Restore',N'Remove'))
 );
END;
GO
-- Media metadata, not public file paths. Quotas should be enforced transactionally by Server.
IF OBJECT_ID(N'dbo.MediaAssets',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.MediaAssets
 (
  Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWID(),
  OwnerId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  ApplicationCode nvarchar(12) NOT NULL,
  MediaType nvarchar(12) NOT NULL,
  StorageKey nvarchar(180) NOT NULL UNIQUE,
  MimeType nvarchar(80) NOT NULL,
  SizeBytes bigint NOT NULL,
  CreatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  CONSTRAINT CK_MediaType CHECK(MediaType IN(N'Image',N'Audio',N'Video')),
  CONSTRAINT CK_MediaSize CHECK(SizeBytes>0),
  CONSTRAINT CK_MediaScope CHECK(ApplicationCode IN(N'Community',N'Management'))
 );
 CREATE INDEX IX_MediaAssets_Owner ON dbo.MediaAssets(OwnerId,ApplicationCode,CreatedUtc);
END;
GO
IF OBJECT_ID(N'dbo.StorageQuotas',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.StorageQuotas
 (
  AccountId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  ApplicationCode nvarchar(12) NOT NULL,
  LimitBytes bigint NOT NULL DEFAULT 52428800,
  UsedBytes bigint NOT NULL DEFAULT 0,
  CONSTRAINT PK_StorageQuotas PRIMARY KEY(AccountId,ApplicationCode),
  CONSTRAINT CK_StorageQuotas_Scope CHECK(ApplicationCode IN(N'Community',N'Management')),
  CONSTRAINT CK_StorageQuotas_Values CHECK(UsedBytes>=0 AND UsedBytes<=LimitBytes)
 );
END;
GO
PRINT N'Beta 1.0: expanded identity, metrics, conceptual map, institutional social and media structures.';
