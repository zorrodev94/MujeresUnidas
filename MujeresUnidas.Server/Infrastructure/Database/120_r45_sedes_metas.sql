-- EarlyAccess R4.5 · Sedes de atención y metas de servicio (datos de demostración).
-- INCREMENTAL / IDEMPOTENTE: respalda la base antes de ejecutar. No elimina cuentas ni expedientes.
USE MujeresUnidas;
GO
IF OBJECT_ID(N'dbo.CareSites',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CareSites
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_CareSites PRIMARY KEY,
        Code nvarchar(24) NOT NULL CONSTRAINT UQ_CareSites_Code UNIQUE,
        DisplayName nvarchar(120) NOT NULL,
        SiteType nvarchar(24) NOT NULL,
        Municipality nvarchar(65) NOT NULL CONSTRAINT DF_CareSites_Municipality DEFAULT N'Ahome',
        StateName nvarchar(65) NOT NULL CONSTRAINT DF_CareSites_State DEFAULT N'Sinaloa',
        AddressNote nvarchar(180) NOT NULL CONSTRAINT DF_CareSites_Address DEFAULT N'Ubicación por confirmar · DEMOSTRACIÓN',
        IsDemo bit NOT NULL CONSTRAINT DF_CareSites_IsDemo DEFAULT 1,
        IsActive bit NOT NULL CONSTRAINT DF_CareSites_IsActive DEFAULT 1,
        CreatedUtc datetime2(3) NOT NULL CONSTRAINT DF_CareSites_Created DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_CareSites_Type CHECK(SiteType IN(N'Administrative',N'Psychology',N'Legal',N'CommunityActivities',N'MultiService'))
    );
    CREATE INDEX IX_CareSites_Active ON dbo.CareSites(IsActive,SiteType,DisplayName);
END;
GO
-- Las sedes se registran manualmente desde Gestión -> Sedes de atención.
-- No se precargan direcciones ficticias ni se crean oficinas sin decisión institucional.
GO
IF COL_LENGTH(N'dbo.Accounts',N'CommunityEligibilityDeclared') IS NULL
 ALTER TABLE dbo.Accounts ADD CommunityEligibilityDeclared bit NOT NULL
 CONSTRAINT DF_Accounts_CommunityEligibilityDeclared DEFAULT(0);
GO
IF COL_LENGTH(N'dbo.Cases',N'SiteId') IS NULL
 ALTER TABLE dbo.Cases ADD SiteId uniqueidentifier NULL CONSTRAINT FK_Cases_CareSites REFERENCES dbo.CareSites(Id);
GO
IF COL_LENGTH(N'dbo.Appointments',N'SiteId') IS NULL
 ALTER TABLE dbo.Appointments ADD SiteId uniqueidentifier NULL CONSTRAINT FK_Appointments_CareSites REFERENCES dbo.CareSites(Id);
GO
IF COL_LENGTH(N'dbo.ActivationVisits',N'SiteId') IS NULL
 ALTER TABLE dbo.ActivationVisits ADD SiteId uniqueidentifier NULL CONSTRAINT FK_ActivationVisits_CareSites REFERENCES dbo.CareSites(Id);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_Appointments_SiteDate' AND object_id=OBJECT_ID(N'dbo.Appointments'))
 CREATE INDEX IX_Appointments_SiteDate ON dbo.Appointments(SiteId,ScheduledUtc);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_Cases_SiteOpened' AND object_id=OBJECT_ID(N'dbo.Cases'))
 CREATE INDEX IX_Cases_SiteOpened ON dbo.Cases(SiteId,OpenedUtc);
GO
IF OBJECT_ID(N'dbo.CareSiteStaff',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.CareSiteStaff
 (
  SiteId uniqueidentifier NOT NULL REFERENCES dbo.CareSites(Id),
  StaffId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  AssignedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  IsActive bit NOT NULL DEFAULT(1),
  CONSTRAINT PK_CareSiteStaff PRIMARY KEY(SiteId,StaffId)
 );
END;
GO
IF OBJECT_ID(N'dbo.AnnualCareGoals',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.AnnualCareGoals
 (
  GoalYear smallint NOT NULL PRIMARY KEY,
  TargetCompletedSessions int NOT NULL,
  UpdatedBy uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  UpdatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  CONSTRAINT CK_AnnualCareGoals_Year CHECK(GoalYear BETWEEN 2020 AND 2100),
  CONSTRAINT CK_AnnualCareGoals_Target CHECK(TargetCompletedSessions BETWEEN 0 AND 1000000)
 );
END;
GO
-- IMPORTANT: No se crean registros de casos/citas ni metas ficticias disfrazadas de resultados.
PRINT N'R4.5: estructuras de sedes y metas configurables listas; registrar las sedes desde Gestión.';
