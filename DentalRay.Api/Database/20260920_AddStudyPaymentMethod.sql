/*
 DentalRay - Add tblStudyPayments.PaymentMethod
 The model StudyPayment has exposed PaymentMethod for a while, but databases
 created from the earlier script never received the column. Without it every
 SELECT on tblStudyPayments fails with "Invalid column name 'PaymentMethod'",
 which is what broke the Study finance panel.

 Safe to run more than once. The ALTER and the CHECK live in separate batches
 because SQL Server compiles a whole batch before executing it.
*/
SET XACT_ABORT ON;

IF COL_LENGTH(N'dbo.tblStudyPayments', N'PaymentMethod') IS NULL
BEGIN
    ALTER TABLE dbo.tblStudyPayments ADD PaymentMethod TINYINT NULL;
END;
GO

IF COL_LENGTH(N'dbo.tblStudyPayments', N'PaymentMethod') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM sys.check_constraints
       WHERE name = N'CK_tblStudyPayments_Method'
         AND parent_object_id = OBJECT_ID(N'dbo.tblStudyPayments')
   )
BEGIN
    ALTER TABLE dbo.tblStudyPayments ADD CONSTRAINT CK_tblStudyPayments_Method
        CHECK (PaymentMethod IS NULL OR PaymentMethod BETWEEN 1 AND 3);
END;
GO

PRINT N'DentalRay: tblStudyPayments.PaymentMethod is ready.';
GO
