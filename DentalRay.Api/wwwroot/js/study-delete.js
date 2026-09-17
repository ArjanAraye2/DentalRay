// ============================================================
// DentalRay Frontend - Study Delete
// ============================================================
// New policy:
// - Shared images are never deleted with a Study.
// - Images used only by this Study can be kept in Patient Images
//   or selected for physical deletion.
// - The Backend preview endpoint is the source of truth.
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

    // The existing confirmation modal returns yes/no. To support the approved
    // keep-all / delete-all / select-specific policy without introducing another
    // modal, selection is done with the browser's multi-select prompt for now.
    // Empty input means keep all; "all" means delete all; comma-separated ImageIDs
    // allow selecting specific Study-only images. Shared images are never offered.
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
                showToast(
                    "Study حذف شد، اما پاک‌سازی یک یا چند فایل فیزیکی کامل نشد.",
                    "warning",
                    "هشدار پاک‌سازی"
                );
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

    const container = document.getElementById("studiesContainer");
    if (container) {
        const observer = new MutationObserver(addDeleteButtonsToStudyCards);
        observer.observe(container, { childList: true, subtree: true });
        addDeleteButtonsToStudyCards();
    }
})();