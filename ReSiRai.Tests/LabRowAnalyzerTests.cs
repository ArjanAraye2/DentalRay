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
}
