-- R3.1 incremental post taxonomy; run once after R3 / 030. Existing R2/R3 rows preserved.
IF DB_ID(N'MujeresUnidas') IS NULL THROW 51050, 'Database MujeresUnidas not found.',1;
GO
USE MujeresUnidas;
GO
IF COL_LENGTH(N'dbo.CommunityPosts',N'Subcategory') IS NULL
    ALTER TABLE dbo.CommunityPosts ADD Subcategory nvarchar(48) NULL;
GO
IF COL_LENGTH(N'dbo.CommunityPosts',N'EventMode') IS NULL
    ALTER TABLE dbo.CommunityPosts ADD EventMode nvarchar(16) NULL;
GO
IF OBJECT_ID(N'dbo.PostMedia',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PostMedia
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_PostMedia PRIMARY KEY,
        PostId uniqueidentifier NOT NULL REFERENCES dbo.CommunityPosts(Id),
        StorageKey nvarchar(90) NOT NULL UNIQUE,
        ContentType nvarchar(20) NOT NULL,
        CreatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_PostMedia_PostId ON dbo.PostMedia(PostId,CreatedUtc);
END;
GO
PRINT N'R3.1 taxonomy and private post media ready. No existing rows deleted.';
GO
