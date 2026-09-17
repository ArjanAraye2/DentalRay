// ============================================================
// DentalRay Frontend - Study Delete + Study Dental Chart bootstrap
// ============================================================
// Delete policy:
// - Shared images are never deleted with a Study.
// - Images used only by this Study can be kept in Patient Images
//   or selected for physical deletion.
//
// Dental Chart:
// - The chart is injected into New Study and Edit Study without
//   disturbing the existing Study form code.
// - Permanent and primary FDI teeth can be selected.
// - Persistence is connected separately to the Study API.
// ============================================================

(function () {
    "use strict";

    function addDeleteButtonsToStudyCards() {
        document.querySelectorAll(".study-card").forEach(card => {
            if (card.querySelector(".study-delete-button")) return;

            const studyID = Number(card.dataset.studyId);
            if (!Number.isInteger(studyID) || studyID <= 0) return;

            const buttons = card.querySelector(".study-action-buttons");
            if (!buttons) return;

            const button = document.createElement("button");
            button.type = "button";
            button.className = "danger-button study-delete-button";
            button.textContent = "حذف Study";
            button.addEventListener("click", () => deleteStudyFromFrontend(studyID));
            buttons.appendChild(button);
        });
    }

    async function getDeletePreview(studyID) {
        const response = await fetch(`/api/radiologystudies/${studyID}/delete-preview`);
        const result = await response.json();
        if (!response.ok || !result.success) {
            throw new Error(getApiError(result, "اطلاعات لازم برای حذف Study دریافت نشد."));
        }
        return result;
    }

    async function chooseStudyOnlyImages(images) {
        if (!images || images.length === 0) return [];

        const names = images.map(x => `${x.imageID}: ${x.fileName}`).join("\n");
        const answer = window.prompt(
            "این تصاویر فقط به همین Study متصل هستند:\n\n" + names +
            "\n\nبرای نگهداری همه، کادر را خالی بگذارید.\n" +
            "برای حذف همه، all را وارد کنید.\n" +
            "برای حذف انتخابی، ImageIDها را با کاما جدا کنید."
        );

        if (answer === null || answer.trim() === "") return [];
        if (answer.trim().toLowerCase() === "all") return images.map(x => x.imageID);

        const allowed = new Set(images.map(x => Number(x.imageID)));
        const selected = answer.split(",")
            .map(x => Number(x.trim()))
            .filter(x => Number.isInteger(x) && allowed.has(x));
        return [...new Set(selected)];
    }

    async function deleteStudyFromFrontend(studyID) {
        try {
            const preview = await getDeletePreview(studyID);
            const sharedCount = preview.sharedImages?.length || 0;
            const studyOnly = preview.studyOnlyImages || [];

            let message = "آیا از حذف این Study مطمئن هستید؟";
            if (sharedCount > 0) {
                message += `\n\n${sharedCount} تصویر مشترک حذف نخواهد شد و برای Studyهای دیگر باقی می‌ماند.`;
            }
            if (studyOnly.length > 0) {
                message += `\n\n${studyOnly.length} تصویر فقط به همین Study متصل است. در مرحله بعد مشخص می‌کنید کدام فایل‌ها از Patient Images نیز حذف شوند.`;
            }

            const confirmed = await askConfirmation({
                title: "حذف Study",
                message,
                confirmText: "ادامه حذف",
                danger: true
            });
            if (!confirmed) return;

            const deleteIDs = await chooseStudyOnlyImages(studyOnly);
            const query = deleteIDs.map(id => `deleteImageIDs=${encodeURIComponent(id)}`).join("&");
            const url = `/api/radiologystudies/${studyID}${query ? `?${query}` : ""}`;
            const response = await fetch(url, { method: "DELETE" });
            const result = await response.json();

            if (!response.ok || !result.success) {
                throw new Error(getApiError(result, "حذف Study انجام نشد."));
            }

            if (result.cleanupErrors?.length) {
                showToast("Study حذف شد، اما پاک‌سازی یک یا چند فایل فیزیکی کامل نشد.", "warning", "هشدار پاک‌سازی");
            }
            else {
                showToast("Study با موفقیت حذف شد.", "success");
            }

            if (selectedPatientID) await openPatient(selectedPatientID);
        }
        catch (error) {
            console.error(error);
            showToast(error.message || "حذف Study انجام نشد.", "error");
        }
    }

    // ------------------------------------------------------------
    // Dental Chart bootstrap
    // ------------------------------------------------------------
    // index.html already loads this file after app.js. Loading the small
    // dental-chart component from here lets us add the chart safely without
    // replacing the large existing index.html file.
    function installDentalChartStyles() {
        if (document.getElementById("dentalChartStyles")) return;
        const style = document.createElement("style");
        style.id = "dentalChartStyles";
        style.textContent = `
            .study-dental-chart-field{grid-column:1/-1;margin-top:4px}
            .study-dental-chart-field>label{display:block;margin-bottom:8px;font-weight:bold}
            .dental-chart{padding:14px;border:1px solid #e2e8f0;border-radius:10px;background:#f8fafc;overflow-x:auto}
            .dental-chart-group+.dental-chart-group{margin-top:18px}
            .dental-chart-group-title{text-align:center;font-weight:bold;margin-bottom:8px;color:#475569}
            .dental-chart-row{display:flex;justify-content:center;gap:5px;min-width:max-content;margin:6px auto;direction:ltr}
            .tooth-button{width:46px;min-width:46px;padding:5px 3px;background:#fff;color:#334155;border:1px solid #cbd5e1;border-radius:9px;box-shadow:none}
            .tooth-button:hover:not(:disabled){transform:none;box-shadow:0 2px 7px rgba(15,23,42,.08)}
            .tooth-button.selected{background:#2563eb;color:#fff;border-color:#1d4ed8}
            .tooth-shape{display:block;font-size:23px;line-height:22px}
            .tooth-number{display:block;font-size:12px;font-weight:bold;direction:ltr}
            .dental-chart-hint{display:block;margin-top:7px;color:#64748b;font-size:12px}
            @media(max-width:700px){.tooth-button{width:40px;min-width:40px}.dental-chart{padding:10px}.dental-chart-row{justify-content:flex-start}}
        `;
        document.head.appendChild(style);
    }

    function createDentalChartField(containerId) {
        const field = document.createElement("div");
        field.className = "form-field full-width study-dental-chart-field";
        const label = document.createElement("label");
        label.textContent = "شمای دندان‌ها";
        const hint = document.createElement("small");
        hint.className = "dental-chart-hint";
        hint.textContent = "دندان‌های مربوط به این Study را انتخاب کنید. شماره‌گذاری بر اساس FDI است.";
        const chart = document.createElement("div");
        chart.id = containerId;
        field.append(label, chart, hint);
        return field;
    }

    function injectDentalCharts() {
        if (!window.DentalRayDentalChart) return;

        const newGrid = document.querySelector("#newStudyForm .form-grid");
        if (newGrid && !document.getElementById("newStudyDentalChart")) {
            const field = createDentalChartField("newStudyDentalChart");
            newGrid.appendChild(field);
            window.DentalRayDentalChart.render(field.querySelector("#newStudyDentalChart"), []);
        }

        const editGrid = document.querySelector("#editStudyForm .form-grid");
        if (editGrid && !document.getElementById("editStudyDentalChart")) {
            const field = createDentalChartField("editStudyDentalChart");
            editGrid.appendChild(field);
            window.DentalRayDentalChart.render(field.querySelector("#editStudyDentalChart"), []);
        }
    }

    function loadDentalChart() {
        installDentalChartStyles();
        if (window.DentalRayDentalChart) {
            injectDentalCharts();
            return;
        }
        const script = document.createElement("script");
        script.src = "/js/dental-chart.js";
        script.onload = injectDentalCharts;
        script.onerror = () => console.error("DentalRay: dental-chart.js could not be loaded.");
        document.body.appendChild(script);
    }

    const container = document.getElementById("studiesContainer");
    if (container) {
        const observer = new MutationObserver(addDeleteButtonsToStudyCards);
        observer.observe(container, { childList: true, subtree: true });
        addDeleteButtonsToStudyCards();
    }

    loadDentalChart();
})();