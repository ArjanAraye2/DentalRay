/* DentalRay database initialization / upgrade */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_ID(N'DentalRay') IS NULL CREATE DATABASE [DentalRay];
GO
USE [DentalRay];
GO

IF OBJECT_ID(N'dbo.tblPatients', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblPatients
    (
        PatientID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblPatients PRIMARY KEY,
        NationalCode NVARCHAR(20) NOT NULL,
        FirstName NVARCHAR(100) NOT NULL,
        LastName NVARCHAR(100) NOT NULL,
        BirthDate DATE NULL,
        Gender TINYINT NULL,
        Mobile NVARCHAR(30) NULL,
        Address NVARCHAR(500) NULL,
        Description NVARCHAR(1000) NULL,
        CreatedDate DATETIME2(0) NOT NULL,
        ModifiedDate DATETIME2(0) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_tblPatients_IsActive DEFAULT(1)
    );
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_tblPatients_NationalCode' AND object_id=OBJECT_ID(N'dbo.tblPatients'))
    CREATE UNIQUE INDEX UX_tblPatients_NationalCode ON dbo.tblPatients(NationalCode);
GO

IF OBJECT_ID(N'dbo.tblRadiologyStudies', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblRadiologyStudies
    (
        StudyID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblRadiologyStudies PRIMARY KEY,
        PatientID INT NOT NULL,
        StudyDate DATETIME2(0) NOT NULL,
        StudyType NVARCHAR(50) NOT NULL,
        BodyPart NVARCHAR(100) NULL,
        Description NVARCHAR(1000) NULL,
        Report NVARCHAR(MAX) NULL,
        CreatedDate DATETIME2(0) NOT NULL,
        ModifiedDate DATETIME2(0) NULL
    );
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_tblRadiologyStudies_tblPatients')
    ALTER TABLE dbo.tblRadiologyStudies ADD CONSTRAINT FK_tblRadiologyStudies_tblPatients FOREIGN KEY(PatientID) REFERENCES dbo.tblPatients(PatientID);
GO

/* Study Type lookup. Business values are NOT Image Types and are not seeded automatically. */
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

/*
 Legacy StudyType text is preserved as Study Type data during upgrade.
 No radiology Image Type values are inserted into tblStudyTypes by the installer.
*/
IF COL_LENGTH(N'dbo.tblRadiologyStudies', N'StudyTypeID') IS NULL
    ALTER TABLE dbo.tblRadiologyStudies ADD StudyTypeID INT NULL;
GO
IF COL_LENGTH(N'dbo.tblRadiologyStudies', N'StudyType') IS NOT NULL
BEGIN
    INSERT INTO dbo.tblStudyTypes(StudyTypeName,IsActive)
    SELECT DISTINCT LTRIM(RTRIM(s.StudyType)),1
    FROM dbo.tblRadiologyStudies s
    WHERE NULLIF(LTRIM(RTRIM(s.StudyType)),N'') IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM dbo.tblStudyTypes t WHERE t.StudyTypeName=LTRIM(RTRIM(s.StudyType)));

    UPDATE s SET StudyTypeID=t.StudyTypeID
    FROM dbo.tblRadiologyStudies s
    JOIN dbo.tblStudyTypes t ON t.StudyTypeName=LTRIM(RTRIM(s.StudyType))
    WHERE s.StudyTypeID IS NULL;
END;
GO

/* Clinical Image Types, separate from Study Type and physical file format. */
IF OBJECT_ID(N'dbo.tblImageTypes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblImageTypes
    (
        ImageTypeID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblImageTypes PRIMARY KEY,
        ImageTypeName NVARCHAR(150) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_tblImageTypes_IsActive DEFAULT(1),
        CONSTRAINT UQ_tblImageTypes_ImageTypeName UNIQUE(ImageTypeName)
    );
END;
GO
DECLARE @ImageTypes TABLE (ImageTypeName NVARCHAR(150));
INSERT INTO @ImageTypes(ImageTypeName)
VALUES (N'CBCT'),(N'اکلوزال'),(N'بایت‌وینگ'),(N'پانورامیک'),
       (N'پری‌اپیکال'),(N'سفالومتری'),(N'عکس داخل دهانی'),(N'عکس دندان');
INSERT INTO dbo.tblImageTypes(ImageTypeName,IsActive)
SELECT s.ImageTypeName,1 FROM @ImageTypes s
WHERE NOT EXISTS(SELECT 1 FROM dbo.tblImageTypes t WHERE t.ImageTypeName=s.ImageTypeName);
GO

/* New installations create the patient-owned image table directly. */
IF OBJECT_ID(N'dbo.tblRadiologyImages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblRadiologyImages
    (
        ImageID BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblRadiologyImages PRIMARY KEY,
        PatientID INT NOT NULL,
        ImageTypeID INT NULL,
        FileName NVARCHAR(255) NOT NULL,
        RelativePath NVARCHAR(1000) NOT NULL,
        ContentType NVARCHAR(50) NOT NULL,
        SerialNumber INT NOT NULL,
        CreatedDate DATETIME2(0) NOT NULL
    );
END;
GO

/* Upgrade an existing pre-v2 database without losing image metadata. */
IF COL_LENGTH(N'dbo.tblRadiologyImages', N'PatientID') IS NULL
    ALTER TABLE dbo.tblRadiologyImages ADD PatientID INT NULL;
GO
IF COL_LENGTH(N'dbo.tblRadiologyImages', N'SerialNumber') IS NULL
    ALTER TABLE dbo.tblRadiologyImages ADD SerialNumber INT NULL;
GO
IF COL_LENGTH(N'dbo.tblRadiologyImages', N'ImageTypeID') IS NULL
    ALTER TABLE dbo.tblRadiologyImages ADD ImageTypeID INT NULL;
GO

IF COL_LENGTH(N'dbo.tblRadiologyImages', N'StudyID') IS NOT NULL
BEGIN
    UPDATE i
       SET PatientID = s.PatientID
      FROM dbo.tblRadiologyImages i
      JOIN dbo.tblRadiologyStudies s ON s.StudyID=i.StudyID
     WHERE i.PatientID IS NULL;

    ;WITH x AS
    (
        SELECT ImageID, ROW_NUMBER() OVER(PARTITION BY PatientID ORDER BY CreatedDate,ImageID) AS rn
        FROM dbo.tblRadiologyImages
        WHERE SerialNumber IS NULL
    )
    UPDATE i SET SerialNumber=x.rn
      FROM dbo.tblRadiologyImages i JOIN x ON x.ImageID=i.ImageID;
END;
GO

IF EXISTS (SELECT 1 FROM dbo.tblRadiologyImages WHERE PatientID IS NULL OR SerialNumber IS NULL)
    THROW 51010, 'Image ownership migration could not be completed.', 1;
GO
ALTER TABLE dbo.tblRadiologyImages ALTER COLUMN PatientID INT NOT NULL;
ALTER TABLE dbo.tblRadiologyImages ALTER COLUMN SerialNumber INT NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_tblRadiologyImages_tblPatients')
    ALTER TABLE dbo.tblRadiologyImages ADD CONSTRAINT FK_tblRadiologyImages_tblPatients FOREIGN KEY(PatientID) REFERENCES dbo.tblPatients(PatientID);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_tblRadiologyImages_tblImageTypes')
    ALTER TABLE dbo.tblRadiologyImages ADD CONSTRAINT FK_tblRadiologyImages_tblImageTypes FOREIGN KEY(ImageTypeID) REFERENCES dbo.tblImageTypes(ImageTypeID);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_tblRadiologyImages_PatientID_SerialNumber' AND object_id=OBJECT_ID(N'dbo.tblRadiologyImages'))
    CREATE UNIQUE INDEX UX_tblRadiologyImages_PatientID_SerialNumber ON dbo.tblRadiologyImages(PatientID,SerialNumber);
GO

IF OBJECT_ID(N'dbo.tblRadiologyStudyImages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblRadiologyStudyImages
    (
        StudyID INT NOT NULL,
        ImageID BIGINT NOT NULL,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_tblRadiologyStudyImages_CreatedDate DEFAULT(SYSDATETIME()),
        CONSTRAINT PK_tblRadiologyStudyImages PRIMARY KEY(StudyID,ImageID),
        CONSTRAINT FK_tblRadiologyStudyImages_Studies FOREIGN KEY(StudyID) REFERENCES dbo.tblRadiologyStudies(StudyID),
        CONSTRAINT FK_tblRadiologyStudyImages_Images FOREIGN KEY(ImageID) REFERENCES dbo.tblRadiologyImages(ImageID)
    );
END;
GO

/* Preserve every old Study -> Image relationship in the new link table. */
IF COL_LENGTH(N'dbo.tblRadiologyImages', N'StudyID') IS NOT NULL
BEGIN
    INSERT INTO dbo.tblRadiologyStudyImages(StudyID,ImageID,CreatedDate)
    SELECT i.StudyID,i.ImageID,i.CreatedDate
      FROM dbo.tblRadiologyImages i
     WHERE NOT EXISTS
           (SELECT 1 FROM dbo.tblRadiologyStudyImages l WHERE l.StudyID=i.StudyID AND l.ImageID=i.ImageID);

    DECLARE @fk sysname;
    SELECT TOP(1) @fk=fk.name FROM sys.foreign_keys fk
      JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id=fk.object_id
     WHERE fk.parent_object_id=OBJECT_ID(N'dbo.tblRadiologyImages')
       AND COL_NAME(fkc.parent_object_id,fkc.parent_column_id)=N'StudyID';
    IF @fk IS NOT NULL EXEC(N'ALTER TABLE dbo.tblRadiologyImages DROP CONSTRAINT ['+@fk+N']');

    DECLARE @idx sysname;
    SELECT TOP(1) @idx=i.name FROM sys.indexes i
      JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
     WHERE i.object_id=OBJECT_ID(N'dbo.tblRadiologyImages')
       AND COL_NAME(ic.object_id,ic.column_id)=N'StudyID'
       AND i.is_primary_key=0 AND i.is_unique_constraint=0;
    IF @idx IS NOT NULL EXEC(N'DROP INDEX ['+@idx+N'] ON dbo.tblRadiologyImages');

    ALTER TABLE dbo.tblRadiologyImages DROP COLUMN StudyID;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_tblRadiologyStudyImages_ImageID' AND object_id=OBJECT_ID(N'dbo.tblRadiologyStudyImages'))
    CREATE INDEX IX_tblRadiologyStudyImages_ImageID ON dbo.tblRadiologyStudyImages(ImageID);
GO

PRINT N'DentalRay database initialization/upgrade completed successfully.';
GO
