/*
 DentalRay - Patient profile photo
 Adds an optional relative path for the Patient photo.
 Safe to run more than once.
*/
IF COL_LENGTH('dbo.tblPatients', 'PhotoRelativePath') IS NULL
BEGIN
    ALTER TABLE dbo.tblPatients
        ADD PhotoRelativePath nvarchar(500) NULL;
END;
GO
