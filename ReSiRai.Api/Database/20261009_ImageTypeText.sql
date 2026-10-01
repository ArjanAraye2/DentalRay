-- ReSiRai: نوعِ تصویر را خودِ رسیرا تشخیص می‌دهد — نامِ متنیِ آزاد، بدونِ
-- نیاز به جدولِ انواع. تصمیمِ ۱۴۰۵/۰۷/۱۰: نمایشِ تصاویر به تفکیکِ نوعِ
-- تشخیص‌داده‌شده (سی‌تی‌اسکن، سونوگرافی، PET، برگهٔ آزمایش، …).
--
-- ستونِ متنی اضافه می‌شود و از نوعِ ثبت‌شدهٔ قبلی (جدولِ tblImageTypes)
-- پُرمی‌شود تا داده‌های موجود بی‌نوع نمانند. جدولِ قبلی حذف نمی‌شود؛ فقط
-- دیگر مرجعِ تشخیص نیست.

IF COL_LENGTH(N'dbo.tblRadiologyImages', N'ImageTypeText') IS NULL
    ALTER TABLE dbo.tblRadiologyImages ADD ImageTypeText NVARCHAR(100) NULL;
GO

UPDATE i
SET i.ImageTypeText = t.ImageTypeName
FROM dbo.tblRadiologyImages i
JOIN dbo.tblImageTypes t ON t.ImageTypeID = i.ImageTypeID
WHERE i.ImageTypeText IS NULL;
GO
