-- Read-only R3.1 verification after running 050_r31_post_taxonomy.sql.
IF DB_ID(N'MujeresUnidas') IS NULL THROW 51060, 'Database MujeresUnidas not found.', 1;
GO
USE MujeresUnidas;
GO
IF COL_LENGTH(N'dbo.CommunityPosts',N'Subcategory') IS NULL
    THROW 51061, 'Column CommunityPosts.Subcategory is missing. Run 050 first.',1;
IF COL_LENGTH(N'dbo.CommunityPosts',N'EventMode') IS NULL
    THROW 51062, 'Column CommunityPosts.EventMode is missing. Run 050 first.',1;
IF OBJECT_ID(N'dbo.PostMedia',N'U') IS NULL
    THROW 51063, 'PostMedia table is missing. Run 050 first.',1;
SELECT 'Accounts' AS Entity,COUNT_BIG(*) AS RowsCount FROM dbo.Accounts
UNION ALL SELECT 'CommunityPosts',COUNT_BIG(*) FROM dbo.CommunityPosts
UNION ALL SELECT 'PostMedia',COUNT_BIG(*) FROM dbo.PostMedia
UNION ALL SELECT 'Conversations',COUNT_BIG(*) FROM dbo.Conversations
UNION ALL SELECT 'Messages',COUNT_BIG(*) FROM dbo.Messages;
GO
SELECT TOP(10) Id,Category,Subcategory,EventMode,Status,CreatedUtc
FROM dbo.CommunityPosts ORDER BY CreatedUtc DESC;
GO
PRINT N'R3.1 verification passed. No data altered.';
