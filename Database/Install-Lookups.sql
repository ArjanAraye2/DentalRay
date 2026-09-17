/*
 DentalRay lookup tables and installation seed data.
 Safe to run repeatedly: tables/rows are created only when missing.
 Lookup maintenance in the application is restricted to SuperAdmin.
*/

IF OBJECT_ID(N'dbo.tblImageTypes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblImageTypes
    (
        ImageTypeID INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_tblImageTypes PRIMARY KEY,
        ImageTypeName NVARCHAR(150) NOT NULL,
        IsActive BIT NOT NULL
            CONSTRAINT DF_tblImageTypes_IsActive DEFAULT (1),
        CONSTRAINT UQ_tblImageTypes_ImageTypeName UNIQUE (ImageTypeName)
    );
END;
GO

/* Initial Image Types. Add future values through the SuperAdmin maintenance form. */
DECLARE @ImageTypes TABLE (ImageTypeName NVARCHAR(150));
INSERT INTO @ImageTypes (ImageTypeName)
VALUES
    (N'JPG / JPEG'),
    (N'PNG'),
    (N'PDF');

INSERT INTO dbo.tblImageTypes (ImageTypeName, IsActive)
SELECT s.ImageTypeName, 1
FROM @ImageTypes s
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.tblImageTypes t WHERE t.ImageTypeName = s.ImageTypeName
);
GO

/* Study Types already used by DentalRay. Seed missing values during installation. */
DECLARE @StudyTypes TABLE (StudyTypeName NVARCHAR(150));
INSERT INTO @StudyTypes (StudyTypeName)
VALUES
    (N'پانورامیک'),
    (N'پری‌اپیکال'),
    (N'بایت‌وینگ'),
    (N'اکلوزال'),
    (N'سفالومتری'),
    (N'CBCT'),
    (N'عکس داخل دهانی'),
    (N'عکس دندان');

INSERT INTO dbo.tblStudyTypes (StudyTypeName, IsActive)
SELECT s.StudyTypeName, 1
FROM @StudyTypes s
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.tblStudyTypes t WHERE t.StudyTypeName = s.StudyTypeName
);
GO
