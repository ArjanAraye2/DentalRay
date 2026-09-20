/*
 Dentix - POS (card reader) settings.

 Dentix can push a payment amount to a POS terminal. Terminals differ wildly:
 some are plain TCP devices, some are PC-POS over USB/COM, some are Android
 terminals with an SDK. Rather than hard-coding one vendor, the connection
 details and the request/response pattern live in this table, so a new device can
 be supported by filling in a form instead of changing code.

 Safe to run more than once.
*/
SET NOCOUNT ON;
-- A filtered index requires QUOTED_IDENTIFIER ON; sqlcmd defaults it off.
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.tblPosSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblPosSettings
    (
        PosSettingID     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblPosSettings PRIMARY KEY,
        Name             NVARCHAR(100) NOT NULL,
        -- 1 = TCP/IP, 2 = serial (COM), 3 = USB / PC-POS, 4 = cloud service
        ConnectionType   TINYINT NOT NULL CONSTRAINT DF_tblPosSettings_Type DEFAULT(1),
        Host             NVARCHAR(100) NULL,
        Port             INT NULL,
        ComPort          NVARCHAR(20) NULL,
        BaudRate         INT NULL,
        -- Name of the protocol implementation; "Generic" ships with Dentix.
        Protocol         NVARCHAR(50) NOT NULL CONSTRAINT DF_tblPosSettings_Protocol DEFAULT(N'Generic'),
        -- Pattern sent to the device. Placeholders: {amount} {currency} {invoice}
        RequestPattern   NVARCHAR(500) NULL,
        -- Text that marks a successful answer from the device.
        SuccessPattern   NVARCHAR(200) NULL,
        Encoding         NVARCHAR(20) NOT NULL CONSTRAINT DF_tblPosSettings_Encoding DEFAULT(N'UTF8'),
        TimeoutSeconds   INT NOT NULL CONSTRAINT DF_tblPosSettings_Timeout DEFAULT(5),
        IsActive         BIT NOT NULL CONSTRAINT DF_tblPosSettings_Active DEFAULT(1),
        IsDefault        BIT NOT NULL CONSTRAINT DF_tblPosSettings_Default DEFAULT(0),
        LastTestAt       DATETIME2(0) NULL,
        LastTestMessage  NVARCHAR(500) NULL,
        CreatedDate      DATETIME2(0) NOT NULL CONSTRAINT DF_tblPosSettings_Created DEFAULT(SYSDATETIME()),
        ModifiedDate     DATETIME2(0) NULL,
        CONSTRAINT CK_tblPosSettings_ConnectionType CHECK(ConnectionType BETWEEN 1 AND 4),
        CONSTRAINT CK_tblPosSettings_Port CHECK(Port IS NULL OR (Port BETWEEN 1 AND 65535)),
        CONSTRAINT CK_tblPosSettings_Timeout CHECK(TimeoutSeconds BETWEEN 1 AND 60)
    );
END;
GO

/* Only one terminal can be the default. */
IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_tblPosSettings_Default' AND object_id = OBJECT_ID(N'dbo.tblPosSettings')
)
BEGIN
    CREATE UNIQUE INDEX UX_tblPosSettings_Default
        ON dbo.tblPosSettings(IsDefault)
        WHERE IsDefault = 1;
END;
GO

PRINT N'Dentix: POS settings table is ready.';
GO
