-- Verificación de solo lectura, no muestra respuestas individuales.
USE MujeresUnidas;
GO
IF OBJECT_ID(N'dbo.CommunitySurveys',N'U') IS NULL OR OBJECT_ID(N'dbo.CommunitySurveyResponses',N'U') IS NULL
 THROW 51195,N'Faltan tablas de encuestas. Ejecuta 190 antes de iniciar.',1;
SELECT N'CommunitySurveys' AS Tabla, OBJECT_ID(N'dbo.CommunitySurveys',N'U') AS Existe
UNION ALL SELECT N'CommunitySurveyResponses',OBJECT_ID(N'dbo.CommunitySurveyResponses',N'U');
SELECT Id,Title,IsActive FROM dbo.CommunitySurveys;
SELECT COUNT_BIG(*) AS NumeroRespuestas_Sin_DatosPersonales FROM dbo.CommunitySurveyResponses;
GO
