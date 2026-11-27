-- EarlyAccess R4.4: incremental, no data drops. Back up MujeresUnidas before running.
USE MujeresUnidas;
GO
IF OBJECT_ID(N'dbo.EmergencyContacts',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmergencyContacts
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_EmergencyContacts PRIMARY KEY,
        OwnerId uniqueidentifier NOT NULL CONSTRAINT FK_EmergencyContacts_Accounts REFERENCES dbo.Accounts(Id),
        DisplayName nvarchar(70) NOT NULL,
        Relationship nvarchar(35) NOT NULL,
        PhoneCipher varbinary(256) NOT NULL,
        Priority int NOT NULL CONSTRAINT DF_EmergencyContacts_Priority DEFAULT (1),
        CreatedUtc datetime2(3) NOT NULL CONSTRAINT DF_EmergencyContacts_CreatedUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_EmergencyContacts_Priority CHECK(Priority BETWEEN 1 AND 3)
    );
    CREATE INDEX IX_EmergencyContacts_Owner ON dbo.EmergencyContacts(OwnerId,CreatedUtc);
END;
GO
IF OBJECT_ID(N'dbo.IncidentReports',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IncidentReports
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_IncidentReports PRIMARY KEY,
        OwnerId uniqueidentifier NOT NULL CONSTRAINT FK_IncidentReports_Accounts REFERENCES dbo.Accounts(Id),
        Category nvarchar(25) NOT NULL,
        Summary nvarchar(900) NOT NULL,
        Status nvarchar(18) NOT NULL CONSTRAINT DF_IncidentReports_Status DEFAULT N'Received',
        CreatedUtc datetime2(3) NOT NULL CONSTRAINT DF_IncidentReports_CreatedUtc DEFAULT SYSUTCDATETIME(),
        UpdatedUtc datetime2(3) NULL,
        CONSTRAINT CK_IncidentReports_Category CHECK(Category IN (N'Violence',N'Theft',N'Harassment',N'Other')),
        CONSTRAINT CK_IncidentReports_Status CHECK(Status IN (N'Received',N'UnderReview',N'Closed'))
    );
    CREATE INDEX IX_IncidentReports_Owner ON dbo.IncidentReports(OwnerId,CreatedUtc DESC);
END;
GO
PRINT N'R4.4 seguridad y contactos: migración incremental aplicada.';
