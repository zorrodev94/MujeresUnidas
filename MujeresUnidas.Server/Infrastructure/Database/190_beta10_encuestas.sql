-- Mujeres Unidas Beta 1.0 - encuestas voluntarias e impacto social.
-- Ejecutar después de 180. SIN DROP, no modifica datos previos.
USE MujeresUnidas;
GO
IF OBJECT_ID(N'dbo.CommunitySurveys', N'U') IS NULL
BEGIN
 CREATE TABLE dbo.CommunitySurveys
 (
  Id uniqueidentifier NOT NULL CONSTRAINT PK_CommunitySurveys PRIMARY KEY,
  Title nvarchar(140) NOT NULL,
  Description nvarchar(500) NOT NULL,
  IsActive bit NOT NULL CONSTRAINT DF_CommunitySurveys_Active DEFAULT(1),
  CreatedUtc datetime2(3) NOT NULL CONSTRAINT DF_CommunitySurveys_Date DEFAULT SYSUTCDATETIME(),
  CONSTRAINT CK_CommunitySurveys_Title CHECK(LEN(LTRIM(RTRIM(Title)))>=5)
 );
END;
GO
IF OBJECT_ID(N'dbo.CommunitySurveyResponses', N'U') IS NULL
BEGIN
 CREATE TABLE dbo.CommunitySurveyResponses
 (
  SurveyId uniqueidentifier NOT NULL REFERENCES dbo.CommunitySurveys(Id),
  AccountId uniqueidentifier NOT NULL REFERENCES dbo.Accounts(Id),
  IsStudying bit NULL,
  IsWorking bit NULL,
  StudyStartedMonth date NULL,
  WorkStartedMonth date NULL,
  Contribution nvarchar(16) NOT NULL,
  HelpfulResource nvarchar(16) NOT NULL,
  ConsentedUtc datetime2(3) NOT NULL CONSTRAINT DF_SurveyResponse_Consent DEFAULT SYSUTCDATETIME(),
  UpdatedUtc datetime2(3) NOT NULL CONSTRAINT DF_SurveyResponse_Updated DEFAULT SYSUTCDATETIME(),
  CONSTRAINT PK_CommunitySurveyResponses PRIMARY KEY(SurveyId,AccountId),
  CONSTRAINT CK_CommunitySurveyResponses_Contribution CHECK(Contribution IN(N'Yes',N'Partial',N'No',N'PreferNot')),
  CONSTRAINT CK_CommunitySurveyResponses_Resource CHECK(HelpfulResource IN(N'Jobs',N'Studies',N'Blogs',N'Community',N'None',N'PreferNot'))
 );
 CREATE INDEX IX_CommunitySurveyResponses_Survey ON dbo.CommunitySurveyResponses(SurveyId,UpdatedUtc);
END;
GO
DECLARE @survey uniqueidentifier='A11EACC0-2026-4001-8001-000000000001';
IF NOT EXISTS(SELECT 1 FROM dbo.CommunitySurveys WHERE Id=@survey)
 INSERT dbo.CommunitySurveys(Id,Title,Description)
 VALUES(@survey,N'Encuesta de progreso y oportunidades',
 N'Cuéntanos de forma voluntaria si estudias o trabajas y si los recursos públicos de la comunidad contribuyeron a tu progreso. Las respuestas individuales no se muestran en Gestión.');
GO
PRINT N'Encuesta de progreso preparada. No se generaron respuestas ficticias.';
