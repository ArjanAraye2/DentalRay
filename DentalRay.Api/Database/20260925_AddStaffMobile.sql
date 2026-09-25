/*
 Dentix - mobile number for staff and dentists.

 The share feature texts the referring dentist too, and until now tblStaff had
 no phone column at all, so there was nowhere to keep that number.

 Safe to run more than once.
*/
SET NOCOUNT ON;

IF COL_LENGTH(N'dbo.tblStaff', N'Mobile') IS NULL
    ALTER TABLE dbo.tblStaff ADD Mobile NVARCHAR(30) NULL;
GO

PRINT N'Dentix: tblStaff.Mobile is ready.';
GO
