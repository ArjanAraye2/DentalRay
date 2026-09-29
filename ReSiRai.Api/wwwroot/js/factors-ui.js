// ReSiRai - "sharayet-e feli" panel: the patient's current condition as structured,
// numeric factors the AI consultation can reason over.
//
// Definitions come from the shared dictionary (keyed by the visit's doctor),
// values are saved per visit and keep their source (manual here; lab-report
// extraction writes through the same API with source = 2). A value outside its
// reference range is flagged while it is typed, so the doctor sees it before AI.
(function () {
    "use strict";

    const CATEGORY_LABELS = {
        Vitals: "علائم حیاتی",
        Anthropometry: "اندازه‌های بدن",
        History: "سابقه",
        Exam: "معاینه",
        Lab: "آزمایشگاه",
        Imaging: "تصویربرداری و نوار قلب",
        Score: "امتیازهای محاسبه‌شده"
    };

    let studyID = null;
    let factors = [];
    let saving = false;

    async function readJson(r) { try { return await r.json(); } catch { return { success: false }; } }

    function latestByFactor(values) {
        const map = new Map();
        for (const v of values || []) {
            const prev = map.get(v.factorID);
            if (!prev || new Date(v.observedAt) > new Date(prev.observedAt)) map.set(v.factorID, v);
        }
        return map;
    }

    function refHint(f) {
        if (f.refText) return f.refText;
        if (f.refLow !== null && f.refLow !== undefined && f.refHigh !== null && f.refHigh !== undefined)
            return `${f.refLow} تا ${f.refHigh}`;
        if (f.refHigh !== null && f.refHigh !== undefined) return `کمتر از ${f.refHigh}`;
        if (f.refLow !== null && f.refLow !== undefined) return `بیشتر از ${f.refLow}`;
        return "";
    }

    function isAbnormal(f, raw) {
        if (f.dataType !== 1 || raw === "" || raw === null || raw === undefined) return false;
        const n = Number(raw);
        if (!Number.isFinite(n)) return false;
        if (f.refLow !== null && f.refLow !== undefined && n < f.refLow) return true;
        if (f.refHigh !== null && f.refHigh !== undefined && n > f.refHigh) return true;
        return false;
    }

    function currentValueOf(f, latest) {
        const v = latest.get(f.factorID);
        if (!v) return "";
        if (f.dataType === 1 || f.dataType === 2) return v.valueNumber ?? "";
        if (f.dataType === 3) return v.valueBit === true ? "1" : (v.valueBit === false ? "0" : "");
        if (f.dataType === 4) return v.valueText ?? "";
        if (f.dataType === 5) {
            // Stored as ISO; shown as Jalali for the Persian UI.
            if (!v.valueDate) return "";
            return window.formatPersianDateForInput ? window.formatPersianDateForInput(v.valueDate) : v.valueDate;
        }
        return "";
    }

    function buildInput(f, value) {
        if (f.dataType === 2) {
            const select = document.createElement("select");
            select.appendChild(new Option("— انتخاب —", ""));
            let options = [];
            try { options = JSON.parse(f.optionsJson || "[]"); } catch { options = []; }
            for (const o of options) select.appendChild(new Option(o.t, String(o.v)));
            select.value = String(value ?? "");
            return select;
        }
        if (f.dataType === 3) {
            const select = document.createElement("select");
            select.appendChild(new Option("— انتخاب —", ""));
            select.appendChild(new Option("بله", "1"));
            select.appendChild(new Option("خیر", "0"));
            select.value = value === "" ? "" : String(value);
            return select;
        }
        const input = document.createElement("input");
        if (f.dataType === 1) { input.type = "number"; input.step = "any"; input.inputMode = "decimal"; }
        else if (f.dataType === 5) { input.type = "text"; input.setAttribute("data-jalali-date", ""); input.placeholder = "مثال: 1405/07/08"; }
        else { input.type = "text"; input.maxLength = 500; }
        input.value = value ?? "";
        return input;
    }

    function readValue(f, input) {
        const raw = (input.value ?? "").trim();
        if (raw === "") return null;
        if (f.dataType === 1) {
            const n = Number(raw);
            return Number.isFinite(n) ? { valueNumber: n } : null;
        }
        if (f.dataType === 2) return { valueNumber: Number(raw) };
        if (f.dataType === 3) return { valueBit: raw === "1" };
        if (f.dataType === 4) return { valueText: raw };
        if (f.dataType === 5) {
            try {
                const iso = window.parsePersianDateForBackend ? window.parsePersianDateForBackend(raw, false) : raw;
                return { valueDate: iso };
            } catch { return { valueText: raw }; }
        }
        return null;
    }

    function setStatus(host, message, isError) {
        const el = host.querySelector(".factors-status");
        if (!el) return;
        el.textContent = message || "";
        el.classList.toggle("error", !!isError);
    }

    async function render(study) {
        const host = document.getElementById("studyFactorsPanel");
        if (!host || !study) return;
        studyID = Number(study.studyID);
        host.replaceChildren();
        const note = document.createElement("div");
        note.className = "factors-empty";
        note.textContent = "در حال دریافت فاکتورها...";
        host.appendChild(note);

        try {
            const [defRes, valRes] = await Promise.all([
                fetch(`/api/factors/definitions?studyID=${studyID}`, { cache: "no-store" }).then(readJson),
                fetch(`/api/factors/values?studyID=${studyID}`, { cache: "no-store" }).then(readJson)
            ]);
            if (!defRes.success) throw new Error(defRes.message || "دریافت فاکتورها ناموفق بود.");

            factors = defRes.factors || [];
            const latest = latestByFactor(valRes.values);
            host.replaceChildren();
            if (factors.length === 0) {
                const empty = document.createElement("div");
                empty.className = "factors-empty";
                empty.textContent = "برای این تخصص هنوز فاکتوری تعریف نشده است.";
                host.appendChild(empty);
                return;
            }

            let group = null, groupTitle = null;
            for (const f of factors) {
                if (!group || group.dataset.category !== f.category) {
                    group = document.createElement("div");
                    group.className = "factors-group";
                    group.dataset.category = f.category;
                    groupTitle = document.createElement("h4");
                    groupTitle.className = "factors-group-title";
                    groupTitle.textContent = CATEGORY_LABELS[f.category] || f.category;
                    group.appendChild(groupTitle);
                    host.appendChild(group);
                }

                const row = document.createElement("div");
                row.className = "factor-row";
                row.dataset.factorId = String(f.factorID);

                const label = document.createElement("label");
                label.textContent = f.nameFa + " ";
                if (f.isRequired) {
                    const req = document.createElement("span");
                    req.className = "factor-required";
                    req.textContent = "*";
                    label.appendChild(req);
                }
                if (f.unitUCUM) {
                    const unit = document.createElement("small");
                    unit.textContent = `(${f.unitUCUM})`;
                    label.appendChild(unit);
                }

                const input = buildInput(f, currentValueOf(f, latest));
                input.addEventListener("input", () => row.classList.toggle("abnormal", isAbnormal(f, input.value)));
                input.addEventListener("change", () => row.classList.toggle("abnormal", isAbnormal(f, input.value)));
                row.classList.toggle("abnormal", isAbnormal(f, input.value));

                const ref = document.createElement("span");
                ref.className = "factor-ref";
                ref.textContent = refHint(f);
                ref.title = `${f.refSource || ""}${f.refPopulation ? " - " + f.refPopulation : ""}`;

                const flag = document.createElement("span");
                flag.className = "factor-flag";
                flag.textContent = "خارج از بازه طبیعی";

                row.append(label, input, ref, flag);
                group.appendChild(row);
            }

            const footer = document.createElement("div");
            footer.className = "factors-footer";
            const saveBtn = document.createElement("button");
            saveBtn.type = "button";
            saveBtn.className = "primary-button";
            saveBtn.textContent = "ثبت مقادیر";
            saveBtn.addEventListener("click", () => save(host, saveBtn));
            const extractBtn = document.createElement("button");
            extractBtn.type = "button";
            extractBtn.className = "secondary-button";
            extractBtn.textContent = "استخراج از برگه آزمایش";
            extractBtn.addEventListener("click", () => window.ReSiRaiLabExtract?.open(studyID));
            const consultBtn = document.createElement("button");
            consultBtn.type = "button";
            consultBtn.className = "primary-button";
            consultBtn.textContent = "مشاوره با هوش مصنوعی";
            consultBtn.addEventListener("click", () => window.ReSiRaiConsult?.open(studyID));
            const status = document.createElement("span");
            status.className = "factors-status";
            footer.append(saveBtn, extractBtn, consultBtn, status);
            host.appendChild(footer);

            window.ReSiRaiJalali?.enhanceAll(host);
        } catch (e) {
            host.replaceChildren();
            const err = document.createElement("div");
            err.className = "factors-empty";
            err.textContent = e.message || "دریافت فاکتورها ناموفق بود.";
            host.appendChild(err);
        }
    }

    async function save(host, button) {
        if (saving || !studyID) return;
        const items = [];
        for (const f of factors) {
            const row = host.querySelector(`.factor-row[data-factor-id="${f.factorID}"]`);
            if (!row) continue;
            const input = row.querySelector("input, select");
            if (!input) continue;
            const value = readValue(f, input);
            if (!value) continue;
            items.push(Object.assign({ factorID: f.factorID, source: 1 }, value));
        }
        if (items.length === 0) { setStatus(host, "هیچ مقداری برای ثبت وارد نشده است.", true); return; }

        try {
            saving = true;
            button.disabled = true;
            button.textContent = "در حال ثبت...";
            setStatus(host, "", false);
            const r = await fetch("/api/factors/values", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ studyID, items })
            });
            const x = await readJson(r);
            if (!r.ok || !x.success) throw new Error(x.message || "ثبت مقادیر ناموفق بود.");
            setStatus(host, `${x.saved} مقدار ثبت شد.`, false);
        } catch (e) {
            setStatus(host, e.message || "ثبت مقادیر ناموفق بود.", true);
        } finally {
            saving = false;
            button.disabled = false;
            button.textContent = "ثبت مقادیر";
        }
    }

    window.ReSiRaiFactors = { render };
})();
