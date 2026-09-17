/*
 DentalRay lookup tables and installation seed data.
 Safe to run repeatedly.
*/

IF OBJECT_ID(N'dbo.tblImageTypes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblImageTypes
    (
        ImageTypeID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblImageTypes PRIMARY KEY,
        ImageTypeName NVARCHAR(150) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_tblImageTypes_IsActive DEFAULT (1),
        CONSTRAINT UQ_tblImageTypes_ImageTypeName UNIQUE (ImageTypeName)
    );
END;
GO

/* These are clinical Image Types, not Study Types and not physical file formats. */
DECLARE @ImageTypes TABLE (ImageTypeName NVARCHAR(150));
INSERT INTO @ImageTypes (ImageTypeName)
VALUES
    (N'CBCT'),
    (N'اکلوزال'),
    (N'بایت‌وینگ'),
    (N'پانورامیک'),
    (N'پری‌اپیکال'),
    (N'سفالومتری'),
    (N'عکس داخل دهانی'),
    (N'عکس دندان');

INSERT INTO dbo.tblImageTypes (ImageTypeName, IsActive)
SELECT s.ImageTypeName, 1
FROM @ImageTypes s
WHERE NOT EXISTS (SELECT 1 FROM dbo.tblImageTypes t WHERE t.ImageTypeName=s.ImageTypeName);
GO

/*
 Study Types are intentionally NOT seeded here.
 Their business values are independent from Image Types and must be defined explicitly.
 Existing tblStudyTypes rows are preserved; this script never deletes them.
*/
