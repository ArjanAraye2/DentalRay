/*
 Dentix - receiving an image from the patient's own phone without installing
 anything.

 tblReceiveTokens holds a short-lived secret bound to one patient. The QR the
 secretary shows carries it, the patient's browser posts the SMS text, and the
 same matcher/import pipeline used for paired phones does the rest.

 The inbox learns a second source: a web submission has no paired device, so
 DeviceID becomes nullable and Source records where the message came from
 (1 = paired phone, 2 = patient's browser).

 Safe to run more than once.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.tblReceiveTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblReceiveTokens
    (
        ReceiveID   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblReceiveTokens PRIMARY KEY,
        PatientID   INT NOT NULL,
        /* Random secret; unguessable and revocable, like the share links. */
        Token       NVARCHAR(64) NOT NULL CONSTRAINT UQ_tblReceiveTokens_Token UNIQUE,
        CreatedBy   INT NULL,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_tblReceiveTokens_Created DEFAULT (SYSDATETIME()),
        ExpiresDate DATETIME2(0) NULL,
        LastUsedAt  DATETIME2(0) NULL,
        UseCount    INT NOT NULL CONSTRAINT DF_tblReceiveTokens_Uses DEFAULT (0),
        CONSTRAINT FK_tblReceiveTokens_Patients FOREIGN KEY (PatientID)
            REFERENCES dbo.tblPatients (PatientID)
    );
    CREATE INDEX IX_tblReceiveTokens_Patient
        ON dbo.tblReceiveTokens (PatientID, CreatedDate DESC);
END;
GO

IF COL_LENGTH(N'dbo.tblInboxMessages', N'Source') IS NULL
    ALTER TABLE dbo.tblInboxMessages
        ADD Source TINYINT NOT NULL CONSTRAINT DF_tblInboxMessages_Source DEFAULT (1);
GO

IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.tblInboxMessages')
             AND name = N'DeviceID' AND is_nullable = 0)
    ALTER TABLE dbo.tblInboxMessages ALTER COLUMN DeviceID INT NULL;
GO

PRINT N'Dentix: patient receive tokens are ready.';
GO
