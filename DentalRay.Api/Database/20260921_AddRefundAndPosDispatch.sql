/*
 Dentix - payment refunds and POS dispatch tracking.

 Refunds
   A refund is stored as a positive amount with IsRefund = 1 rather than a
   negative amount. Keeping amounts positive means CK_tblStudyPayments_Amount
   stays valid, totals are unambiguous and the UI can label the row clearly.
   Net received is therefore SUM(CASE WHEN IsRefund = 1 THEN -Amount ELSE Amount END).

 POS dispatch
   When a payment is pushed to a card reader the outcome is recorded here, so a
   failed send can be retried and audited later instead of being lost with the
   moment it happened.

 Safe to run more than once.
*/
SET NOCOUNT ON;

IF COL_LENGTH(N'dbo.tblStudyPayments', N'IsRefund') IS NULL
BEGIN
    ALTER TABLE dbo.tblStudyPayments
        ADD IsRefund BIT NOT NULL CONSTRAINT DF_tblStudyPayments_IsRefund DEFAULT(0);
END;
GO

/* Refunds always carry a reason; the reason text lives in Description, and this
   constraint guarantees a refund can never be saved without one. */
IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_tblStudyPayments_RefundReason'
      AND parent_object_id = OBJECT_ID(N'dbo.tblStudyPayments')
)
BEGIN
    ALTER TABLE dbo.tblStudyPayments ADD CONSTRAINT CK_tblStudyPayments_RefundReason
        CHECK (IsRefund = 0 OR (Description IS NOT NULL AND LEN(LTRIM(RTRIM(Description))) >= 3));
END;
GO

IF COL_LENGTH(N'dbo.tblStudyPayments', N'PosSettingID') IS NULL
BEGIN
    ALTER TABLE dbo.tblStudyPayments ADD PosSettingID INT NULL;
END;
GO

IF COL_LENGTH(N'dbo.tblStudyPayments', N'PosSentAt') IS NULL
BEGIN
    ALTER TABLE dbo.tblStudyPayments ADD PosSentAt DATETIME2(0) NULL;
END;
GO

IF COL_LENGTH(N'dbo.tblStudyPayments', N'PosSuccess') IS NULL
BEGIN
    ALTER TABLE dbo.tblStudyPayments ADD PosSuccess BIT NULL;
END;
GO

IF COL_LENGTH(N'dbo.tblStudyPayments', N'PosMessage') IS NULL
BEGIN
    ALTER TABLE dbo.tblStudyPayments ADD PosMessage NVARCHAR(500) NULL;
END;
GO

PRINT N'Dentix: refunds and POS dispatch tracking are ready.';
GO
