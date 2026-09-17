// ============================================================
// DentalRay Frontend - Study Delete
// ============================================================
//
// این فایل عملیات حذف Study را به Frontend اضافه می‌کند.
//
// سیاست:
// - اگر Study تصویر داشته باشد، Backend حذف را رد می‌کند.
// - پیام مناسب فارسی به کاربر نمایش داده می‌شود.
// - اگر Study تصویر نداشته باشد، پس از تأیید کاربر حذف می‌شود.
// - بعد از حذف موفق، پرونده بیمار Refresh می‌شود.
// ============================================================

(function () {
    "use strict";

    const STUDY_HAS_IMAGES_MESSAGE =
        "حذف این Study امکان‌پذیر نیست، زیرا دارای تصاویر رادیولوژی است. برای حذف آن، ابتدا تصاویر مربوطه را حذف کنید.";

    // --------------------------------------------------------
    // افزودن دکمه حذف به کارت‌های Study موجود
    // --------------------------------------------------------
    function addDeleteButtonsToStudyCards() {
        document.querySelectorAll(".study-card").forEach(card => {
            if (card.querySelector(".study-delete-button")) {
                return;
            }

            const studyID = Number(card.dataset.studyId);
            if (!Number.isInteger(studyID) || studyID <= 0) {
                return;
            }

            const buttons = card.querySelector(".study-action-buttons");
            if (!buttons) {
                return;
            }

            const deleteButton = document.createElement("button");
            deleteButton.type = "button";
            deleteButton.className = "danger-button study-delete-button";
            deleteButton.textContent = "حذف Study";

            deleteButton.addEventListener("click", async () => {
                await deleteStudyFromFrontend(studyID);
            });

            buttons.appendChild(deleteButton);
        });
    }

    // --------------------------------------------------------
    // حذف Study
    // --------------------------------------------------------
    async function deleteStudyFromFrontend(studyID) {
        // قبل از ارسال درخواست حذف، از کاربر تأیید می‌گیریم.
        const confirmed = await askConfirmation({
            title: "حذف Study",
            message: "آیا از حذف این Study مطمئن هستید؟ این عملیات فقط زمانی انجام می‌شود که هیچ تصویر رادیولوژی به آن متصل نباشد.",
            confirmText: "حذف Study",
            danger: true
        });

        if (!confirmed) {
            return;
        }

        try {
            const response = await fetch(
                `/api/radiologystudies/${studyID}`,
                { method: "DELETE" }
            );

            let result = null;

            try {
                result = await response.json();
            }
            catch {
                // اگر پاسخ JSON نبود، پیام عمومی استفاده می‌شود.
            }

            // 409 یعنی Study دارای Image است و طبق Policy حذف مجاز نیست.
            if (response.status === 409) {
                showToast(
                    STUDY_HAS_IMAGES_MESSAGE,
                    "warning",
                    "حذف امکان‌پذیر نیست"
                );
                return;
            }

            if (!response.ok || !result || !result.success) {
                throw new Error(
                    getApiError(
                        result,
                        "حذف Study انجام نشد."
                    )
                );
            }

            showToast(
                "Study با موفقیت حذف شد.",
                "success"
            );

            // پرونده بیمار را دوباره از Backend دریافت می‌کنیم تا
            // تعداد Studyها و اطلاعات صفحه کاملاً هماهنگ شوند.
            if (selectedPatientID) {
                await openPatient(selectedPatientID);
            }
        }
        catch (error) {
            console.error(error);
            showToast(
                error.message || "حذف Study انجام نشد.",
                "error"
            );
        }
    }

    // --------------------------------------------------------
    // چون کارت‌های Study هنگام باز کردن Patient دوباره ساخته
    // می‌شوند، تغییرات studiesContainer را زیر نظر می‌گیریم و
    // دکمه حذف را به کارت‌های جدید اضافه می‌کنیم.
    // --------------------------------------------------------
    const studiesContainerElement = document.getElementById("studiesContainer");

    if (studiesContainerElement) {
        const observer = new MutationObserver(() => {
            addDeleteButtonsToStudyCards();
        });

        observer.observe(studiesContainerElement, {
            childList: true,
            subtree: true
        });

        addDeleteButtonsToStudyCards();
    }
})();
