using ReSiRai.Api.Services;
using Xunit;

namespace ReSiRai.Tests;

/// <summary>
/// نگهبانِ ستونِ «تحلیل»: حکمِ هر ردیف باید از دادهٔ واقعی بیاید — تناقض از
/// موتورِ ناسازگاری، بازه از برگه — و برایِ مقادیرِ کیفی چیزی ساخته نشود.
/// </summary>
public class LabRowAnalyzerTests
{
    private static readonly LabRowAnalyzer.RowIdentity Phosphorus =
        new("Phosphorus", "فسفر", "P", "LAB.P");
    private static readonly LabRowAnalyzer.RowIdentity Cholesterol =
        new("Total cholesterol", "کلسترول تام", "CHOL", "LAB.CHOL");

    [Fact]
    public void ContradictionShowsMessageAndRetestWithReason()
    {
        var findings = new List<ConsistencyFinding>
        {
            new("p-vs-ca", "error", new[] { "Phosphorus" },
                "فسفر با کلسیم نمی‌خواند", "P vs Ca mismatch", "تکرارِ فسفر — دلیل: ناسازگاری با کلسیم")
        };

        var (text, level) = LabRowAnalyzer.Analyze(Phosphorus, "2.8", 2.5m, 4.5m, findings);

        Assert.Equal("error", level);
        Assert.Contains("فسفر با کلسیم نمی‌خواند", text);
        // توصیه به تکرار هرگز بدونِ دلیل نمی‌آید.
        Assert.Contains("تکرارِ فسفر — دلیل: ناسازگاری با کلسیم", text);
    }

    [Fact]
    public void FindingAboutAnotherTestDoesNotTouchThisRow()
    {
        var findings = new List<ConsistencyFinding>
        {
            new("hdl>chol", "error", new[] { "HDL", "Total cholesterol" },
                "کلسترول خوب بیشتر از کل کلسترول است", "HDL > total", null)
        };

        // ردیفِ فسفر نباید حکمِ کلسترول را بگیرد (بازه‌ای هم ندارد تا چیزی بگوید).
        var (text, _) = LabRowAnalyzer.Analyze(Phosphorus, "2.8", null, null, findings);
        Assert.Equal("", text);

        // اما ردیفِ کلسترول — با نامِ انگلیسیِ همان فاکتور — باید بگیرد.
        var (cholText, cholLevel) = LabRowAnalyzer.Analyze(Cholesterol, "146", 0m, 200m, findings);
        Assert.Equal("error", cholLevel);
        Assert.Contains("کلسترول", cholText);
    }

    [Fact]
    public void OutOfRangeNeedsFollowUpInBothDirections()
    {
        var none = new List<ConsistencyFinding>();

        var (lowText, lowLevel) = LabRowAnalyzer.Analyze(Phosphorus, "2.8", 3.0m, 4.5m, none);
        Assert.Equal("warn", lowLevel);
        Assert.Contains("پایین‌تر از بازه", lowText);
        Assert.Contains("نیاز به پیگیری", lowText);

        var (highText, highLevel) = LabRowAnalyzer.Analyze(Phosphorus, "5.2", 3.0m, 4.5m, none);
        Assert.Equal("warn", highLevel);
        Assert.Contains("بالاتر از بازه", highText);
    }

    [Fact]
    public void InRangeIsSaidPlainAndKeepsItsLevel()
    {
        var (text, level) = LabRowAnalyzer.Analyze(Phosphorus, "3.2", 3.0m, 4.5m, new List<ConsistencyFinding>());
        Assert.Equal("در محدوده", text);
        Assert.Equal("ok", level);
    }

    [Fact]
    public void QualitativeOrEmptyValuesGetNoVerdict()
    {
        var none = new List<ConsistencyFinding>();

        // نتیجهٔ کیفی (رنگ ادرار) و مقدارِ خالی: چیزی ساخته نمی‌شود.
        Assert.Equal(("", ""), LabRowAnalyzer.Analyze(Phosphorus, "Yellow", null, null, none));
        Assert.Equal(("", ""), LabRowAnalyzer.Analyze(Phosphorus, "", 2.5m, 4.5m, none));
        // عدد بدونِ بازه هم حکم ندارد.
        Assert.Equal(("", ""), LabRowAnalyzer.Analyze(Phosphorus, "2.8", null, null, none));
    }

    [Fact]
    public void RangeValuesPrintedAsIntervalAreNotJudgedAsNumbers()
    {
        // «10-12» نتیجهٔ ادراری است نه عددِ تکی؛ نباید با بازه مقایسه شود.
        var (text, _) = LabRowAnalyzer.Analyze(Phosphorus, "10-12", 0m, 3m, new List<ConsistencyFinding>());
        Assert.Equal("", text);
    }

    // ---- مواردِ واقعیِ برگهٔ نمونه ------------------------------------------

    [Fact]
    public void TheRealRdwSdCase_FlagsAboveThePrintedRange()
    {
        // RDW-SD: مقدارِ ۵۱٫۱ با بازهٔ چاپیِ ۳۹–۴۷ (دقیقاً ردیفِ گزارش‌شده).
        var rdwSd = new LabRowAnalyzer.RowIdentity("RDW-SD", "انحراف استاندارد عرض گلبول قرمز", "RDW-SD", "LAB.RDWSD");
        var (text, level) = LabRowAnalyzer.Analyze(rdwSd, "51.1", 39m, 47m, new List<ConsistencyFinding>(), "51.1");

        Assert.Equal("warn", level);
        Assert.Contains("بالاتر از بازه", text);
        Assert.Contains("نیاز به پیگیری", text);
    }

    [Fact]
    public void PercentValuesAreStillJudgedAgainstTheRange()
    {
        var rdwcv = new LabRowAnalyzer.RowIdentity("RDW-CV", "ضریب تغییرات عرض گلبول قرمز", "RDWCV", "LAB.RDWCV");
        var none = new List<ConsistencyFinding>();

        // اگر ممیزِ اعشار برگشته بود (۱۴٫۵٪) در بازهٔ ۱۱–۱۵ است.
        Assert.Equal(("در محدوده", "ok"), LabRowAnalyzer.Analyze(rdwcv, "14.5%", 11m, 15m, none, "14.5%"));
        // و اگر نگشته بود (۱۴۵٪) باید پرچم بخورد، نه اینکه بی‌صدا رد شود.
        var (text, level) = LabRowAnalyzer.Analyze(rdwcv, "145%", 11m, 15m, none, "145%");
        Assert.Equal("warn", level);
        Assert.Contains("بالاتر از بازه", text);
    }

    [Fact]
    public void CorrectedValueWithHealthyPrintedRangeGetsNoFalseFlag()
    {
        // موردِ واقعیِ برگه (گزارش‌شده توسطِ پزشک): مقدارِ چاپی «145%» بوده،
        // قاعدهٔ بازه درستش کرده «14.5%»، و بازهٔ چاپیِ ۱۱–۱۵ سالم است.
        // جابه‌جاییِ بازه در این حالت پرچمِ دروغینِ «بالاتر از بازه» می‌ساخت.
        var rdwcv = new LabRowAnalyzer.RowIdentity("RDW-CV", "ضریب تغییرات عرض گلبول قرمز", "RDWCV", "LAB.RDWCV");
        var (text, level) = LabRowAnalyzer.Analyze(rdwcv, "14.5%", 11m, 15m, new List<ConsistencyFinding>(), "145%");

        Assert.Equal("ok", level);
        Assert.Equal("در محدوده", text);
    }

    [Fact]
    public void ScaleCorrectedValueIsJudgedAgainstTheSameShiftedRange()
    {
        // MCHC: «329» با قاعدهٔ بازه «32.9» شده؛ بازهٔ چاپیِ ۳۱۳–۳۵۷ هم باید
        // همان‌قدر جابه‌جا شود تا مقایسه با برگه عادلانه بماند.
        var mchc = new LabRowAnalyzer.RowIdentity("MCHC", "میانگین غلظت هموگلوبین سلولی", "MCHC", "LAB.MCHC");
        Assert.Equal(("در محدوده", "ok"), LabRowAnalyzer.Analyze(mchc, "32.9", 313m, 357m, new List<ConsistencyFinding>(), "329"));

        // و اگر واقعاً بیرون از بازه باشد، با همان بازهٔ جابه‌جاشده پرچم می‌خورد.
        var (text, _) = LabRowAnalyzer.Analyze(mchc, "42.0", 313m, 357m, new List<ConsistencyFinding>(), "420");
        Assert.Contains("بالاتر از بازه", text);
    }

    [Fact]
    public void ConsistencyEngineNeverThrowsOnRealSheetRows()
    {
        // شکستِ موتورِ ناسازگاری نباید حکمِ بازه را از بین ببرد؛ پس خودِ موتور
        // هم رویِ ردیف‌هایِ واقعی نباید استثنایی بدهد.
        var obs = new List<LabObs>
        {
            new("LAB.RDWSD", "RDW-SD", "Red cell distribution width - SD", 51.1m, "fL", 39m, 47m),
            new("LAB.RDWCV", "RDWCV", "Red cell distribution width - CV", 14.5m, "%", 11m, 15m),
            new("LAB.WBC", "WBC", "White blood cell", 6.13m, "10^3/uL", 4m, 10m),
            new("LAB.CHOL", "CHOL", "Total cholesterol", 146m, "mg/dL", 0m, 200m),
            new("LAB.HDL", "HDL", "HDL", 30m, "mg/dL", 40m, 60m)
        };
        var findings = LabConsistencyChecker.Check(obs, null, null);
        Assert.NotNull(findings);
    }
}
