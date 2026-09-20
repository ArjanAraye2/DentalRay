/*
 Dentix - patient messaging and appointments.

 Two needs, one migration:

 1. Message log
    The clinic sends SMS reminders, but had no record of what was sent. When a
    patient says "I never got a message" there was nothing to check. Every send
    is now logged with its outcome.

 2. Appointments
    Reminders need an appointment date, and until now the only date on a patient
    was the Study date, which is the visit that already happened. A light
    appointment table lets the clinic schedule the next visit and remind for it.

 Safe to run more than once.
*/
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.tblPatientMessages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblPatientMessages
    (
        MessageID     BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblPatientMessages PRIMARY KEY,
        PatientID     INT NOT NULL,
        Mobile        NVARCHAR(30) NOT NULL,
        Body          NVARCHAR(2000) NOT NULL,
        -- Name of the template used, or NULL when the text was typed freely.
        TemplateKey   NVARCHAR(50) NULL,
        -- 0 = queued, 1 = sent, 2 = failed
        Status        TINYINT NOT NULL CONSTRAINT DF_tblPatientMessages_Status DEFAULT(0),
        ErrorMessage  NVARCHAR(500) NULL,
        ProviderKey   NVARCHAR(100) NULL,   -- provider message id, for future delivery checks
        AppointmentID INT NULL,
        SentAt        DATETIME2(0) NULL,
        CreatedBy     INT NULL,
        CreatedDate   DATETIME2(0) NOT NULL CONSTRAINT DF_tblPatientMessages_Created DEFAULT(SYSDATETIME()),
        CONSTRAINT CK_tblPatientMessages_Status CHECK(Status BETWEEN 0 AND 2),
        CONSTRAINT FK_tblPatientMessages_Patients FOREIGN KEY(PatientID) REFERENCES dbo.tblPatients(PatientID)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_tblPatientMessages_PatientID' AND object_id = OBJECT_ID(N'dbo.tblPatientMessages'))
    CREATE INDEX IX_tblPatientMessages_PatientID ON dbo.tblPatientMessages(PatientID, CreatedDate DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_tblPatientMessages_CreatedDate' AND object_id = OBJECT_ID(N'dbo.tblPatientMessages'))
    CREATE INDEX IX_tblPatientMessages_CreatedDate ON dbo.tblPatientMessages(CreatedDate DESC);
GO

IF OBJECT_ID(N'dbo.tblAppointments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblAppointments
    (
        AppointmentID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblAppointments PRIMARY KEY,
        PatientID     INT NOT NULL,
        -- Local date and time shown to the clinic; stored as DateTime like StudyDate.
        AppointmentDate DATETIME2(0) NOT NULL,
        DentistStaffID  INT NULL,
        -- 1 = scheduled, 2 = attended, 3 = cancelled, 4 = no-show
        Status        TINYINT NOT NULL CONSTRAINT DF_tblAppointments_Status DEFAULT(1),
        Note          NVARCHAR(500) NULL,
        -- Set once a reminder has gone out, so it is never sent twice.
        ReminderSentAt DATETIME2(0) NULL,
        CreatedDate   DATETIME2(0) NOT NULL CONSTRAINT DF_tblAppointments_Created DEFAULT(SYSDATETIME()),
        ModifiedDate  DATETIME2(0) NULL,
        CONSTRAINT CK_tblAppointments_Status CHECK(Status BETWEEN 1 AND 4),
        CONSTRAINT FK_tblAppointments_Patients FOREIGN KEY(PatientID) REFERENCES dbo.tblPatients(PatientID)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_tblAppointments_Date' AND object_id = OBJECT_ID(N'dbo.tblAppointments'))
    CREATE INDEX IX_tblAppointments_Date ON dbo.tblAppointments(AppointmentDate);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_tblAppointments_PatientID' AND object_id = OBJECT_ID(N'dbo.tblAppointments'))
    CREATE INDEX IX_tblAppointments_PatientID ON dbo.tblAppointments(PatientID, AppointmentDate DESC);
GO

PRINT N'Dentix: patient messaging and appointments are ready.';
GO
