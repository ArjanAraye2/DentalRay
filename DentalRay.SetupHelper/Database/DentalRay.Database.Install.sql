/*
    DentalRay.Database.Install.sql
    ------------------------------------------------------------
    Installer-safe database initialization script.

    Goals:
    - Create database only if it does not exist.
    - Create tables only if they do not exist.
    - Create keys/indexes only if they do not exist.
    - Never DROP the database or patient data.
    - Safe to run again during reinstall/upgrade.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_ID(N'DentalRay') IS NULL
BEGIN
    PRINT N'Creating DentalRay database...';
    CREATE DATABASE [DentalRay];
END
ELSE
BEGIN
    PRINT N'DentalRay database already exists. Existing data will be preserved.';
END;
GO

USE [DentalRay];
GO

IF OBJECT_ID(N'dbo.tblPatients', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblPatients
    (
        PatientID      INT IDENTITY(1,1) NOT NULL,
        NationalCode   NVARCHAR(20) NOT NULL,
        FirstName      NVARCHAR(100) NOT NULL,
        LastName       NVARCHAR(100) NOT NULL,
        BirthDate      DATE NULL,
        Gender         TINYINT NULL,
        Mobile         NVARCHAR(30) NULL,
        Address        NVARCHAR(500) NULL,
        Description    NVARCHAR(1000) NULL,
        CreatedDate    DATETIME2(0) NOT NULL,
        ModifiedDate   DATETIME2(0) NULL,
        IsActive       BIT NOT NULL
            CONSTRAINT DF_tblPatients_IsActive DEFAULT (1),

        CONSTRAINT PK_tblPatients
            PRIMARY KEY CLUSTERED (PatientID)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_tblPatients_NationalCode'
      AND object_id = OBJECT_ID(N'dbo.tblPatients')
)
BEGIN
    CREATE UNIQUE INDEX UX_tblPatients_NationalCode
        ON dbo.tblPatients (NationalCode);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_tblPatients_LastName'
      AND object_id = OBJECT_ID(N'dbo.tblPatients')
)
BEGIN
    CREATE INDEX IX_tblPatients_LastName
        ON dbo.tblPatients (LastName);
END;
GO

IF OBJECT_ID(N'dbo.tblRadiologyStudies', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblRadiologyStudies
    (
        StudyID       INT IDENTITY(1,1) NOT NULL,
        PatientID     INT NOT NULL,
        StudyDate     DATETIME2(0) NOT NULL,
        StudyType     NVARCHAR(50) NOT NULL,
        BodyPart      NVARCHAR(100) NULL,
        Description   NVARCHAR(1000) NULL,
        Report        NVARCHAR(MAX) NULL,
        CreatedDate   DATETIME2(0) NOT NULL,
        ModifiedDate  DATETIME2(0) NULL,

        CONSTRAINT PK_tblRadiologyStudies
            PRIMARY KEY CLUSTERED (StudyID)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_tblRadiologyStudies_tblPatients'
      AND parent_object_id = OBJECT_ID(N'dbo.tblRadiologyStudies')
)
BEGIN
    ALTER TABLE dbo.tblRadiologyStudies WITH CHECK
    ADD CONSTRAINT FK_tblRadiologyStudies_tblPatients
        FOREIGN KEY (PatientID)
        REFERENCES dbo.tblPatients (PatientID);

    ALTER TABLE dbo.tblRadiologyStudies
        CHECK CONSTRAINT FK_tblRadiologyStudies_tblPatients;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_tblRadiologyStudies_PatientID'
      AND object_id = OBJECT_ID(N'dbo.tblRadiologyStudies')
)
BEGIN
    CREATE INDEX IX_tblRadiologyStudies_PatientID
        ON dbo.tblRadiologyStudies (PatientID);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_tblRadiologyStudies_StudyDate'
      AND object_id = OBJECT_ID(N'dbo.tblRadiologyStudies')
)
BEGIN
    CREATE INDEX IX_tblRadiologyStudies_StudyDate
        ON dbo.tblRadiologyStudies (StudyDate);
END;
GO

IF OBJECT_ID(N'dbo.tblRadiologyImages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblRadiologyImages
    (
        ImageID       BIGINT IDENTITY(1,1) NOT NULL,
        StudyID       INT NOT NULL,
        FileName      NVARCHAR(255) NOT NULL,
        RelativePath  NVARCHAR(1000) NOT NULL,
        ContentType   NVARCHAR(50) NOT NULL,
        CreatedDate   DATETIME2(0) NOT NULL,

        CONSTRAINT PK_tblRadiologyImages
            PRIMARY KEY CLUSTERED (ImageID)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_tblRadiologyImages_tblRadiologyStudies'
      AND parent_object_id = OBJECT_ID(N'dbo.tblRadiologyImages')
)
BEGIN
    ALTER TABLE dbo.tblRadiologyImages WITH CHECK
    ADD CONSTRAINT FK_tblRadiologyImages_tblRadiologyStudies
        FOREIGN KEY (StudyID)
        REFERENCES dbo.tblRadiologyStudies (StudyID);

    ALTER TABLE dbo.tblRadiologyImages
        CHECK CONSTRAINT FK_tblRadiologyImages_tblRadiologyStudies;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_tblRadiologyImages_StudyID_CreatedDate'
      AND object_id = OBJECT_ID(N'dbo.tblRadiologyImages')
)
BEGIN
    CREATE INDEX IX_tblRadiologyImages_StudyID_CreatedDate
        ON dbo.tblRadiologyImages (StudyID, CreatedDate);
END;
GO

IF OBJECT_ID(N'dbo.tblPatients', N'U') IS NULL
    THROW 51000, 'dbo.tblPatients is missing after initialization.', 1;

IF OBJECT_ID(N'dbo.tblRadiologyStudies', N'U') IS NULL
    THROW 51001, 'dbo.tblRadiologyStudies is missing after initialization.', 1;

IF OBJECT_ID(N'dbo.tblRadiologyImages', N'U') IS NULL
    THROW 51002, 'dbo.tblRadiologyImages is missing after initialization.', 1;

PRINT N'DentalRay database initialization completed successfully.';
GO


/* ============================================================
   Version 2 - Authentication, Users and Audit Log
   ------------------------------------------------------------
   این بخش Upgrade-safe است و اطلاعات قبلی را حذف نمی‌کند.
   ============================================================ */

USE [DentalRay];
GO

IF OBJECT_ID(N'dbo.tblUsers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblUsers
    (
        UserID          INT IDENTITY(1,1) NOT NULL,
        UserName        NVARCHAR(50) NOT NULL,
        DisplayName     NVARCHAR(100) NOT NULL,
        PasswordHash    NVARCHAR(500) NOT NULL,
        Role            NVARCHAR(20) NOT NULL
            CONSTRAINT DF_tblUsers_Role DEFAULT (N'User'),
        IsActive        BIT NOT NULL
            CONSTRAINT DF_tblUsers_IsActive DEFAULT (1),
        CreatedDate     DATETIME2(0) NOT NULL
            CONSTRAINT DF_tblUsers_CreatedDate DEFAULT (SYSDATETIME()),
        ModifiedDate    DATETIME2(0) NULL,
        LastLoginDate   DATETIME2(0) NULL,

        CONSTRAINT PK_tblUsers
            PRIMARY KEY CLUSTERED (UserID),

        CONSTRAINT CK_tblUsers_Role
            CHECK (Role IN (N'Admin', N'User'))
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_tblUsers_UserName'
      AND object_id = OBJECT_ID(N'dbo.tblUsers')
)
BEGIN
    CREATE UNIQUE INDEX UX_tblUsers_UserName
        ON dbo.tblUsers (UserName);
END;
GO

IF OBJECT_ID(N'dbo.tblAuditLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblAuditLogs
    (
        AuditLogID      BIGINT IDENTITY(1,1) NOT NULL,
        UserID          INT NULL,
        UserName        NVARCHAR(50) NULL,
        Action          NVARCHAR(150) NOT NULL,
        HttpMethod      NVARCHAR(10) NULL,
        Path            NVARCHAR(500) NULL,
        StatusCode      INT NULL,
        IsSuccess       BIT NOT NULL
            CONSTRAINT DF_tblAuditLogs_IsSuccess DEFAULT (1),
        Details         NVARCHAR(2000) NULL,
        IpAddress       NVARCHAR(64) NULL,
        UserAgent       NVARCHAR(500) NULL,
        CreatedDate     DATETIME2(0) NOT NULL
            CONSTRAINT DF_tblAuditLogs_CreatedDate DEFAULT (SYSDATETIME()),

        CONSTRAINT PK_tblAuditLogs
            PRIMARY KEY CLUSTERED (AuditLogID)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_tblAuditLogs_CreatedDate'
      AND object_id = OBJECT_ID(N'dbo.tblAuditLogs')
)
BEGIN
    CREATE INDEX IX_tblAuditLogs_CreatedDate
        ON dbo.tblAuditLogs (CreatedDate DESC);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_tblAuditLogs_UserID'
      AND object_id = OBJECT_ID(N'dbo.tblAuditLogs')
)
BEGIN
    CREATE INDEX IX_tblAuditLogs_UserID
        ON dbo.tblAuditLogs (UserID, CreatedDate DESC);
END;
GO

IF OBJECT_ID(N'dbo.tblSchemaVersions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblSchemaVersions
    (
        VersionNumber   INT NOT NULL,
        Description     NVARCHAR(300) NOT NULL,
        AppliedDate     DATETIME2(0) NOT NULL
            CONSTRAINT DF_tblSchemaVersions_AppliedDate
            DEFAULT (SYSDATETIME()),

        CONSTRAINT PK_tblSchemaVersions
            PRIMARY KEY CLUSTERED (VersionNumber)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.tblSchemaVersions
    WHERE VersionNumber = 2
)
BEGIN
    INSERT INTO dbo.tblSchemaVersions
    (
        VersionNumber,
        Description
    )
    VALUES
    (
        2,
        N'Users, authentication and audit logging'
    );
END;
GO

IF OBJECT_ID(N'dbo.tblUsers', N'U') IS NULL
    THROW 51003, 'dbo.tblUsers is missing after initialization.', 1;

IF OBJECT_ID(N'dbo.tblAuditLogs', N'U') IS NULL
    THROW 51004, 'dbo.tblAuditLogs is missing after initialization.', 1;
GO
