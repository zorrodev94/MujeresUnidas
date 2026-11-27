-- Mujeres Unidas Beta 1.0.2: reparación incremental del ámbito de sesión.
-- No elimina cuentas, mensajes ni tokens. Ejecutar en la MISMA base de Server.
USE MujeresUnidas;
GO
IF OBJECT_ID(N'dbo.Accounts',N'U') IS NULL OR OBJECT_ID(N'dbo.Sessions',N'U') IS NULL
    THROW 51153, N'Falta el esquema base. Ejecuta 000 antes de esta migración.',1;
IF OBJECT_ID(N'dbo.ApplicationAccess',N'U') IS NULL
    THROW 51154, N'Falta ApplicationAccess. Ejecuta 150_beta10_dominio.sql antes de 155.',1;
GO
IF COL_LENGTH(N'dbo.Sessions',N'ApplicationCode') IS NULL
    ALTER TABLE dbo.Sessions ADD ApplicationCode nvarchar(12) NULL;
GO
-- Respetar las sesiones previas: establecer su ámbito conforme a la cuenta de origen.
UPDATE s SET ApplicationCode=CASE WHEN a.Kind=0 THEN N'Community' ELSE N'Management' END
FROM dbo.Sessions AS s JOIN dbo.Accounts AS a ON a.Id=s.AccountId
WHERE s.ApplicationCode IS NULL;
GO
IF EXISTS(SELECT 1 FROM dbo.Sessions WHERE ApplicationCode IS NULL)
    THROW 51155, N'Hay sesiones sin aplicación. Revisar antes de continuar.',1;
IF EXISTS(SELECT 1 FROM dbo.Sessions WHERE ApplicationCode NOT IN (N'Community',N'Management'))
    THROW 51156, N'Hay sesiones con aplicación desconocida.',1;
IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Sessions') AND name=N'ApplicationCode' AND is_nullable=1)
    ALTER TABLE dbo.Sessions ALTER COLUMN ApplicationCode nvarchar(12) NOT NULL;
GO
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.Sessions') AND name=N'CK_Sessions_ApplicationCode')
    ALTER TABLE dbo.Sessions WITH CHECK ADD CONSTRAINT CK_Sessions_ApplicationCode CHECK(ApplicationCode IN(N'Community',N'Management'));
GO
IF COL_LENGTH(N'dbo.Sessions',N'ApplicationCode') IS NULL
    THROW 51157, N'NO SE PUDO CREAR Sessions.ApplicationCode.',1;
SELECT N'Sesiones preparadas' AS Resultado, COUNT(*) AS Total,
       SUM(CASE WHEN ApplicationCode=N'Community' THEN 1 ELSE 0 END) AS Comunitarias,
       SUM(CASE WHEN ApplicationCode=N'Management' THEN 1 ELSE 0 END) AS Institucionales
FROM dbo.Sessions;
GO
