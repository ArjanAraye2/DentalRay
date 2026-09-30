using System.Globalization;

namespace ReSiRai.Api.Services;

/// <summary>
/// One observed number, flattened for the consistency rules. The reference range
/// from the dictionary (or the printed sheet) is carried only to spot numbers
/// that are off by an order of magnitude - a unit or transcription error.
/// </summary>
public sealed record LabObs(string? Code, string? ShortCode, string? NameEn,
    decimal? Number, string? UnitText, decimal? RefLow = null, decimal? RefHigh = null);

/// <summary>
/// One contradiction found between recorded values (or a value that cannot be
/// true as printed). MessageFa is for the doctor; RetestFa names the test to
/// repeat together with the reason - a doctor must never be left guessing why.
/// </summary>
public sealed record ConsistencyFinding(string Rule, string Severity,
    string[] Factors, string MessageFa, string MessageEn, string? RetestFa);

/// <summary>
/// موتورِ قطعیِ «ناسازگاریِ فاکتورها»: مقادیرِ ثبت‌شده باید **با هم** بخوانند،
/// نه فقط تک‌تک داخلِ بازه باشند. آزمایشگاه همیشه می‌تواند اشتباه اندازه‌گیری
/// یا اشتباه ثبت کرده باشد؛ این قواعدِ ریاضی/فیزیولوژیک آن اشتباه‌ها را بدونِ
/// نیاز به AI پیدا می‌کنند و به پزشک و به مدل گزارش می‌شوند.
/// هیچ‌چیز اینجا تفسیرِ بالینی نیست؛ فقط «این دو عدد نمی‌توانند هر دو درست باشند».
/// </summary>
public static class LabConsistencyChecker
{
    public static List<ConsistencyFinding> Check(IReadOnlyList<LabObs> obs, int? ageYears, string? sex)
    {
        var findings = new List<ConsistencyFinding>();
        var live = obs.Where(x => x.Number != null).ToList();

        LabObs? At(string key) => live.FirstOrDefault(x => Matches(x, key));

        // ---- Lipids: HDL <= total, and the Friedewald identity ----------------
        var chol = At("chol"); var hdl = At("hdl"); var ldl = At("ldl"); var tg = At("tg");
        if (chol?.Number is { } cN && hdl?.Number is { } hN && hN > cN)
            findings.Add(new("hdl>chol", "error", new[] { "HDL", "Total cholesterol" },
                $"ناسازگاری: مقدارِ HDL ({hN}) از کلسترولِ کل ({cN}) بزرگ‌تر است؛ از نظرِ فیزیکی ممکن نیست. یکی از اندازه‌گیری‌ها یا ثبت‌ها اشتباه است.",
                $"HDL ({hN}) is greater than total cholesterol ({cN}), which cannot be true; one measurement or entry is wrong.",
                "پروفایلِ چربی (کلسترولِ کل، HDL، LDL، تری‌گلیسرید) را تکرار کنید — دلیل: HDL بزرگ‌تر از کلسترولِ کل ثبت شده که از نظرِ فیزیکی ممکن نیست."));

        if (chol?.Number is { } c2 && hdl?.Number is { } h2 && ldl?.Number is { } l2 && tg?.Number is { } t2)
        {
            bool mmol = ((chol.UnitText ?? ldl.UnitText) ?? "").Contains("mmol");
            double divisor = mmol ? 2.2 : 5.0;
            double friedewald = (double)(c2 - h2) - (double)t2 / divisor;
            double diff = Math.Abs(friedewald - (double)l2);
            double tolerance = Math.Max(mmol ? 0.8 : 30.0, Math.Abs(friedewald) * 0.25);
            if (diff > tolerance)
                findings.Add(new("friedewald", "warn", new[] { "LDL", "TG", "Total cholesterol", "HDL" },
                    $"ناسازگاری: LDL ثبت‌شده ({l2}) با فرمولِ فریدوالد از مقادیرِ همین برگه ({F(friedewald)}) نمی‌خواند؛ ممکن است یکی از چهار مقدار یا واحدِ آن‌ها اشتباه اندازه‌گیری یا اشتباه ثبت شده باشد.",
                    $"Recorded LDL ({l2}) does not match the Friedewald value computed from this sheet ({F(friedewald)}); one of the four lipid numbers or their units may be mis-measured or mis-entered.",
                    "پروفایلِ چربی را تکرار کنید — دلیل: LDL با فرمولِ فریدوالدِ مقادیرِ همان برگه نمی‌خواند و ممکن است یکی از چهار مقدار اشتباه باشد."));
        }

        // ---- CBC: Hct ~ 3 x Hb, and RBC x MCV ~ Hct --------------------------
        var hb = At("hgb"); var hct = At("hct");
        if (hb?.Number is { } hbN && hct?.Number is { } hctN && hbN > 0)
        {
            double expected = (double)hbN * 3.0;
            if (Math.Abs(expected - (double)hctN) / expected > 0.20)
                findings.Add(new("hct~3hb", "warn", new[] { "Hemoglobin", "Hematocrit" },
                    $"ناسازگاری: هماتوکریت ({hctN}) با هموگلوبین ({hbN}) نمی‌خواند (انتظار ≈ {F(expected)})؛ از دو شمارنده/دستگاهِ مختلف آمده یا یکی اشتباه ثبت شده است.",
                    $"Hematocrit ({hctN}) is inconsistent with hemoglobin ({hbN}) (expected about {F(expected)}); they may come from different analyzers or one is wrong.",
                    "شمارشِ کاملِ خون (CBC) را تکرار کنید — دلیل: هماتوکریت و هموگلوبین با هم نمی‌خوانند."));
        }

        var rbc = At("rbc"); var mcv = At("mcv");
        if (rbc?.Number is { } rbcN && mcv?.Number is { } mcvN && hct?.Number is { } hct2 && rbcN > 0)
        {
            double expected = (double)(rbcN * mcvN) / 10.0;
            if (expected > 0 && Math.Abs(expected - (double)hct2) / expected > 0.20)
                findings.Add(new("rbc*mcv~hct", "warn", new[] { "RBC", "MCV", "Hematocrit" },
                    $"ناسازگاری: حاصلِ ضربِ RBC×MCV ({F(expected)}) با هماتوکریتِ ثبت‌شده ({hct2}) نمی‌خواند؛ یکی از سه مقدار اشتباه است.",
                    $"RBC x MCV ({F(expected)}) does not match the recorded hematocrit ({hct2}); one of the three is wrong.",
                    "شمارشِ کاملِ خون (CBC) را تکرار کنید — دلیل: RBC، MCV و هماتوکریت با هم نمی‌خوانند."));
        }

        // ---- Chemistry identities --------------------------------------------
        var alb = At("alb"); var glob = At("glob"); var tprot = At("tprot");
        if (alb?.Number is { } aN && glob?.Number is { } gN && tprot?.Number is { } tpN)
        {
            double sum = (double)(aN + gN);
            if (Math.Abs(sum - (double)tpN) > 0.6)
                findings.Add(new("tprot=alb+glob", "warn", new[] { "Total protein", "Albumin", "Globulin" },
                    $"ناسازگاری: آلبومین + گلوبولین ({aN} + {gN} = {F(sum)}) با پروتئینِ توتال ({tpN}) نمی‌خواند؛ احتمالِ خطای اندازه‌گیری یا ثبت وجود دارد.",
                    $"Albumin + globulin ({aN} + {gN}) does not add up to the recorded total protein ({tpN}); measurement or entry error is possible.",
                    "پروتئینِ توتال، آلبومین و گلوبولین را تکرار کنید — دلیل: جمعِ آلبومین و گلوبولین با پروتئینِ توتالِ ثبت‌شده نمی‌خواند."));
        }

        var tbil = At("tbil"); var dbil = At("dbil");
        if (tbil?.Number is { } tbN && dbil?.Number is { } dbN && dbN > tbN)
            findings.Add(new("dbil<=tbil", "error", new[] { "Bilirubin direct", "Bilirubin total" },
                $"ناسازگاری: بیلی‌روبینِ مستقیم ({dbN}) از کل ({tbN}) بزرگ‌تر است؛ امکان‌پذیر نیست و معمولاً یعنی اشتباهِ ثبت یا کالیبراسیون.",
                $"Direct bilirubin ({dbN}) is greater than total bilirubin ({tbN}), which cannot be; usually an entry or calibration error.",
                "بیلی‌روبینِ توتال و مستقیم را تکرار کنید — دلیل: مقدارِ مستقیم از کل بزرگ‌تر ثبت شده است."));

        // ---- Derived vs recorded: eGFR from creatinine (CKD-EPI 2021) --------
        var creat = At("creat");
        if (creat?.Number is { } crN && crN > 0 && ageYears is > 0 and <= 120)
        {
            double scr = (double)crN;
            bool female = string.Equals(sex, "female", StringComparison.OrdinalIgnoreCase);
            double kappa = female ? 0.7 : 1.0;
            double alpha = female ? -0.241 : -0.302;
            double ratio = scr / kappa;
            double egfrCalc = 142.0 * Math.Pow(Math.Min(ratio, 1.0), alpha)
                * Math.Pow(Math.Max(ratio, 1.0), -1.200)
                * Math.Pow(0.9938, ageYears.Value) * (female ? 1.012 : 1.0);
            if (At("egfr")?.Number is { } egfrN && egfrN > 0
                && Math.Abs(egfrCalc - (double)egfrN) / Math.Max(egfrCalc, 1.0) > 0.30)
                findings.Add(new("egfr!=creat", "warn", new[] { "eGFR", "Creatinine", "Age", "Sex" },
                    $"ناسازگاری: eGFR ثبت‌شده ({egfrN}) با مقدارِ محاسبه‌شده از کراتینین ({crN}) و سن و جنس (CKD-EPI 2021 ≈ {F(egfrCalc)}) بیش از ۳۰٪ فاصله دارد؛ یا یکی از ورودی‌ها اشتباه است یا eGFR متعلق به زمانِ دیگری است.",
                    $"The recorded eGFR ({egfrN}) differs by more than 30% from the value computed from creatinine ({crN}), age and sex (CKD-EPI 2021 about {F(egfrCalc)}); an input is wrong or the eGFR belongs to another time.",
                    "کراتینین را تکرار کنید — دلیل: eGFR ثبت‌شده با مقدارِ محاسبه‌شده از کراتینین، سن و جنس نمی‌خواند."));
        }

        // ---- Body measures: BMI from height and weight -------------------------
        var height = At("height"); var weight = At("weight"); var bmi = At("bmi");
        if (height?.Number is { } hM && weight?.Number is { } wKg && bmi?.Number is { } bN && hM > 0)
        {
            double calc = (double)wKg / Math.Pow((double)hM / 100.0, 2.0);
            if (calc > 5 && Math.Abs(calc - (double)bN) / calc > 0.05)
                findings.Add(new("bmi!=h/w", "warn", new[] { "BMI", "Height", "Weight" },
                    $"ناسازگاری: BMI ثبت‌شده ({bN}) با قد و وزن ({F(calc)}) نمی‌خواند؛ قد یا وزن اشتباه ثبت شده است.",
                    $"The recorded BMI ({bN}) does not match height and weight ({F(calc)}); height or weight was entered wrong.",
                    "قد و وزن را دوباره اندازه‌گیری کنید — دلیل: BMI با قد و وزنِ ثبت‌شده نمی‌خواند."));
        }

        // ---- White cell differential ------------------------------------------
        double pctSum = 0; bool anyPct = false;
        foreach (var key in new[] { "neutp", "lymp", "monop", "eosp", "basp" })
            if (At(key)?.Number is { } p) { pctSum += (double)p; anyPct = true; }
        if (anyPct && Math.Abs(pctSum - 100.0) > 5.0)
            findings.Add(new("diff-sum", "warn", new[] { "WBC differential %" },
                $"ناسازگاری: جمعِ درصدهایِ افتراقِ گلبولِ سفید ({F(pctSum)}٪) باید نزدیکِ ۱۰۰٪ باشد؛ یک ردیف جا افتاده یا اشتباه ثبت شده است.",
                $"The white cell differential percentages add up to {F(pctSum)}% instead of about 100%; a row is missing or wrong.",
                "شمارشِ تفکیکیِ گلبولِ سفید را تکرار کنید — دلیل: جمعِ درصدها به ۱۰۰٪ نمی‌رسد."));

        var wbc = At("wbc");
        foreach (var (pctKey, absKey, label) in new[]
        {
            ("neutp", "neuta", "Neutrophils"), ("lymp", "lyma", "Lymphocytes"),
            ("monop", "monoa", "Monocytes"), ("eosp", "eosa", "Eosinophils")
        })
        {
            if (At(pctKey)?.Number is { } pN && At(absKey)?.Number is { } aN2
                && wbc?.Number is { } wbcN && wbcN > 0)
            {
                double expected = (double)wbcN * (double)pN / 100.0;
                if (expected > 0 && Math.Abs(expected - (double)aN2) / Math.Max(expected, 0.1) > 0.35)
                    findings.Add(new("abs!=%*wbc", "warn", new[] { label },
                        $"ناسازگاری: تعدادِ مطلقِ {label} ({aN2}) با درصدِ آن ({pN}٪) ضرب‌در WBC ({wbcN}) نمی‌خواند (انتظار ≈ {F(expected)})؛ یکی از مقادیر اشتباه است.",
                        $"Absolute {label} count ({aN2}) does not match its percentage ({pN}%) times WBC ({wbcN}) (expected about {F(expected)}); one of them is wrong.",
                        $"شمارشِ تفکیکیِ گلبولِ سفید را تکرار کنید — دلیل: تعدادِ مطلقِ {label} با درصدِ آن ضرب‌در WBC نمی‌خواند."));
            }
        }

        // ---- Grossly implausible numbers: the classic unit or entry error -----
        foreach (var o in live)
        {
            if (o.Number is not { } n || n == 0) continue;
            string name = o.NameEn ?? o.ShortCode ?? o.Code ?? "?";
            if (o.RefHigh is { } hi && n > hi * 10)
                findings.Add(new("implausible-high", "warn", new[] { name },
                    $"احتمالِ خطای ثبت/واحد: مقدارِ {name} ({n}) بیش از ۱۰ برابرِ بالاترینِ بازهٔ معمول است؛ برگه را دوباره ببینید و واحد را بررسی کنید.",
                    $"Probable unit or entry error: {name} ({n}) is more than 10 times the top of the usual range; re-check the sheet and the unit.",
                    $"تکرارِ {name} و تأییدِ واحد با آزمایشگاه — دلیل: مقدار بیش از ۱۰ برابرِ بازهٔ معمول است (احتمال خطای واحد یا ثبت)."));
            if (o.RefLow is { } lo && lo > 0 && n < lo / 10)
                findings.Add(new("implausible-low", "warn", new[] { name },
                    $"احتمالِ خطای ثبت/واحد: مقدارِ {name} ({n}) کمتر از یک‌دهمِ پایینِ بازهٔ معمول است؛ برگه را دوباره ببینید.",
                    $"Probable unit or entry error: {name} ({n}) is below a tenth of the usual range; re-check the sheet.",
                    $"تکرارِ {name} و تأییدِ واحد با آزمایشگاه — دلیل: مقدار کمتر از یک‌دهمِ بازهٔ معمول است (احتمال خطای واحد یا ثبت)."));
            if (n < 0 && !name.Contains("base", StringComparison.OrdinalIgnoreCase))
                findings.Add(new("negative", "error", new[] { name },
                    $"ناسازگاری: مقدارِ {name} منفی ثبت شده ({n})؛ این فاکتور منفی نمی‌شود.",
                    $"{name} is recorded as negative ({n}); this factor cannot be negative.",
                    $"تکرارِ {name} — دلیل: مقدارِ منفی ثبت شده که برای این فاکتور ممکن نیست."));
        }

        return findings;
    }

    private static string F(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

    private static bool Matches(LabObs x, string key)
    {
        string code = (x.Code ?? "").ToUpperInvariant();
        string shortc = (x.ShortCode ?? "").Trim().ToUpperInvariant();
        string name = (x.NameEn ?? "").Trim().ToLowerInvariant();
        return key switch
        {
            "chol" => shortc == "CHOL" || (name.Contains("cholesterol") && name.Contains("total")),
            "hdl" => shortc == "HDL" || name.Contains("hdl"),
            "ldl" => shortc == "LDL" || name.Contains("ldl"),
            "tg" => shortc == "TG" || name.Contains("triglyc"),
            "hgb" => (name.Contains("hemoglobin") || shortc == "HGB") && !name.Contains("a1c"),
            "hct" => name.Contains("hematocrit") || shortc == "HCT",
            "rbc" => (shortc == "RBC" || name == "rbc" || name.Contains("red blood")) && !name.Contains("urine"),
            "mcv" => name.Contains("mcv"),
            "alb" => name.Contains("albumin"),
            "glob" => name.Contains("globulin") && !name.Contains("immuno"),
            "tprot" => name.Contains("protein") && name.Contains("total"),
            "tbil" => name.Contains("bilirubin") && name.Contains("total"),
            "dbil" => name.Contains("bilirubin") && (name.Contains("direct") || name.Contains("conjugat")),
            "creat" => name.Contains("creatinine") || shortc == "CR" || shortc == "CREAT",
            "egfr" => name.Contains("egfr"),
            "bmi" => code.EndsWith(".BMI") || name.Contains("body mass"),
            "height" => code.Contains("HEIGHT") || name.Contains("height"),
            "weight" => code.Contains("WEIGHT") || (name.Contains("weight") && !name.Contains("gain") && !name.Contains("loss")),
            "wbc" => shortc == "WBC" || name.Contains("white blood") || name == "wbc",
            "neutp" => name.Contains("neutroph") && !name.Contains("absolute") && !name.Contains("count"),
            "lymp" => name.Contains("lymphoc") && !name.Contains("absolute") && !name.Contains("count"),
            "monop" => name.Contains("monocyt") && !name.Contains("absolute") && !name.Contains("count"),
            "eosp" => name.Contains("eosinoph") && !name.Contains("absolute") && !name.Contains("count"),
            "basp" => name.Contains("basoph") && !name.Contains("absolute") && !name.Contains("count"),
            "neuta" => name.Contains("neutroph") && (name.Contains("absolute") || name.Contains("count") || name.Contains("#")),
            "lyma" => name.Contains("lymphoc") && (name.Contains("absolute") || name.Contains("count") || name.Contains("#")),
            "monoa" => name.Contains("monocyt") && (name.Contains("absolute") || name.Contains("count") || name.Contains("#")),
            "eosa" => name.Contains("eosinoph") && (name.Contains("absolute") || name.Contains("count") || name.Contains("#")),
            _ => false
        };
    }
}
