-- 20261007_FixLostDecimals.sql
-- OCR loses the decimal point of some printed values ("145" for the printed
-- 14.5). Those rows were saved uncorrected. Where a tenth of the value sits
-- inside the factor's own range and the stored value is far outside it, the
-- decimal point is restored. Nothing else is touched.

UPDATE v SET v.ValueNumber = v.ValueNumber / 10
FROM tblStudyFactorValues v
JOIN tblClinicalFactors f ON f.FactorID = v.FactorID
WHERE v.Source = 2 AND f.RefLow IS NOT NULL AND f.RefHigh IS NOT NULL
  AND v.ValueNumber > f.RefHigh * 8
  AND v.ValueNumber / 10 BETWEEN f.RefLow * 0.5 AND f.RefHigh * 1.6;
