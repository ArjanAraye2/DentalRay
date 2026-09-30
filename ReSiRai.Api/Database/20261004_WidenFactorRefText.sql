/* =========================================================
   ReSiRai - printed reference text is often several lines
   ("70-100 Normal / 100-126 Impaired / > 126 Diabetic").
   200 characters truncate it and break saving new factors.
   ========================================================= */
SET NOCOUNT ON;

IF EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
          WHERE TABLE_NAME = 'tblClinicalFactors' AND COLUMN_NAME = 'RefText' AND CHARACTER_MAXIMUM_LENGTH = 200)
    ALTER TABLE dbo.tblClinicalFactors ALTER COLUMN RefText NVARCHAR(500) NULL;
GO
