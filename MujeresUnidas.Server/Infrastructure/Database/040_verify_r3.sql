USE MujeresUnidas;
GO
SELECT 'Accounts' AS [Entity],COUNT_BIG(*) AS Records FROM dbo.Accounts
UNION ALL SELECT 'CommunityPosts',COUNT_BIG(*) FROM dbo.CommunityPosts
UNION ALL SELECT 'Conversations',COUNT_BIG(*) FROM dbo.Conversations
UNION ALL SELECT 'Messages',COUNT_BIG(*) FROM dbo.Messages
UNION ALL SELECT 'UserFollows',COUNT_BIG(*) FROM dbo.UserFollows
UNION ALL SELECT 'Notifications',COUNT_BIG(*) FROM dbo.Notifications;
SELECT t.name AS R3Table,COUNT(c.column_id) AS Columns
FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id
WHERE t.name IN (N'Conversations',N'Messages',N'UserFollows',N'Notifications',N'ProfileMedia')
GROUP BY t.name ORDER BY t.name;
GO
