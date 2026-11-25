-- R1 candidate: run on a LOCAL SQL Server test instance only.
-- The schema is created from scratch; use 910_drop_demo_database.sql when restarting a test.
IF DB_ID(N'MujeresUnidas') IS NULL CREATE DATABASE MujeresUnidas;
GO
USE MujeresUnidas;
GO
IF OBJECT_ID(N'dbo.ManagementRoles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ManagementRoles
    (
        Code nvarchar(64) NOT NULL CONSTRAINT PK_ManagementRoles PRIMARY KEY,
        DisplayName nvarchar(100) NOT NULL CONSTRAINT UQ_ManagementRoles_DisplayName UNIQUE,
        IsActive bit NOT NULL CONSTRAINT DF_ManagementRoles_IsActive DEFAULT (1)
    );
END;
GO
-- Roles that have their own behavior require code; additional positions may use the base staff access.
IF NOT EXISTS (SELECT 1 FROM dbo.ManagementRoles WHERE Code = N'Administrator')
    INSERT dbo.ManagementRoles(Code,DisplayName) VALUES(N'Administrator',N'Administradora');
IF NOT EXISTS (SELECT 1 FROM dbo.ManagementRoles WHERE Code = N'Psychology')
    INSERT dbo.ManagementRoles(Code,DisplayName) VALUES(N'Psychology',N'Psicóloga');
IF NOT EXISTS (SELECT 1 FROM dbo.ManagementRoles WHERE Code = N'Legal')
    INSERT dbo.ManagementRoles(Code,DisplayName) VALUES(N'Legal',N'Jurídica');
IF NOT EXISTS (SELECT 1 FROM dbo.ManagementRoles WHERE Code = N'SocialWork')
    INSERT dbo.ManagementRoles(Code,DisplayName) VALUES(N'SocialWork',N'Trabajo social');
GO
IF OBJECT_ID(N'dbo.Accounts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Accounts
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Accounts PRIMARY KEY,
        Email nvarchar(254) NOT NULL CONSTRAINT UQ_Accounts_Email UNIQUE,
        DisplayName nvarchar(100) NOT NULL,
        Curp char(18) NULL,
        Biography nvarchar(300) NULL,
        Kind tinyint NOT NULL, -- 0 Community; 1 Management staff; 2 Owner
        StaffRole nvarchar(64) NULL CONSTRAINT FK_Accounts_StaffRole REFERENCES dbo.ManagementRoles(Code),
        Status tinyint NOT NULL CONSTRAINT DF_Accounts_Status DEFAULT (0),
        ActivationStatus tinyint NOT NULL CONSTRAINT DF_Accounts_ActivationStatus DEFAULT (0),
        PasswordSalt varbinary(16) NOT NULL,
        PasswordHash varbinary(32) NOT NULL,
        CreatedUtc datetime2(3) NOT NULL CONSTRAINT DF_Accounts_CreatedUtc DEFAULT SYSUTCDATETIME(),
        ActivatedBy uniqueidentifier NULL,
        ActivatedUtc datetime2(3) NULL,
        CONSTRAINT CK_Accounts_Kind CHECK (Kind BETWEEN 0 AND 2),
        CONSTRAINT CK_Accounts_Status CHECK (Status BETWEEN 0 AND 2),
        CONSTRAINT CK_Accounts_ActivationStatus CHECK (ActivationStatus BETWEEN 0 AND 2),
        CONSTRAINT CK_Accounts_RoleBoundary CHECK
            ((Kind = 1 AND StaffRole IS NOT NULL) OR (Kind <> 1 AND StaffRole IS NULL))
    );
    CREATE UNIQUE INDEX UX_Accounts_Curp ON dbo.Accounts(Curp) WHERE Curp IS NOT NULL;
END;
GO
IF OBJECT_ID(N'dbo.Sessions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Sessions
    (
        TokenDigest varbinary(32) NOT NULL CONSTRAINT PK_Sessions PRIMARY KEY,
        AccountId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
        ExpiresUtc datetime2(3) NOT NULL,
        CreatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_Sessions_AccountId ON dbo.Sessions(AccountId);
END;
GO
IF OBJECT_ID(N'dbo.AuditEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditEvents
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_AuditEvents PRIMARY KEY,
        AccountId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
        ActorId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
        Action nvarchar(30) NOT NULL,
        CreatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_Accounts_OneOwner' AND object_id=OBJECT_ID(N'dbo.Accounts'))
    CREATE UNIQUE INDEX UX_Accounts_OneOwner ON dbo.Accounts(Kind) WHERE Kind=2;
GO
