/*
 Dentix - content hash of every radiology image.

 SHA-256 of the file bytes lets DentalRay recognise the same picture when it is
 uploaded a second time (renamed cameras, a photo taken twice, a re-scan of the
 same plate). Rows that predate this migration keep ContentHash = NULL and are
 simply skipped by the comparison until they are uploaded again.

 Comparison is scoped to one patient, which is why the index starts with
 PatientID: it never reveals whether another patient owns the same picture.

 Safe to run more than once.
*/
SET NOCOUNT ON;

IF COL_LENGTH(N'dbo.tblRadiologyImages', N'ContentHash') IS NULL
    ALTER TABLE dbo.tblRadiologyImages ADD ContentHash NVARCHAR(64) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_tblRadiologyImages_Patient_ContentHash')
    CREATE INDEX IX_tblRadiologyImages_Patient_ContentHash
        ON dbo.tblRadiologyImages (PatientID, ContentHash);
GO

PRINT N'Dentix: tblRadiologyImages.ContentHash is ready.';
GO
