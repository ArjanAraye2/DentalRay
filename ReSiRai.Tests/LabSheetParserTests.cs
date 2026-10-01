using ReSiRai.Api.Services;
using Xunit;

namespace ReSiRai.Tests;

/// <summary>
/// Golden tests for the lab-sheet parser. Every case is a synthetic TSV (no
/// patient data) that reproduces one real failure mode seen on printed sheets:
/// skewed rows, garbled OCR tokens, lost decimal points, two-panel pages and
/// multi-line reference texts. If one of these breaks, extraction quality has
/// regressed - stop and fix before shipping.
/// </summary>
public class LabSheetParserTests
{
    // Tesseract TSV line for one word: level, page, block, par, line, word,
    // left, top, width, height, conf, text.
    private static string W(int line, int left, int top, int width, double conf, string text)
        => $"5\t1\t1\t1\t{line}\t1\t{left}\t{top}\t{width}\t20\t{conf.ToString(System.Globalization.CultureInfo.InvariantCulture)}\t{text}";

    private static List<LabSheetParser.Row> Parse(params string[] words)
        => LabSheetParser.ParseTsv(string.Join("\n", words));

    [Fact]
    public void ValueGluedToTheName_IsSplitOff()
    {
        // «POTASSIUM 8.2» در عکسِ نمایشگرِ آزمایشگاه: عددِ آخرِ نام، مقدارِ
        // همان ردیف است. «COVID-19» که شماره‌اش به نام چسبیده هرگز شکسته نمی‌شود.
        var rows = Parse(
            W(1, 200, 100, 180, 95, "POTASSIUM"),
            W(1, 390, 102, 60, 95, "8.2"),
            W(2, 200, 170, 140, 95, "COVID-19"),
            W(2, 560, 172, 110, 95, "IU/mL"));

        Assert.Equal(2, rows.Count);
        Assert.Equal("POTASSIUM", rows[0].Name);
        Assert.Equal("8.2", rows[0].Value);
        Assert.Equal("COVID-19", rows[1].Name);
        Assert.Equal("", rows[1].Value);
    }

    [Fact]
    public void SectionTitles_MarkRowsBelowAndAreNotRowsThemselves()
    {
        // «Urine Analysis» سطرِ عنوانِ بخش است، نه تست: کنار گذاشته می‌شود و
        // نامش کنارِ ردیف‌های بعدی می‌ماند تا نتایج به تفکیکِ پانل دیده شوند.
        var rows = Parse(
            W(1, 200, 100, 120, 95, "Urine"),
            W(1, 330, 100, 150, 95, "Analysis"),
            W(2, 200, 165, 120, 95, "Color"),
            W(2, 600, 168, 110, 95, "Yellow"),
            W(3, 200, 230, 120, 95, "WBC"),
            W(3, 600, 233, 110, 95, "6.13"));

        Assert.Equal(2, rows.Count); // خودِ عنوان، ردیف نیست
        Assert.Equal("Color", rows[0].Name);
        Assert.Equal("Urine Analysis", rows[0].Section);
        Assert.Equal("WBC", rows[1].Name);
        Assert.Equal("Urine Analysis", rows[1].Section);
    }

    [Fact]
    public void AValuelessTestRowIsNeverTreatedAsASectionTitle()
    {
        // «25-OH-Vitamin D» با مقدارِ گم‌شده تست است نه عنوانِ بخش (ارقام دارد)؛
        // نه حذف می‌شود و نه عنوان می‌شود.
        var rows = Parse(
            W(1, 200, 100, 200, 95, "25-OH-Vitamin"),
            W(1, 420, 100, 80, 95, "D"),
            W(1, 700, 105, 120, 95, "ng/ml"),
            W(2, 200, 165, 120, 95, "Color"),
            W(2, 600, 168, 110, 95, "Yellow"));

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Name.Contains("Vitamin"));
        Assert.Contains(rows, r => r.Section == "");
    }

    [Fact]
    public void DenseRow_KeepsNameValueUnitRefInTheirColumns()
    {
        // "Alkaline Phosphatase 220 U/L 80 - 306" with the scan's shear: each
        // column has its own vertical offset, rows are ~65px apart.
        var rows = Parse(
            W(1, 170, 100, 150, 95, "Alkaline"),
            W(1, 300, 105, 240, 95, "Phosphatase"),
            W(1, 1020, 80, 60, 92, "220"),
            W(1, 1420, 85, 55, 95, "U/L"),
            W(1, 1790, 90, 45, 90, "80"),
            W(1, 1870, 92, 12, 90, "-"),
            W(1, 1900, 95, 55, 90, "306"));

        var row = Assert.Single(rows);
        Assert.Equal("Alkaline Phosphatase", row.Name);
        Assert.Equal("220", row.Value);
        Assert.Equal("U/L", row.Unit);
        Assert.Equal("80 - 306", row.RefText);
        Assert.Equal(80m, row.RefLow);
        Assert.Equal(306m, row.RefHigh);
    }

    [Fact]
    public void GarbledToken_IsNeverTurnedIntoAValue()
    {
        // The real failure: OCR read the printed 2.5 as "4a}" with low
        // confidence. A missing number is safe; an invented one is not.
        var rows = Parse(
            W(1, 180, 200, 160, 95, "Globulin"),
            W(1, 1025, 190, 60, 20, "4a}"),
            W(1, 1420, 185, 60, 95, "g/dL"));

        var row = Assert.Single(rows);
        Assert.Equal("Globulin", row.Name);
        Assert.Equal("", row.Value);
        Assert.Equal("g/dL", row.Unit);
    }

    [Fact]
    public void AlmostNumber_WithGarbagePrefix_IsRecovered()
    {
        // Colored print lowers confidence: "51.1" came back as "`51.1" (conf 28).
        var rows = Parse(
            W(1, 160, 300, 150, 88, "RDW-SD"),
            W(1, 535, 305, 70, 28, "`51.1"),
            W(1, 1018, 300, 80, 95, "39-47"));

        var row = Assert.Single(rows);
        Assert.Equal("51.1", row.Value);
    }

    [Fact]
    public void TwoPanelLine_SplitsIntoTwoRows()
    {
        // Urine sheets print Macroscopic | Microscopic side by side on one line.
        var rows = Parse(
            W(1, 150, 500, 80, 95, "Color"),
            W(1, 700, 500, 100, 95, "Yellow"),
            W(1, 1700, 500, 60, 95, "RBC."),
            W(1, 2300, 500, 90, 95, "10-12"));

        Assert.Equal(2, rows.Count);
        Assert.Equal("Color", rows[0].Name);
        Assert.Equal("Yellow", rows[0].Value);
        Assert.Equal("RBC.", rows[1].Name);
        // The printed count result lives in the "range" position on urine sheets.
        Assert.Equal("10-12", rows[1].Value);
    }

    [Fact]
    public void ContinuationLine_JoinsTheReferenceAbove()
    {
        var rows = Parse(
            W(1, 150, 100, 60, 95, "P"),
            W(1, 300, 100, 140, 95, "(Phosphorus"),
            W(1, 450, 100, 25, 95, ")"),
            W(1, 1020, 100, 60, 95, "2.8"),
            W(1, 1420, 100, 60, 95, "mg/dL"),
            W(1, 1790, 100, 90, 95, "Children:"),
            W(1, 1900, 100, 60, 95, "4"),
            W(1, 1970, 100, 12, 95, "-"),
            W(1, 1990, 100, 30, 95, "7"),
            W(2, 150, 180, 80, 95, "Adults"),
            W(2, 240, 180, 15, 95, ":"),
            W(2, 1790, 180, 110, 95, "2.6-4.5"));

        var row = Assert.Single(rows);
        Assert.Contains("2.6-4.5", row.RefText);
    }

    [Fact]
    public void MergedValueUnitCell_SplitsIntoValueAndUnit()
    {
        // Dense CBC rows print value+unit close together ("14.1 g/dL").
        var rows = Parse(
            W(1, 150, 100, 70, 95, "HGB"),
            W(1, 1020, 100, 60, 95, "14.1"),
            W(1, 1090, 100, 60, 95, "g/dL"));

        var row = Assert.Single(rows);
        Assert.Equal("14.1", row.Value);
        Assert.Equal("g/dL", row.Unit);
    }

    [Fact]
    public void LowConfidenceGarbage_DoesNotPolluteTheRow()
    {
        var rows = Parse(
            W(1, 170, 400, 170, 95, "HDL"),
            W(1, 360, 400, 150, 95, "Cholesterol"),
            W(1, 1010, 405, 40, 12, "Ry"),
            W(1, 1420, 400, 60, 95, "mg/dL"));

        var row = Assert.Single(rows);
        Assert.Equal("", row.Value);
        Assert.Equal("mg/dL", row.Unit);
    }

    // ---------------- FitScale: the lost decimal point ----------------

    [Theory]
    [InlineData("145%", 11, 15, "14.5%")]        // percent suffix must not block it
    [InlineData("329", 32, 36, "32.9")]          // MCHC printed 32.9
    [InlineData("1016", 1.005, 1.030, "1.016")]  // Specific Gravity needs /1000
    public void LostDecimalPoint_IsRestoredFromTheRange(string printed, decimal lo, decimal hi, string expected)
        => Assert.Equal(expected, LabSheetParser.FitScale(printed, lo, hi));

    [Theory]
    [InlineData("45", 4, 11)]    // a genuinely high WBC must never be "corrected"
    [InlineData("88", 70, 100)]
    public void InRangeValues_AreNeverTouched(string printed, decimal lo, decimal hi)
        => Assert.Equal(printed, LabSheetParser.FitScale(printed, lo, hi));

    [Fact]
    public void BoundOnlyRange_DoesNotScaleARealValue()
        => Assert.Equal("22", LabSheetParser.FitScale("22", null, 10m)); // بدونِ بازهٔ چاپ‌شده تکان نمی‌خورد

    // ---------------- rule 4: decimal-printed one-sided range ----------------

    [Fact]
    public void DecimalPrintedBound_ScalesOneStep()
        // «up to 10.0» و «22»: عددِ خارج از بازه، با بازهٔ اعشاریِ چاپ‌شده، فقط
        // یک گامِ ÷۱۰ و فقط اگر دقیقاً داخلِ بازه بنشیند (مبنایِ دستی: 2.2).
        => Assert.Equal("2.2", LabSheetParser.FitScale("22", null, 10m, "up to 10.0"));

    [Theory]
    [InlineData("45", 4, 11, "4-11")]              // WBC 45 واقعی است
    [InlineData("164", 70, 110, "70-100 Normal")]  // گلوکز 164 واقعی است
    public void IntegerPrintedRange_NeverScales(string printed, decimal lo, decimal hi, string refText)
        => Assert.Equal(printed, LabSheetParser.FitScale(printed, lo, hi, refText));

    [Fact]
    public void UpToBound_IsParsedAsOneSidedRange()
    {
        var (lo, hi) = LabSheetParser.ParseRef("up to 10.0");
        Assert.Null(lo);
        Assert.Equal(10m, hi);
    }

    // ---------------- golden TSV rows: real failure modes ----------------

    [Fact]
    public void TableBorderPipes_DoNotKillTheRow()
    {
        // «| PLT | 169 | 10%L | 150-450 |» — خطوطِ جدول به متن می‌چسبیدند و
        // نامِ «| PLT» ردیف را می‌انداخت.
        var rows = Parse(
            W(1, 88, 100, 12, 90, "|"),
            W(1, 133, 100, 80, 93, "PLT"),
            W(1, 552, 102, 60, 54, "169"),
            W(1, 712, 102, 80, 62, "10%L"),
            W(1, 987, 102, 110, 38, "150-450"));
        Assert.Single(rows);
        Assert.Equal("PLT", rows[0].Name);
        Assert.Equal("169", rows[0].Value);
        Assert.Equal(150m, rows[0].RefLow);
    }

    [Fact]
    public void LowConfidenceNumber_IsKeptAndScaled()
    {
        // عددِ ستونِ مقدار با اعتمادِ ≈۴ خوانده شده بود («118» ← 1.18) و با
        // آستانهٔ اطمینان می‌افتاد؛ عددِ گم‌شده بدتر از عددِ پرچم‌دار است.
        var rows = Parse(
            W(1, 145, 100, 258, 96, "Creatinine"),
            W(1, 987, 92, 101, 3.9, "118"),
            W(1, 1379, 86, 148, 88, "mg/dL"),
            W(1, 1757, 83, 72, 0, "0.5"),
            W(1, 1839, 82, 104, 0, "-1.4"));
        Assert.Single(rows);
        Assert.Equal("118", rows[0].Value);
        Assert.Equal(0.5m, rows[0].RefLow);
        Assert.Equal(1.4m, rows[0].RefHigh);
        Assert.Equal("1.18", LabSheetParser.FitScale(rows[0].Value, rows[0].RefLow, rows[0].RefHigh, rows[0].RefText));
    }

    [Fact]
    public void SlashInTestName_IsNotAUnit()
    {
        // «Blood/Hgb» نامِ آزمایش است؛ هر چیزِ دارایِ «/» واحد نیست.
        var rows = Parse(
            W(1, 172, 100, 200, 68, "Blood/Hgb"),
            W(1, 746, 100, 160, 62, "Positive(I+)"));
        Assert.Single(rows);
        Assert.Equal("Blood/Hgb", rows[0].Name);
        Assert.Equal("Positive(I+)", rows[0].Value);
    }

    [Fact]
    public void ColumnHeaderRow_DoesNotSwallowTheRowBesideIt()
    {
        // «Urine Analysis | W.B.C. | Many» — عنوانِ ستونِ چپ نباید ردیفِ کنارش
        // را ببلعد (قبلاً «W.B.C. Many» به‌عنوانِ عنوانِ بخش می‌رفت).
        var rows = Parse(
            W(1, 144, 100, 216, 95, "Urine"),
            W(1, 281, 100, 100, 95, "Analysis"),
            W(1, 1543, 100, 110, 40, "W.B.C."),
            W(1, 2167, 100, 90, 96, "Many"));
        var wb = rows.SingleOrDefault(r => r.Name == "W.B.C.");
        Assert.NotNull(wb);
        Assert.Equal("Many", wb!.Value);
    }

    [Fact]
    public void PercentGluedToValue_MovesToTheUnit()
    {
        // «145%» ← مقدار 145 و واحدِ «%».
        var rows = Parse(
            W(1, 159, 100, 120, 84, "RDWCV"),
            W(1, 536, 100, 90, 40, "145%"),
            W(1, 1019, 100, 90, 78, "11-15"));
        Assert.Single(rows);
        Assert.Equal("145", rows[0].Value);
        Assert.Equal("%", rows[0].Unit);
    }

    [Fact]
    public void DottedAbbreviation_IsNotEatenByNumberRecovery()
    {
        // «S.G.0.T.» با اعتمادِ ۳۴: صفر/او را OCR در هم می‌ریزد؛ سرواژه نباید به
        // عددِ زباله تبدیل شود («25-OH-Vitamin» هم از همین محافظ می‌آید).
        var rows = Parse(
            W(1, 161, 100, 200, 34, "S.G.0.T."),
            W(1, 391, 100, 120, 92, "(AST"),
            W(1, 543, 100, 20, 89, ")"),
            W(1, 1013, 100, 40, 93, "33"),
            W(1, 1412, 100, 60, 96, "U/L"),
            W(1, 1784, 100, 50, 90, "<40"));
        Assert.Single(rows);
        Assert.Equal("33", rows[0].Value);
        Assert.Contains("S.G.0.T.", rows[0].Name);
    }

    [Fact]
    public void TsvToText_FallbackNeedsNoSecondOcrPass()
    {
        string text = LabSheetParser.TsvToText(W(1, 100, 50, 60, 95, "Ketone") + "\n" + W(1, 700, 55, 90, 95, "Negative"));
        Assert.Contains("Ketone", text);
        Assert.Contains("Negative", text);
    }

    // ---------------- Confidence: review the weak rows first ----------------

    [Fact]
    public void CleanRow_CarriesHighConfidence()
    {
        var rows = Parse(
            W(1, 170, 100, 150, 96, "Alkaline"),
            W(1, 300, 105, 240, 95, "Phosphatase"),
            W(1, 1020, 80, 60, 94, "220"),
            W(1, 1420, 85, 55, 95, "U/L"),
            W(1, 1790, 90, 45, 93, "80"),
            W(1, 1870, 92, 12, 93, "-"),
            W(1, 1900, 95, 55, 93, "306"));

        var row = Assert.Single(rows);
        Assert.True(row.Confidence >= 85, $"confidence was {row.Confidence}");
    }

    [Fact]
    public void RecoveredLowConfidenceValue_IsMarkedForReview()
    {
        var rows = Parse(
            W(1, 160, 300, 150, 88, "RDW-SD"),
            W(1, 535, 305, 70, 28, "`51.1"),
            W(1, 1018, 300, 80, 95, "39-47"));

        var row = Assert.Single(rows);
        Assert.True(row.Confidence <= 65, $"confidence was {row.Confidence}");
        Assert.NotEqual("", row.ConfidenceNote);
        Assert.Contains("OCR", row.ConfidenceNote);
    }

    [Fact]
    public void MissingValue_LandsAtTheBottomOfConfidence()
    {
        var rows = Parse(
            W(1, 180, 200, 160, 95, "Globulin"),
            W(1, 1025, 190, 60, 20, "4a}"),
            W(1, 1420, 185, 60, 95, "g/dL"));

        var row = Assert.Single(rows);
        Assert.True(row.Confidence <= 35, $"confidence was {row.Confidence}");
    }
}
