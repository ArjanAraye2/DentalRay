/*
 Dentix - paired phones and the SMS inbox.

 Two tables back the "read the radiology SMS for me" workflow:

   tblPairedDevices - a phone that agreed to forward its SMS. The pairing is
   started from Dentix as a QR code, so consent is explicit and the device
   carries a random key instead of any patient secret.

   tblInboxMessages - one row per received SMS, kept with the sender, the
   verdict of the matcher (which patient, how sure) and what was imported.
   Ambiguous rows stay pending until the secretary confirms them.

 Safe to run more than once.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.tblPairedDevices', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblPairedDevices
    (
        DeviceID     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblPairedDevices PRIMARY KEY,
        /* Random secret sent in the header of every forward; never a password. */
        DeviceToken  NVARCHAR(64) NOT NULL CONSTRAINT UQ_tblPairedDevices_Token UNIQUE,
        Label        NVARCHAR(100) NOT NULL,
        /* 1 = clinic phone, 2 = patient's phone. */
        OwnerKind    TINYINT NOT NULL CONSTRAINT DF_tblPairedDevices_Owner DEFAULT (1),
        PatientID    INT NULL,
        PairedDate   DATETIME2(0) NOT NULL CONSTRAINT DF_tblPairedDevices_Paired DEFAULT (SYSDATETIME()),
        LastSeenDate DATETIME2(0) NULL,
        IsActive     BIT NOT NULL CONSTRAINT DF_tblPairedDevices_Active DEFAULT (1),
        CONSTRAINT FK_tblPairedDevices_Patients FOREIGN KEY (PatientID)
            REFERENCES dbo.tblPatients (PatientID)
    );
END;
GO

IF OBJECT_ID(N'dbo.tblInboxMessages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblInboxMessages
    (
        MessageID       BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblInboxMessages PRIMARY KEY,
        DeviceID        INT NOT NULL,
        SenderMobile    NVARCHAR(30) NULL,
        Body            NVARCHAR(2000) NOT NULL,
        /* What the SMS actually carried: share links found in the text. */
        Links           NVARCHAR(2000) NULL,
        ReceivedDate    DATETIME2(0) NOT NULL,
        /* The patient the matcher believes owns this SMS. */
        PatientID       INT NULL,
        /* 1 = share link token, 2 = national code, 3 = sender mobile, 4 = name. */
        MatchMethod     TINYINT NULL,
        /* 0 = waiting for the secretary, 1 = attached to a record, 2 = rejected. */
        Status          TINYINT NOT NULL CONSTRAINT DF_tblInboxMessages_Status DEFAULT (0),
        LinkedStudyID   INT NULL,
        ImportedCount   INT NOT NULL CONSTRAINT DF_tblInboxMessages_Imported DEFAULT (0),
        Note            NVARCHAR(300) NULL,
        CreatedDate     DATETIME2(0) NOT NULL CONSTRAINT DF_tblInboxMessages_Created DEFAULT (SYSDATETIME()),
        CONSTRAINT FK_tblInboxMessages_Devices FOREIGN KEY (DeviceID)
            REFERENCES dbo.tblPairedDevices (DeviceID),
        CONSTRAINT FK_tblInboxMessages_Patients FOREIGN KEY (PatientID)
            REFERENCES dbo.tblPatients (PatientID)
    );
    CREATE INDEX IX_tblInboxMessages_Status
        ON dbo.tblInboxMessages (Status, CreatedDate DESC);
    CREATE INDEX IX_tblInboxMessages_Patient
        ON dbo.tblInboxMessages (PatientID, CreatedDate DESC);
END;
GO

PRINT N'Dentix: paired devices and SMS inbox are ready.';
GO
