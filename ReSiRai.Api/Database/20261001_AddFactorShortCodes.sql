/* =========================================================
   ReSiRai - factor short codes (lab-sheet style abbreviations)

   The lab sheet prints tests as short codes (Hb, WBC, Cr). The visit card now
   shows the same code next to the Persian factor name, so the doctor reads the
   screen and the paper with one pair of eyes.

   Safe to run more than once.
   ========================================================= */

IF COL_LENGTH('dbo.tblClinicalFactors', 'ShortCode') IS NULL
    ALTER TABLE dbo.tblClinicalFactors ADD ShortCode NVARCHAR(20) NULL;
GO

/* --- Vitals & anthropometry --- */
UPDATE dbo.tblClinicalFactors SET ShortCode = N'SBP'    WHERE FactorCode = N'VITAL.SBP';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'DBP'    WHERE FactorCode = N'VITAL.DBP';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'HR'     WHERE FactorCode = N'VITAL.HR';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'Temp'   WHERE FactorCode = N'VITAL.TEMP';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'RR'     WHERE FactorCode = N'VITAL.RR';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'SpO2'   WHERE FactorCode = N'VITAL.SPO2';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'Ht'     WHERE FactorCode = N'ANTH.HEIGHT';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'Wt'     WHERE FactorCode = N'ANTH.WEIGHT';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'BMI'    WHERE FactorCode = N'ANTH.BMI';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'WC'     WHERE FactorCode = N'ANTH.WC';

/* --- Laboratory --- */
UPDATE dbo.tblClinicalFactors SET ShortCode = N'Hgb'     WHERE FactorCode = N'LAB.HGB';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'Hct'     WHERE FactorCode = N'LAB.HCT';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'WBC'     WHERE FactorCode = N'LAB.WBC';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'PLT'     WHERE FactorCode = N'LAB.PLT';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'MCV'     WHERE FactorCode = N'LAB.MCV';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'FBS'     WHERE FactorCode = N'LAB.FBS';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'HbA1c'   WHERE FactorCode = N'LAB.HBA1C';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'Chol'    WHERE FactorCode = N'LAB.CHOL';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'TG'      WHERE FactorCode = N'LAB.TG';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'LDL'     WHERE FactorCode = N'LAB.LDL';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'HDL'     WHERE FactorCode = N'LAB.HDL';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'TSH'     WHERE FactorCode = N'LAB.TSH';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'FT4'     WHERE FactorCode = N'LAB.FT4';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'Cr'      WHERE FactorCode = N'LAB.CREAT';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'BUN'     WHERE FactorCode = N'LAB.BUN';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'eGFR'    WHERE FactorCode = N'LAB.EGFR';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'AST'     WHERE FactorCode = N'LAB.AST';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'ALT'     WHERE FactorCode = N'LAB.ALT';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'ALP'     WHERE FactorCode = N'LAB.ALP';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'T.Bili'  WHERE FactorCode = N'LAB.BILI';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'CRP'     WHERE FactorCode = N'LAB.CRP';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'ESR'     WHERE FactorCode = N'LAB.ESR';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'UA'      WHERE FactorCode = N'LAB.UA';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'Vit-D'   WHERE FactorCode = N'LAB.VITD';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'B12'     WHERE FactorCode = N'LAB.B12';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'TnI'     WHERE FactorCode = N'LAB.TROP';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'U/A'     WHERE FactorCode = N'LAB.URINE';

/* --- Imaging & ECG --- */
UPDATE dbo.tblClinicalFactors SET ShortCode = N'CXR'  WHERE FactorCode = N'IMG.CXR';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'USG'  WHERE FactorCode = N'IMG.US_ABD';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'ECG'  WHERE FactorCode = N'ECG.RHYTHM';
UPDATE dbo.tblClinicalFactors SET ShortCode = N'QTc'  WHERE FactorCode = N'ECG.QTC';
GO
