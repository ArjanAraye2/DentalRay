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
        => Assert.Equal("22", LabSheetParser.FitScale("22", null, 10m)); // CRP 22 is real, not 2.2

    [Fact]
    public void TsvToText_FallbackNeedsNoSecondOcrPass()
    {
        string text = LabSheetParser.TsvToText(W(1, 100, 50, 60, 95, "Ketone") + "\n" + W(1, 700, 55, 90, 95, "Negative"));
        Assert.Contains("Ketone", text);
        Assert.Contains("Negative", text);
    }
}
