/*
 Dentix - contact log.

 The clinic reaches patients in more than one way: an SMS, a phone call, a note
 left at reception. Until now only SMS was recorded, so a call made from the desk
 left no trace and the next person had no idea it had happened.

 tblPatientMessages becomes the general contact log by gaining:

   Channel  1 = SMS, 2 = phone call, 3 = in person, 4 = other
   Outcome  how the contact ended, for calls especially:
            1 = answered, 2 = no answer, 3 = left a message,
            4 = asked for an SMS, 5 = appointment booked, 6 = will call back
   ContactedByName  who made the contact, stored as a name because the built-in
                    SuperAdmin account has no staff record.

 SMS rows keep working unchanged: Channel defaults to 1 and the SMS fields stay
 as they are.

 Safe to run more than once.
*/
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH(N'dbo.tblPatientMessages', N'Channel') IS NULL
BEGIN
    ALTER TABLE dbo.tblPatientMessages
        ADD Channel TINYINT NOT NULL CONSTRAINT DF_tblPatientMessages_Channel DEFAULT(1);
END;
GO

IF COL_LENGTH(N'dbo.tblPatientMessages', N'Outcome') IS NULL
BEGIN
    ALTER TABLE dbo.tblPatientMessages ADD Outcome TINYINT NULL;
END;
GO

IF COL_LENGTH(N'dbo.tblPatientMessages', N'ContactedByName') IS NULL
BEGIN
    ALTER TABLE dbo.tblPatientMessages ADD ContactedByName NVARCHAR(150) NULL;
END;
GO

/* Duration is only meaningful for a phone call, and only the caller knows it. */
IF COL_LENGTH(N'dbo.tblPatientMessages', N'DurationMinutes') IS NULL
BEGIN
    ALTER TABLE dbo.tblPatientMessages ADD DurationMinutes INT NULL;
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_tblPatientMessages_Channel'
      AND parent_object_id = OBJECT_ID(N'dbo.tblPatientMessages')
)
BEGIN
    ALTER TABLE dbo.tblPatientMessages ADD CONSTRAINT CK_tblPatientMessages_Channel
        CHECK (Channel BETWEEN 1 AND 4);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_tblPatientMessages_Outcome'
      AND parent_object_id = OBJECT_ID(N'dbo.tblPatientMessages')
)
BEGIN
    ALTER TABLE dbo.tblPatientMessages ADD CONSTRAINT CK_tblPatientMessages_Outcome
        CHECK (Outcome IS NULL OR Outcome BETWEEN 1 AND 6);
END;
GO

/* Phone calls are not "sent", so SentAt stays empty for them; the index on
   CreatedDate already covers listing the log newest first. */
IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_tblPatientMessages_Channel' AND object_id = OBJECT_ID(N'dbo.tblPatientMessages')
)
    CREATE INDEX IX_tblPatientMessages_Channel ON dbo.tblPatientMessages(Channel, CreatedDate DESC);
GO

PRINT N'Dentix: contact log is ready.';
GO
