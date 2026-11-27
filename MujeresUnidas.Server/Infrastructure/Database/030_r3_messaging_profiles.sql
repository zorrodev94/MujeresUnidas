-- R3 INCREMENTAL MIGRATION. Run AFTER 000_create_database.sql on the existing MujeresUnidas DB.
-- PRESERVES ALL ACCOUNTS, POSTS AND SESSIONS. Never drops tables or rewrites old records.
-- Take a backup before deploying to an installation that holds non-test data.
IF DB_ID(N'MujeresUnidas') IS NULL THROW 51030, 'Create the demo database with 000 first.', 1;
GO
USE MujeresUnidas;
GO
IF OBJECT_ID(N'dbo.Conversations',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Conversations
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Conversations PRIMARY KEY,
        Title nvarchar(100) NOT NULL,
        Type nvarchar(12) NOT NULL,
        CreatedBy uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
        -- Internal C# ConversationData.UserIds collection serialized as JSON. Not an authorization claim from client.
        ParticipantIdsJson nvarchar(max) NOT NULL,
        IsClosed bit NOT NULL CONSTRAINT DF_Conversations_Closed DEFAULT 0,
        CreatedUtc datetime2(3) NOT NULL CONSTRAINT DF_Conversations_Created DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_Conversations_Type CHECK (Type IN (N'Direct',N'Group',N'Public')),
        CONSTRAINT CK_Conversations_Json CHECK (ISJSON(ParticipantIdsJson)=1)
    );
    CREATE INDEX IX_Conversations_Updated ON dbo.Conversations(CreatedUtc DESC);
END;
GO
IF OBJECT_ID(N'dbo.Messages',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Messages
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Messages PRIMARY KEY,
        ConversationId uniqueidentifier NOT NULL CONSTRAINT FK_Messages_Conversation REFERENCES dbo.Conversations(Id),
        SenderId uniqueidentifier NOT NULL CONSTRAINT FK_Messages_Sender REFERENCES dbo.Accounts(Id),
        Body nvarchar(max) NOT NULL, -- AES-256-GCM Base64 ciphertext; no plaintext message bodies in SQL
        CreatedUtc datetime2(3) NOT NULL CONSTRAINT DF_Messages_Created DEFAULT SYSUTCDATETIME(),
        EditedUtc datetime2(3) NULL,
        DeletedUtc datetime2(3) NULL
    );
    CREATE INDEX IX_Messages_Conversation ON dbo.Messages(ConversationId,CreatedUtc DESC);
END;
GO
IF OBJECT_ID(N'dbo.UserFollows',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserFollows
    (
        FollowerId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
        FollowedId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
        CreatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_UserFollows PRIMARY KEY(FollowerId,FollowedId),
        CONSTRAINT CK_UserFollows_NoSelf CHECK(FollowerId<>FollowedId)
    );
    CREATE INDEX IX_UserFollows_Followed ON dbo.UserFollows(FollowedId);
END;
GO
IF OBJECT_ID(N'dbo.Notifications',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Notifications
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Notifications PRIMARY KEY,
        RecipientId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
        ActorId uniqueidentifier NULL REFERENCES dbo.Accounts(Id),
        Type nvarchar(32) NOT NULL,
        Title nvarchar(100) NOT NULL,
        RelatedId uniqueidentifier NULL,
        CreatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        ReadUtc datetime2(3) NULL
    );
    CREATE INDEX IX_Notifications_Recipient ON dbo.Notifications(RecipientId,CreatedUtc DESC);
END;
GO
IF OBJECT_ID(N'dbo.ProfileMedia',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProfileMedia
    (
        AccountId uniqueidentifier NOT NULL CONSTRAINT PK_ProfileMedia PRIMARY KEY REFERENCES dbo.Accounts(Id),
        AvatarFile nvarchar(90) NULL,
        CoverFile nvarchar(90) NULL,
        UpdatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
GO
-- Allow editorial long-form content without truncation; preserve existing text and IDs.
IF COL_LENGTH(N'dbo.CommunityPosts',N'Body') IS NOT NULL
    ALTER TABLE dbo.CommunityPosts ALTER COLUMN Body nvarchar(max) NOT NULL;
GO
PRINT N'R3 incremental schema installed. Existing R2 data is preserved.';
