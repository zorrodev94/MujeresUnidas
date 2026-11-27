-- Non-mutating QA: run AFTER 080_r42_mensajes_gestion.sql on test DB.
USE MujeresUnidas;
GO
SELECT Name, CASE WHEN OBJECT_ID(N'dbo.'+Name,N'U') IS NULL THEN N'MISSING' ELSE N'OK' END AS Status
FROM (VALUES(N'ManagementDirectConversations'),(N'ManagementDirectMessages'),
(N'PostReactions'),(N'PostBookmarks'),(N'PostComments')) AS Required(Name);
GO
SELECT COLUMNPROPERTY(OBJECT_ID(N'dbo.CommunityPosts'),'Body','AllowsNull') AS BodyAllowsNull,
       c.max_length AS BodyMaxLength /* -1 == nvarchar(max) */
FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.CommunityPosts') AND c.name=N'Body';
GO
SELECT COUNT(*) AS StaffConversations FROM dbo.ManagementDirectConversations;
SELECT COUNT(*) AS StaffMessages FROM dbo.ManagementDirectMessages;
SELECT COUNT(*) AS CommunityLikes FROM dbo.PostReactions;
SELECT COUNT(*) AS CommunityBookmarks FROM dbo.PostBookmarks;
SELECT COUNT(*) AS CommunityComments FROM dbo.PostComments WHERE DeletedUtc IS NULL;
GO
