/*
 Dentix - Study status and follow-up.

 Until now a Study had no state: once created it looked the same whether the work
 was finished or still waiting on a second visit. The clinic needs to see, at a
 glance, which patients still have something outstanding.

 Status
   1 = open      (work in progress)
   2 = completed (work finished)
   3 = needs another study later (a follow-up is planned)

 FollowUpDate is the date the user wants to be reminded about. It drives the
 dashboard's "due" counter and the overdue filter.

 Safe to run more than once.
*/
SET NOCOUNT ON;

IF COL_LENGTH(N'dbo.tblRadiologyStudies', N'Status') IS NULL
BEGIN
    /* Existing Studies default to completed: they were created before status
       existed and have been sitting in the system, so treating them as open
       would flood the new "open studies" list with old records. */
    ALTER TABLE dbo.tblRadiologyStudies
        ADD Status TINYINT NOT NULL CONSTRAINT DF_tblRadiologyStudies_Status DEFAULT(2);
END;
GO

IF COL_LENGTH(N'dbo.tblRadiologyStudies', N'FollowUpDate') IS NULL
BEGIN
    ALTER TABLE dbo.tblRadiologyStudies ADD FollowUpDate DATETIME2(0) NULL;
END;
GO

IF COL_LENGTH(N'dbo.tblRadiologyStudies', N'FollowUpNote') IS NULL
BEGIN
    ALTER TABLE dbo.tblRadiologyStudies ADD FollowUpNote NVARCHAR(500) NULL;
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_tblRadiologyStudies_Status'
      AND parent_object_id = OBJECT_ID(N'dbo.tblRadiologyStudies')
)
BEGIN
    ALTER TABLE dbo.tblRadiologyStudies ADD CONSTRAINT CK_tblRadiologyStudies_Status
        CHECK (Status BETWEEN 1 AND 3);
END;
GO

/* Status 3 means a follow-up is planned, so a date is expected. The constraint
   keeps the reminder data usable instead of silently empty. */
IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_tblRadiologyStudies_FollowUp'
      AND parent_object_id = OBJECT_ID(N'dbo.tblRadiologyStudies')
)
BEGIN
    ALTER TABLE dbo.tblRadiologyStudies ADD CONSTRAINT CK_tblRadiologyStudies_FollowUp
        CHECK (Status <> 3 OR FollowUpDate IS NOT NULL);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_tblRadiologyStudies_Status' AND object_id = OBJECT_ID(N'dbo.tblRadiologyStudies')
)
    CREATE INDEX IX_tblRadiologyStudies_Status ON dbo.tblRadiologyStudies(Status, FollowUpDate);
GO

PRINT N'Dentix: Study status and follow-up are ready.';
GO
