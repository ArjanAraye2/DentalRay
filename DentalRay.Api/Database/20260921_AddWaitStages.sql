/*
 Dentix - waiting stages, so "waiting" means the right thing per specialty.

 A patient in a radiology clinic waits for an image; in a prosthodontics clinic
 waits for a prosthesis; in endodontics waits for the next root canal session.
 One fixed list cannot express that, so the stage a study is waiting for is
 configurable and can be scoped to a specialty.

 The base status stays a small fixed set so counts remain meaningful:
   1 = open, 2 = completed, 3 = waiting

 Safe to run more than once.
*/
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.tblWaitStages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblWaitStages
    (
        WaitStageID  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblWaitStages PRIMARY KEY,
        Name         NVARCHAR(100) NOT NULL,
        -- NULL means the stage applies to every specialty.
        SpecialtyID  INT NULL,
        -- Message sent to the patient when this stage becomes ready.
        SmsTemplate  NVARCHAR(500) NULL,
        SortOrder    INT NOT NULL CONSTRAINT DF_tblWaitStages_Sort DEFAULT(0),
        IsActive     BIT NOT NULL CONSTRAINT DF_tblWaitStages_Active DEFAULT(1),
        CreatedDate  DATETIME2(0) NOT NULL CONSTRAINT DF_tblWaitStages_Created DEFAULT(SYSDATETIME()),
        ModifiedDate DATETIME2(0) NULL,
        CONSTRAINT FK_tblWaitStages_Specialties FOREIGN KEY(SpecialtyID)
            REFERENCES dbo.tblDentalSpecialties(SpecialtyID)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_tblWaitStages_Specialty' AND object_id = OBJECT_ID(N'dbo.tblWaitStages'))
    CREATE INDEX IX_tblWaitStages_Specialty ON dbo.tblWaitStages(SpecialtyID, SortOrder);
GO

/* Seed a sensible stage per specialty, using the specialty names already present.
   Existing rows are left alone, so the clinic can rename or add freely. */
IF NOT EXISTS (SELECT 1 FROM dbo.tblWaitStages)
BEGIN
    INSERT INTO dbo.tblWaitStages (Name, SpecialtyID, SmsTemplate, SortOrder)
    SELECT v.Name, s.SpecialtyID, v.Sms, v.Sort
    FROM (VALUES
        (N'رادیولوژی دهان، فک و صورت', N'آماده شدن عکس',   N'{patient} عزیز، تصاویر رادیولوژی شما آماده است. Dentix', 10),
        (N'پروتزهای دندانی',          N'آماده شدن پروتز', N'{patient} عزیز، پروتز شما آماده تحویل است. Dentix',      20),
        (N'اندودانتیکس (درمان ریشه)', N'جلسه بعدی ریشه',  N'{patient} عزیز، نوبت جلسه بعدی درمان ریشه شما ثبت شد. Dentix', 30),
        (N'ارتودنسی',                 N'جلسه تنظیم',      N'{patient} عزیز، زمان جلسه تنظیم شما فرا رسیده است. Dentix', 40),
        (N'جراحی دهان، فک و صورت',    N'بهبود پس از جراحی', N'{patient} عزیز، برای بررسی پس از جراحی با مطب تماس بگیرید. Dentix', 50),
        (N'پریودانتیکس (بیماری‌های لثه)', N'جلسه جرم‌گیری', N'{patient} عزیز، نوبت جلسه جرم‌گیری شما ثبت شد. Dentix', 60),
        (N'دندانپزشکی کودکان',        N'نوبت کودک',       N'{patient} عزیز، نوبت دندانپزشکی کودک شما ثبت شد. Dentix', 70),
        (N'دندانپزشک عمومی',          N'پیگیری درمان',    N'{patient} عزیز، برای پیگیری درمان با مطب تماس بگیرید. Dentix', 80)
    ) AS v(SpecialtyName, Name, Sms, Sort)
    LEFT JOIN dbo.tblDentalSpecialties s
        ON s.SpecialtyName = v.SpecialtyName AND s.IsActive = 1
    WHERE s.SpecialtyID IS NOT NULL;
END;
GO

/* A stage that applies to everyone, used when the dentist has no specialty set. */
IF NOT EXISTS (SELECT 1 FROM dbo.tblWaitStages WHERE SpecialtyID IS NULL)
BEGIN
    INSERT INTO dbo.tblWaitStages (Name, SpecialtyID, SmsTemplate, SortOrder)
    VALUES (N'تماس مجدد', NULL, N'{patient} عزیز، برای پیگیری با مطب تماس بگیرید. Dentix', 100);
END;
GO

/* Link a study to the stage it is waiting for. */
IF COL_LENGTH(N'dbo.tblRadiologyStudies', N'WaitStageID') IS NULL
BEGIN
    ALTER TABLE dbo.tblRadiologyStudies ADD WaitStageID INT NULL;
END;
GO

PRINT N'Dentix: waiting stages are ready.';
GO

SELECT N'wait stages: ' + CAST(COUNT(*) AS nvarchar(5)) FROM dbo.tblWaitStages;
GO
