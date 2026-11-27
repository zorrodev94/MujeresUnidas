USE MujeresUnidas;
GO
SELECT N'EmergencyContacts' AS TableName,OBJECT_ID(N'dbo.EmergencyContacts') AS ObjectId
UNION ALL SELECT N'IncidentReports',OBJECT_ID(N'dbo.IncidentReports');
SELECT COUNT_BIG(*) AS ContactosRegistrados FROM dbo.EmergencyContacts;
SELECT COUNT_BIG(*) AS ReportesDemo FROM dbo.IncidentReports;
-- Verify encrypted phone column, never SELECT or log the actual ciphertext or plaintext.
SELECT name,TYPE_NAME(system_type_id) AS sql_type FROM sys.columns
WHERE object_id=OBJECT_ID(N'dbo.EmergencyContacts') AND name=N'PhoneCipher';
