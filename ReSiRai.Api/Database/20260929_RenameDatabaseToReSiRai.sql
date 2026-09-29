/*
 ReSiRai - rename the legacy database in place.

 The product is now ReSiRai. An existing installation keeps all patient data;
 this script renames the database instead of creating an empty one, so nothing
 has to be exported or re-imported.

 Handles both legacy names:
   Dentix     -> ReSiRai   (renamed 2026-09-21)
   DentalRay  -> ReSiRai   (original name)

 Safe to run more than once. If the database is already ReSiRai, nothing happens.

 Usage:
   sqlcmd -S <server> -E -i 20260929_RenameDatabaseToReSiRai.sql
   sqlcmd -S <server> -U sa -P <password> -i 20260929_RenameDatabaseToReSiRai.sql
*/
SET NOCOUNT ON;
GO

IF DB_ID(N'ReSiRai') IS NOT NULL
BEGIN
    PRINT N'Database is already named ReSiRai - nothing to do.';
END
ELSE IF DB_ID(N'Dentix') IS NOT NULL
BEGIN
    PRINT N'Renaming Dentix database to ReSiRai (patient data is preserved).';
    ALTER DATABASE [Dentix] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    ALTER DATABASE [Dentix] MODIFY NAME = [ReSiRai];
    ALTER DATABASE [ReSiRai] SET MULTI_USER;
    PRINT N'Rename complete.';
END
ELSE IF DB_ID(N'DentalRay') IS NOT NULL
BEGIN
    PRINT N'Renaming DentalRay database to ReSiRai (patient data is preserved).';
    ALTER DATABASE [DentalRay] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    ALTER DATABASE [DentalRay] MODIFY NAME = [ReSiRai];
    ALTER DATABASE [ReSiRai] SET MULTI_USER;
    PRINT N'Rename complete.';
END
ELSE
BEGIN
    PRINT N'No legacy database found (Dentix / DentalRay). Create ReSiRai with ReSiRai.Database.Install.sql.';
END
GO
