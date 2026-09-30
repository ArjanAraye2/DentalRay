-- 20261006_CleanAutoFactors.sql
-- The old line-based parser polluted the dictionary: names merged from
-- neighbouring rows ("Appearance Turbid Epithelial") and reference texts
-- carrying the following lines. Those rows are deactivated (never deleted -
-- recorded values keep their factor) and the surviving ones get clean refs.

-- Names that are OCR fragments, not tests.
UPDATE tblClinicalFactors SET IsActive = 0
WHERE FactorCode LIKE 'LAB.CUSTOM.%' AND (
       NameEn LIKE 'wt %' OR NameEn LIKE '%Kise%' OR NameEn LIKE '%Colall%'
    OR NameEn IN ('Insufficient', 'Sufficient', 'Urine Analysis', 'MICH')
    OR NameEn LIKE 'Color Yellow%' OR NameEn LIKE 'Appearance Turbid%'
    OR NameEn LIKE 'S.G.0.T%');

-- Reference texts cleaned to what the sheet actually prints; unknown ranges
-- become NULL instead of a wrong guess.
UPDATE tblClinicalFactors SET RefText = '0-6', RefLow = 0, RefHigh = 6
    WHERE NameEn = 'EOS%' AND FactorCode LIKE 'LAB.CUSTOM.%';
UPDATE tblClinicalFactors SET RefText = '0-2', RefLow = 0, RefHigh = 2
    WHERE NameEn = 'BAS%' AND FactorCode LIKE 'LAB.CUSTOM.%';
UPDATE tblClinicalFactors SET RefText = '4.2-5.6', RefLow = 4.2, RefHigh = 5.6
    WHERE NameEn = 'RBC' AND FactorCode LIKE 'LAB.CUSTOM.%';
UPDATE tblClinicalFactors SET RefText = '32-36', RefLow = 32, RefHigh = 36
    WHERE NameEn = 'MCHC' AND FactorCode LIKE 'LAB.CUSTOM.%';
UPDATE tblClinicalFactors SET RefText = '11-15', RefLow = 11, RefHigh = 15
    WHERE NameEn = 'RDWCV' AND FactorCode LIKE 'LAB.CUSTOM.%';
UPDATE tblClinicalFactors SET RefText = '9.4-18.1', RefLow = 9.4, RefHigh = 18.1
    WHERE NameEn = 'PDW' AND FactorCode LIKE 'LAB.CUSTOM.%';
UPDATE tblClinicalFactors SET RefText = '70-100', RefLow = 70, RefHigh = 100
    WHERE NameEn = 'fasting blood sugar' AND FactorCode LIKE 'LAB.CUSTOM.%';
UPDATE tblClinicalFactors SET RefText = '3.5-5.2', RefLow = 3.5, RefHigh = 5.2
    WHERE NameEn = 'Albumin' AND FactorCode LIKE 'LAB.CUSTOM.%';
UPDATE tblClinicalFactors SET RefText = NULL, RefLow = NULL, RefHigh = NULL
    WHERE FactorCode LIKE 'LAB.CUSTOM.%'
      AND NameEn IN ('Globulin', 'Specific Gravity', 'K (Potassium )', 'LDL/HDL Ratio', 'Cholesterol/HDL Ratio');

-- Calcium: the name carried OCR junk ("oS. mg/dL").
UPDATE tblClinicalFactors SET NameEn = 'Ca ( Calcium )', RefText = '8.6 - 10.3', RefLow = 8.6, RefHigh = 10.3
    WHERE FactorCode LIKE 'LAB.CUSTOM.%' AND NameEn LIKE 'Ca ( Calcium )%';
