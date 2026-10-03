// ReSiRai — history of one clinical factor, and the patient's factor ledger.
//
// Two related views, both opened from the visit/patient screen:
//   * openFactor(factor): every recorded value of ONE factor for this patient,
//     newest first, as a table plus a line chart (a history of Hb, for example).
//   * openReport(): the whole ledger — rows are factors, columns are the
//     measurement dates, and each crossing cell carries that day's value + unit.
//     Printable, so a doctor can hand the sheet to the patient.
//
// Every point keeps its own date, unit and printed reference range: ranges and
// units differ between labs, so values are shown side by side, never converted.
(function () {
    "use strict";

    const esc = value => { const d = document.createElement("div"); d.textContent = value ?? ""; return d.innerHTML; };
    const numFmt = value => Number(value).toLocaleString("fa-IR", { maximumFractionDigits: 4 });

    const SOURCE_LABELS = { 1: "دستی", 2: "استخراج برگه آزمایش", 3: "دستگاه", 4: "محاسبه‌شده" };

    function jalali(value) {
        if (!value) return "—";
        try {
            return new Intl.DateTimeFormat("fa-IR-u-ca-persian", { year: "numeric", month: "2-digit", day: "2-digit" })
                .format(new Date(value));
        } catch { return String(value).slice(0, 10); }
    }

    function valueText(point) {
        if (point.valueText !== null && point.valueText !== undefined && point.valueText !== "") return point.valueText;
        if (point.valueNumber !== null && point.valueNumber !== undefined) return numFmt(point.valueNumber);
        if (point.valueBit === true) return "بله";
        if (point.valueBit === false) return "خیر";
        if (point.valueDate) return jalali(point.valueDate);
        return "—";
    }

    async function readJson(r) { try { return await r.json(); } catch { return { success: false }; } }

    // ---- مودال پایه ---------------------------------------------------------
    function modal(title, subtitle) {
        closeModal();
        const overlay = document.createElement("div");
        overlay.id = "factorsHistoryOverlay";
        overlay.className = "factors-modal-overlay";
        overlay.innerHTML = `
          <div class="factors-modal" role="dialog" aria-modal="true" aria-label="${esc(title)}">
            <div class="factors-modal-head">
              <div><h2>${esc(title)}</h2>${subtitle ? `<p>${esc(subtitle)}</p>` : ""}</div>
              <div class="factors-modal-actions">
                <button type="button" class="secondary-button" data-role="print">چاپ</button>
                <button type="button" class="secondary-button" data-role="close" aria-label="بستن">بستن</button>
              </div>
            </div>
            <div class="factors-modal-body"></div>
          </div>`;
        document.body.appendChild(overlay);
        overlay.querySelector('[data-role="close"]').addEventListener("click", closeModal);
        overlay.querySelector('[data-role="print"]').addEventListener("click", () => window.print());
        overlay.addEventListener("click", e => { if (e.target === overlay) closeModal(); });
        document.addEventListener("keydown", escClose);
        return overlay.querySelector(".factors-modal-body");
    }

    function escClose(e) { if (e.key === "Escape") closeModal(); }

    function closeModal() {
        document.getElementById("factorsHistoryOverlay")?.remove();
        document.removeEventListener("keydown", escClose);
    }

    // ---- نمودار خطی ---------------------------------------------------------
    // A small dependency-free line chart. It draws the numeric series (newest on
    // the right) with the reference band, so the trend is read at a glance.
    function lineChart(points, factor) {
        const numeric = points.filter(p => p.valueNumber !== null && p.valueNumber !== undefined);
        if (numeric.length < 2) {
            const note = document.createElement("p");
            note.className = "field-hint";
            note.textContent = "برای رسم نمودار حداقل دو مقدار عددی لازم است.";
            return note;
        }
        const ordered = numeric.slice().sort((a, b) => new Date(a.observedAt) - new Date(b.observedAt));
        const W = 720, H = 240, padL = 46, padR = 16, padT = 16, padB = 34;
        const values = ordered.map(p => Number(p.valueNumber));
        let min = Math.min(...values), max = Math.max(...values);
        if (factor.refLow !== null && factor.refLow !== undefined) min = Math.min(min, Number(factor.refLow));
        if (factor.refHigh !== null && factor.refHigh !== undefined) max = Math.max(max, Number(factor.refHigh));
        if (min === max) { min -= 1; max += 1; }
        const span = max - min;
        min -= span * 0.08; max += span * 0.08;

        const x = i => padL + (ordered.length === 1 ? 0 : (i * (W - padL - padR)) / (ordered.length - 1));
        const y = v => padT + (H - padT - padB) * (1 - (v - min) / (max - min));

        const ns = "http://www.w3.org/2000/svg";
        const svg = document.createElementNS(ns, "svg");
        svg.setAttribute("viewBox", `0 0 ${W} ${H}`);
        svg.setAttribute("class", "factor-chart");
        svg.setAttribute("preserveAspectRatio", "none");

        // Reference band.
        if (factor.refLow !== null && factor.refLow !== undefined && factor.refHigh !== null && factor.refHigh !== undefined
            && Number(factor.refHigh) >= Number(factor.refLow)) {
            const rect = document.createElementNS(ns, "rect");
            rect.setAttribute("x", padL); rect.setAttribute("y", y(Number(factor.refHigh)));
            rect.setAttribute("width", W - padL - padR);
            rect.setAttribute("height", Math.max(0, y(Number(factor.refLow)) - y(Number(factor.refHigh))));
            rect.setAttribute("class", "factor-chart-band");
            svg.appendChild(rect);
        }

        // Y axis labels (min / mid / max).
        for (const v of [min, (min + max) / 2, max]) {
            const label = document.createElementNS(ns, "text");
            label.setAttribute("x", padL - 6); label.setAttribute("y", y(v) + 4);
            label.setAttribute("text-anchor", "end"); label.setAttribute("class", "factor-chart-label");
            label.textContent = numFmt(Math.round(v * 100) / 100);
            svg.appendChild(label);
        }

        // The line and its points.
        const poly = document.createElementNS(ns, "polyline");
        poly.setAttribute("points", ordered.map((p, i) => `${x(i)},${y(Number(p.valueNumber))}`).join(" "));
        poly.setAttribute("class", "factor-chart-line");
        svg.appendChild(poly);

        ordered.forEach((p, i) => {
            const cx = x(i), cy = y(Number(p.valueNumber));
            const dot = document.createElementNS(ns, "circle");
            dot.setAttribute("cx", cx); dot.setAttribute("cy", cy); dot.setAttribute("r", 4);
            dot.setAttribute("class", "factor-chart-dot");
            const title = document.createElementNS(ns, "title");
            title.textContent = `${jalali(p.observedAt)} — ${numFmt(p.valueNumber)} ${p.unitText || factor.unitUCUM || ""}`.trim();
            dot.appendChild(title);
            svg.appendChild(dot);
            // The date under the first and last point only, to avoid clutter.
            if (i === 0 || i === ordered.length - 1) {
                const t = document.createElementNS(ns, "text");
                t.setAttribute("x", cx); t.setAttribute("y", H - 12);
                t.setAttribute("text-anchor", i === 0 ? "start" : "end");
                t.setAttribute("class", "factor-chart-label");
                t.textContent = jalali(p.observedAt);
                svg.appendChild(t);
            }
        });

        const figure = document.createElement("figure");
        figure.className = "factor-chart-figure";
        figure.appendChild(svg);
        return figure;
    }

    function pointsTable(points, factor) {
        const wrap = document.createElement("div");
        wrap.className = "table-container";
        const table = document.createElement("table");
        table.className = "patient-table factor-history-table";
        table.innerHTML = `<thead><tr>
            <th>تاریخ</th><th>مقدار</th><th>واحد</th><th>بازهٔ مرجع</th><th>وضعیت</th><th>منبع</th>
          </tr></thead><tbody></tbody>`;
        const body = table.querySelector("tbody");
        // Newest first, as a clinician reads a chart.
        for (const p of points.slice().sort((a, b) => new Date(b.observedAt) - new Date(a.observedAt))) {
            const tr = document.createElement("tr");
            const low = Number(factor.refLow), high = Number(factor.refHigh);
            const n = Number(p.valueNumber);
            let abnormal = false;
            if (Number.isFinite(n)) {
                if (Number.isFinite(low) && n < low) abnormal = true;
                if (Number.isFinite(high) && n > high) abnormal = true;
            }
            if (abnormal) tr.className = "abnormal";
            const unit = p.unitText || factor.unitUCUM || "—";
            const ref = p.refText || (Number.isFinite(low) && Number.isFinite(high) ? `${numFmt(low)} تا ${numFmt(high)}`
                : Number.isFinite(high) ? `کمتر از ${numFmt(high)}` : Number.isFinite(low) ? `بیشتر از ${numFmt(low)}` : "—");
            tr.innerHTML = `<td>${esc(jalali(p.observedAt))}</td>
                <td><strong>${esc(valueText(p))}</strong></td>
                <td>${esc(unit)}</td>
                <td>${esc(ref)}</td>
                <td>${abnormal ? '<span class="factor-flag-inline">خارج از بازه</span>' : "طبیعی"}</td>
                <td>${esc(SOURCE_LABELS[p.source] || "—")}</td>`;
            body.appendChild(tr);
        }
        wrap.appendChild(table);
        return wrap;
    }

    // ---- تاریخچهٔ یک فاکتور --------------------------------------------------
    // target: { studyID } for a saved visit, or { patientID } for a new-visit draft.
    async function openFactor(factorID, target) {
        if (!factorID) return;
        const studyID = Number(target?.studyID) || 0;
        const patientID = Number(target?.patientID) || 0;
        if (!studyID && !patientID) return;
        const query = studyID ? `studyID=${studyID}` : `patientID=${patientID}`;
        const body = modal("سابقهٔ مقدار فاکتور", "در حال دریافت...");
        body.innerHTML = '<p class="field-hint">در حال دریافت سابقه...</p>';
        try {
            const r = await fetch(`/api/factors/history?${query}&factorID=${factorID}`, { cache: "no-store" });
            const x = await readJson(r);
            if (!r.ok || !x.success) throw new Error(x.message || "دریافت سابقه ناموفق بود.");
            const factor = x.factor || {};
            const head = document.querySelector("#factorsHistoryOverlay .factors-modal-head p");
            if (head) head.textContent = `${factor.nameFa || ""}${factor.shortCode ? ` (${factor.shortCode})` : ""} — ${x.count} مقدار در ${new Set((x.points || []).map(p => String(p.observedAt).slice(0, 10))).size} تاریخ`;
            body.replaceChildren();
            if (!x.points || x.points.length === 0) {
                body.innerHTML = '<p class="field-hint">برای این فاکتور مقداری ثبت نشده است.</p>';
                return;
            }
            body.appendChild(lineChart(x.points, factor));
            body.appendChild(pointsTable(x.points, factor));
        } catch (e) {
            body.innerHTML = `<p class="field-hint error">${esc(e.message || "دریافت سابقه ناموفق بود.")}</p>`;
        }
    }

    // ---- گزارش ماتریسی بیمار ------------------------------------------------
    // Rows: factors. Columns: measurement dates. Cell: value + unit.
    async function openReport() {
        const patientID = Number(window.selectedPatientID) || 0;
        if (!patientID) { window.showToast?.("ابتدا یک بیمار را باز کنید.", "error"); return; }
        const body = modal("روند فاکتورهای بیمار", "در حال محاسبه...");
        body.innerHTML = '<p class="field-hint">در حال محاسبهٔ جدول...</p>';
        try {
            const r = await fetch(`/api/factors/matrix?patientID=${patientID}`, { cache: "no-store" });
            const x = await readJson(r);
            if (!r.ok || !x.success) throw new Error(x.message || "دریافت گزارش ناموفق بود.");
            body.replaceChildren();

            const allDates = x.dates || [];
            if (allDates.length === 0 || !(x.factors || []).length) {
                body.innerHTML = '<p class="field-hint">برای این بیمار فاکتوری ثبت نشده است.</p>';
                return;
            }
            const head = document.querySelector("#factorsHistoryOverlay .factors-modal-head p");
            if (head) head.textContent = `${x.factors.length} فاکتور در ${allDates.length} تاریخ — بیمار: ${window.selectedPatient ? `${window.selectedPatient.firstName || ""} ${window.selectedPatient.lastName || ""}`.trim() : ""}`;
            // Newest dates first; the last 10 by default, the rest behind a button.
            const ordered = allDates.slice().sort().reverse();
            const state = { limit: 10 };
            const render = () => {
                const shown = ordered.slice(0, state.limit);
                const wrap = document.createElement("div");
                wrap.className = "table-container factor-matrix-scroll";
                const table = document.createElement("table");
                table.className = "patient-table factor-matrix";
                const head = document.createElement("thead");
                const hr = document.createElement("tr");
                hr.innerHTML = `<th class="factor-matrix-corner">فاکتور سنجیده‌شده</th>` +
                    shown.map(d => `<th>${esc(jalali(d))}</th>`).join("");
                head.appendChild(hr);
                const tbody = document.createElement("tbody");
                for (const f of x.factors) {
                    const byDate = new Map((f.cells || []).filter(c => c.value !== null || c.valueNumber !== null || c.valueText).map(c => [c.date, c]));
                    const tr = document.createElement("tr");
                    const nameCell = document.createElement("th");
                    nameCell.className = "factor-matrix-name";
                    nameCell.innerHTML = `${esc(f.nameFa)}${f.shortCode ? ` <small>(${esc(f.shortCode)})</small>` : ""}` +
                        (f.unitUCUM ? ` <small class="factor-matrix-unit">${esc(f.unitUCUM)}</small>` : "");
                    tr.appendChild(nameCell);
                    for (const d of shown) {
                        const cell = byDate.get(d);
                        const td = document.createElement("td");
                        if (!cell) { td.textContent = "—"; td.className = "factor-matrix-empty"; }
                        else {
                            const val = cell.valueText ?? (cell.valueNumber !== null && cell.valueNumber !== undefined ? numFmt(cell.valueNumber) : "—");
                            const low = Number(f.refLow), high = Number(f.refHigh), n = Number(cell.valueNumber);
                            const abnormal = Number.isFinite(n) && ((Number.isFinite(low) && n < low) || (Number.isFinite(high) && n > high));
                            if (abnormal) td.className = "abnormal";
                            td.innerHTML = `<strong>${esc(val)}</strong>${cell.unit ? `<small> ${esc(cell.unit)}</small>` : ""}` +
                                (cell.source === 4 ? ' <small class="factor-matrix-computed">محاسبه‌شده</small>' : "");
                        }
                        tr.appendChild(td);
                    }
                    tbody.appendChild(tr);
                }
                table.append(head, tbody);
                wrap.appendChild(table);
                body.replaceChildren(wrap);
                if (ordered.length > state.limit) {
                    const more = document.createElement("button");
                    more.type = "button";
                    more.className = "secondary-button factor-matrix-more";
                    more.textContent = `نمایش ${ordered.length - state.limit} تاریخ قدیمی‌تر`;
                    more.addEventListener("click", () => { state.limit = ordered.length; render(); });
                    body.appendChild(more);
                }
                const note = document.createElement("p");
                note.className = "field-hint";
                note.textContent = "هر خانه، آخرین مقدار ثبت‌شدهٔ آن فاکتور در آن تاریخ است. واحد از برگهٔ همان آزمایش می‌آید و ممکن است در طول زمان تغییر کند.";
                body.appendChild(note);
            };
            render();
        } catch (e) {
            body.innerHTML = `<p class="field-hint error">${esc(e.message || "دریافت گزارش ناموفق بود.")}</p>`;
        }
    }

    // ---- اتصال دکمه‌ها --------------------------------------------------------
    function attach() {
        document.getElementById("patientFactorsReportButton")
            ?.addEventListener("click", openReport);
    }
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", attach);
    else attach();

    window.ReSiRaiFactorsHistory = { openFactor, openReport, close: closeModal };
})();
