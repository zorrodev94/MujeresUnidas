-- Attachment schema for community text/chat, private files outside wwwroot.
USE MujeresUnidas;
GO
IF OBJECT_ID(N'dbo.Messages',N'U') IS NULL OR OBJECT_ID(N'dbo.MediaAssets',N'U') IS NULL
 THROW 51170,N'Aplica las migraciones 030 y 150 antes de 170.',1;
GO
IF OBJECT_ID(N'dbo.MessageAttachments',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.MessageAttachments
 (
  MessageId uniqueidentifier NOT NULL REFERENCES dbo.Messages(Id),
  AssetId uniqueidentifier NOT NULL REFERENCES dbo.MediaAssets(Id),
  CreatedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
  CONSTRAINT PK_MessageAttachments PRIMARY KEY(MessageId,AssetId),
  CONSTRAINT UQ_MessageAttachments_Asset UNIQUE(AssetId)
 );
 CREATE INDEX IX_MessageAttachments_Message ON dbo.MessageAttachments(MessageId);
END;
GO
PRINT N'MessageAttachments disponible; la clave de cifrado y los archivos se configuran solo en Server.';
GO
