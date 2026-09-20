/* Dentix database initialization / upgrade - authoritative schema
   Source: verified DentalRay database schema export (15 user tables).
   Non-destructive: existing data and legacy columns are preserved.

   The product was renamed DentalRay -> Dentix. The database follows the new
   name, and an existing installation is renamed in place so patient data is
   never copied or lost.
*/
SET NOCOUNT ON;

IF DB_ID(N'Dentix') IS NULL
BEGIN
    IF DB_ID(N'DentalRay') IS NOT NULL
    BEGIN
        /* ALTER DATABASE ... MODIFY NAME cannot run inside a transaction, so the
           name change happens here before XACT_ABORT is enabled for the schema
           work below. */
        PRINT N'Renaming DentalRay database to Dentix (patient data is preserved).';
        ALTER DATABASE [DentalRay] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
        ALTER DATABASE [DentalRay] MODIFY NAME = [Dentix];
        ALTER DATABASE [Dentix] SET MULTI_USER;
    END
    ELSE
        CREATE DATABASE [Dentix];
END;
GO

SET XACT_ABORT ON;
GO

USE [Dentix];
GO

/* =========================
   1. Lookup / organization
   ========================= */
IF OBJECT_ID(N'dbo.tblClinics',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblClinics(
        ClinicID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblClinics PRIMARY KEY,
        ClinicName NVARCHAR(200) NOT NULL,
        Phone NVARCHAR(30) NULL,
        Address NVARCHAR(500) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_tblClinics_IsActive DEFAULT(1)
    );
END;
GO

IF OBJECT_ID(N'dbo.tblDentalSpecialties',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblDentalSpecialties(
        SpecialtyID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblDentalSpecialties PRIMARY KEY,
        SpecialtyName NVARCHAR(150) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_tblDentalSpecialties_IsActive DEFAULT(1),
        CONSTRAINT UQ_tblDentalSpecialties_SpecialtyName UNIQUE(SpecialtyName)
    );
END;
GO

/* Base dental specialties from the verified DentalRay reference data.
   Existing rows are preserved; only missing specialties are inserted. */
DECLARE @DentalSpecialties TABLE(SpecialtyName NVARCHAR(150), IsActive BIT);
INSERT INTO @DentalSpecialties VALUES
(N'دندانپزشک عمومی',1),
(N'ارتودنسی',1),
(N'اندودانتیکس (درمان ریشه)',1),
(N'پریودانتیکس (بیماری‌های لثه)',1),
(N'پروتزهای دندانی',1),
(N'دندانپزشکی کودکان',1),
(N'جراحی دهان، فک و صورت',1),
(N'بیماری‌های دهان، فک و صورت',1),
(N'رادیولوژی دهان، فک و صورت',1);
INSERT INTO dbo.tblDentalSpecialties(SpecialtyName,IsActive)
SELECT d.SpecialtyName,d.IsActive
FROM @DentalSpecialties d
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.tblDentalSpecialties s
    WHERE s.SpecialtyName=d.SpecialtyName
);
GO

IF OBJECT_ID(N'dbo.tblStaff',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblStaff(
        StaffID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblStaff PRIMARY KEY,
        NationalCode NVARCHAR(10) NOT NULL,
        FirstName NVARCHAR(100) NOT NULL,
        LastName NVARCHAR(100) NOT NULL,
        StaffType TINYINT NOT NULL,
        SpecialtyID INT NULL,
        StartDate DATE NOT NULL,
        EndDate DATE NULL,
        CONSTRAINT UQ_tblStaff_NationalCode UNIQUE(NationalCode),
        CONSTRAINT CK_tblStaff_StaffType CHECK(StaffType IN(1,2)),
        CONSTRAINT CK_tblStaff_Specialty CHECK((StaffType=1 AND SpecialtyID IS NULL) OR (StaffType=2 AND SpecialtyID IS NOT NULL)),
        CONSTRAINT CK_tblStaff_EndDate CHECK(EndDate IS NULL OR EndDate>=StartDate)
    );
END;
GO

IF OBJECT_ID(N'dbo.tblClinicStaff',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblClinicStaff(
        ClinicID INT NOT NULL,
        StaffID INT NOT NULL,
        CONSTRAINT PK_tblClinicStaff PRIMARY KEY(ClinicID,StaffID),
        CONSTRAINT FK_tblClinicStaff_Clinic FOREIGN KEY(ClinicID) REFERENCES dbo.tblClinics(ClinicID),
        CONSTRAINT FK_tblClinicStaff_Staff FOREIGN KEY(StaffID) REFERENCES dbo.tblStaff(StaffID)
    );
END;
GO

IF OBJECT_ID(N'dbo.tblUsers',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblUsers(
        UserID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblUsers PRIMARY KEY,
        StaffID INT NOT NULL,
        UserName NVARCHAR(100) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_tblUsers_IsActive DEFAULT(1),
        PasswordHash NVARCHAR(500) NULL,
        EndDate DATE NULL,
        StartDate DATE NULL,
        CONSTRAINT UQ_tblUsers_StaffID UNIQUE(StaffID),
        CONSTRAINT UQ_tblUsers_UserName UNIQUE(UserName),
        CONSTRAINT FK_tblUsers_Staff FOREIGN KEY(StaffID) REFERENCES dbo.tblStaff(StaffID)
    );
END;
GO

/* Account recovery mobile: safe upgrade for existing installations. */
IF COL_LENGTH(N'dbo.tblUsers',N'RecoveryMobile') IS NULL
BEGIN
    ALTER TABLE dbo.tblUsers ADD RecoveryMobile NVARCHAR(30) NULL;
END;
GO

IF OBJECT_ID(N'dbo.tblUserDentists',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblUserDentists(
        UserID INT NOT NULL,
        ClinicID INT NOT NULL,
        DentistStaffID INT NOT NULL,
        CONSTRAINT PK_tblUserDentists PRIMARY KEY(UserID,ClinicID,DentistStaffID),
        CONSTRAINT FK_tblUserDentists_User FOREIGN KEY(UserID) REFERENCES dbo.tblUsers(UserID),
        CONSTRAINT FK_tblUserDentists_Clinic FOREIGN KEY(ClinicID) REFERENCES dbo.tblClinics(ClinicID),
        CONSTRAINT FK_tblUserDentists_Dentist FOREIGN KEY(DentistStaffID) REFERENCES dbo.tblStaff(StaffID)
    );
END;
GO

/* =========================
   2. Patients
   ========================= */
IF OBJECT_ID(N'dbo.tblPatients',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblPatients(
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
        IsActive BIT NOT NULL CONSTRAINT DF_tblPatients_IsActive DEFAULT(1),
        PhotoRelativePath NVARCHAR(500) NULL
    );
END;
GO
IF COL_LENGTH(N'dbo.tblPatients',N'PhotoRelativePath') IS NULL
    ALTER TABLE dbo.tblPatients ADD PhotoRelativePath NVARCHAR(500) NULL;
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'UX_tblPatients_NationalCode' AND object_id=OBJECT_ID(N'dbo.tblPatients'))
    CREATE UNIQUE INDEX UX_tblPatients_NationalCode ON dbo.tblPatients(NationalCode);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_tblPatients_LastName' AND object_id=OBJECT_ID(N'dbo.tblPatients'))
    CREATE INDEX IX_tblPatients_LastName ON dbo.tblPatients(LastName);
GO

/* =========================
   3. Study types
   ========================= */
IF OBJECT_ID(N'dbo.tblStudyTypes',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblStudyTypes(
        StudyTypeID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblStudyTypes PRIMARY KEY,
        StudyTypeName NVARCHAR(150) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_tblStudyTypes_IsActive DEFAULT(1),
        CONSTRAINT UQ_tblStudyTypes_StudyTypeName UNIQUE(StudyTypeName)
    );
END;
GO

DECLARE @StudyTypes TABLE(StudyTypeName NVARCHAR(150));
INSERT INTO @StudyTypes VALUES
(N'تعیین نشده'),(N'معاینه و تشخیص'),(N'مشاوره درمان'),(N'عصب‌کشی'),
(N'درمان مجدد ریشه'),(N'پرکردن دندان'),(N'ترمیم کامپوزیت'),(N'کشیدن دندان'),
(N'کشیدن دندان عقل'),(N'جراحی دندان عقل'),(N'جراحی دهان و فک'),(N'روکش'),
(N'بریج'),(N'ونیر / لمینت'),(N'ایمپلنت'),(N'پیوند استخوان'),(N'سینوس لیفت'),
(N'جرم‌گیری'),(N'بروساژ'),(N'درمان لثه'),(N'جراحی لثه'),(N'ارتودنسی'),
(N'درمان دندان شیری'),(N'پالپوتومی'),(N'فیشور سیلانت'),(N'فلورایدتراپی'),
(N'پروتز متحرک'),(N'پروتز کامل'),(N'تنظیم یا تعمیر پروتز'),(N'سایر');
INSERT INTO dbo.tblStudyTypes(StudyTypeName,IsActive)
SELECT x.StudyTypeName,1 FROM @StudyTypes x
WHERE NOT EXISTS(SELECT 1 FROM dbo.tblStudyTypes t WHERE t.StudyTypeName=x.StudyTypeName);
GO

/* =========================
   4. Radiology studies
   ========================= */
IF OBJECT_ID(N'dbo.tblRadiologyStudies',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblRadiologyStudies(
        StudyID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblRadiologyStudies PRIMARY KEY,
        PatientID INT NOT NULL,
        StudyDate DATETIME2(0) NOT NULL,
        BodyPart NVARCHAR(100) NULL,
        Description NVARCHAR(1000) NULL,
        Report NVARCHAR(MAX) NULL,
        CreatedDate DATETIME2(0) NOT NULL,
        ModifiedDate DATETIME2(0) NULL,
        ClinicID INT NULL,
        DentistStaffID INT NULL,
        StudyTypeID INT NOT NULL
    );
END;
GO
IF COL_LENGTH(N'dbo.tblRadiologyStudies',N'StudyTypeID') IS NULL
    ALTER TABLE dbo.tblRadiologyStudies ADD StudyTypeID INT NULL;
GO
/* Legacy StudyType text, if present, is intentionally retained.
   The whole migration is dynamic because an IF/CASE does not prevent SQL Server
   from binding a missing column name during compilation. */
IF COL_LENGTH(N'dbo.tblRadiologyStudies',N'StudyType') IS NOT NULL
BEGIN
    EXEC(N'
        INSERT INTO dbo.tblStudyTypes(StudyTypeName,IsActive)
        SELECT DISTINCT LTRIM(RTRIM(s.StudyType)),1
        FROM dbo.tblRadiologyStudies s
        WHERE NULLIF(LTRIM(RTRIM(s.StudyType)),N'''') IS NOT NULL
          AND NOT EXISTS(
              SELECT 1 FROM dbo.tblStudyTypes t
              WHERE t.StudyTypeName=LTRIM(RTRIM(s.StudyType))
          );

        UPDATE s
        SET StudyTypeID=t.StudyTypeID
        FROM dbo.tblRadiologyStudies s
        JOIN dbo.tblStudyTypes t
          ON t.StudyTypeName=LTRIM(RTRIM(s.StudyType))
        WHERE s.StudyTypeID IS NULL;
    ');
END;
GO
IF COL_LENGTH(N'dbo.tblRadiologyStudies',N'ClinicID') IS NULL
    ALTER TABLE dbo.tblRadiologyStudies ADD ClinicID INT NULL;
GO
IF COL_LENGTH(N'dbo.tblRadiologyStudies',N'DentistStaffID') IS NULL
    ALTER TABLE dbo.tblRadiologyStudies ADD DentistStaffID INT NULL;
GO
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_tblRadiologyStudies_tblPatients')
    ALTER TABLE dbo.tblRadiologyStudies ADD CONSTRAINT FK_tblRadiologyStudies_tblPatients FOREIGN KEY(PatientID) REFERENCES dbo.tblPatients(PatientID);
GO
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_tblRadiologyStudies_Clinic')
    ALTER TABLE dbo.tblRadiologyStudies ADD CONSTRAINT FK_tblRadiologyStudies_Clinic FOREIGN KEY(ClinicID) REFERENCES dbo.tblClinics(ClinicID);
GO
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_tblRadiologyStudies_Dentist')
    ALTER TABLE dbo.tblRadiologyStudies ADD CONSTRAINT FK_tblRadiologyStudies_Dentist FOREIGN KEY(DentistStaffID) REFERENCES dbo.tblStaff(StaffID);
GO
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_tblRadiologyStudies_StudyType')
    ALTER TABLE dbo.tblRadiologyStudies ADD CONSTRAINT FK_tblRadiologyStudies_StudyType FOREIGN KEY(StudyTypeID) REFERENCES dbo.tblStudyTypes(StudyTypeID);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_tblRadiologyStudies_ClinicID' AND object_id=OBJECT_ID(N'dbo.tblRadiologyStudies'))
    CREATE INDEX IX_tblRadiologyStudies_ClinicID ON dbo.tblRadiologyStudies(ClinicID);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_tblRadiologyStudies_DentistStaffID' AND object_id=OBJECT_ID(N'dbo.tblRadiologyStudies'))
    CREATE INDEX IX_tblRadiologyStudies_DentistStaffID ON dbo.tblRadiologyStudies(DentistStaffID);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_tblRadiologyStudies_PatientID' AND object_id=OBJECT_ID(N'dbo.tblRadiologyStudies'))
    CREATE INDEX IX_tblRadiologyStudies_PatientID ON dbo.tblRadiologyStudies(PatientID);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_tblRadiologyStudies_StudyDate' AND object_id=OBJECT_ID(N'dbo.tblRadiologyStudies'))
    CREATE INDEX IX_tblRadiologyStudies_StudyDate ON dbo.tblRadiologyStudies(StudyDate);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_tblRadiologyStudies_StudyTypeID' AND object_id=OBJECT_ID(N'dbo.tblRadiologyStudies'))
    CREATE INDEX IX_tblRadiologyStudies_StudyTypeID ON dbo.tblRadiologyStudies(StudyTypeID);
GO

/* =========================
   5. Image types and images
   ========================= */
IF OBJECT_ID(N'dbo.tblImageTypes',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblImageTypes(
        ImageTypeID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblImageTypes PRIMARY KEY,
        ImageTypeName NVARCHAR(150) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_tblImageTypes_IsActive DEFAULT(1),
        CONSTRAINT UQ_tblImageTypes_ImageTypeName UNIQUE(ImageTypeName)
    );
END;
GO
DECLARE @ImageTypes TABLE(ImageTypeName NVARCHAR(150));
INSERT INTO @ImageTypes VALUES
(N'CBCT'),(N'اکلوزال'),(N'بایت‌وینگ'),(N'پانورامیک'),(N'پری‌اپیکال'),
(N'سفالومتری'),(N'عکس داخل دهانی'),(N'عکس دندان'),(N'کارت بایگانی');
INSERT INTO dbo.tblImageTypes(ImageTypeName,IsActive)
SELECT x.ImageTypeName,1 FROM @ImageTypes x
WHERE NOT EXISTS(SELECT 1 FROM dbo.tblImageTypes t WHERE t.ImageTypeName=x.ImageTypeName);
GO

IF OBJECT_ID(N'dbo.tblRadiologyImages',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblRadiologyImages(
        ImageID BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblRadiologyImages PRIMARY KEY,
        PatientID INT NOT NULL,
        FileName NVARCHAR(255) NOT NULL,
        RelativePath NVARCHAR(1000) NOT NULL,
        ContentType NVARCHAR(50) NOT NULL,
        SerialNumber INT NOT NULL,
        CreatedDate DATETIME2(0) NOT NULL,
        ImageTypeID INT NULL
    );
END;
GO
IF COL_LENGTH(N'dbo.tblRadiologyImages',N'PatientID') IS NULL ALTER TABLE dbo.tblRadiologyImages ADD PatientID INT NULL;
IF COL_LENGTH(N'dbo.tblRadiologyImages',N'SerialNumber') IS NULL ALTER TABLE dbo.tblRadiologyImages ADD SerialNumber INT NULL;
IF COL_LENGTH(N'dbo.tblRadiologyImages',N'ImageTypeID') IS NULL ALTER TABLE dbo.tblRadiologyImages ADD ImageTypeID INT NULL;
GO
/* Legacy StudyID is retained; relationships are also copied to the link table below. */
IF COL_LENGTH(N'dbo.tblRadiologyImages',N'StudyID') IS NOT NULL
BEGIN
    EXEC(N'
      UPDATE i SET PatientID=s.PatientID
      FROM dbo.tblRadiologyImages i JOIN dbo.tblRadiologyStudies s ON s.StudyID=i.StudyID
      WHERE i.PatientID IS NULL;

      ;WITH x AS(
        SELECT ImageID,ROW_NUMBER() OVER(PARTITION BY PatientID ORDER BY CreatedDate,ImageID) rn
        FROM dbo.tblRadiologyImages WHERE SerialNumber IS NULL
      )
      UPDATE i SET SerialNumber=x.rn FROM dbo.tblRadiologyImages i JOIN x ON x.ImageID=i.ImageID;
    ');
END;
GO
IF EXISTS(SELECT 1 FROM dbo.tblRadiologyImages WHERE PatientID IS NULL OR SerialNumber IS NULL)
    THROW 51010,'Image ownership migration could not be completed.',1;
GO
ALTER TABLE dbo.tblRadiologyImages ALTER COLUMN PatientID INT NOT NULL;
ALTER TABLE dbo.tblRadiologyImages ALTER COLUMN SerialNumber INT NOT NULL;
GO
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_tblRadiologyImages_tblPatients')
    ALTER TABLE dbo.tblRadiologyImages ADD CONSTRAINT FK_tblRadiologyImages_tblPatients FOREIGN KEY(PatientID) REFERENCES dbo.tblPatients(PatientID);
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_tblRadiologyImages_tblImageTypes')
    ALTER TABLE dbo.tblRadiologyImages ADD CONSTRAINT FK_tblRadiologyImages_tblImageTypes FOREIGN KEY(ImageTypeID) REFERENCES dbo.tblImageTypes(ImageTypeID);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'UX_tblRadiologyImages_PatientID_SerialNumber' AND object_id=OBJECT_ID(N'dbo.tblRadiologyImages'))
    CREATE UNIQUE INDEX UX_tblRadiologyImages_PatientID_SerialNumber ON dbo.tblRadiologyImages(PatientID,SerialNumber);
GO

IF OBJECT_ID(N'dbo.tblRadiologyStudyImages',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblRadiologyStudyImages(
        StudyID INT NOT NULL,
        ImageID BIGINT NOT NULL,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_tblRadiologyStudyImages_CreatedDate DEFAULT(SYSDATETIME()),
        CONSTRAINT PK_tblRadiologyStudyImages PRIMARY KEY(StudyID,ImageID),
        CONSTRAINT FK_tblRadiologyStudyImages_Studies FOREIGN KEY(StudyID) REFERENCES dbo.tblRadiologyStudies(StudyID),
        CONSTRAINT FK_tblRadiologyStudyImages_Images FOREIGN KEY(ImageID) REFERENCES dbo.tblRadiologyImages(ImageID)
    );
END;
GO
IF COL_LENGTH(N'dbo.tblRadiologyImages',N'StudyID') IS NOT NULL
BEGIN
    EXEC(N'
      INSERT INTO dbo.tblRadiologyStudyImages(StudyID,ImageID,CreatedDate)
      SELECT i.StudyID,i.ImageID,i.CreatedDate
      FROM dbo.tblRadiologyImages i
      WHERE NOT EXISTS(SELECT 1 FROM dbo.tblRadiologyStudyImages l WHERE l.StudyID=i.StudyID AND l.ImageID=i.ImageID);
    ');
END;
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_tblRadiologyStudyImages_ImageID' AND object_id=OBJECT_ID(N'dbo.tblRadiologyStudyImages'))
    CREATE INDEX IX_tblRadiologyStudyImages_ImageID ON dbo.tblRadiologyStudyImages(ImageID);
GO

IF OBJECT_ID(N'dbo.tblRadiologyStudyTeeth',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblRadiologyStudyTeeth(
        StudyID INT NOT NULL,
        ToothNumber TINYINT NOT NULL,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_tblRadiologyStudyTeeth_CreatedDate DEFAULT(SYSDATETIME()),
        CONSTRAINT PK_tblRadiologyStudyTeeth PRIMARY KEY(StudyID,ToothNumber),
        CONSTRAINT CK_tblRadiologyStudyTeeth_ToothNumber CHECK(
          (ToothNumber BETWEEN 11 AND 18) OR (ToothNumber BETWEEN 21 AND 28) OR
          (ToothNumber BETWEEN 31 AND 38) OR (ToothNumber BETWEEN 41 AND 48) OR
          (ToothNumber BETWEEN 51 AND 55) OR (ToothNumber BETWEEN 61 AND 65) OR
          (ToothNumber BETWEEN 71 AND 75) OR (ToothNumber BETWEEN 81 AND 85)),
        CONSTRAINT FK_tblRadiologyStudyTeeth_Studies FOREIGN KEY(StudyID) REFERENCES dbo.tblRadiologyStudies(StudyID)
    );
END;
GO

/* =========================
   6. Financial tables
   ========================= */
IF OBJECT_ID(N'dbo.tblStudyActions',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblStudyActions(
        StudyActionID BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblStudyActions PRIMARY KEY,
        StudyID INT NOT NULL,
        Description NVARCHAR(500) NOT NULL,
        Amount DECIMAL(18,2) NOT NULL CONSTRAINT DF_tblStudyActions_Amount DEFAULT(0),
        DiscountAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_tblStudyActions_DiscountAmount DEFAULT(0),
        CreatedDate DATETIME2(7) NOT NULL CONSTRAINT DF_tblStudyActions_CreatedDate DEFAULT(SYSDATETIME()),
        ModifiedDate DATETIME2(7) NULL,
        CONSTRAINT CK_tblStudyActions_Amount CHECK(Amount>=0),
        CONSTRAINT CK_tblStudyActions_Discount CHECK(DiscountAmount>=0 AND DiscountAmount<=Amount),
        CONSTRAINT FK_tblStudyActions_tblRadiologyStudies FOREIGN KEY(StudyID) REFERENCES dbo.tblRadiologyStudies(StudyID)
    );
END;
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_tblStudyActions_StudyID' AND object_id=OBJECT_ID(N'dbo.tblStudyActions'))
    CREATE INDEX IX_tblStudyActions_StudyID ON dbo.tblStudyActions(StudyID);
GO

IF OBJECT_ID(N'dbo.tblStudyPayments',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblStudyPayments(
        StudyPaymentID BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblStudyPayments PRIMARY KEY,
        StudyID INT NOT NULL,
        PaymentDate DATETIME2(7) NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        Description NVARCHAR(500) NULL,
        CreatedDate DATETIME2(7) NOT NULL CONSTRAINT DF_tblStudyPayments_CreatedDate DEFAULT(SYSDATETIME()),
        ModifiedDate DATETIME2(7) NULL,
        PaymentMethod TINYINT NULL,
        CONSTRAINT CK_tblStudyPayments_Amount CHECK(Amount>0),
        CONSTRAINT CK_tblStudyPayments_Method CHECK(PaymentMethod IS NULL OR PaymentMethod BETWEEN 1 AND 3),
        CONSTRAINT FK_tblStudyPayments_tblRadiologyStudies FOREIGN KEY(StudyID) REFERENCES dbo.tblRadiologyStudies(StudyID)
    );
END;
GO
IF COL_LENGTH(N'dbo.tblStudyPayments',N'PaymentMethod') IS NULL
BEGIN
    ALTER TABLE dbo.tblStudyPayments ADD PaymentMethod TINYINT NULL;
    ALTER TABLE dbo.tblStudyPayments ADD CONSTRAINT CK_tblStudyPayments_Method CHECK(PaymentMethod IS NULL OR PaymentMethod BETWEEN 1 AND 3);
END;
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_tblStudyPayments_StudyID_PaymentDate' AND object_id=OBJECT_ID(N'dbo.tblStudyPayments'))
    CREATE INDEX IX_tblStudyPayments_StudyID_PaymentDate ON dbo.tblStudyPayments(StudyID,PaymentDate DESC);
GO

PRINT N'Dentix: complete 15-table schema initialization/upgrade completed successfully.';
GO
