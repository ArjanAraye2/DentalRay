-- ReSiRai: عبارت‌هایی که به‌اشتباه «تستِ جدید» شناخته شده‌اند.
--
-- نکته (تصمیمِ ۱۴۰۵/۰۷/۱۰): عبارتِ بدونِ مقدار معمولاً تست نیست — متنِ اضافیِ
-- برگه است («Comment:»، «wt»، «regimen(higher» …). این ردیف‌ها از خطوطِ توضیحی
-- و سربرگِ پانل‌ها ساخته شده‌اند و نام‌شان معنایِ آزمایشگاهی ندارد.
--
-- غیرفعال می‌شوند (IsActive = 0)، نه حذف: مقادیرِ قبلاً ثبت‌شده با ارجاعِ
-- FactorID سرِ جایشان می‌مانند، اما این عبارت‌ها دیگر در دیکشنری، تطبیقِ
-- خودکار و پیشنهادها دیده نمی‌شوند.
--
-- معیارِ انتخاب: نامِ بدونِ معنایِ آزمایشگاهی (توکنِ کوتاه، مقدارنما، سربرگِ
-- پانل، متنِ درهمِ OCR یا تکراریِ بی‌کیفیت). تست‌هایِ واقعیِ ادرار (پروتئین،
-- بیلی‌روبین، اپیتلیال، RBC ادرار و…) عمداً دست نمی‌خورند.

UPDATE tblClinicalFactors SET IsActive = 0
WHERE FactorCode IN (
    'LAB.CUSTOM.dy',                    -- dy
    'LAB.CUSTOM.wt',                    -- wt
    'LAB.CUSTOM.test',                  -- a
    'LAB.CUSTOM.test-2',                -- D
    'LAB.CUSTOM.hd',                    -- Hd
    'LAB.CUSTOM.tal',                   -- TAL
    'LAB.CUSTOM.of',                    -- Of
    'LAB.CUSTOM.ee',                    -- ee
    'LAB.CUSTOM.abuey',                 -- abuey
    'LAB.CUSTOM.positivei',             -- Positive(I+)  ← مقدار است، نه تست
    'LAB.CUSTOM.insufficient',          -- Insufficient  ← مقدار است، نه تست
    'LAB.CUSTOM.sufficient',            -- Sufficient    ← مقدار است، نه تست
    'LAB.CUSTOM.comment',               -- Comment:      ← سربرگِ توضیح
    'LAB.CUSTOM.regimenhigher',         -- regimen(higher
    'LAB.CUSTOM.wtkiselaicolallcpalygs',-- متنِ درهمِ OCR
    'LAB.CUSTOM.coloryellowrbc',        -- Color Yellow RBC.   ← دو سلولِ درهم
    'LAB.CUSTOM.appearanceturbidepitheli', -- Appearance Turbid Epithelial
    'LAB.CUSTOM.urineanalysis-2',       -- Urine Analysis      ← سربرگِ پانل
    'LAB.CUSTOM.urineculturesensitivity',-- Urine ( Culture & Sensitivity ) ← سربرگ
    'LAB.CUSTOM.mich',                  -- MICH ← تست نیست (MCH واقعی جدا دارد)
    'LAB.CUSTOM.mich-2',                -- MICH
    'LAB.CUSTOM.sg0tast'                -- S.G.0.T. (AST ) ← تکراریِ بی‌کیفیتِ AST
);
