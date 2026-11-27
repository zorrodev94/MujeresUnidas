-- EarlyAccess R4.2: institutional direct messages (staff and owner only).
-- Incremental, idempotent, NON-DESTRUCTIVE. Execute on the existing TEST database
-- AFTER 070_r4_integracion.sql, using a backup. Never run 910 to install R4.2.
IF DB_ID(N'MujeresUnidas') IS NULL
    THROW 51080, 'Create the existing MujeresUnidas test database first.', 1;
GO
USE MujeresUnidas;
GO
IF OBJECT_ID(N'dbo.ManagementDirectConversations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ManagementDirectConversations
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_ManagementDirectConversations PRIMARY KEY,
        AccountA uniqueidentifier NOT NULL CONSTRAINT FK_ManagementDirect_A REFERENCES dbo.Accounts(Id),
        AccountB uniqueidentifier NOT NULL CONSTRAINT FK_ManagementDirect_B REFERENCES dbo.Accounts(Id),
        CreatedUtc datetime2(3) NOT NULL CONSTRAINT DF_ManagementDirect_Created DEFAULT SYSUTCDATETIME(),
        LastMessageUtc datetime2(3) NULL,
        CONSTRAINT CK_ManagementDirect_Different CHECK(AccountA<>AccountB),
        CONSTRAINT UQ_ManagementDirect_Pair UNIQUE(AccountA,AccountB)
    );
    CREATE INDEX IX_ManagementDirect_AccountB ON dbo.ManagementDirectConversations(AccountB,LastMessageUtc DESC);
END;
GO
IF OBJECT_ID(N'dbo.ManagementDirectMessages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ManagementDirectMessages
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_ManagementDirectMessages PRIMARY KEY,
        ConversationId uniqueidentifier NOT NULL
            CONSTRAINT FK_ManagementMessage_Conversation REFERENCES dbo.ManagementDirectConversations(Id),
        SenderId uniqueidentifier NOT NULL CONSTRAINT FK_ManagementMessage_Sender REFERENCES dbo.Accounts(Id),
        Body nvarchar(max) NOT NULL, -- AES-256-GCM ciphertext in Base64, never plaintext in SQL
        CreatedUtc datetime2(3) NOT NULL CONSTRAINT DF_ManagementMessage_Created DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_ManagementMessage_Conversation ON dbo.ManagementDirectMessages(ConversationId,CreatedUtc DESC);
END;
GO
-- Confirm BOTH tables exist; does not alter community Conversations or Messages.
SELECT name FROM sys.tables
WHERE name IN (N'ManagementDirectConversations', N'ManagementDirectMessages')
ORDER BY name;
GO

-- Community publication interactions. Comments do not create cases or conversations.
IF OBJECT_ID(N'dbo.PostReactions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PostReactions
    (
        PostId uniqueidentifier NOT NULL REFERENCES dbo.CommunityPosts(Id),
        AccountId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
        CreatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_PostReactions PRIMARY KEY (PostId,AccountId)
    );
END;
GO
IF OBJECT_ID(N'dbo.PostBookmarks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PostBookmarks
    (
        PostId uniqueidentifier NOT NULL REFERENCES dbo.CommunityPosts(Id),
        AccountId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
        CreatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_PostBookmarks PRIMARY KEY (PostId,AccountId)
    );
END;
GO
IF OBJECT_ID(N'dbo.PostComments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PostComments
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_PostComments PRIMARY KEY,
        PostId uniqueidentifier NOT NULL REFERENCES dbo.CommunityPosts(Id),
        AuthorId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
        Body nvarchar(600) NOT NULL,
        CreatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        DeletedUtc datetime2(3) NULL,
        CONSTRAINT CK_PostComments_Body CHECK(LEN(LTRIM(RTRIM(Body))) BETWEEN 1 AND 600)
    );
    CREATE INDEX IX_PostComments_Recent ON dbo.PostComments(PostId,CreatedUtc DESC) INCLUDE (DeletedUtc);
END;
GO

-- Align existing SQL column with Blog/Guide body limits already enforced in C#.
-- This is a widening-only change; it retains existing post content.
IF EXISTS (SELECT 1 FROM sys.columns
 WHERE object_id=OBJECT_ID(N'dbo.CommunityPosts') AND name=N'Body' AND max_length<>-1)
BEGIN
    ALTER TABLE dbo.CommunityPosts ALTER COLUMN Body nvarchar(max) NOT NULL;
END;
GO
