-- Read-only SQL check for the local demo database after running 000_create_database.sql.
-- StaffRole MUST allow NULL for Community and Owner accounts; staff must have a role.
USE MujeresUnidas;
GO
SELECT c.name AS ColumnName, c.is_nullable AS AllowsNull,
       CASE WHEN c.is_nullable=1 THEN 'OK' ELSE 'ERROR: reset demo database' END AS Verification
FROM sys.columns c
WHERE c.object_id=OBJECT_ID(N'dbo.Accounts') AND c.name=N'StaffRole';
GO
SELECT N'Accounts' AS TableName, COUNT(*) AS TotalRows FROM dbo.Accounts;
SELECT N'ManagementRoles' AS TableName, Code,DisplayName,IsActive FROM dbo.ManagementRoles ORDER BY DisplayName;
GO
