-- 20261005_ValueRefText.sql
-- The reference range and unit printed on the lab sheet belong to that one
-- observation, not to the dictionary: ranges differ per lab, age, sex and date.
-- Until now the panel and the AI prompt showed the dictionary's default range
-- (example: sheet 80-306 vs dictionary 44-147 for Alkaline Phosphatase).
-- Keep what the paper printed, per recorded value.

IF COL_LENGTH('tblStudyFactorValues', 'RefText') IS NULL
    ALTER TABLE tblStudyFactorValues ADD RefText nvarchar(500) NULL;

IF COL_LENGTH('tblStudyFactorValues', 'UnitText') IS NULL
    ALTER TABLE tblStudyFactorValues ADD UnitText nvarchar(100) NULL;
