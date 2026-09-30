using System.Text.RegularExpressions;

namespace ReSiRai.Api.Services;

/// <summary>
/// A printed lab sheet is a table: test name, result, unit, reference range.
/// Reading it needs no AI model - a deterministic parse of the OCR text is
/// exact and, which matters most in a patient record, can never invent a row.
/// The AI path remains only as a fallback for sheets this parser cannot read.
/// </summary>
public static class LabSheetParser
{
    public sealed record Row(string Name, string Value, string Unit, string RefText,
        decimal? RefLow, decimal? RefHigh);

    // "name value unit ref..." - the shape of a printed result row.
    private static readonly Regex RowRegex = new(
        @"^(?<name>[A-Za-z][A-Za-z0-9()\./\s\-\+,%]*?)\s+(?<value>\d+(?:[.,]\d+)?)\s*(?<unit>[A-Za-z%/μu]{2,}(?:/[A-Za-z%]{1,8})?|%)?\s*(?<ref>.*)$",
        RegexOptions.Compiled);

    // A real row whose result OCR lost ("Triglycerides Mh mg/dL ...") is still
    // shown to the doctor - with an empty value instead of a guessed one.
    private static readonly Regex RowWithValueLostRegex = new(
        @"^(?<name>[A-Za-z][A-Za-z0-9()\./\s\-\+,%]*?)\s+(?<rest>.*\b(?:mg/dL|U/L|g/dL|mEq/L|Ratio|mmol/L|mIU/ml|μIU/ml)\b.*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex RangeRegex = new(
        @"(?<lo>\d+(?:[.,]\d+)?)\s*(?:-|–|to)\s*(?<hi>\d+(?:[.,]\d+)?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BoundRegex = new(@"[<>]=?\s*(?<n>\d+(?:[.,]\d+)?)", RegexOptions.Compiled);

    private static readonly Regex NumberRegex = new(@"\d+(?:[.,]\d+)?", RegexOptions.Compiled);

    private static readonly string[] Headers =
        { "Test", "Result", "Unit", "Refrence", "Reference", "Blood", "Normal ranges", "Page" };

    private static readonly string[] KnownUnits =
        { "mg/dL", "U/L", "g/dL", "mEq/L", "mmol/L", "mIU/ml", "μIU/ml", "ng/mL", "pg/mL", "IU/mL", "Ratio", "%" };

    public static List<Row> Parse(string? ocrText)
    {
        var rows = new List<Row>();
        foreach (var rawLine in (ocrText ?? string.Empty).Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length < 3) continue;
            if (Headers.Any(h => line.StartsWith(h, StringComparison.OrdinalIgnoreCase))) continue;

            var m = RowRegex.Match(line);
            if (m.Success)
            {
                string name = m.Groups["name"].Value.Trim();
                if (Letters(name) < 3) continue;
                string unit = m.Groups["unit"].Value.Trim();
                string refText = m.Groups["ref"].Value.Trim();
                bool unitOk = unit.Length >= 2;
                if (!unitOk && !NumberRegex.IsMatch(refText)) continue;
                if (unit.Length == 1) { refText = (unit + " " + refText).Trim(); unit = string.Empty; }
                var (lo, hi) = ParseRef(refText);
                rows.Add(new Row(name, m.Groups["value"].Value.Trim(), unit, Clamp(refText, 500), lo, hi));
                continue;
            }

            var lost = RowWithValueLostRegex.Match(line);
            if (lost.Success && Letters(lost.Groups["name"].Value) >= 3)
            {
                string rest = lost.Groups["rest"].Value.Trim();
                var (lo, hi) = ParseRef(rest);
                rows.Add(new Row(lost.Groups["name"].Value.Trim(), string.Empty, GuessUnit(rest), Clamp(rest, 500), lo, hi));
                continue;
            }

            // Continuation lines describe the reference of the row above
            // ("100-126 Impaired fasting glucose" under the glucose row).
            if (rows.Count > 0 && NumberRegex.IsMatch(line))
            {
                var prev = rows[^1];
                string joined = (prev.RefText + " / " + line).Trim(' ', '/');
                if (joined.Length <= 500) rows[^1] = prev with { RefText = joined };
            }
        }
        return rows;
    }

    /// <summary>
    /// Printed rows lose decimal points in OCR ("118" for 1.18). The reference
    /// range restores the scale: the true value sits near it. Digits are never
    /// invented - at most the decimal point moves, and only when the range
    /// demands it.
    /// </summary>
    public static string FitScale(string value, decimal? lo, decimal? hi)
    {
        if (value.Contains('.') || value.Contains(',')) return value;
        if (!decimal.TryParse(value, out decimal v)) return value;
        if (lo is null && hi is null) return value;
        foreach (decimal d in new[] { 1m, 10m, 100m })
        {
            decimal c = v / d;
            bool inside = (lo is null || c >= lo.Value * 0.5m) && (hi is null || c <= hi.Value * 1.6m);
            if (inside) return c.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        return value;
    }

    private static (decimal?, decimal?) ParseRef(string refText)
    {
        var range = RangeRegex.Match(refText);
        if (range.Success &&
            decimal.TryParse(range.Groups["lo"].Value.Replace(',', '.'), out decimal lo) &&
            decimal.TryParse(range.Groups["hi"].Value.Replace(',', '.'), out decimal hi))
            return (lo, hi);
        var bound = BoundRegex.Match(refText);
        if (bound.Success && decimal.TryParse(bound.Groups["n"].Value.Replace(',', '.'), out decimal n))
            return bound.Value.StartsWith('<') ? (null, n) : (n, null);
        return (null, null);
    }

    private static string GuessUnit(string rest)
        => KnownUnits.FirstOrDefault(u => rest.Contains(u, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;

    private static string Clamp(string s, int max) => s.Length <= max ? s : s[..max];

    private static int Letters(string s) => s.Count(char.IsLetter);
}
