/* =========================================================
   ReSiRai - "how many years since quitting tobacco"

   For a former smoker the interesting number is not only how much was smoked
   (pack-years) but how long ago it stopped. Shown only when the smoking status
   answer is "سابقه" (the panel hides rows that make no sense beside an answer).
   ========================================================= */
SET NOCOUNT ON;

IF NOT EXISTS(SELECT 1 FROM dbo.tblClinicalFactors WHERE FactorCode = N'HIST.QUIT')
INSERT INTO dbo.tblClinicalFactors
    (FactorCode,NameFa,NameEn,Category,DataType,UnitUCUM,LoincCode,LoincStatus,RefLow,RefHigh,RefText,RefSource,RefPopulation,AbnormalDirection,OptionsJson)
VALUES
    (N'HIST.QUIT',N'چند سال پیش دخانیات ترک شد؟',N'Years since quitting tobacco',N'History',1,NULL,NULL,0,0,60,NULL,N'روایج بالینی',N'سابقه مصرف دخانیات',1,NULL);
GO

DECLARE @sid INT = (SELECT TOP 1 SpecialtyID FROM dbo.tblSpecialties WHERE SpecialtyName = N'بیماری‌های داخلی');
DECLARE @fid INT = (SELECT TOP 1 FactorID FROM dbo.tblClinicalFactors WHERE FactorCode = N'HIST.QUIT');
IF @sid IS NOT NULL AND @fid IS NOT NULL
   AND NOT EXISTS(SELECT 1 FROM dbo.tblSpecialtyFactorSets WHERE SpecialtyID = @sid AND FactorID = @fid)
    INSERT INTO dbo.tblSpecialtyFactorSets(SpecialtyID,FactorID,IsRequired,IsCommon,SortOrder)
    VALUES(@sid,@fid,0,0,(SELECT ISNULL(MAX(SortOrder),0)+1 FROM dbo.tblSpecialtyFactorSets WHERE SpecialtyID = @sid));
GO
