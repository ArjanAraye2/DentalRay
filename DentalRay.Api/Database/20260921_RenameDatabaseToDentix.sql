/*
 Dentix - rename the DentalRay database in place.

 The product and its database were renamed from DentalRay to Dentix. Existing
 installations keep all patient data; this script renames the database instead of
 creating an empty one, so nothing has to be exported or re-imported.

 Safe to run more than once. If the database is already Dentix, nothing happens.

 Usage:
   sqlcmd -S <server> -E -i DentalRayToDentix.sql
   sqlcmd -S <server> -U sa -P <password> -i DentalRayToDentix.sql
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_ID(N'Dentix') IS NOT NULL
BEGIN
    PRINT N'Dentix database already exists - nothing to rename.';
    RETURN;
END;

IF DB_ID(N'DentalRay') IS NULL
BEGIN
    PRINT N'Neither Dentix nor DentalRay was found. Nothing to do.';
    RETURN;
END;

PRINT N'Renaming DentalRay -> Dentix ...';

/* Exclusive access is required before a database can be renamed. */
ALTER DATABASE [DentalRay] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
GO

ALTER DATABASE [DentalRay] MODIFY NAME = [Dentix];
GO

ALTER DATABASE [Dentix] SET MULTI_USER;
GO

PRINT N'Database renamed to Dentix.';
GO

/* Verification: report the tables found in the renamed database. */
SELECT N'Dentix now contains ' + CAST(COUNT(*) AS nvarchar(10)) + N' tables.'
FROM sys.tables;
GO
