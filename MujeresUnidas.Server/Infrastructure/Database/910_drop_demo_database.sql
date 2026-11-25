-- DESTRUCTIVE: use ONLY for the local hackathon test database named MujeresUnidas.
-- Stop MujeresUnidas.Server first. Set @Confirm=1 intentionally, then run 000_create_database.sql.
USE master;
GO
DECLARE @Confirm bit = 0;
IF @Confirm <> 1
    THROW 50091, 'Set @Confirm=1 only after confirming this is the LOCAL demo database.', 1;
IF DB_ID(N'MujeresUnidas') IS NOT NULL
BEGIN
    ALTER DATABASE MujeresUnidas SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE MujeresUnidas;
END;
GO
