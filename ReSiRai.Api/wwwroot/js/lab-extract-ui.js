// ReSiRai - lab-report extraction review.
//
// The doctor picks the pages of a printed lab report among the visit's images
// (a report is often several pages; the pages are sent to the AI together and
// can also be photographed right inside this dialog), the AI returns a draft
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
        studyID = Number(studyID) || 0;

        overlay = el("div", "lab-extract-overlay");
        const dialog = el("div", "lab-extract-dialog");
        const head = el("div", "lab-extract-head");
        head.appendChild(el("h3", null, studyID > 0
            ? "استخراج برگه آزمایش"
            : "استخراج برگه آزمایش — پیش از ثبت مراجعه"));
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

    // The new-visit form: the pages are picked from this device and read
    // straight away - nothing is attached to a visit that does not exist yet.
    function renderFilePicker(body) {
        body.replaceChildren();
        body.appendChild(el("div", "factors-empty",
            "عکس صفحه‌های برگه آزمایش را انتخاب کنید (چند فایل با هم):"));
        const fileInput = document.createElement("input");
        fileInput.type = "file";
        fileInput.accept = "image/*";
        fileInput.multiple = true;
        const runBtn = el("button", "primary-button", "استخراج با هوش مصنوعی");
        runBtn.type = "button";
        runBtn.addEventListener("click", () => {
            const files = Array.from(fileInput.files || []);
            if (files.length === 0) { setStatus(overlay, "ابتدا دست‌کم یک تصویر انتخاب کنید.", true); return; }
            renderReviewFiles(body, files);
        });
        body.append(fileInput, runBtn);
    }

    // Step 1: pick the report pages among the visit's images. A multi-page report
    // is selected as several images (or uploaded right here) and all pages go to
    // the AI together, so nothing is lost at a page break. Before the visit row
    // exists (new-visit form), the pages are read straight from the picked files.
    async function renderPicker(body, studyID) {
        if (!studyID) { renderFilePicker(body); return; }
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
        body.appendChild(el("div", "factors-empty",
            "صفحه‌های برگه آزمایش را انتخاب کنید — برای برگهٔ چندصفحه‌ای چند تصویر را با هم تیک بزنید:"));
        const grid = el("div", "lab-extract-grid");
        body.appendChild(grid);

        const drawGrid = () => {
            grid.replaceChildren();
            for (const image of images) {
                const item = el("label", "lab-extract-pick");
                const box = document.createElement("input");
                box.type = "checkbox";
                box.name = "labExtractImage";
                box.value = String(image.imageID);
                const thumb = document.createElement("img");
                thumb.loading = "lazy";
                thumb.alt = "تصویر";
                thumb.src = `/api/radiologyimages/${image.imageID}`;
                thumb.addEventListener("click", () => { box.checked = !box.checked; });
                item.append(box, thumb);
                grid.appendChild(item);
            }
        };
        drawGrid();

        // Pages not attached yet can be photographed/added right here.
        const fileInput = document.createElement("input");
        fileInput.type = "file";
        fileInput.accept = "image/*";
        fileInput.multiple = true;
        fileInput.style.display = "none";
        const uploadBtn = el("button", "secondary-button", "افزودن تصویر جدید (عکس برگه)");
        uploadBtn.type = "button";
        uploadBtn.addEventListener("click", () => fileInput.click());
        fileInput.addEventListener("change", async () => {
            const files = Array.from(fileInput.files || []);
            fileInput.value = "";
            if (files.length === 0) return;
            uploadBtn.disabled = true;
            uploadBtn.textContent = "در حال آپلود...";
            let added = 0;
            for (const f of files) {
                const fd = new FormData();
                fd.append("file", f);
                try {
                    const r = await fetch(`/api/radiologyimages?studyID=${studyID}`, { method: "POST", body: fd });
                    const rx = await readJson(r);
                    if (r.ok && rx.success) {
                        images.push({ imageID: rx.imageID });
                        added++;
                    }
                } catch { }
            }
            uploadBtn.disabled = false;
            uploadBtn.textContent = "افزودن تصویر جدید (عکس برگه)";
            drawGrid();
            if (added) setStatus(overlay, `${added} تصویر اضافه شد؛ صفحه‌های برگه را تیک بزنید.`, false);
        });
        body.appendChild(uploadBtn);
        body.appendChild(fileInput);

        const runBtn = el("button", "primary-button", "استخراج با هوش مصنوعی");
        runBtn.type = "button";
        runBtn.addEventListener("click", () => {
            const chosen = Array.from(grid.querySelectorAll('input[type="checkbox"]:checked'))
                .map(b => Number(b.value));
            if (chosen.length === 0) { setStatus(overlay, "ابتدا دست‌کم یک تصویر انتخاب کنید.", true); return; }
            renderReview(body, studyID, chosen);
        });
        body.appendChild(runBtn);
    }

    // Step 2: ask the AI for a draft over all selected pages and show it for review.
    async function renderReview(body, studyID, imageIDs) {
        await runExtraction(body, studyID, () => fetch(`/api/ai/images/extract-lab`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ studyID, imageIDs })
        }));
    }

    // The same, but the pages are files on this device (new-visit form: the
    // visit row does not exist yet, so nothing is attached anywhere).
    async function renderReviewFiles(body, files) {
        const fd = new FormData();
        for (const f of files) fd.append("files", f);
        await runExtraction(body, 0, () => fetch(`/api/ai/images/extract-lab-files`, { method: "POST", body: fd }));
    }

    async function runExtraction(body, studyID, send) {
        if (busy) return;
        busy = true;
        body.replaceChildren(el("div", "factors-empty", "در حال استخراج برگه آزمایش... این مرحله ممکن است کمی طول بکشد."));
        let x;
        try {
            const r = await send();
            x = await readJson(r);
            if (!r.ok || !x.success) throw new Error(x.message || "استخراج ناموفق بود.");
        } catch (e) {
            body.replaceChildren(el("div", "factors-empty", e.message || "استخراج ناموفق بود."));
            busy = false;
            return;
        }
        busy = false;

        const defsX = await fetch(studyID
            ? `/api/factors/definitions?studyID=${studyID}`
            : `/api/factors/definitions`, { cache: "no-store" }).then(readJson);
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

    // The count of saved and unsaved data with the reason for every unsaved row
    // - shown after saving and kept until the doctor closes the dialog.
    function showSummary(body, savedCount, totalTicked, addedCount, skipped) {
        body.querySelector(".lab-extract-summary")?.remove();
        const box = el("div", "lab-extract-summary");
        box.appendChild(el("div", savedCount > 0 ? "sum-ok" : "sum-warn",
            savedCount > 0
                ? `✓ ${savedCount} مقدار ثبت شد${totalTicked > savedCount ? ` از ${totalTicked} ردیف تأییدشده` : ""}.`
                : "هیچ مقداری ثبت نشد."));
        if (addedCount) box.appendChild(el("div", "sum-ok", `➕ ${addedCount} تست جدید به دیکشنری افزوده شد.`));
        if (skipped.length) {
            box.appendChild(el("div", "sum-warn", `⚠️ ${skipped.length} مورد ثبت نشد:`));
            for (const s of skipped) box.appendChild(el("div", "sum-item", "• " + s));
            box.appendChild(el("div", "sum-hint", "ردیف‌های قرمز را اصلاح کنید و دوباره «ثبت موارد تأییدشده» را بزنید."));
        }
        body.insertBefore(box, body.firstChild);
        box.scrollIntoView({ block: "nearest" });
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
        // Each unsaved row is tagged in the table itself, so the eye finds it.
        const mark = (tr, reason) => {
            skipped.push(reason);
            if (tr && tr.classList) { tr.classList.add("is-skipped"); tr.title = reason; }
        };
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
                    mark(tr, `${printedName}: ${e.message || "افزودن به دیکشنری ناموفق بود"}`);
                    continue;
                }
            }
            if (!factorID) { mark(tr, `${printedName}: فاکتور انتخاب نشده`); continue; }

            const f = byId.get(factorID);
            const raw = (tr.dataset.value || "").trim();
            if (raw === "") { mark(tr, `${printedName}: مقدار خالی`); continue; }
            const item = { factorID, source: 2, extractionID, confidence: Number(tr.dataset.confidence || 0) };
            if (f && f.dataType === 1) {
                const n = parsePrintedNumber(raw);
                if (n === null) { mark(tr, `${printedName}: مقدار عددی خوانده نشد`); continue; }
                item.valueNumber = n;
            } else if (f && f.dataType === 3) {
                item.valueBit = /^(yes|بله|positive|pos|1)/i.test(normalizeDigits(raw));
            } else if (f && f.dataType === 2) {
                let options = [];
                try { options = JSON.parse(f.optionsJson || "[]"); } catch { }
                const rawN = normalizeDigits(raw).trim().toLowerCase();
                const hit = options.find(o => normalizeDigits(o.t).trim().toLowerCase() === rawN);
                if (!hit) { mark(tr, `${printedName}: گزینهٔ مناسب پیدا نشد`); continue; }
                item.valueNumber = Number(hit.v);
            } else if (f && f.dataType === 5) {
                item.valueDate = raw;
            } else {
                item.valueText = raw;
            }
            items.push(item);
            tr.dataset.picked = "1";
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
            let savedCount = 0;
            if (!studyID) {
                // New-visit form: the values are held in the panel and saved the
                // moment the visit row is created.
                window.ReSiRaiFactors?.addPending(items);
                savedCount = items.length;
            } else {
                const r = await fetch("/api/factors/values", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ studyID, items })
                });
                const x = await readJson(r);
                if (!r.ok || !x.success) throw new Error(x.message || "ثبت مقادیر ناموفق بود.");
                savedCount = Number(x.saved) || items.length;
                window.ReSiRaiFactors?.render({ studyID });
            }
            // A summary that stays on the screen: how many rows made it, how many
            // did not, and why. The doctor must not depend on a vanishing toast.
            for (const tr of body.querySelectorAll(".lab-extract-table tbody tr"))
                if (tr.dataset.picked === "1") tr.classList.add("is-saved");
            showSummary(body, savedCount, items.length + skipped.length, added.length, skipped);
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
