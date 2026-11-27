-- Full Beta 1.0 candidate preflight: read-only, do not execute DROP.
USE MujeresUnidas;
GO
DECLARE @needed table (ObjectName sysname NOT NULL);
INSERT @needed VALUES(N'Accounts'),(N'Sessions'),(N'ApplicationAccess'),(N'ApplicationProfiles'),
 (N'CommunityPosts'),(N'Conversations'),(N'Messages'),(N'MessageAttachments'),
 (N'MediaAssets'),(N'StorageQuotas'),(N'CareSites'),(N'CareSiteStaff'),(N'Cases'),
 (N'Appointments'),(N'ManagementMoments'),(N'ServiceZones'),(N'MetricGoals'),
 (N'GoalEvaluations'),(N'CommunityOutcomeSnapshots'),(N'Badges'),(N'UserBadges');
SELECT n.ObjectName,CASE WHEN OBJECT_ID(N'dbo.'+n.ObjectName,N'U') IS NULL THEN N'FALTA' ELSE N'OK' END Estado
FROM @needed n ORDER BY n.ObjectName;
IF EXISTS(SELECT 1 FROM @needed n WHERE OBJECT_ID(N'dbo.'+n.ObjectName,N'U') IS NULL)
 THROW 51180,N'Faltan tablas; detén la instalación y aplica migraciones anteriores.',1;
IF COL_LENGTH(N'dbo.Sessions',N'ApplicationCode') IS NULL
 THROW 51181,N'Falta Sessions.ApplicationCode. Ejecuta 155.',1;
IF COL_LENGTH(N'dbo.CareSites',N'Latitude') IS NULL OR COL_LENGTH(N'dbo.CareSites',N'Longitude') IS NULL
 THROW 51182,N'Faltan coordenadas de sede. Revisa 150.',1;
SELECT N'Listo para QA' AS Inspeccion, COUNT(*) AS Cuentas FROM dbo.Accounts;
GO
