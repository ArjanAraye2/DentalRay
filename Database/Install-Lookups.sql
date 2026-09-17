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

/* Study Types describe dental procedures/work, not radiology image categories. */
IF OBJECT_ID(N'dbo.tblStudyTypes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblStudyTypes
    (
        StudyTypeID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblStudyTypes PRIMARY KEY,
        StudyTypeName NVARCHAR(150) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_tblStudyTypes_IsActive DEFAULT(1),
        CONSTRAINT UQ_tblStudyTypes_StudyTypeName UNIQUE(StudyTypeName)
    );
END;
GO

DECLARE @StudyTypes TABLE (StudyTypeName NVARCHAR(150));
INSERT INTO @StudyTypes (StudyTypeName)
VALUES
    (N'تعیین نشده'),
    (N'معاینه و تشخیص'),
    (N'مشاوره درمان'),
    (N'عصب‌کشی'),
    (N'درمان مجدد ریشه'),
    (N'پرکردن دندان'),
    (N'ترمیم کامپوزیت'),
    (N'کشیدن دندان'),
    (N'کشیدن دندان عقل'),
    (N'جراحی دندان عقل'),
    (N'جراحی دهان و فک'),
    (N'روکش'),
    (N'بریج'),
    (N'ونیر / لمینت'),
    (N'ایمپلنت'),
    (N'پیوند استخوان'),
    (N'سینوس لیفت'),
    (N'جرم‌گیری'),
    (N'بروساژ'),
    (N'درمان لثه'),
    (N'جراحی لثه'),
    (N'ارتودنسی'),
    (N'درمان دندان شیری'),
    (N'پالپوتومی'),
    (N'فیشور سیلانت'),
    (N'فلورایدتراپی'),
    (N'پروتز متحرک'),
    (N'پروتز کامل'),
    (N'تنظیم یا تعمیر پروتز'),
    (N'سایر');

INSERT INTO dbo.tblStudyTypes (StudyTypeName, IsActive)
SELECT s.StudyTypeName, 1
FROM @StudyTypes s
WHERE NOT EXISTS (SELECT 1 FROM dbo.tblStudyTypes t WHERE t.StudyTypeName=s.StudyTypeName);
GO
