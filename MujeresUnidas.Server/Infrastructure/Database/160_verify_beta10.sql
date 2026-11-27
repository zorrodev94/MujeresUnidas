-- Mujeres Unidas Beta 1.0 · inspección de solo lectura después de 150
-- Nunca borra tablas, secretos, cuentas ni mensajes.
USE MujeresUnidas;
GO
DECLARE @requirements TABLE(TableName sysname NOT NULL);
INSERT @requirements(TableName) VALUES
(N'Accounts'),(N'Sessions'),(N'ApplicationAccess'),(N'ApplicationProfiles'),
(N'ApplicationProfileMedia'),(N'CareSites'),(N'CaseSiteTransfers'),(N'ServiceZones'),
(N'MetricDefinitions'),(N'MetricGoals'),(N'MetricObservations'),(N'GoalEvaluations'),
(N'CommunityOutcomeSnapshots'),(N'ZoneMetrics'),(N'ManagementMoments'),
(N'ManagementFollows'),(N'ManagementMomentComments'),(N'ManagementMomentReactions'),
(N'ManagementSavedMoments'),(N'BadgeAwardEvents'),(N'ContentModerationActions'),
(N'MediaAssets'),(N'StorageQuotas'),(N'EmergencyContacts'),(N'IncidentReports');
SELECT r.TableName,
  CASE WHEN OBJECT_ID(N'dbo.'+r.TableName,N'U') IS NULL THEN N'FALTA' ELSE N'OK' END AS Estado
FROM @requirements r ORDER BY r.TableName;
IF EXISTS(SELECT 1 FROM @requirements r WHERE OBJECT_ID(N'dbo.'+r.TableName,N'U') IS NULL)
 THROW 51150, 'Faltan tablas. Aplica las migraciones en orden antes del QA.', 1;
IF COL_LENGTH(N'dbo.Sessions',N'ApplicationCode') IS NULL
 THROW 51151, 'Falta Sessions.ApplicationCode. Ejecuta 150 y 155_beta10_reconciliar_sesiones.sql.', 1;
SELECT ApplicationCode,RoleCode,AccessStatus,COUNT(*) AS Cuentas
FROM dbo.ApplicationAccess GROUP BY ApplicationCode,RoleCode,AccessStatus
ORDER BY ApplicationCode,RoleCode,AccessStatus;
SELECT Code,DisplayName,IsConceptual FROM dbo.ServiceZones ORDER BY Code;
SELECT Code,Title,Unit,Direction FROM dbo.MetricDefinitions ORDER BY Code;
SELECT COUNT(*) AS Sedes, SUM(CASE WHEN IsDemo=1 THEN 1 ELSE 0 END) AS SedesDemo
FROM dbo.CareSites;
PRINT N'Inspección Beta 1.0 terminada. Usar únicamente datos ficticios en el laboratorio.';
GO
