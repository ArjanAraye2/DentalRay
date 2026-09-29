// ReSiRai - lab-report extraction review.
//
// The doctor picks one visit image (a printed lab report), the AI returns a draft
// of the printed tests, and a human confirms the rows. Only confirmed rows are
// stored - with source = 2 (lab-report extraction) and the extraction batch id -
// so every number in the record keeps its evidence and its confidence.
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

    function setStatus(host, message, isError) {
        const s = host.querySelector(".lab-extract-status");
        if (!s) return;
        s.textContent = message || "";
        s.classList.toggle("error", !!isError);
    }

    function close() {
        if (overlay) { overlay.remove(); overlay = null; }
    }

    function open(studyID) {
        close();
        studyID = Number(studyID);
        if (!Number.isInteger(studyID) || studyID <= 0) return;

        overlay = el("div", "lab-extract-overlay");
        const dialog = el("div", "lab-extract-dialog");
        const head = el("div", "lab-extract-head");
        head.appendChild(el("h3", null, "استخراج برگه آزمایش"));
        const closeBtn = el("button", "secondary-button", "بستن");
        closeBtn.type = "button";
        closeBtn.addEventListener("click", close);
        head.appendChild(closeBtn);
        dialog.appendChild(head);

        const body = el("div", "lab-extract-body");
        dialog.appendChild(body);

        const foot = el("div", "lab-extract-foot");
        foot.appendChild(el("span", "lab-extract-status"));
        dialog.appendChild(foot);

        overlay.appendChild(dialog);
        overlay.addEventListener("click", e => { if (e.target === overlay) close(); });
        document.body.appendChild(overlay);

        renderPicker(body, studyID);
    }

    // Step 1: pick the report image among the visit's images.
    async function renderPicker(body, studyID) {
        body.replaceChildren(el("div", "factors-empty", "در حال دریافت تصاویر مراجعه..."));
        let x;
        try {
            const r = await fetch(`/api/radiologyimages/study/${studyID}`, { cache: "no-store" });
            x = await readJson(r);
            if (!r.ok || !x.success) throw new Error(x.message || "دریافت تصاویر ناموفق بود.");
        } catch (e) {
            body.replaceChildren(el("div", "factors-empty", e.message || "دریافت تصاویر ناموفق بود."));
            return;
        }

        const images = x.images || [];
        body.replaceChildren();
        if (images.length === 0) {
            body.appendChild(el("div", "factors-empty", "برای این مراجعه تصویری ثبت نشده است."));
            return;
        }

        body.appendChild(el("div", "factors-empty", "تصویر برگه آزمایش را انتخاب کنید:"));
        const grid = el("div", "lab-extract-grid");
        let chosen = null;
        for (const image of images) {
            const item = el("label", "lab-extract-pick");
            const radio = document.createElement("input");
            radio.type = "radio";
            radio.name = "labExtractImage";
            radio.value = String(image.imageID);
            const thumb = document.createElement("img");
            thumb.loading = "lazy";
            thumb.alt = "تصویر";
            thumb.src = `/api/radiologyimages/${image.imageID}`;
            thumb.addEventListener("click", () => { radio.checked = true; chosen = image; });
            radio.addEventListener("change", () => { chosen = image; });
            item.append(radio, thumb);
            grid.appendChild(item);
        }
        body.appendChild(grid);

        const runBtn = el("button", "primary-button", "استخراج با هوش مصنوعی");
        runBtn.type = "button";
        runBtn.addEventListener("click", () => {
            if (!chosen) { setStatus(overlay, "ابتدا یک تصویر انتخاب کنید.", true); return; }
            renderReview(body, studyID, chosen.imageID);
        });
        body.appendChild(runBtn);
    }

    // Step 2: ask the AI for a draft and show it for review.
    async function renderReview(body, studyID, imageID) {
        if (busy) return;
        busy = true;
        body.replaceChildren(el("div", "factors-empty", "در حال استخراج برگه آزمایش... این مرحله ممکن است کمی طول بکشد."));
        let x;
        try {
            const r = await fetch(`/api/ai/images/${imageID}/extract-lab?studyID=${studyID}`, { method: "POST" });
            x = await readJson(r);
            if (!r.ok || !x.success) throw new Error(x.message || "استخراج ناموفق بود.");
        } catch (e) {
            body.replaceChildren(el("div", "factors-empty", e.message || "استخراج ناموفق بود."));
            busy = false;
            return;
        }
        busy = false;

        const defsX = await fetch(`/api/factors/definitions?studyID=${studyID}`, { cache: "no-store" }).then(readJson);
        const defs = defsX.factors || [];

        body.replaceChildren();
        const info = el("div", "factors-empty",
            `${x.labName ? "آزمایشگاه: " + x.labName + " - " : ""}${x.sampleDateRaw ? "تاریخ نمونه: " + x.sampleDateRaw : ""}`);
        body.appendChild(info);

        const table = el("table", "lab-extract-table");
        const thead = el("thead");
        const hr = el("tr");
        ["", "نام تست (چاپ‌شده)", "مقدار", "واحد", "بازه چاپ‌شده", "فاکتورِ متناظر"].forEach(t => hr.appendChild(el("th", null, t)));
        thead.appendChild(hr);
        table.appendChild(thead);

        const tbody = el("tbody");
        for (const item of (x.items || [])) {
            const tr = el("tr");
            const include = document.createElement("input");
            include.type = "checkbox";
            // Every readable row is pre-selected: the doctor unchecks what is not
            // wanted instead of hunting for what the AI happened to tick.
            include.checked = String(item.value ?? "").trim() !== "";
            tr.appendChild(tdOf(include));

            tr.appendChild(el("td", null, item.name || ""));
            tr.appendChild(el("td", null, item.value ?? ""));
            tr.appendChild(el("td", null, item.unit || ""));
            tr.appendChild(el("td", null, item.refText || ""));

            const mapCell = el("td");
            if (item.factorID) {
                mapCell.appendChild(el("span", null,
                    `${item.factorNameFa}${item.factorShortCode ? " (" + item.factorShortCode + ")" : ""} (${item.matchConfidence}%)`));
            } else {
                const select = document.createElement("select");
                select.appendChild(new Option("— انتخاب فاکتور —", ""));
                for (const d of defs) select.appendChild(new Option(d.nameFa + (d.shortCode ? ` (${d.shortCode})` : ""), String(d.factorID)));
                mapCell.appendChild(select);
                // A printed test the dictionary does not know is added to it,
                // with the sheet itself as the source of its identity.
                const addBtn = el("button", "secondary-button lab-extract-add", "افزودن به دیکشنری");
                addBtn.type = "button";
                addBtn.addEventListener("click", () => addUnknownFactor(tr, studyID, mapCell, addBtn));
                mapCell.appendChild(addBtn);
            }
            tr.appendChild(mapCell);
            tr.dataset.factorId = item.factorID ? String(item.factorID) : "";
            tr.dataset.value = item.value ?? "";
            tr.dataset.unit = item.unit || "";
            tr.dataset.name = item.name || "";
            tr.dataset.nameFa = item.nameFa || "";
            tr.dataset.refLow = item.refLow ?? "";
            tr.dataset.refHigh = item.refHigh ?? "";
            tr.dataset.refText = item.refText || "";
            tr.dataset.confidence = String(item.matchConfidence || 0);
            tbody.appendChild(tr);
        }
        table.appendChild(tbody);
        body.appendChild(table);

        const foot = overlay.querySelector(".lab-extract-foot");
        const confirmBtn = el("button", "primary-button", "ثبت موارد تأییدشده");
        confirmBtn.type = "button";
        confirmBtn.addEventListener("click", () => confirmRows(body, studyID, x.extractionID, confirmBtn));
        foot.insertBefore(confirmBtn, foot.firstChild);
    }

    function tdOf(child) {
        const td = el("td");
        td.appendChild(child);
        return td;
    }

    // Printed values may carry Persian digits, thousands separators or a
    // "<"/">" mark; all of those must survive into a number instead of the row
    // being silently dropped.
    function normalizeDigits(s) {
        return String(s ?? "")
            .replace(/[\u06F0-\u06F9]/g, c => String(c.charCodeAt(0) - 0x06F0))
            .replace(/[\u0660-\u0669]/g, c => String(c.charCodeAt(0) - 0x0660));
    }

    function parsePrintedNumber(raw) {
        let s = normalizeDigits(raw).trim();
        s = s.replace(/[٬,\s]/g, "").replace(/^[<>≤≥]+/, "").replace(/%$/, "");
        if (s === "") return null;
        const n = Number(s);
        return Number.isFinite(n) ? n : null;
    }

    // A printed test the dictionary does not know becomes a dictionary row
    // (pending specialist review, no guessed LOINC code) instead of being lost.
    // The lab sheet itself is the source of the new factor's identity.
    async function createFactorFor(tr, studyID) {
        const raw = (tr.dataset.value || "").trim();
        const body = {
            nameEn: tr.dataset.name || "",
            nameFa: tr.dataset.nameFa || tr.dataset.name || "",
            unitUCUM: tr.dataset.unit || null,
            refText: tr.dataset.refText || null,
            dataType: parsePrintedNumber(raw) === null ? 4 : 1,
            studyID
        };
        for (const key of ["refLow", "refHigh"]) {
            const v = tr.dataset[key];
            if (!v) continue;
            const n = Number(normalizeDigits(v));
            if (Number.isFinite(n)) body[key] = n;
        }
        const r = await fetch("/api/factors/definitions", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(body)
        });
        const x = await readJson(r);
        if (!r.ok || !x.success) throw new Error(x.message || "افزودن تست به دیکشنری ناموفق بود.");
        return x.factor || {};
    }

    async function addUnknownFactor(tr, studyID, mapCell, button) {
        if (busy) return;
        busy = true;
        button.disabled = true;
        button.textContent = "در حال افزودن...";
        try {
            const f = await createFactorFor(tr, studyID);
            tr.dataset.factorId = String(f.factorID);
            mapCell.replaceChildren(el("span", null,
                `${f.nameFa}${f.shortCode ? " (" + f.shortCode + ")" : ""} — به دیکشنری افزوده شد`));
            const include = tr.querySelector('input[type="checkbox"]');
            if (include) include.checked = true;
        } catch (e) {
            setStatus(overlay, e.message || "افزودن تست به دیکشنری ناموفق بود.", true);
            button.disabled = false;
            button.textContent = "افزودن به دیکشنری";
        } finally {
            busy = false;
        }
    }

    // Step 3: store the confirmed rows through the factors API (source = 2).
    async function confirmRows(body, studyID, extractionID, button) {
        if (busy) return;
        const defsX = await fetch(`/api/factors/definitions?studyID=${studyID}`, { cache: "no-store" }).then(readJson);
        const defs = defsX.factors || [];
        const byId = new Map(defs.map(d => [d.factorID, d]));

        const items = [];
        const skipped = [];
        const added = [];
        for (const tr of body.querySelectorAll(".lab-extract-table tbody tr")) {
            const include = tr.querySelector('input[type="checkbox"]');
            if (!include || !include.checked) continue;
            const printedName = (tr.cells[1]?.textContent || "").trim();
            let factorID = Number(tr.dataset.factorId || 0);
            const select = tr.querySelector("select");
            if (select && select.value) factorID = Number(select.value);
            // A ticked row is never dropped in silence: an unknown test is added
            // to the dictionary, and anything unsaved is reported with its reason.
            if (!factorID) {
                try {
                    const created = await createFactorFor(tr, studyID);
                    factorID = Number(created.factorID);
                    if (factorID) {
                        tr.dataset.factorId = String(factorID);
                        byId.set(factorID, created);
                        added.push(created.nameFa + (created.shortCode ? ` (${created.shortCode})` : ""));
                    }
                } catch (e) {
                    skipped.push(`${printedName}: ${e.message || "افزودن به دیکشنری ناموفق بود"}`);
                    continue;
                }
            }
            if (!factorID) { skipped.push(`${printedName}: فاکتور انتخاب نشده`); continue; }

            const f = byId.get(factorID);
            const raw = (tr.dataset.value || "").trim();
            if (raw === "") { skipped.push(`${printedName}: مقدار خالی`); continue; }
            const item = { factorID, source: 2, extractionID, confidence: Number(tr.dataset.confidence || 0) };
            if (f && f.dataType === 1) {
                const n = parsePrintedNumber(raw);
                if (n === null) { skipped.push(`${printedName}: مقدار عددی خوانده نشد`); continue; }
                item.valueNumber = n;
            } else if (f && f.dataType === 3) {
                item.valueBit = /^(yes|بله|positive|pos|1)/i.test(normalizeDigits(raw));
            } else if (f && f.dataType === 2) {
                let options = [];
                try { options = JSON.parse(f.optionsJson || "[]"); } catch { }
                const rawN = normalizeDigits(raw).trim().toLowerCase();
                const hit = options.find(o => normalizeDigits(o.t).trim().toLowerCase() === rawN);
                if (!hit) { skipped.push(`${printedName}: گزینهٔ مناسب پیدا نشد`); continue; }
                item.valueNumber = Number(hit.v);
            } else if (f && f.dataType === 5) {
                item.valueDate = raw;
            } else {
                item.valueText = raw;
            }
            items.push(item);
        }

        if (items.length === 0) {
            setStatus(overlay, skipped.length
                ? `هیچ موردی ثبت نشد: ${skipped.slice(0, 3).join("؛ ")}${skipped.length > 3 ? "؛ ..." : ""}`
                : "هیچ ردیفی برای ثبت انتخاب نشده است.", true);
            return;
        }

        try {
            busy = true;
            button.disabled = true;
            button.textContent = "در حال ثبت...";
            setStatus(overlay, "", false);
            const r = await fetch("/api/factors/values", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ studyID, items })
            });
            const x = await readJson(r);
            if (!r.ok || !x.success) throw new Error(x.message || "ثبت مقادیر ناموفق بود.");
            let msg = `${x.saved} مقدار از برگه آزمایش ثبت شد.`;
            if (added.length) msg += ` ${added.length} تست جدید به دیکشنری افزوده شد.`;
            if (skipped.length) {
                msg += ` ${skipped.length} مورد ثبت نشد: ${skipped.slice(0, 3).join("؛ ")}${skipped.length > 3 ? "؛ ..." : ""}`;
            }
            setStatus(overlay, msg, skipped.length > 0);
            window.ReSiRaiFactors?.render({ studyID });
            setTimeout(close, 900);
        } catch (e) {
            setStatus(overlay, e.message || "ثبت مقادیر ناموفق بود.", true);
        } finally {
            busy = false;
            button.disabled = false;
            button.textContent = "ثبت موارد تأییدشده";
        }
    }

    window.ReSiRaiLabExtract = { open };
})();
