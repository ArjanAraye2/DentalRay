using System.Globalization;

namespace ReSiRai.Api.Services;

/// <summary>
/// حکمِ هر ردیفِ استخراج: تناقض‌هایش با مقادیرِ دیگرِ برگه (از موتورِ ناسازگاری)
/// و سپس وضعیتش نسبت به بازه. خروجی در ستونِ «تحلیل» کنارِ همان عدد می‌آید تا
/// پزشک برای هر تصمیم، دلیلش را همان‌جا ببیند. هیچ عددی ساخته نمی‌شود؛ فقط وضعیت.
/// </summary>
public static class LabRowAnalyzer
{
    /// <summary>شناسهٔ ردیف برایِ تطبیق با نامِ فاکتورهایِ موتورِ ناسازگاری.</summary>
    public sealed record RowIdentity(string? NameEn, string? NameFa, string? ShortCode, string? FactorCode);

    /// <summary>
    /// متنِ تحلیل و سطحش (error/warn/ok یا خالی). تناقض بر بازه مقدم است؛
    /// برایِ مقادیرِ کیفی یا بدونِ بازه چیزی ساخته نمی‌شود. بازه، همان چیزی است
    /// که روی برگه چاپ شده؛ اگر قاعدهٔ بازه ممیزِ مقدار را جابه‌جا کرده باشد،
    /// بازه هم همان‌قدر جابه‌جا می‌شود تا مقایسه با برگه عادلانه بماند.
    /// </summary>
    public static (string Text, string Level) Analyze(RowIdentity id, string value,
        decimal? low, decimal? high, IReadOnlyList<ConsistencyFinding> findings,
        string? printedValue = null)
    {
        var hits = findings.Where(f => FindingHits(f, id)).ToList();
        if (hits.Count > 0)
        {
            string text = string.Join("؛ ", hits.Select(f => f.MessageFa).Distinct());
            string level = hits.Any(f => f.Severity == "error") ? "error" : "warn";
            string retest = string.Join("؛ ", hits.Select(f => f.RetestFa)
                .Where(x => !string.IsNullOrEmpty(x)).Distinct()!);
            if (retest.Length > 0) text += " — توصیه به تکرار: " + retest;
            return (text, level);
        }
        if (TryNumber(value, out var num))
        {
            // «۱۴٫۵٪» و «۳۲۹ → ۳۲٫۹» نباید از قضاوتِ بازه جا بمانند.
            if (printedValue is not null && TryNumber(printedValue, out var printed)
                && printed != 0 && num != printed)
            {
                decimal factor = num / printed;
                if (factor is 0.01m or 0.1m or 10m or 100m)
                {
                    low = low is null ? null : low * factor;
                    high = high is null ? null : high * factor;
                }
            }
            if (low is not null && num < low) return ("پایین‌تر از بازه — نیاز به پیگیری", "warn");
            if (high is not null && num > high) return ("بالاتر از بازه — نیاز به پیگیری", "warn");
            if (low is not null || high is not null) return ("در محدوده", "ok");
        }
        return ("", "");
    }

    /// <summary>
    /// عددِ چاپی: ارقامِ فارسی/عربی، «٪» و فاصله‌ها تحمل می‌شوند؛ «10-12» یا
    /// «Negative» عدد نیستند و هرگز قضاوتِ بازه نمی‌گیرند.
    /// </summary>
    private static bool TryNumber(string value, out decimal num)
    {
        var s = value.Trim().TrimEnd('%').Trim();
        s = new string(s.Select(c =>
            c is >= '۰' and <= '۹' ? (char)('0' + (c - '۰'))
            : c is >= '٠' and <= '٩' ? (char)('0' + (c - '٠'))
            : c).ToArray());
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out num);
    }

    /// <summary>
    /// آیا این یافته به همین ردیف مربوط است؟ نامِ فاکتورها در موتورِ ناسازگاری
    /// انگلیسی است و اینجا نامِ چاپی/فارسی/کوتاه — با نرمال‌سازی یکی می‌شوند.
    /// </summary>
    public static bool FindingHits(ConsistencyFinding finding, RowIdentity id)
    {
        var cands = new List<string?> { id.NameEn, id.NameFa, id.ShortCode, id.FactorCode?.Split('.').Last() };
        foreach (var key in finding.Factors)
        {
            var k = Norm(key);
            if (k.Length < 2) continue;
            foreach (var cand in cands)
            {
                var c = Norm(cand ?? "");
                if (c.Length == 0) continue;
                if (c == k || (k.Length >= 3 && (c.Contains(k) || k.Contains(c)))) return true;
            }
        }
        return false;
    }

    private static string Norm(string s) =>
        new(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
