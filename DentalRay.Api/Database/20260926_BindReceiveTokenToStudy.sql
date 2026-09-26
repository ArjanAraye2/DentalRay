/*
 Dentix - a receive link can belong to one Study, not just to one patient.

 Scenario 1: the secretary opens a Study, presses "receive for this Study", and
 the images the patient sends land in that very Study instead of whatever is
 newest for the patient.

 Safe to run more than once.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF COL_LENGTH(N'dbo.tblReceiveTokens', N'StudyID') IS NULL
    ALTER TABLE dbo.tblReceiveTokens ADD StudyID INT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_tblReceiveTokens_Studies')
    ALTER TABLE dbo.tblReceiveTokens
        ADD CONSTRAINT FK_tblReceiveTokens_Studies FOREIGN KEY (StudyID)
            REFERENCES dbo.tblRadiologyStudies (StudyID);
GO

PRINT N'Dentix: receive tokens can point at a Study.';
GO
