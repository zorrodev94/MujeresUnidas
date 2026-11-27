USE MujeresUnidas;
GO
SELECT N'CareSites' AS Objeto,OBJECT_ID(N'dbo.CareSites',N'U') AS ObjectId
UNION ALL SELECT N'CareSiteStaff',OBJECT_ID(N'dbo.CareSiteStaff',N'U')
UNION ALL SELECT N'AnnualCareGoals',OBJECT_ID(N'dbo.AnnualCareGoals',N'U');
SELECT Code,DisplayName,SiteType,Municipality,IsDemo,IsActive FROM dbo.CareSites ORDER BY Code;
SELECT N'Cases.SiteId' AS Columna,COL_LENGTH(N'dbo.Cases',N'SiteId') AS Bytes
UNION ALL SELECT N'Appointments.SiteId',COL_LENGTH(N'dbo.Appointments',N'SiteId')
UNION ALL SELECT N'ActivationVisits.SiteId',COL_LENGTH(N'dbo.ActivationVisits',N'SiteId');
SELECT COUNT_BIG(*) AS CitasConSede FROM dbo.Appointments WHERE SiteId IS NOT NULL;
SELECT COUNT_BIG(*) AS CasosConSede FROM dbo.Cases WHERE SiteId IS NOT NULL;
GO
