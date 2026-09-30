/*
 20260930_AddClinicalFactors.sql - Clinical decision factors (phase 1: internal medicine)

 Purpose:
   Structured, numeric patient-state ("sharayet-e feli") that is later fed to the
   AI consultation service. Every factor carries its scientific identity:
   international code (LOINC where available), standard unit (UCUM), reference
   range and its guideline source.

 Quality policy (3 layers):
   1. No factor is added without a source reference.
   2. Before go-live of each specialty, a specialist of that field reviews and
      signs the factor list (LoincStatus column tracks this).
   3. Ranges are versioned by guideline year and re-reviewed when guidelines
      update (e.g. ADA 2024 -> ADA 2026).
   LoincStatus: 0 = pending review, 1 = verified by specialist.
   NULL LoincCode means "to be confirmed" - never guess a code.

 Tables:
   tblClinicalFactors        global factor dictionary (reused by every specialty)
   tblSpecialtyFactorSets    which factors a specialty uses (required/common/order)
   tblStudyFactorValues      recorded values per visit (trend-capable, source-tagged)
   tblLabReportExtractions   AI extraction batches from lab-report images

 Usage:
   sqlcmd -S <server> -E -i 20260930_AddClinicalFactors.sql
*/
SET NOCOUNT ON;
GO

/* =========================
   1. Factor dictionary
   ========================= */
IF OBJECT_ID(N'dbo.tblClinicalFactors',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblClinicalFactors(
        FactorID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblClinicalFactors PRIMARY KEY,
        FactorCode NVARCHAR(60) NOT NULL CONSTRAINT UQ_tblClinicalFactors_Code UNIQUE,
        NameFa NVARCHAR(200) NOT NULL,
        NameEn NVARCHAR(200) NOT NULL,
        Category NVARCHAR(40) NOT NULL,           -- Vitals|Anthropometry|History|Exam|Lab|Imaging|Score
        DataType TINYINT NOT NULL,                -- 1=number 2=enum 3=boolean 4=text 5=date
        UnitUCUM NVARCHAR(30) NULL,
        LoincCode NVARCHAR(20) NULL,
        LoincStatus TINYINT NOT NULL CONSTRAINT DF_tblClinicalFactors_LoincStatus DEFAULT(0),
        RefLow DECIMAL(18,4) NULL,
        RefHigh DECIMAL(18,4) NULL,
        RefText NVARCHAR(200) NULL,
        RefSource NVARCHAR(200) NOT NULL,
        RefPopulation NVARCHAR(120) NULL,
        AbnormalDirection TINYINT NULL,           -- 1=high is bad 2=low is bad 3=both
        OptionsJson NVARCHAR(MAX) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_tblClinicalFactors_IsActive DEFAULT(1),
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_tblClinicalFactors_CreatedDate DEFAULT(SYSDATETIME())
    );
END;
GO

/* =========================
   2. Specialty factor sets
   ========================= */
IF OBJECT_ID(N'dbo.tblSpecialtyFactorSets',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblSpecialtyFactorSets(
        SpecialtyID INT NOT NULL,
        FactorID INT NOT NULL,
        IsRequired BIT NOT NULL CONSTRAINT DF_tblSpecialtyFactorSets_IsRequired DEFAULT(0),
        IsCommon BIT NOT NULL CONSTRAINT DF_tblSpecialtyFactorSets_IsCommon DEFAULT(0),
        SortOrder INT NOT NULL CONSTRAINT DF_tblSpecialtyFactorSets_SortOrder DEFAULT(0),
        CONSTRAINT PK_tblSpecialtyFactorSets PRIMARY KEY(SpecialtyID,FactorID),
        CONSTRAINT FK_tblSpecialtyFactorSets_Factors FOREIGN KEY(FactorID) REFERENCES dbo.tblClinicalFactors(FactorID),
        CONSTRAINT FK_tblSpecialtyFactorSets_Specialties FOREIGN KEY(SpecialtyID) REFERENCES dbo.tblSpecialties(SpecialtyID)
    );
END;
GO

/* =========================
   3. Values per visit (trend-capable)
   ========================= */
IF OBJECT_ID(N'dbo.tblStudyFactorValues',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblStudyFactorValues(
        FactorValueID BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblStudyFactorValues PRIMARY KEY,
        StudyID INT NOT NULL,
        FactorID INT NOT NULL,
        ValueNumber DECIMAL(18,4) NULL,
        ValueText NVARCHAR(500) NULL,
        ValueBit BIT NULL,
        ValueDate DATETIME2(0) NULL,
        ObservedAt DATETIME2(0) NOT NULL CONSTRAINT DF_tblStudyFactorValues_ObservedAt DEFAULT(SYSDATETIME()),
        Source TINYINT NOT NULL,                  -- 1=manual 2=lab-report extraction 3=device 4=computed
        ExtractionID BIGINT NULL,
        Confidence DECIMAL(5,2) NULL,
        CreatedByUserID INT NULL,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_tblStudyFactorValues_CreatedDate DEFAULT(SYSDATETIME()),
        CONSTRAINT FK_tblStudyFactorValues_Studies FOREIGN KEY(StudyID) REFERENCES dbo.tblRadiologyStudies(StudyID),
        CONSTRAINT FK_tblStudyFactorValues_Factors FOREIGN KEY(FactorID) REFERENCES dbo.tblClinicalFactors(FactorID)
    );
    CREATE INDEX IX_tblStudyFactorValues_Study ON dbo.tblStudyFactorValues(StudyID, FactorID, ObservedAt);
END;
GO

/* =========================
   4. Lab-report extraction batches
   ========================= */
IF OBJECT_ID(N'dbo.tblLabReportExtractions',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblLabReportExtractions(
        ExtractionID BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblLabReportExtractions PRIMARY KEY,
        StudyID INT NOT NULL,
        FileName NVARCHAR(300) NULL,
        ImagePath NVARCHAR(500) NULL,
        LabName NVARCHAR(200) NULL,
        SampleDate DATETIME2(0) NULL,
        RawJson NVARCHAR(MAX) NULL,
        Status TINYINT NOT NULL CONSTRAINT DF_tblLabReportExtractions_Status DEFAULT(1), -- 1=pending 2=confirmed 3=partial 4=failed
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_tblLabReportExtractions_CreatedDate DEFAULT(SYSDATETIME()),
        ConfirmedByUserID INT NULL,
        ConfirmedAt DATETIME2(0) NULL,
        CONSTRAINT FK_tblLabReportExtractions_Studies FOREIGN KEY(StudyID) REFERENCES dbo.tblRadiologyStudies(StudyID)
    );
END;
GO

/* =========================
   5. Seed: internal medicine
   ========================= */
IF NOT EXISTS(SELECT 1 FROM dbo.tblSpecialties WHERE SpecialtyName = N'بیماری‌های داخلی')
    INSERT INTO dbo.tblSpecialties(SpecialtyName, IsActive) VALUES (N'بیماری‌های داخلی', 1);
GO

/* --- 5a. Vitals & anthropometry (always shown) --- */
IF NOT EXISTS(SELECT 1 FROM dbo.tblClinicalFactors WHERE FactorCode = N'VITAL.SBP')
INSERT INTO dbo.tblClinicalFactors
    (FactorCode,NameFa,NameEn,Category,DataType,UnitUCUM,LoincCode,LoincStatus,RefLow,RefHigh,RefText,RefSource,RefPopulation,AbnormalDirection,OptionsJson)
VALUES
    (N'VITAL.SBP',N'فشار سیستولیک',N'Systolic blood pressure',N'Vitals',1,N'mm[Hg]',N'8480-6',1,90,120,N'هدف <130 بر پایه ریسک',N'ACC/AHA 2017',N'بزرگسال',1,NULL),
    (N'VITAL.DBP',N'فشار دیاستولیک',N'Diastolic blood pressure',N'Vitals',1,N'mm[Hg]',N'8462-4',1,60,80,N'هدف <80',N'ACC/AHA 2017',N'بزرگسال',1,NULL),
    (N'VITAL.HR',N'نبض',N'Heart rate',N'Vitals',1,N'/min',N'8867-4',1,60,100,NULL,N'روایج بالینی',N'بزرگسال',3,NULL),
    (N'VITAL.TEMP',N'دما',N'Body temperature',N'Vitals',1,N'Cel',N'8310-5',1,36.1,37.2,NULL,N'WHO',N'بزرگسال',3,NULL),
    (N'VITAL.RR',N'تعداد تنفس',N'Respiratory rate',N'Vitals',1,N'/min',N'9279-1',1,12,20,NULL,N'WHO',N'بزرگسال',3,NULL),
    (N'VITAL.SPO2',N'اشباع اکسیژن',N'Oxygen saturation',N'Vitals',1,N'%',N'2708-6',1,95,100,NULL,N'WHO',N'بزرگسال',2,NULL),
    (N'ANTH.HEIGHT',N'قد',N'Height',N'Anthropometry',1,N'cm',N'8302-2',1,NULL,NULL,NULL,N'WHO',N'بزرگسال',NULL,NULL),
    (N'ANTH.WEIGHT',N'وزن',N'Weight',N'Anthropometry',1,N'kg',N'29463-7',1,NULL,NULL,NULL,N'WHO',N'بزرگسال',NULL,NULL),
    (N'ANTH.BMI',N'شاخص توده بدنی (BMI)',N'Body mass index',N'Score',1,N'kg/m2',N'39156-5',1,18.5,24.9,N'اضافه وزن 25-29.9 / چاقی >=30',N'WHO',N'بزرگسال',3,NULL),
    (N'ANTH.WC',N'دور کمر',N'Waist circumference',N'Anthropometry',1,N'cm',N'56086-2',0,NULL,NULL,N'مرد <102 / زن <88',N'NHLBI',N'بزرگسال',1,NULL);
GO

/* --- 5b. History & structured exam --- */
IF NOT EXISTS(SELECT 1 FROM dbo.tblClinicalFactors WHERE FactorCode = N'EXAM.GENERAL')
INSERT INTO dbo.tblClinicalFactors
    (FactorCode,NameFa,NameEn,Category,DataType,UnitUCUM,LoincCode,LoincStatus,RefLow,RefHigh,RefText,RefSource,RefPopulation,AbnormalDirection,OptionsJson)
VALUES
    (N'HIST.SMOKING',N'استعمال دخانیات',N'Smoking status',N'History',2,NULL,N'72166-2',0,NULL,NULL,NULL,N'WHO',N'بزرگسال',NULL,N'[{"v":0,"t":"هرگز"},{"v":1,"t":"سابقه"},{"v":2,"t":"فعلی"}]'),
    (N'HIST.PACKYEAR',N'پک سال',N'Pack-years',N'History',1,NULL,NULL,0,0,10,NULL,N'WHO',N'سیگاری',1,NULL),
    (N'HIST.FAMHX',N'سابقه خانوادگی (دیابت/فشار/قلب/سرطان)',N'Family history',N'History',3,NULL,NULL,0,NULL,NULL,NULL,N'روایج بالینی',N'بزرگسال',1,NULL),
    (N'HIST.PREG',N'بارداری/شیردهی',N'Pregnancy / lactation',N'History',2,NULL,NULL,0,NULL,NULL,NULL,N'WHO',N'زن',NULL,N'[{"v":0,"t":"خیر"},{"v":1,"t":"باردار"},{"v":2,"t":"شیرده"}]'),
    (N'HIST.ALLERGY',N'آلرژی دارویی/غذایی',N'Allergies',N'History',4,NULL,NULL,0,NULL,NULL,NULL,N'روایج بالینی',N'عمومی',NULL,NULL),
    (N'HIST.MEDS',N'داروهای مصرفی',N'Current medications',N'History',4,NULL,NULL,0,NULL,NULL,NULL,N'ATC',N'عمومی',NULL,NULL),
    (N'EXAM.GENERAL',N'وضعیت عمومی',N'General appearance',N'Exam',2,NULL,NULL,0,NULL,NULL,NULL,N'روایج بالینی',N'عمومی',NULL,N'[{"v":0,"t":"نرمال"},{"v":1,"t":"خفیف"},{"v":2,"t":"متوسط"},{"v":3,"t":"شدید"}]'),
    (N'EXAM.NECK',N'تیروئید/غدد لنفاوی گردن',N'Neck exam',N'Exam',2,NULL,NULL,0,NULL,NULL,NULL,N'روایج بالینی',N'عمومی',NULL,N'[{"v":0,"t":"نرمال"},{"v":1,"t":"خفیف"},{"v":2,"t":"متوسط"},{"v":3,"t":"شدید"}]'),
    (N'EXAM.CARDIO',N'معاینه قلب و عروق (سوفل/ادم)',N'Cardiovascular exam',N'Exam',2,NULL,NULL,0,NULL,NULL,NULL,N'روایج بالینی',N'عمومی',NULL,N'[{"v":0,"t":"نرمال"},{"v":1,"t":"خفیف"},{"v":2,"t":"متوسط"},{"v":3,"t":"شدید"}]'),
    (N'EXAM.LUNG',N'معاینه ریه',N'Lung exam',N'Exam',2,NULL,NULL,0,NULL,NULL,NULL,N'روایج بالینی',N'عمومی',NULL,N'[{"v":0,"t":"نرمال"},{"v":1,"t":"خفیف"},{"v":2,"t":"متوسط"},{"v":3,"t":"شدید"}]'),
    (N'EXAM.ABDOMEN',N'معاینه شکم',N'Abdominal exam',N'Exam',2,NULL,NULL,0,NULL,NULL,NULL,N'روایج بالینی',N'عمومی',NULL,N'[{"v":0,"t":"نرمال"},{"v":1,"t":"خفیف"},{"v":2,"t":"متوسط"},{"v":3,"t":"شدید"}]'),
    (N'EXAM.NEURO',N'معاینه عصبی',N'Neurologic exam',N'Exam',2,NULL,NULL,0,NULL,NULL,NULL,N'روایج بالینی',N'عمومی',NULL,N'[{"v":0,"t":"نرمال"},{"v":1,"t":"خفیف"},{"v":2,"t":"متوسط"},{"v":3,"t":"شدید"}]'),
    (N'EXAM.SKIN',N'پوست',N'Skin exam',N'Exam',2,NULL,NULL,0,NULL,NULL,NULL,N'روایج بالینی',N'عمومی',NULL,N'[{"v":0,"t":"نرمال"},{"v":1,"t":"خفیف"},{"v":2,"t":"متوسط"},{"v":3,"t":"شدید"}]');
GO

/* --- 5c. Laboratory (numeric core of the AI input) --- */
IF NOT EXISTS(SELECT 1 FROM dbo.tblClinicalFactors WHERE FactorCode = N'LAB.HGB')
INSERT INTO dbo.tblClinicalFactors
    (FactorCode,NameFa,NameEn,Category,DataType,UnitUCUM,LoincCode,LoincStatus,RefLow,RefHigh,RefText,RefSource,RefPopulation,AbnormalDirection,OptionsJson)
VALUES
    (N'LAB.HGB',N'هموگلوبین',N'Hemoglobin',N'Lab',1,N'g/dL',N'718-7',1,NULL,NULL,N'مرد 13.5-17.5 / زن 12-15.5',N'WHO',N'بزرگسال',2,NULL),
    (N'LAB.HCT',N'هماتوکریت',N'Hematocrit',N'Lab',1,N'%',N'4544-3',0,NULL,NULL,N'مرد 40-50 / زن 36-44',N'WHO',N'بزرگسال',2,NULL),
    (N'LAB.WBC',N'گلبول سفید',N'White blood cell count',N'Lab',1,N'10*3/uL',N'6690-2',1,4,11,NULL,N'روایج بالینی',N'بزرگسال',3,NULL),
    (N'LAB.PLT',N'پلاکت',N'Platelet count',N'Lab',1,N'10*3/uL',N'777-3',1,150,450,NULL,N'روایج بالینی',N'بزرگسال',3,NULL),
    (N'LAB.MCV',N'حجم متوسط گلبول قرمز (MCV)',N'Mean corpuscular volume',N'Lab',1,N'fL',N'787-2',1,80,100,NULL,N'روایج بالینی',N'بزرگسال',3,NULL),
    (N'LAB.FBS',N'قند خون ناشتا',N'Fasting blood glucose',N'Lab',1,N'mg/dL',N'1558-6',1,70,99,N'پره‌دیابت 100-125 / دیابت >=126',N'ADA 2024',N'بزرگسال',1,NULL),
    (N'LAB.HBA1C',N'هموگلوبین A1c',N'Hemoglobin A1c',N'Lab',1,N'%',N'4548-4',1,NULL,NULL,N'هدف کنترل <7',N'ADA Standards of Care 2024',N'بزرگسال دیابتی',1,NULL),
    (N'LAB.CHOL',N'کلسترول تام',N'Total cholesterol',N'Lab',1,N'mg/dL',N'2093-3',1,NULL,NULL,N'مطلوب <200',N'ESC/EAS 2019',N'بزرگسال',1,NULL),
    (N'LAB.TG',N'تری‌گلیسرید',N'Triglycerides',N'Lab',1,N'mg/dL',N'2571-8',1,NULL,NULL,N'مطلوب <150',N'ESC/EAS 2019',N'بزرگسال',1,NULL),
    (N'LAB.LDL',N'LDL کلسترول',N'LDL cholesterol',N'Lab',1,N'mg/dL',N'13457-7',1,NULL,NULL,N'هدف بر پایه ریسک قلبی',N'ESC/EAS 2019',N'بزرگسال',1,NULL),
    (N'LAB.HDL',N'HDL کلسترول',N'HDL cholesterol',N'Lab',1,N'mg/dL',N'2085-9',1,NULL,NULL,N'مرد >40 / زن >50',N'ESC/EAS 2019',N'بزرگسال',2,NULL),
    (N'LAB.TSH',N'هورمون محرک تیروئید (TSH)',N'Thyroid stimulating hormone',N'Lab',1,N'mIU/L',N'3016-3',1,0.4,4.2,NULL,N'NACB/ATA',N'بزرگسال',3,NULL),
    (N'LAB.FT4',N'تیروکسین آزاد (Free T4)',N'Thyroxine free',N'Lab',1,N'ng/dL',N'3024-7',0,0.8,1.8,NULL,N'NACB/ATA',N'بزرگسال',3,NULL),
    (N'LAB.CREAT',N'کراتینین',N'Creatinine',N'Lab',1,N'mg/dL',N'2160-0',1,NULL,NULL,N'مرد 0.7-1.2 / زن 0.5-1.1',N'KDIGO 2021',N'بزرگسال',1,NULL),
    (N'LAB.BUN',N'اوره خون',N'Blood urea nitrogen',N'Lab',1,N'mg/dL',N'3094-0',0,7,20,NULL,N'روایج بالینی',N'بزرگسال',3,NULL),
    (N'LAB.EGFR',N'نرخ فیلتراسیون کلیه (eGFR)',N'eGFR CKD-EPI',N'Score',1,N'mL/min/{1.73_m2}',N'33914-3',0,NULL,NULL,N'CKD stage G1 >=90 / G3a 45-59',N'KDIGO 2021',N'بزرگسال',2,NULL),
    (N'LAB.AST',N'AST',N'Aspartate aminotransferase',N'Lab',1,N'U/L',N'1920-8',1,10,40,NULL,N'روایج بالینی',N'بزرگسال',1,NULL),
    (N'LAB.ALT',N'ALT',N'Alanine aminotransferase',N'Lab',1,N'U/L',N'1742-6',1,7,56,NULL,N'روایج بالینی',N'بزرگسال',1,NULL),
    (N'LAB.ALP',N'آلکالین فسفاتاز',N'Alkaline phosphatase',N'Lab',1,N'U/L',N'6768-6',0,44,147,NULL,N'روایج بالینی',N'بزرگسال',3,NULL),
    (N'LAB.BILI',N'بیلی‌روبین تام',N'Bilirubin total',N'Lab',1,N'mg/dL',N'1975-2',0,0.1,1.2,NULL,N'روایج بالینی',N'بزرگسال',1,NULL),
    (N'LAB.CRP',N'پروتئین واکنشی C',N'C-reactive protein',N'Lab',1,N'mg/L',N'1988-5',1,NULL,6,NULL,N'روایج بالینی',N'بزرگسال',1,NULL),
    (N'LAB.ESR',N'سرعت رسوب گلبول قرمز',N'Erythrocyte sedimentation rate',N'Lab',1,N'mm/h',N'4537-7',0,NULL,NULL,N'مرد <15 / زن <20',N'روایج بالینی',N'بزرگسال',1,NULL),
    (N'LAB.UA',N'اسید اوریک',N'Uric acid',N'Lab',1,N'mg/dL',N'3084-1',0,3.5,7.2,NULL,N'روایج بالینی',N'بزرگسال',1,NULL),
    (N'LAB.VITD',N'ویتامین D',N'Vitamin D 25-OH',N'Lab',1,N'ng/mL',N'1989-3',0,30,100,N'کمبود <20',N'IOM',N'بزرگسال',2,NULL),
    (N'LAB.B12',N'ویتامین B12',N'Vitamin B12',N'Lab',1,N'pg/mL',N'2132-9',0,200,900,NULL,N'روایج بالینی',N'بزرگسال',2,NULL),
    (N'LAB.TROP',N'تروپونین',N'Troponin I',N'Lab',1,N'ng/mL',N'10839-9',0,NULL,NULL,N'بالای صدم برابر نرمال = آسیب ماهیچه قلب',N'ACC/AHA 2021',N'بزرگسال',1,NULL),
    (N'LAB.URINE',N'آنالیز ادرار',N'Urinalysis',N'Lab',2,NULL,NULL,0,NULL,NULL,NULL,N'روایج بالینی',N'عمومی',NULL,N'[{"v":0,"t":"نرمال"},{"v":1,"t":"پروتین‌اوری"},{"v":2,"t":"هماچوری"},{"v":3,"t":"عفونت"}]');
GO

/* --- 5d. Imaging & ECG --- */
IF NOT EXISTS(SELECT 1 FROM dbo.tblClinicalFactors WHERE FactorCode = N'ECG.RHYTHM')
INSERT INTO dbo.tblClinicalFactors
    (FactorCode,NameFa,NameEn,Category,DataType,UnitUCUM,LoincCode,LoincStatus,RefLow,RefHigh,RefText,RefSource,RefPopulation,AbnormalDirection,OptionsJson)
VALUES
    (N'IMG.CXR',N'یافته‌های رادیوگرافی قفسه سینه',N'Chest X-ray findings',N'Imaging',2,NULL,NULL,0,NULL,NULL,NULL,N'ACR',N'عمومی',NULL,N'[{"v":0,"t":"نرمال"},{"v":1,"t":"غیرطبیعی خفیف"},{"v":2,"t":"غیرطبیعی معنادار"}]'),
    (N'IMG.US_ABD',N'سونوگرافی شکم',N'Abdominal ultrasound',N'Imaging',2,NULL,NULL,0,NULL,NULL,NULL,N'ACR',N'عمومی',NULL,N'[{"v":0,"t":"نرمال"},{"v":1,"t":"غیرطبیعی خفیف"},{"v":2,"t":"غیرطبیعی معنادار"}]'),
    (N'ECG.RHYTHM',N'ریتم قلب',N'ECG rhythm',N'Imaging',2,NULL,NULL,0,NULL,NULL,NULL,N'ACC/AHA 2021',N'عمومی',NULL,N'[{"v":0,"t":"سینوسی"},{"v":1,"t":"فیبریلاسیون دهلیزی"},{"v":2,"t":"سایر"}]'),
    (N'ECG.QTC',N'فاصله QT اصلاح‌شده (QTc)',N'QTc interval',N'Imaging',1,N'ms',NULL,0,NULL,NULL,N'مرد <450 / زن <460',N'ACC/AHA 2021',N'بزرگسال',1,NULL);
GO

/* --- 5e. Bind factor set to internal medicine --- */
DECLARE @sid INT = (SELECT TOP 1 SpecialtyID FROM dbo.tblSpecialties WHERE SpecialtyName = N'بیماری‌های داخلی');

/* always-shown core */
INSERT INTO dbo.tblSpecialtyFactorSets(SpecialtyID,FactorID,IsRequired,IsCommon,SortOrder)
SELECT @sid, f.FactorID, 1, 1, ROW_NUMBER() OVER (ORDER BY f.FactorID)
FROM dbo.tblClinicalFactors f
WHERE f.FactorCode IN (N'VITAL.SBP',N'VITAL.DBP',N'VITAL.HR',N'VITAL.TEMP',N'VITAL.RR',N'VITAL.SPO2',
                     N'ANTH.HEIGHT',N'ANTH.WEIGHT',N'ANTH.BMI',N'HIST.MEDS',N'HIST.ALLERGY',N'EXAM.GENERAL')
  AND NOT EXISTS (SELECT 1 FROM dbo.tblSpecialtyFactorSets s
                  WHERE s.SpecialtyID = @sid AND s.FactorID = f.FactorID);

/* rest of the internal-medicine set (never re-bind a factor already in the set) */
INSERT INTO dbo.tblSpecialtyFactorSets(SpecialtyID,FactorID,IsRequired,IsCommon,SortOrder)
SELECT @sid, f.FactorID, 0, 0, 100 + ROW_NUMBER() OVER (ORDER BY f.FactorID)
FROM dbo.tblClinicalFactors f
WHERE (f.FactorCode LIKE N'LAB.%' OR f.FactorCode LIKE N'EXAM.%' OR f.FactorCode LIKE N'HIST.%'
    OR f.FactorCode LIKE N'IMG.%' OR f.FactorCode LIKE N'ECG.%' OR f.FactorCode = N'ANTH.WC')
  AND NOT EXISTS (SELECT 1 FROM dbo.tblSpecialtyFactorSets s
                  WHERE s.SpecialtyID = @sid AND s.FactorID = f.FactorID);
GO

PRINT N'20260930_AddClinicalFactors: factor dictionary + internal-medicine set created.';
SELECT Category, COUNT(*) AS Factors FROM dbo.tblClinicalFactors GROUP BY Category ORDER BY Category;
GO
