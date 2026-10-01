using System.Text.RegularExpressions;

namespace ReSiRai.Api.Services;

/// <summary>
/// A printed lab sheet is a table: test name, result, unit, reference range.
/// Reading it needs no AI model - a deterministic parse is exact and, which
/// matters most in a patient record, can never invent a row. The AI path remains
/// only as a fallback for sheets this parser cannot read.
///
/// The TSV path (word positions from Tesseract) is the main one: every printed
/// row is one OCR line even on a skewed scan, and each line is split into cells
/// (name | value | unit | reference) by the gaps between words. A garbled OCR
/// token never becomes a value: the row keeps an empty value and the doctor
/// fills it in - a missing number is safe, a wrong one is not.
/// </summary>
public static class LabSheetParser
{
    public sealed record Row(string Name, string Value, string Unit, string RefText,
        decimal? RefLow, decimal? RefHigh, bool Suggested = false,
        int PixelTop = -1, int PixelHeight = -1, int Page = 0,
        int Confidence = 0, string ConfidenceNote = "", string Section = "");

    // سطرِ عنوانِ بخشِ چاپی («Urine Analysis»، «CBC» و…) — نه اسمِ تست، نه مقدار.
    // عنوان‌ها ارقام ندارند و خودِ ردیف، واحد و بازه هم ندارند.
    private static readonly Regex SectionTitleRegex = new(
        @"\b(analysis|culture|sensitivity|count|cbc|differential|hematolog|haematolog|biochem|chemist|hormone|serolog|immunolog|thyroid|diabet|lipid|liver|kidney|cardiac|tumor|allerg|drug|urine|stool|panel|electrolyte|enzyme|function|microscop|macroscop)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

    // A result value is only ever a clean number ("<5", "4.3", "12%") or a short
    // qualitative word ("Negative", "Yellow"). A garbled token like "4a}" is
    // never turned into "4".
    private static readonly Regex PureNumberRegex = new(
        @"^[<>]?\s*-?\d+(?:[.,]\d+)?\s*%?$", RegexOptions.Compiled);

    // «10-12» یا «2-3»: شکلِ نتیجهٔ شمارشی در برگه‌های آنالیز ادرار.
    private static readonly Regex BareRangeRegex = new(
        @"^\s*\d+(?:[.,]\d+)?\s*-\s*\d+(?:[.,]\d+)?\s*$", RegexOptions.Compiled);

    // عددِ کم‌اعتماد با یکی‌دو نمادِ زباله جلویش («`51.1»): بعد از پاک‌سازی،
    // خودِ عددِ چاپ‌شده به دست می‌آید.
    private static readonly Regex AlmostNumberRegex = new(
        @"^[^\d<>\-+]{0,2}(?<n>[<>]?\s*-?\d+(?:[.,]\d+)?\s*(?:-\s*\d+(?:[.,]\d+)?)?\s*%?)$",
        RegexOptions.Compiled);

    // سطرهایِ توضیحِ بازه، نه تست: «Adults :»، «Moderate risk :»، «Highrisk >6».
    private static readonly string[] ContinuationWords =
        { "adult", "children", "risk", "normal", "average", "borderline", "high", "low",
          "moderate", "desirable", "deficient", "insufficient", "sufficient",
          "intoxication", "impaired", "diabetic", "female", "male" };

    private static bool IsContinuation(string name)
    {
        if (name.Length == 0 || !char.IsLetter(name[0])) return true;
        if (name.EndsWith(':')) return true;
        string n = name.Trim().ToLowerInvariant();
        return ContinuationWords.Any(w => n.StartsWith(w, StringComparison.Ordinal));
    }

    private sealed record TsvWord(string Text, int Left, int Top, int Width, int Height,
        double Conf, string Line);

    private static readonly string[] Headers =
        { "Test", "Result", "Unit", "Refrence", "Reference", "Blood", "Normal ranges", "Parameters", "Page" };

    private static readonly string[] KnownUnits =
        { "mg/dL", "U/L", "g/dL", "mEq/L", "mmol/L", "mIU/ml", "μIU/ml", "ng/mL", "pg/mL", "IU/mL", "Ratio", "%" };

    // ---------------- plain-text fallback ----------------

    public static List<Row> Parse(string? ocrText)
    {
        var rows = new List<Row>();
        string section = string.Empty;
        foreach (var rawLine in (ocrText ?? string.Empty).Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length < 3) continue;
            if (Headers.Any(h => line.StartsWith(h, StringComparison.OrdinalIgnoreCase))) continue;

            // سطرِ عنوانِ بخشِ چاپی: نه ردیفِ آزمایش است و نه مقدار — نامِ
            // پانلِ ردیف‌های بعدی می‌شود تا نتایج به تفکیکِ نوع بیایند.
            if (!line.Any(char.IsDigit) && line.Split(' ').Length <= 4 && !line.Contains('.')
                && SectionTitleRegex.IsMatch(line) && Letters(line) >= 5)
            {
                section = line;
                continue;
            }

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
                rows.Add(new Row(name, m.Groups["value"].Value.Trim(), unit, Clamp(refText, 500), lo, hi, Section: section));
                continue;
            }

            var lost = RowWithValueLostRegex.Match(line);
            if (lost.Success && Letters(lost.Groups["name"].Value) >= 3)
            {
                string rest = lost.Groups["rest"].Value.Trim();
                var (lo, hi) = ParseRef(rest);
                rows.Add(new Row(lost.Groups["name"].Value.Trim(), string.Empty, GuessUnit(rest), Clamp(rest, 500), lo, hi, Section: section));
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

    // ---------------- TSV path: word positions ----------------

    /// <summary>
    /// بازسازیِ جدول از خروجیِ Tesseract-TSV (مختصاتِ هر کلمه). هر خطِ TSV یک
    /// ردیفِ چاپی است - حتی روی اسکنِ کج - و هر خط با شکافِ بینِ کلمات به
    /// سلول‌های (نام | مقدار | واحد | بازه) تقسیم می‌شود. کلماتِ کم‌اعتمادِ OCR
    /// هرگز مقدار نمی‌سازند؛ مقدارِ خالی را پزشک پر می‌کند.
    /// </summary>
    public static List<Row> ParseTsv(string? tsv)
    {
        var words = ReadWords(tsv);
        if (words.Count < 2) return new();

        // شکافِ بینِ سلول‌ها با اندازهٔ صفحه می‌آید (حدودِ ۴٫۵٪ عرضِ صفحه)، نه
        // با اندازهٔ کلمه: فاصلهٔ کلماتِ یک نام کوتاه است، فاصلهٔ ستون‌ها بزرگ.
        double pageWidth = words.Max(w => w.Left + w.Width);
        double cellGap = Math.Clamp(pageWidth * 0.045, 100, 200);

        var rows = new List<Row>();
        string section = string.Empty;
        foreach (var line in words.GroupBy(w => w.Line).Select(g => g.OrderBy(w => w.Left).ToList()))
        {
            foreach (var segment in SegmentLine(line, cellGap))
            {
                // سطرِ عنوانِ بخشِ چاپی («Urine Analysis»، «CBC» و…) نه ردیفِ
                // آزمایش است و نه مقدار: نامِ پانلِ ردیف‌های بعدی می‌شود تا
                // نتایج به تفکیکِ نوع (خون‌شناسی، ادرار، کشت، …) دیده شوند.
                string text = string.Join(" ", segment.Select(JoinWords)).Trim();
                if (IsSectionTitle(text)) { section = text; continue; }
                int before = rows.Count;
                AppendRow(rows, segment);
                for (int i = before; i < rows.Count; i++)
                    rows[i] = rows[i] with { Section = section };
            }
        }
        return rows;
    }

    private static bool IsSectionTitle(string text)
        => text.Length >= 3 && Letters(text) >= 3 && !text.Any(char.IsDigit)
            && SectionTitleRegex.IsMatch(text);

    /// <summary>
    /// متنِ پشتیبان از همان TSV ساخته می‌شود؛ اجرای دوبارهٔ Tesseract برایِ
    /// هر صفحه دقیقه‌ها طول می‌کشید.
    /// </summary>
    public static string TsvToText(string? tsv)
        => string.Join("\n", ReadWords(tsv)
            .GroupBy(w => w.Line)
            .Select(g => string.Join(" ", g.OrderBy(w => w.Left).Select(w => w.Text))));

    private static List<TsvWord> ReadWords(string? tsv)
    {
        var words = new List<TsvWord>();
        foreach (var raw in (tsv ?? string.Empty).Split('\n'))
        {
            var parts = raw.Split('\t');
            if (parts.Length < 12) continue;
            if (!int.TryParse(parts[0], out int level) || level != 5) continue;
            if (!int.TryParse(parts[6], out int left) || !int.TryParse(parts[7], out int top) ||
                !int.TryParse(parts[8], out int width) || !int.TryParse(parts[9], out int height)) continue;
            if (!double.TryParse(parts[10], out double conf)) conf = 0;
            string text = NormalizeDigits(string.Join("\t", parts[11..]).Trim());
            // خطوطِ جدولِ «|» در متنِ سلول می‌چسبند و نام را می‌کُشند («| PLT» ردیف
            // را می‌انداخت). فقط جداکننده‌ها بریده می‌شوند؛ پرانتزِ «(AST)» می‌ماند.
            // گیومهٔ چسبیده به عدد («6.13"») هم زبالهٔ OCR است.
            text = text.Trim('|', '¦', '[', ']', '{', '}', '‖', '«', '»', '"', '\'', '`', '~').Trim();
            if (text.Length == 0) continue;
            bool pureNumber = PureNumberRegex.IsMatch(text);
            var almost = AlmostNumberRegex.Match(text);
            bool numberish = pureNumber || (almost.Success && Letters(text) <= 1);
            if (numberish)
            {
                // عددِ کم‌اعتماد هرگز انداخته نمی‌شود: با ضریبِ اطمینانِ پایین
                // می‌نشیند، تحلیل پرچمش می‌زند و انسان می‌بیندش. عددِ گم‌شده
                // («118» با اعتمادِ ≈۴ که 1.18 بود) بدتر از عددِ پرچم‌دار است.
                // ولی سرواژه‌ها هرگز به عدد تبدیل نمی‌شوند: «S.G.0.T.» و
                // «25-OH-Vitamin» نام‌اند، نه عددِ با زبالهٔ جلویش.
                if (!pureNumber && almost.Success)
                    text = NormalizeDigits(almost.Groups["n"].Value.Trim());
            }
            // سرواژهٔ نقطه‌دارِ میان‌رده («S.G.0.T.» با اعتمادِ ۳۴) می‌ماند؛ OCR
            // صفر/او را در هم می‌ریزد و دیکشنریِ برنامه با مترادف می‌بنددش.
            else if (conf < 40 && !(conf >= 25 && text.Contains('.') && text.Count(char.IsLetter) >= 2)) continue;
            words.Add(new TsvWord(text, left, top, width, height, conf, $"{parts[1]}/{parts[2]}/{parts[3]}/{parts[4]}"));
        }
        return words;
    }

    // شکافِ بزرگِ بینِ کلمات = مرزِ سلول. شکافِ خیلی بزرگ = دو جدولِ کنارِ هم در
    // یک خط (برگهٔ آنالیز ادرار: ماکروسکوپی | میکروسکوپی).
    private static List<List<List<TsvWord>>> SegmentLine(List<TsvWord> line, double cellGap)
    {
        var cells = new List<List<TsvWord>> { new() { line[0] } };
        var gaps = new List<double> { 0 };
        double right = line[0].Left + line[0].Width;
        foreach (var w in line.Skip(1))
        {
            double gap = w.Left - right;
            if (gap > cellGap) { cells.Add(new()); gaps.Add(gap); }
            cells[^1].Add(w);
            right = Math.Max(right, w.Left + w.Width);
        }

        int splitAt = -1;
        double maxGap = 0;
        for (int i = 1; i < cells.Count; i++)
            if (gaps[i] > maxGap) { maxGap = gaps[i]; splitAt = i; }

        // دو جدولِ کنارِ هم در یک خط (برگهٔ ادرار) فقط وقتی جدا می‌شود که هر دو
        // طرفِ شکاف، خودش یک ردیفِ کامل باشد: دست‌کم دو سلول با یک نام.
        var segments = new List<List<List<TsvWord>>>();
        bool TwoPanels()
        {
            // سمتِ چپ می‌تواند تنها یک سلولِ نام‌مانند باشد (سطرِ عنوانِ ستون:
            // «Urine Analysis | W.B.C. Many») وگرنه «W.B.C.=Many» بلعیدهٔ عنوان
            // می‌شد. سمتِ راست همچنان دست‌کم دو سلول می‌خواهد تا ردیفِ کیفیِ
            // «Bilirubin Negative» با فاصلهٔ زیاد دو تکه نشود.
            bool HalfLeft(List<List<TsvWord>> side) => side.Any(NameLike);
            bool HalfRight(List<List<TsvWord>> side) => side.Count >= 2 && side.Any(NameLike);
            return splitAt > 0 && splitAt < cells.Count && maxGap > cellGap * 2.5
                && HalfLeft(cells.Take(splitAt).ToList()) && HalfRight(cells.Skip(splitAt).ToList());
        }
        if (TwoPanels())
        {
            segments.Add(cells.Take(splitAt).ToList());
            segments.Add(cells.Skip(splitAt).ToList());
        }
        else
        {
            segments.Add(cells);
        }
        return segments;
    }

    private static bool NameLike(List<TsvWord> cell)
    {
        string t = JoinWords(cell);
        return !t.Any(char.IsDigit) && Letters(t) >= 2 && !IsUnit(t);
    }

    // طبقه‌بندیِ سلول‌ها: واحد | عددِ تمیز (اولی = مقدار) | متنِ دارایِ رقم (بازه)
    // | متن (اولی = نام، دومی = مقدارِ کیفی مثل Negative).
    private static void AppendRow(List<Row> rows, List<List<TsvWord>> cells)
    {
        string name = string.Empty, value = string.Empty, unit = string.Empty, refCell = string.Empty;
        bool valueSet = false;
        int valueConf = 0, lastDigitConf = 0;

        foreach (var cell in cells)
        {
            string text = JoinWords(cell);
            if (text.Length == 0) continue;
            int cellConf = (int)cell.Average(w => w.Conf);
            if (unit.Length == 0 && IsUnit(text)) { unit = text; continue; }
            if (PureNumberRegex.IsMatch(text))
            {
                // در جدول‌های چاپی مقدار همیشه قبل از واحد است؛ عددِ بعد از واحد
                // بخشی از بازه است ("< 480" کنارِ U/L مقدارِ LDH نیست).
                if (!valueSet && unit.Length == 0) { value = text; valueSet = true; valueConf = cellConf; }
                else { refCell = (refCell + " " + text).Trim(); lastDigitConf = cellConf; }
                continue;
            }
            if (text.Any(char.IsDigit))
            {
                // نامِ تست می‌تواند رقم داشته باشد («25-OH-Vitamin D»، «HbA1c»،
                // «S.G.0.T.»)؛ تا وقتی نام چیزی نشده، متنِ حرف‌دارِ بلند نام است،
                // نه مقدار یا بازه.
                if (name.Length == 0 && !valueSet && unit.Length == 0 && Letters(text) >= 4
                    && !text.Contains('>') && !text.Contains('<'))
                {
                    name = text;
                    continue;
                }
                // سلولِ درهمِ «مقدار واحد بازه» («4.85 10%L 4.2-5.6») وقتی هنوز
                // مقدار و واحد ثبت نشده‌اند: عددِ اول = مقدار، بعدیِ واحددار = واحد.
                if (!valueSet && unit.Length == 0 && TrySplitMerged(text, out string v, out string u, out string rest))
                {
                    value = v;
                    valueSet = true;
                    valueConf = cellConf;
                    unit = u;
                    if (rest.Length > 0) refCell = (refCell + " " + rest).Trim();
                    continue;
                }
                // واحدِ این سلول به‌کلی زباله است («6.13 7105» ← واحدِ «10^3/uL» را
                // OCR خراب خوانده) ولی عددِ سرِ سلول معتبر است؛ باقی به بازه می‌رود.
                // «0.5 -1.4» بازه است و با خطِ تیرهٔ سرِ بقیه کنار گذاشته می‌شود.
                if (!valueSet && unit.Length == 0
                    && Regex.IsMatch(text, @"^[<>]?\s*\d+(?:[.,]\d+)?\s+\S")
                    && !text.TrimStart().StartsWith('-') && !text.TrimStart().StartsWith('+'))
                {
                    var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2 && !parts[1].StartsWith('-') && !parts[1].StartsWith('+'))
                    {
                        value = parts[0].Trim();
                        valueSet = true;
                        valueConf = cellConf;
                        refCell = (refCell + " " + parts[1]).Trim();
                        continue;
                    }
                }
                refCell = (refCell + " " + text).Trim();
                lastDigitConf = cellConf;
                continue;
            }
            if (name.Length == 0) { name = text; continue; }
            if (!valueSet && Letters(text) >= 3) { value = text; valueSet = true; valueConf = cellConf; continue; }
            refCell = (refCell + " " + text).Trim();
        }

        if (Letters(name) < 1 && name.Length > 0) return;

        // (ب) سطرِ توضیحِ بازه («Adults : 2.6-4.5»، «100-126 Impaired ...») به
        // بازهٔ ردیفِ بالا می‌پیوندد؛ اطلاعاتِ برگه گم نمی‌شود ولی ردیفِ جعلی
        // هم ساخته نمی‌شود. باید قبل از قاعدهٔ نتیجهٔ ادراری بررسی شود.
        if (!valueSet && unit.Length == 0 && rows.Count > 0
            && NumberRegex.IsMatch(refCell) && IsContinuation(name))
        {
            var prev = rows[^1];
            string joined = (prev.RefText + " / " + name + " " + refCell).Trim(' ', '/');
            if (joined.Length <= 500) rows[^1] = prev with { RefText = joined };
            return;
        }

        // (الف) نتیجه در ستونِ بازه: برگه‌های آنالیز ادرار نتیجه را بدونِ واحد
        // چاپ می‌کنند («RBC.» کنارِ «10-12») — این عدد نتیجه است، نه بازه.
        if (!valueSet && unit.Length == 0 && BareRangeRegex.IsMatch(refCell))
        {
            value = refCell.Trim();
            valueSet = true;
            valueConf = lastDigitConf;
            refCell = string.Empty;
        }

        if (name.Length == 0) return;
        // مقدارِ چسبیده به نام («POTASSIUM 8.2» در عکسِ نمایشگرِ آزمایشگاه):
        // عددِ آخرِ نام، مقدارِ همان ردیف است — اگر پیش از آن حرف باشد و نام
        // با شماره تمام شود («COVID-19» هرگز شکسته نمی‌شود).
        if (value.Length == 0 && unit.Length == 0)
        {
            var tail = System.Text.RegularExpressions.Regex.Match(name, @"^(?<n>.*\S)\s+(?<v>\d+(?:[.,]\d+)?)$");
            if (tail.Success && Letters(tail.Groups["n"].Value) >= 3)
            {
                name = tail.Groups["n"].Value.Trim();
                value = tail.Groups["v"].Value;
                valueSet = true;
                valueConf = 50;
            }
        }
        // نامِ تست می‌تواند با رقم شروع شود («25-OH-Vitamin D»)؛ تا وقتی حرفِ
        // کافی دارد، نام است.
        if (!char.IsLetter(name[0]) && Letters(name) < 4) return;
        if (Letters(name) < 2 && value.Length == 0 && unit.Length == 0) return;
        // نامِ دارایِ خطِ تیره («Blood/Hgb») هرگز سربرگ نیست — سربرگ‌ها برچسبِ
        // خالی‌اند و نامِ آزمایشِ واقعی نباید قربانیِ آنها شود.
        if (!name.Contains('/') && Headers.Any(h => name.Equals(h, StringComparison.OrdinalIgnoreCase)
                || name.StartsWith(h + " ", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith(h + ":", StringComparison.OrdinalIgnoreCase))) return;
        // مقدارِ کیفیِ ردیفی که بازهٔ عددی دارد، مشکوک است (اغلب نامِ جدولِ کناری
        // است)؛ به‌جایِ مقدارِ اشتباه، کنارِ بازه می‌ماند و پزشک می‌بیند.
        if (valueSet && !PureNumberRegex.IsMatch(value) && NumberRegex.IsMatch(refCell))
        {
            refCell = (value + " " + refCell).Trim();
            value = string.Empty;
        }
        // ردیفی که نه مقدار دارد، نه واحد، نه بازه، ردیفِ واقعیِ آزمایش نیست.
        if (value.Length == 0 && unit.Length == 0 && refCell.Length == 0) return;

        // ضریبِ اطمینانِ این ردیف از خودِ سیگنال‌ها می‌آید: اعتمادِ OCR همان
        // کلماتِ مقدار، و کامل بودنِ سلول‌ها. زیرِ ۶۰ یعنی «خودت چک کن».
        int confidence;
        string confNote = string.Empty;
        if (value.Length == 0)
        {
            confidence = 30;
            confNote = "مقدار خوانده نشد — با دست وارد کنید";
        }
        else
        {
            confidence = Math.Clamp(valueConf <= 0 ? 50 : valueConf, 5, 100);
            if (valueConf > 0 && valueConf < 40)
            {
                confidence = Math.Min(confidence, 65);
                confNote = "مقدار با اعتمادِ کمِ OCR بازیابی شد";
            }
            else if (unit.Length == 0)
            {
                confidence = Math.Min(confidence, 85);
                confNote = "واحد خوانده نشد";
            }
            else if (refCell.Length == 0)
            {
                confidence = Math.Min(confidence, 88);
                confNote = "بازه چاپ‌شده خوانده نشد";
            }
        }

        // درصدِ چسبیده به عدد («145%») مقدار نیست؛ واحد است.
        if (unit.Length == 0 && value.EndsWith('%') && value.Length > 1)
        {
            unit = "%";
            value = value[..^1].TrimEnd();
        }

        var (lo, hi) = ParseRef(refCell);
        // جایِ ردیف روی تصویر تا بعداً بتوان همان ناحیه را برایِ خواندنِ مدل برید.
        int pixTop = cells.SelectMany(c => c).Min(w => w.Top);
        int pixBottom = cells.SelectMany(c => c).Max(w => w.Top + w.Height);
        rows.Add(new Row(name, value, unit, Clamp(refCell, 500), lo, hi,
            PixelTop: pixTop, PixelHeight: pixBottom - pixTop,
            Confidence: confidence, ConfidenceNote: confNote));
    }

    // پسوندهایِ واحد بعد از خطِ تیره: «mg/dL»، «10^9/L»، «cells/µL». نامِ آزمایش
    // با خطِ تیره («Blood/Hgb»، «LDL/HDL») هرگز واحد نیست.
    private static readonly string[] UnitSuffixes =
        { "l", "dl", "ml", "ul", "l", "mol", "mmol", "eq", "meq", "iu", "miu", "u",
          "g", "mg", "ng", "pg", "kg", "hr", "min", "sec", "day", "hpf", "lpf",
          "cells", "vol", "mosm", "mm", "cm", "m" };

    private static bool IsUnit(string s)
    {
        s = s.Trim();
        if (s.Length == 0 || s.Length > 12 || s.Contains(' ')) return false;
        if (s == "%" || KnownUnits.Any(k => k.Equals(s, StringComparison.OrdinalIgnoreCase))) return true;
        // "g/dL", "IU/mL", "10^9/L": شکلِ واحد با خطِ تیره — بخشِ بعد از خط باید
        // واقعاً پسوندِ واحد باشد، وگرنه «Blood/Hgb» به‌جایِ نام، واحد می‌خورد.
        int slash = s.IndexOf('/');
        if (slash <= 0 || slash >= s.Length - 1) return false;
        string head = s[..slash], tail = s[(slash + 1)..];
        if (head.Length > 4 || tail.Length > 4) return false;
        return UnitSuffixes.Any(u => u.Equals(tail, StringComparison.OrdinalIgnoreCase))
            && head.All(c => char.IsLetterOrDigit(c) || c == '%' || c == '^');
    }

    // «4.85 10%L 4.2-5.6» → مقدار 4.85، واحد 10%L، بازهٔ 4.2-5.6. فقط وقتی
    // الگو واقعاً همین باشد؛ «6 - 22» یا «18:0 ...» دست نمی‌خورند.
    private static bool TrySplitMerged(string text, out string value, out string unit, out string rest)
    {
        value = unit = rest = string.Empty;
        var m = Regex.Match(text, @"^(?<v>[<>]?\s*\d+(?:[.,]\d+)?)\s+(?<tail>.+)$");
        if (!m.Success) return false;
        var tokens = m.Groups["tail"].Value.Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return false;
        int idx = 0;
        if (IsUnit(tokens[0]) || (tokens[0].Length <= 12 && !tokens[0].Contains('-')
            && tokens[0].Any(c => char.IsLetter(c) || c == '%')))
        {
            unit = tokens[0];
            idx = 1;
        }
        rest = string.Join(" ", tokens.Skip(idx));
        if (unit.Length == 0 && !RangeRegex.IsMatch(rest) && !BoundRegex.IsMatch(rest)) return false;
        value = m.Groups["v"].Value.Trim();
        return true;
    }

    private static string JoinWords(List<TsvWord> words)
        => string.Join(" ", words.Select(w => w.Text)).Trim();

    private static double Median(List<double> values)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(x => x).ToList();
        return sorted[sorted.Count / 2];
    }

    private static string NormalizeDigits(string s)
    {
        if (s.Length == 0) return s;
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s)
            sb.Append(c switch
            {
                >= '\u06F0' and <= '\u06F9' => (char)('0' + (c - '\u06F0')),
                >= '\u0660' and <= '\u0669' => (char)('0' + (c - '\u0660')),
                '\u066B' => '.',
                '\u066C' => ',',
                _ => c
            });
        return sb.ToString();
    }

    /// <summary>
    /// Printed rows lose decimal points in OCR ("145" for 14.5). The reference
    /// range restores the scale: the true value sits near it. Digits are never
    /// invented - at most the decimal point moves, and only when the number is
    /// so far outside the range that a missing decimal is the likely cause: a
    /// genuinely extreme value (WBC 45) is never touched.
    /// </summary>
    public static string FitScale(string value, decimal? lo, decimal? hi, string? refText = null)
    {
        string core = value.Trim();
        string prefix = string.Empty, suffix = string.Empty;
        while (core.Length > 0 && "<>≤≥=".Contains(core[0]))
        {
            prefix += core[0];
            core = core[1..].TrimStart();
        }
        if (core.EndsWith('%'))
        {
            suffix = "%";
            core = core[..^1].TrimEnd();
        }
        if (core.Contains('.') || core.Contains(',')) return value;
        if (!decimal.TryParse(core, out decimal v) || v <= 0) return value;
        if (lo is null && hi is null) return value;

        decimal ruler = hi ?? lo!.Value;
        if (ruler <= 0) return value;

        // (۱) موردِ آشکار: عدد چند برابرِ بالایِ بازه است («329» برایِ ۳۲–۳۶).
        if (v >= ruler * 8m)
        {
            foreach (decimal d in new[] { 10m, 100m, 1000m })
            {
                decimal c = v / d;
                bool inside = (lo is null || c >= lo.Value * 0.5m) && (hi is null || c <= hi.Value * 1.6m);
                if (inside) return prefix + c.ToString(System.Globalization.CultureInfo.InvariantCulture) + suffix;
            }
        }

        // (۲) ممیزِ گم‌شده در گزارش‌های PDF («72» برایِ ۷٫۲ با بازهٔ ۴٫۵–۱۱):
        // جابه‌جایی فقط و فقط اگر نتیجه دقیقاً داخلِ بازه بنشیند. شرطِ ۵ برابرِ
        // بازه هم هست تا «WBC 45» واقعی هرگز تکان نخورد — پرچمِ دروغین
        // بدترین چیز است.
        if (v >= ruler * 5m)
        {
            foreach (decimal d in new[] { 10m, 100m, 1000m })
            {
                decimal c = v / d;
                if ((lo is null || c >= lo.Value) && (hi is null || c <= hi.Value))
                    return prefix + c.ToString(System.Globalization.CultureInfo.InvariantCulture) + suffix;
            }
            // (۳) مقدارِ زیرِ بازه («34» برایِ ۳٫۴ با بازهٔ ۳٫۵–۵٫۵): فقط وقتی
            // که خودِ بازه اعشاری باشد (یعنی برگه مقادیر را اعشاری می‌نویسد) و
            // نتیجه با همان بازهٔ آزادتر بخواند. بازهٔ صحیح («4-11») هرگز دست
            // نمی‌خورد تا «WBC 45» واقعی سرِ جایش بماند.
            bool decimalRange = (lo is not null && lo.Value % 1 != 0) || (hi is not null && hi.Value % 1 != 0);
            if (decimalRange)
            {
                foreach (decimal d in new[] { 10m, 100m, 1000m })
                {
                    decimal c = v / d;
                    if ((lo is null || c >= lo.Value * 0.5m) && (hi is null || c <= hi.Value * 1.6m))
                        return prefix + c.ToString(System.Globalization.CultureInfo.InvariantCulture) + suffix;
                }
            }
        }
        // (۴) بازهٔ «اعشاریِ چاپ‌شده» (مثلِ «up to 10.0»): «22» ← 2.2. فقط یک گامِ
        // تقسیم بر ۱۰، فقط وقتی خودِ عدد خارج از بازه باشد، و فقط اگر نتیجه دقیقاً
        // داخلِ بازه بنشیند. بازهٔ عددِ صحیحِ چاپ‌شده («4-11»، «70-100») هرگز
        // مشمول نمی‌شود؛ پس «WBC 45» و «گلوکز 164» تکان نمی‌خورند.
        bool printedDecimal = refText is not null && (refText.Contains('.') || refText.Contains(','));
        bool vInside = (lo is null || v >= lo.Value) && (hi is null || v <= hi.Value);
        if (printedDecimal && !vInside)
        {
            decimal c = v / 10m;
            if ((lo is null || c >= lo.Value) && (hi is null || c <= hi.Value))
                return prefix + c.ToString(System.Globalization.CultureInfo.InvariantCulture) + suffix;
        }
        return value;
    }

    public static (decimal?, decimal?) ParseRef(string refText)
    {
        var range = RangeRegex.Match(refText);
        if (range.Success &&
            decimal.TryParse(range.Groups["lo"].Value.Replace(',', '.'), out decimal lo) &&
            decimal.TryParse(range.Groups["hi"].Value.Replace(',', '.'), out decimal hi))
            return (lo, hi);
        var bound = BoundRegex.Match(refText);
        if (bound.Success && decimal.TryParse(bound.Groups["n"].Value.Replace(',', '.'), out decimal n))
            return bound.Value.StartsWith('<') ? (null, n) : (n, null);
        // «up to 10.0»: بازهٔ یک‌طرفه به شکلِ انگلیسی. همین «22» ← 2.2 را ممکن
        // می‌کند وگرنه بازه‌ای در کار نیست و ممیز جرأت نمی‌کند.
        var upTo = Regex.Match(refText, @"(?:up\s*to|less\s*than|below)\s*(?<n>\d+(?:[.,]\d+)?)", RegexOptions.IgnoreCase);
        if (upTo.Success && decimal.TryParse(upTo.Groups["n"].Value.Replace(',', '.'), out decimal u))
            return (null, u);
        return (null, null);
    }

    private static string GuessUnit(string rest)
        => KnownUnits.FirstOrDefault(u => rest.Contains(u, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;

    private static string Clamp(string s, int max) => s.Length <= max ? s : s[..max];

    private static int Letters(string s) => s.Count(char.IsLetter);
}
