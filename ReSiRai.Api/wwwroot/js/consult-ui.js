// ReSiRai - AI consultation panel.
//
// Sends the visit's structured state to the model and shows the ranked result:
// differential with probabilities and reasons, red flags, suggested workup and
// the factors that were not recorded. The output is decision support only: the
// closing line says the physician confirms every diagnosis.
(function () {
    "use strict";

    let overlay = null;
    let busy = false;

    async function readJson(r) { try { return await r.json(); } catch { return { success: false }; } }

    function el(tag, cls, text) {
        const node = document.createElement(tag);
        if (cls) node.className = cls;
        if (text !== undefined) node.textContent = text;
        return node;
    }

    function close() { if (overlay) { overlay.remove(); overlay = null; } }

    function open(studyID, specialtyID) {
        studyID = Number(studyID);
        if (!Number.isInteger(studyID) || studyID <= 0 || busy) return;
        startDialog({ studyID, specialtyID: specialtyID || null });
    }

    // The new-visit form: the consultation runs over the values typed in but
    // not saved yet. When the data is not enough, the AI reports what is missing
    // instead of guessing.
    function openDraft(opts) {
        if (busy) return;
        startDialog({
            studyID: 0,
            patientID: opts && opts.patientID ? Number(opts.patientID) : null,
            description: (opts && opts.description) || null,
            specialtyID: (opts && opts.specialtyID) || null,
            items: (opts && opts.items) || []
        });
    }

    function startDialog(payload) {
        close();
        busy = true;

        overlay = el("div", "lab-extract-overlay");
        const dialog = el("div", "lab-extract-dialog");
        const head = el("div", "lab-extract-head");
        head.appendChild(el("h3", null, "مشاوره هوش مصنوعی"));
        const closeBtn = el("button", "secondary-button", "بستن");
        closeBtn.type = "button";
        closeBtn.addEventListener("click", close);
        head.appendChild(closeBtn);
        dialog.appendChild(head);

        const body = el("div", "lab-extract-body");
        body.appendChild(el("div", "factors-empty", "در حال آماده‌سازی وضعیت بیمار و مشاوره... این مرحله ممکن است کمی طول بکشد."));
        dialog.appendChild(body);

        const foot = el("div", "lab-extract-foot");
        foot.appendChild(el("span", "consult-disclaimer", "این خروجی کمک‌تشخیصی است؛ تشخیص نهایی با پزشک است."));
        dialog.appendChild(foot);

        overlay.appendChild(dialog);
        overlay.addEventListener("click", e => { if (e.target === overlay) close(); });
        document.body.appendChild(overlay);

        load(body, payload);
    }

    async function load(body, payload) {
        let x;
        try {
            const r = await fetch("/api/ai/consult", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            });
            x = await readJson(r);
            if (!r.ok || !x.success) throw new Error(x.message || "مشاوره ناموفق بود.");
        } catch (e) {
            body.replaceChildren(el("div", "factors-empty", e.message || "مشاوره ناموفق بود."));
            busy = false;
            return;
        }
        busy = false;

        const c = x.consultation || {};
        body.replaceChildren();

        const meta = el("div", "consult-meta");
        meta.appendChild(el("span", null, `پوشش فاکتورها: ${x.coverage}%`));
        if (x.model) meta.appendChild(el("span", null, `مدل: ${x.model}`));
        body.appendChild(meta);

        if (c.rawText) {
            // The model answered but not in a readable structure: show its own
            // words rather than hiding the answer behind an error.
            const rawWarn = el("div", "consult-warning",
                "پاسخِ مدل ساختاریافته نبود؛ متنِ کاملِ پاسخ در پایین آمده است.");
            body.appendChild(rawWarn);
            body.appendChild(el("pre", "consult-raw", String(c.rawText)));
        }

        if (c.notEnoughData && !c.rawText) {
            const warn = el("div", "consult-warning",
                "داده‌ها برای نظر کافی نیست" + (c.missingFactors && c.missingFactors.length
                    ? "؛ فاکتورهای ثبت‌نشده: " + c.missingFactors.join("، ") : "."));
            body.appendChild(warn);
        }

        if (c.redFlags && c.redFlags.length) {
            const box = el("div", "consult-redflags");
            box.appendChild(el("h4", null, "هشدارهای مهم"));
            const ul = el("ul");
            for (const f of c.redFlags) ul.appendChild(el("li", null, f));
            box.appendChild(ul);
            body.appendChild(box);
        }

        if (c.differential && c.differential.length) {
            const table = el("table", "consult-table");
            const thead = el("thead");
            const hr = el("tr");
            ["تشخیص‌های محتمل", "احتمال", "دلایل"].forEach(t => hr.appendChild(el("th", null, t)));
            thead.appendChild(hr);
            table.appendChild(thead);
            const tbody = el("tbody");
            for (const d of c.differential) {
                const tr = el("tr");
                tr.appendChild(el("td", null, d.diagnosis || ""));

                const probCell = el("td");
                const bar = el("div", "consult-bar");
                const fill = el("div", "consult-bar-fill");
                fill.style.width = Math.max(0, Math.min(100, Number(d.probability) || 0)) + "%";
                bar.appendChild(fill);
                probCell.appendChild(bar);
                probCell.appendChild(el("span", "consult-prob", `${Number(d.probability) || 0}%`));
                tr.appendChild(probCell);

                const reasons = el("td");
                const ul = el("ul", "consult-reasons");
                for (const reason of (d.reasons || [])) ul.appendChild(el("li", null, reason));
                reasons.appendChild(ul);
                tr.appendChild(reasons);

                tbody.appendChild(tr);
            }
            table.appendChild(tbody);
            body.appendChild(table);
        }

        if (c.suggestedWorkup && c.suggestedWorkup.length) {
            const box = el("div", "consult-workup");
            box.appendChild(el("h4", null, "بررسی‌های پیشنهادی"));
            const ul = el("ul");
            for (const w of c.suggestedWorkup) ul.appendChild(el("li", null, w));
            box.appendChild(ul);
            body.appendChild(box);
        }
    }

    window.ReSiRaiConsult = { open, openDraft };
})();
