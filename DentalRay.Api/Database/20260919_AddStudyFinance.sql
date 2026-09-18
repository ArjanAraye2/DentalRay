SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.tblStudyActions',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.tblStudyActions
 (
  StudyActionID BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblStudyActions PRIMARY KEY,
  StudyID INT NOT NULL,
  Description NVARCHAR(500) NOT NULL,
  Amount DECIMAL(18,2) NOT NULL,
  DiscountAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_tblStudyActions_Discount DEFAULT(0),
  CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_tblStudyActions_Created DEFAULT(SYSDATETIME()),
  ModifiedDate DATETIME2(0) NULL,
  CONSTRAINT CK_tblStudyActions_Amount CHECK(Amount>=0),
  CONSTRAINT CK_tblStudyActions_Discount CHECK(DiscountAmount>=0 AND DiscountAmount<=Amount),
  CONSTRAINT FK_tblStudyActions_Studies FOREIGN KEY(StudyID) REFERENCES dbo.tblRadiologyStudies(StudyID)
 );
 CREATE INDEX IX_tblStudyActions_StudyID ON dbo.tblStudyActions(StudyID);
END;

IF OBJECT_ID(N'dbo.tblStudyPayments',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.tblStudyPayments
 (
  StudyPaymentID BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblStudyPayments PRIMARY KEY,
  StudyID INT NOT NULL,
  PaymentDate DATETIME2(0) NOT NULL,
  Amount DECIMAL(18,2) NOT NULL,
  Description NVARCHAR(500) NULL,
  CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_tblStudyPayments_Created DEFAULT(SYSDATETIME()),
  ModifiedDate DATETIME2(0) NULL,
  CONSTRAINT CK_tblStudyPayments_Amount CHECK(Amount>0),
  CONSTRAINT FK_tblStudyPayments_Studies FOREIGN KEY(StudyID) REFERENCES dbo.tblRadiologyStudies(StudyID)
 );
 CREATE INDEX IX_tblStudyPayments_StudyID ON dbo.tblStudyPayments(StudyID);
END;

COMMIT TRANSACTION;
