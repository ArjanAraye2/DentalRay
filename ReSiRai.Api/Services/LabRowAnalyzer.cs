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
    /// برایِ مقادیرِ کیفی یا بدونِ بازه چیزی ساخته نمی‌شود.
    /// </summary>
    public static (string Text, string Level) Analyze(RowIdentity id, string value,
        decimal? low, decimal? high, IReadOnlyList<ConsistencyFinding> findings)
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
        if (value.Length > 0 &&
            decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
        {
            if (low is not null && num < low) return ("پایین‌تر از بازه — نیاز به پیگیری", "warn");
            if (high is not null && num > high) return ("بالاتر از بازه — نیاز به پیگیری", "warn");
            if (low is not null || high is not null) return ("در محدوده", "ok");
        }
        return ("", "");
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
