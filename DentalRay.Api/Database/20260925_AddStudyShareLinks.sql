/*
 Dentix - share links for Study images.

 The radiologist sends the images as a link by SMS. The link carries a random
 token, so whoever holds it can open the Study on a phone without logging in.
 Every view is counted and the link can be expired or revoked later.

 Safe to run more than once.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.tblStudyShareLinks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblStudyShareLinks
    (
        ShareID       BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblStudyShareLinks PRIMARY KEY,
        StudyID       INT NOT NULL,
        /* URL-safe random secret; never derived from patient data. */
        Token         NVARCHAR(64) NOT NULL CONSTRAINT UQ_tblStudyShareLinks_Token UNIQUE,
        RecipientName NVARCHAR(150) NULL,
        Mobile        NVARCHAR(30) NULL,
        /* The message exactly as sent, so the clinic can audit what went out. */
        Message       NVARCHAR(1000) NULL,
        CreatedBy     INT NULL,
        CreatedDate   DATETIME2(0) NOT NULL CONSTRAINT DF_tblStudyShareLinks_Created DEFAULT (SYSDATETIME()),
        ExpiresDate   DATETIME2(0) NULL,
        LastViewedAt  DATETIME2(0) NULL,
        ViewCount     INT NOT NULL CONSTRAINT DF_tblStudyShareLinks_Views DEFAULT (0),
        CONSTRAINT FK_tblStudyShareLinks_Studies FOREIGN KEY (StudyID)
            REFERENCES dbo.tblRadiologyStudies (StudyID)
    );
    CREATE INDEX IX_tblStudyShareLinks_StudyID
        ON dbo.tblStudyShareLinks (StudyID, CreatedDate DESC);
END;
GO

PRINT N'Dentix: tblStudyShareLinks is ready.';
GO
