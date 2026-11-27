-- Mujeres Unidas · R4.6 · Momentos de Comunidad (migración incremental).
-- Necesita 000..120 y respaldos antes de ejecutar. No cambia los contenidos previos.
USE MujeresUnidas;
GO
IF OBJECT_ID(N'dbo.CommunityPosts',N'U') IS NULL
    THROW 51460,N'Ejecuta las migraciones base antes de habilitar Momentos.',1;
GO
IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_Posts_Category' AND parent_object_id=OBJECT_ID(N'dbo.CommunityPosts'))
BEGIN
    ALTER TABLE dbo.CommunityPosts DROP CONSTRAINT CK_Posts_Category;
END;
GO
ALTER TABLE dbo.CommunityPosts WITH CHECK ADD CONSTRAINT CK_Posts_Category
CHECK(Category IN (N'Moment',N'Blog',N'Guide',N'Job',N'Event'));
GO
PRINT N'R4.6: Momentos comunitarios habilitados sin borrar información previa.';
