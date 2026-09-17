// ============================================================
// DentalRay Frontend - Study Delete + Dental Chart bootstrap
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
        if (!response.ok || !result.success) throw new Error(getApiError(result, "اطلاعات لازم برای حذف Study دریافت نشد."));
        return result;
    }

    async function chooseStudyOnlyImages(images) {
        if (!images || images.length === 0) return [];
        const names = images.map(x => `${x.imageID}: ${x.fileName}`).join("\n");
        const answer = window.prompt("این تصاویر فقط به همین Study متصل هستند:\n\n" + names + "\n\nبرای نگهداری همه، کادر را خالی بگذارید.\nبرای حذف همه، all را وارد کنید.\nبرای حذف انتخابی، ImageIDها را با کاما جدا کنید.");
        if (answer === null || answer.trim() === "") return [];
        if (answer.trim().toLowerCase() === "all") return images.map(x => x.imageID);
        const allowed = new Set(images.map(x => Number(x.imageID)));
        return [...new Set(answer.split(",").map(x => Number(x.trim())).filter(x => Number.isInteger(x) && allowed.has(x)))];
    }

    async function deleteStudyFromFrontend(studyID) {
        try {
            const preview = await getDeletePreview(studyID);
            const sharedCount = preview.sharedImages?.length || 0;
            const studyOnly = preview.studyOnlyImages || [];
            let message = "آیا از حذف این Study مطمئن هستید؟";
            if (sharedCount > 0) message += `\n\n${sharedCount} تصویر مشترک حذف نخواهد شد و برای Studyهای دیگر باقی می‌ماند.`;
            if (studyOnly.length > 0) message += `\n\n${studyOnly.length} تصویر فقط به همین Study متصل است. در مرحله بعد مشخص می‌کنید کدام فایل‌ها از Patient Images نیز حذف شوند.`;
            const confirmed = await askConfirmation({ title: "حذف Study", message, confirmText: "ادامه حذف", danger: true });
            if (!confirmed) return;
            const deleteIDs = await chooseStudyOnlyImages(studyOnly);
            const query = deleteIDs.map(id => `deleteImageIDs=${encodeURIComponent(id)}`).join("&");
            const response = await fetch(`/api/radiologystudies/${studyID}${query ? `?${query}` : ""}`, { method: "DELETE" });
            const result = await response.json();
            if (!response.ok || !result.success) throw new Error(getApiError(result, "حذف Study انجام نشد."));
            showToast(result.cleanupErrors?.length ? "Study حذف شد، اما پاک‌سازی یک یا چند فایل فیزیکی کامل نشد." : "Study با موفقیت حذف شد.", result.cleanupErrors?.length ? "warning" : "success");
            if (selectedPatientID) await openPatient(selectedPatientID);
        } catch (error) {
            console.error(error);
            showToast(error.message || "حذف Study انجام نشد.", "error");
        }
    }

    // Load the dedicated stylesheet without replacing the large index.html file.
    function installStudyDentalStyles() {
        if (document.getElementById("studyDentalStylesheet")) return;
        const link = document.createElement("link");
        link.id = "studyDentalStylesheet";
        link.rel = "stylesheet";
        link.href = "/css/study-dental.css";
        document.head.appendChild(link);
    }

    function createDentalChartField(containerId) {
        const panel = document.createElement("div");
        panel.className = "study-dental-panel";
        const hint = document.createElement("small");
        hint.className = "field-hint";
        hint.textContent = "دندان‌های مربوط به این Study را انتخاب کنید. نوع نمایش فقط یک ترجیح ظاهری است و اطلاعات FDI تغییر نمی‌کند.";
        const chart = document.createElement("div");
        chart.id = containerId;
        panel.append(chart, hint);
        return panel;
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
        installStudyDentalStyles();
        if (window.DentalRayDentalChart) return injectDentalCharts();
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