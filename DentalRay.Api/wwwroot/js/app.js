// ============================================================
// DentalRay Frontend
// Final Pre-Deployment Version
// ============================================================
//
// قابلیت‌های این فایل:
//
// - لیست و جستجوی بیماران
// - نمایش / عدم نمایش بیماران غیرفعال
// - ثبت بیمار
// - ویرایش بیمار
// - تاریخ تولد شمسی
// - فعال / غیرفعال‌سازی بیمار
// - نمایش پرونده بیمار
// - ثبت Study
// - ویرایش Study
// - تاریخ و زمان شمسی Study
// - نمایش تصاویر Study
// - Upload تصویر
// - حذف تصویر
// - نمایش بزرگ تصویر
// - Merge بیمار
// - Toast برای پیام‌ها
// - Confirm Modal برای عملیات حساس
// - Validation سمت Frontend
//
// ============================================================


// ============================================================
// Helper - گرفتن Element با ID
// ============================================================

function byId(id) {
    return document.getElementById(id);
}


// ============================================================
// State
// ============================================================
//
// اطلاعات Patient و Study انتخاب‌شده را در این متغیرها
// نگه می‌داریم.
//
// ============================================================

let selectedPatientID = null;
let selectedPatient = null;

let selectedStudyID = null;
let selectedStudy = null;




// ============================================================
// Quick Clinic / Dentist Management
// ============================================================
const manageClinicsButton = byId("manageClinicsButton");
const clinicsSection = byId("clinicsSection");
const backFromClinicsButton = byId("backFromClinicsButton");
const clinicName = byId("clinicName");
const clinicType = byId("clinicType");
const clinicPhone = byId("clinicPhone");
const clinicAddress = byId("clinicAddress");
const saveClinicButton = byId("saveClinicButton");
const clinicStatus = byId("clinicStatus");
const dentistClinic = byId("dentistClinic");
const dentistFirstName = byId("dentistFirstName");
const dentistLastName = byId("dentistLastName");
const dentistCouncilCode = byId("dentistCouncilCode");
const saveDentistButton = byId("saveDentistButton");
const dentistStatus = byId("dentistStatus");

async function refreshClinicManagementList() {
    const response = await fetch("/api/organizations");
    if (!response.ok) throw new Error("دریافت فهرست مطب‌ها انجام نشد.");
    const items = await response.json();
    dentistClinic.innerHTML = '<option value="">انتخاب مطب...</option>';
    items.forEach(item => {
        const option = document.createElement("option");
        option.value = item.organizationID; option.textContent = item.name;
        dentistClinic.appendChild(option);
    });
    if (items.length === 1) dentistClinic.value = String(items[0].organizationID);
}

manageClinicsButton.addEventListener("click", async () => {
    hideMainSections();
    clinicsSection.classList.remove("hidden");
    try { await refreshClinicManagementList(); } catch (e) { setFormStatus(clinicStatus, e.message, true); }
    window.scrollTo(0, 0);
});

backFromClinicsButton.addEventListener("click", () => {
    hideMainSections(); patientsSection.classList.remove("hidden"); window.scrollTo(0, 0);
});

saveClinicButton.addEventListener("click", async () => {
    try {
        const name = clinicName.value.trim();
        if (!name) throw new Error("نام مطب یا مرکز را وارد کنید.");
        setFormStatus(clinicStatus, "در حال ثبت...", false);
        const response = await fetch("/api/organizations", {
            method: "POST", headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ name, organizationType: Number(clinicType.value), phone: emptyToNull(clinicPhone.value), address: emptyToNull(clinicAddress.value) })
        });
        const result = await response.json();
        if (!response.ok) throw new Error(getApiError(result, "ثبت مطب انجام نشد."));
        clinicName.value = ""; clinicPhone.value = ""; clinicAddress.value = "";
        await refreshClinicManagementList();
        dentistClinic.value = String(result.organizationID);
        setFormStatus(clinicStatus, "مطب / مرکز با موفقیت ثبت شد.", false);
    } catch (e) { setFormStatus(clinicStatus, e.message, true); }
});

saveDentistButton.addEventListener("click", async () => {
    try {
        const organizationID = Number(dentistClinic.value);
        const firstName = dentistFirstName.value.trim(), lastName = dentistLastName.value.trim();
        if (!organizationID) throw new Error("مطب را انتخاب کنید.");
        if (!firstName || !lastName) throw new Error("نام و نام خانوادگی دندانپزشک را وارد کنید.");
        setFormStatus(dentistStatus, "در حال ثبت...", false);
        const response = await fetch(`/api/organizations/${organizationID}/dentists`, {
            method: "POST", headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ firstName, lastName, positionName: "دندانپزشک", medicalCouncilCode: emptyToNull(dentistCouncilCode.value) })
        });
        const result = await response.json();
        if (!response.ok) throw new Error(getApiError(result, "ثبت دندانپزشک انجام نشد."));
        dentistFirstName.value = ""; dentistLastName.value = ""; dentistCouncilCode.value = "";
        setFormStatus(dentistStatus, "دندانپزشک با موفقیت ایجاد و به مطب متصل شد.", false);
    } catch (e) { setFormStatus(dentistStatus, e.message, true); }
});

// ============================================================
// Elements - Patient List
// ============================================================

const patientsSection =
    byId("patientsSection");

const patientSearch =
    byId("patientSearch");

const searchButton =
    byId("searchButton");

const clearSearchButton =
    byId("clearSearchButton");

const includeInactivePatients =
    byId("includeInactivePatients");

const newPatientButton =
    byId("newPatientButton");

const patientsTableBody =
    byId("patientsTableBody");

const statusMessage =
    byId("statusMessage");


// ============================================================
// Elements - Patient Details
// ============================================================

const patientDetailsSection =
    byId("patientDetailsSection");

const backToPatientsButton =
    byId("backToPatientsButton");

const editPatientButton =
    byId("editPatientButton");

const newStudyButton =
    byId("newStudyButton");

const mergePatientButton =
    byId("mergePatientButton");

const deactivatePatientButton =
    byId("deactivatePatientButton");

const patientFullName =
    byId("patientFullName");

const patientNationalCode =
    byId("patientNationalCode");

const patientStatusBadge =
    byId("patientStatusBadge");

const detailFirstName =
    byId("detailFirstName");

const detailLastName =
    byId("detailLastName");

const detailNationalCode =
    byId("detailNationalCode");

const detailMobile =
    byId("detailMobile");

const detailBirthDate =
    byId("detailBirthDate");

const detailIsActive =
    byId("detailIsActive");

const detailAddress =
    byId("detailAddress");

const detailDescription =
    byId("detailDescription");

const studyCount =
    byId("studyCount");

const totalImageCount =
    byId("totalImageCount");

const studiesContainer =
    byId("studiesContainer");


// ============================================================
// Elements - New Patient
// ============================================================

const newPatientSection =
    byId("newPatientSection");

const newPatientForm =
    byId("newPatientForm");

const cancelNewPatientButton =
    byId("cancelNewPatientButton");

const cancelNewPatientButtonBottom =
    byId("cancelNewPatientButtonBottom");

const newFirstName =
    byId("newFirstName");

const newLastName =
    byId("newLastName");

const newNationalCode =
    byId("newNationalCode");

const newMobile =
    byId("newMobile");

const newBirthDate =
    byId("newBirthDate");

const newGender =
    byId("newGender");

const newAddress =
    byId("newAddress");

const newDescription =
    byId("newDescription");

const newPatientStatus =
    byId("newPatientStatus");


// ============================================================
// Elements - Edit Patient
// ============================================================

const editPatientSection =
    byId("editPatientSection");

const editPatientForm =
    byId("editPatientForm");

const cancelEditPatientButton =
    byId("cancelEditPatientButton");

const cancelEditPatientButtonBottom =
    byId("cancelEditPatientButtonBottom");

const editFirstName =
    byId("editFirstName");

const editLastName =
    byId("editLastName");

const editNationalCode =
    byId("editNationalCode");

const editMobile =
    byId("editMobile");

const editBirthDate =
    byId("editBirthDate");

const editGender =
    byId("editGender");

const editAddress =
    byId("editAddress");

const editDescription =
    byId("editDescription");

const editPatientStatus =
    byId("editPatientStatus");


// ============================================================
// Elements - New Study
// ============================================================

const newStudySection =
    byId("newStudySection");

const newStudyForm =
    byId("newStudyForm");

const cancelNewStudyButton =
    byId("cancelNewStudyButton");

const cancelNewStudyButtonBottom =
    byId("cancelNewStudyButtonBottom");

const newStudyOrganization = byId("newStudyOrganization");
const newStudyDentist = byId("newStudyDentist");

const newStudyType =
    byId("newStudyType");

const newBodyPart =
    byId("newBodyPart");

const newStudyDate =
    byId("newStudyDate");

const newStudyDescription =
    byId("newStudyDescription");

const newStudyReport =
    byId("newStudyReport");

const newStudyStatus =
    byId("newStudyStatus");


// ============================================================
// Elements - Edit Study
// ============================================================

const editStudySection =
    byId("editStudySection");

const editStudyForm =
    byId("editStudyForm");

const cancelEditStudyButton =
    byId("cancelEditStudyButton");

const cancelEditStudyButtonBottom =
    byId("cancelEditStudyButtonBottom");

const editStudySubtitle =
    byId("editStudySubtitle");

const editStudyOrganization = byId("editStudyOrganization");
const editStudyDentist = byId("editStudyDentist");

const editStudyType =
    byId("editStudyType");

const editBodyPart =
    byId("editBodyPart");

const editStudyDate =
    byId("editStudyDate");

const editStudyDescription =
    byId("editStudyDescription");

const editStudyReport =
    byId("editStudyReport");

const editStudyStatus =
    byId("editStudyStatus");


// ============================================================
// Elements - Upload Image
// ============================================================

const uploadImageSection =
    byId("uploadImageSection");

const uploadImageForm =
    byId("uploadImageForm");

const cancelUploadImageButton =
    byId("cancelUploadImageButton");

const cancelUploadImageButtonBottom =
    byId("cancelUploadImageButtonBottom");

const uploadImageStudyInfo =
    byId("uploadImageStudyInfo");

const imageFileInput =
    byId("imageFileInput");

const uploadImageStatus =
    byId("uploadImageStatus");


// ============================================================
// Elements - Merge
// ============================================================

const mergePatientSection =
    byId("mergePatientSection");

const mergePatientForm =
    byId("mergePatientForm");

const cancelMergePatientButton =
    byId("cancelMergePatientButton");

const cancelMergePatientButtonBottom =
    byId("cancelMergePatientButtonBottom");

const mergeTargetNationalCode =
    byId("mergeTargetNationalCode");

const mergePatientStatus =
    byId("mergePatientStatus");


// ============================================================
// Elements - Image Modal
// ============================================================

const imageModal =
    byId("imageModal");

const closeImageModalButton =
    byId("closeImageModalButton");

const largeImage =
    byId("largeImage");

const largeImageCaption =
    byId("largeImageCaption");


// ============================================================
// Elements - Confirm Modal
// ============================================================

const confirmModal =
    byId("confirmModal");

const confirmTitle =
    byId("confirmTitle");

const confirmMessage =
    byId("confirmMessage");

const confirmYesButton =
    byId("confirmYesButton");

const confirmNoButton =
    byId("confirmNoButton");


// ============================================================
// Elements - Toast
// ============================================================

const toastContainer =
    byId("toastContainer");


// ============================================================
// Navigation
// ============================================================
//
// تمام Sectionهای اصلی را مخفی می‌کند.
//
// ============================================================

function hideMainSections() {

    const sections = [
        patientsSection,
        patientDetailsSection,
        newPatientSection,
        editPatientSection,
        newStudySection,
        editStudySection,
        uploadImageSection,
        mergePatientSection,
        clinicsSection
    ];


    sections.forEach(section => {

        section.classList.add(
            "hidden"
        );
    });
}


// ============================================================
// نمایش صفحه لیست بیماران
// ============================================================

function showPatientsScreen() {

    hideMainSections();


    patientsSection.classList.remove(
        "hidden"
    );


    window.scrollTo(
        0,
        0
    );
}


// ============================================================
// Toast
// ============================================================
//
// پیام‌های موفقیت / خطا / هشدار را بدون alert مرورگر
// نمایش می‌دهد.
//
// type:
//
// success
// error
// warning
//
// ============================================================

function showToast(
    message,
    type = "success",
    title = ""
) {

    const toast =
        document.createElement(
            "div"
        );


    toast.className =
        `toast ${type}`;


    const titleElement =
        document.createElement(
            "div"
        );


    titleElement.className =
        "toast-title";


    if (title !== "") {

        titleElement.textContent =
            title;
    }
    else {

        if (type === "success") {

            titleElement.textContent =
                "انجام شد";
        }
        else if (type === "error") {

            titleElement.textContent =
                "خطا";
        }
        else {

            titleElement.textContent =
                "توجه";
        }
    }


    const messageElement =
        document.createElement(
            "div"
        );


    messageElement.className =
        "toast-message";


    messageElement.textContent =
        message;


    toast.appendChild(
        titleElement
    );


    toast.appendChild(
        messageElement
    );


    toastContainer.appendChild(
        toast
    );


    // بعد از چند ثانیه Toast حذف می‌شود.
    setTimeout(
        () => {

            toast.remove();

        },
        4300
    );
}


// ============================================================
// Confirm Modal
// ============================================================
//
// به جای confirm ساده Browser استفاده می‌شود.
//
// خروجی Promise<boolean> است.
//
// ============================================================

function askConfirmation({
    title = "تأیید عملیات",
    message,
    confirmText = "تأیید",
    danger = true
}) {

    return new Promise(
        resolve => {

            confirmTitle.textContent =
                title;


            confirmMessage.textContent =
                message;


            confirmYesButton.textContent =
                confirmText;


            // ----------------------------------------------------
            // Style دکمه تأیید
            // ----------------------------------------------------

            confirmYesButton
                .classList
                .toggle(
                    "danger-button",
                    danger
                );


            confirmYesButton
                .classList
                .toggle(
                    "warning-button",
                    !danger
                );


            confirmModal
                .classList
                .remove(
                    "hidden"
                );


            // ----------------------------------------------------
            // Cleanup
            // ----------------------------------------------------

            const cleanup =
                () => {

                    confirmModal
                        .classList
                        .add(
                            "hidden"
                        );


                    confirmYesButton.onclick =
                        null;


                    confirmNoButton.onclick =
                        null;
                };


            // ----------------------------------------------------
            // Yes
            // ----------------------------------------------------

            confirmYesButton.onclick =
                () => {

                    cleanup();

                    resolve(
                        true
                    );
                };


            // ----------------------------------------------------
            // No
            // ----------------------------------------------------

            confirmNoButton.onclick =
                () => {

                    cleanup();

                    resolve(
                        false
                    );
                };
        }
    );
}


// ============================================================
// API Error Translator
// ============================================================
//
// برخی پیام‌های Backend را برای کاربر مطب
// به فارسی قابل فهم تبدیل می‌کنیم.
//
// ============================================================

function getApiError(
    result,
    fallback
) {

    if (!result) {

        return fallback;
    }


    const message =
        result.message ||
        result.error ||
        "";


    const translations = {

        "A patient with this NationalCode already exists.":
            "بیماری با این کد ملی از قبل ثبت شده است.",

        "The new NationalCode already belongs to another patient. Use the Merge operation if these records represent the same patient.":
            "این کد ملی متعلق به بیمار دیگری است. اگر دو پرونده متعلق به یک نفر هستند از «ادغام بیمار» استفاده کنید.",

        "NationalCode must contain only digits.":
            "کد ملی فقط باید شامل عدد باشد.",

        "Patient not found.":
            "بیمار پیدا نشد.",

        "Study not found.":
            "رادیولوژی پیدا نشد.",

        "Organization not found.":
            "مطب یا مرکز پیدا نشد.",

        "Organization not found or inactive.":
            "مطب یا مرکز پیدا نشد یا غیرفعال است.",

        "Organization name is required.":
            "نام مطب یا مرکز الزامی است.",

        "Organization is required when a dentist is selected.":
            "برای انتخاب دندانپزشک ابتدا مطب را مشخص کنید.",

        "Selected dentist does not belong to this organization.":
            "دندانپزشک انتخاب‌شده عضو این مطب نیست.",

        "Dentist first name and last name are required.":
            "نام و نام خانوادگی دندانپزشک الزامی است.",

        "Image not found.":
            "تصویر پیدا نشد.",

        "Physical image file not found.":
            "فایل فیزیکی تصویر پیدا نشد.",

        "Physical image file not found. Database metadata was not deleted.":
            "فایل فیزیکی تصویر پیدا نشد. برای جلوگیری از ناسازگاری، رکورد آن از دیتابیس حذف نشد.",

        "Only JPG, JPEG and PNG files are allowed.":
            "فقط فایل‌های JPG، JPEG و PNG مجاز هستند.",

        "The selected file is not a valid JPG or PNG image.":
            "فایل انتخاب‌شده یک تصویر JPG یا PNG معتبر نیست.",

        "Image file is too large. Maximum size is 25 MB.":
            "حجم تصویر بیشتر از حد مجاز ۲۵ مگابایت است.",

        "StudyType is required.":
            "نوع رادیولوژی را وارد کنید.",

        "StudyDate is required.":
            "تاریخ رادیولوژی را وارد کنید.",

        "Maximum image serial 999 has been reached for this study.":
            "تعداد تصاویر این رادیولوژی به حداکثر شماره مجاز رسیده است."
    };


    return (
        translations[message] ||
        fallback ||
        message ||
        "عملیات انجام نشد."
    );
}


// ============================================================
// Patient List
// ============================================================

async function loadPatients(
    searchText = ""
) {

    try {

        showStatus(
            "در حال دریافت اطلاعات...",
            false
        );


        // --------------------------------------------------------
        // QueryString
        // --------------------------------------------------------

        const parameters =
            new URLSearchParams();


        if (
            searchText.trim() !== ""
        ) {

            parameters.set(
                "search",
                searchText.trim()
            );
        }


        if (
            includeInactivePatients.checked
        ) {

            parameters.set(
                "includeInactive",
                "true"
            );
        }


        // --------------------------------------------------------
        // URL
        // --------------------------------------------------------

        let url =
            "/api/patients";


        if (
            parameters.toString() !== ""
        ) {

            url +=
                "?" +
                parameters.toString();
        }


        // --------------------------------------------------------
        // Fetch
        // --------------------------------------------------------

        const response =
            await fetch(
                url
            );


        const result =
            await response.json();


        if (
            !response.ok ||
            !result.success
        ) {

            throw new Error(
                getApiError(
                    result,
                    "خطا در دریافت فهرست بیماران."
                )
            );
        }


        // --------------------------------------------------------
        // Render
        // --------------------------------------------------------

        renderPatients(
            result.patients
        );


        showStatus(
            result.count === 0
                ? "بیماری پیدا نشد."
                : `${result.count} بیمار نمایش داده شد.`,
            false
        );
    }
    catch (error) {

        console.error(
            error
        );


        renderPatients(
            []
        );


        showStatus(
            error.message,
            true
        );
    }
}


// ============================================================
// Render Patient List
// ============================================================

function renderPatients(
    patients
) {

    patientsTableBody.innerHTML =
        "";


    // --------------------------------------------------------
    // Empty
    // --------------------------------------------------------

    if (
        !patients ||
        patients.length === 0
    ) {

        const row =
            document.createElement(
                "tr"
            );


        const cell =
            document.createElement(
                "td"
            );


        cell.colSpan =
            5;


        cell.textContent =
            "اطلاعاتی برای نمایش وجود ندارد.";


        row.appendChild(
            cell
        );


        patientsTableBody.appendChild(
            row
        );


        return;
    }


    // --------------------------------------------------------
    // Rows
    // --------------------------------------------------------

    patients.forEach(
        patient => {

            const row =
                document.createElement(
                    "tr"
                );


            // ----------------------------------------------------
            // Inactive Patient
            // ----------------------------------------------------

            if (
                !patient.isActive
            ) {

                row.classList.add(
                    "inactive-patient-row"
                );
            }


            // ----------------------------------------------------
            // نام
            // ----------------------------------------------------

            const firstNameText =
                patient.isActive
                    ? patient.firstName
                    : `${patient.firstName} (غیرفعال)`;


            row.appendChild(
                createCell(
                    firstNameText
                )
            );


            // ----------------------------------------------------
            // نام خانوادگی
            // ----------------------------------------------------

            row.appendChild(
                createCell(
                    patient.lastName
                )
            );


            // ----------------------------------------------------
            // کد ملی
            // ----------------------------------------------------

            row.appendChild(
                createCell(
                    patient.nationalCode
                )
            );


            // ----------------------------------------------------
            // موبایل
            // ----------------------------------------------------

            row.appendChild(
                createCell(
                    patient.mobile ||
                    "-"
                )
            );


            // ----------------------------------------------------
            // عملیات
            // ----------------------------------------------------

            const actionCell =
                document.createElement(
                    "td"
                );


            const button =
                document.createElement(
                    "button"
                );


            button.textContent =
                "باز کردن پرونده";


            button.addEventListener(
                "click",
                () => {

                    openPatient(
                        patient.patientID
                    );
                }
            );


            actionCell.appendChild(
                button
            );


            row.appendChild(
                actionCell
            );


            patientsTableBody.appendChild(
                row
            );
        }
    );
}


// ============================================================
// Patient Details
// ============================================================

async function openPatient(
    patientID
) {

    try {

        const response =
            await fetch(
                `/api/patients/${patientID}/details`
            );


        const result =
            await response.json();


        if (
            !response.ok ||
            !result.success
        ) {

            throw new Error(
                getApiError(
                    result,
                    "خطا در دریافت پرونده بیمار."
                )
            );
        }


        selectedPatientID =
            patientID;


        selectedPatient =
            result.patient;


        // Study قبلی را پاک نمی‌کنیم چون ممکن است
        // برای بازگشت از Upload یا Images لازم باشد.


        renderPatientDetails(
            result
        );


        hideMainSections();


        patientDetailsSection
            .classList
            .remove(
                "hidden"
            );


        window.scrollTo(
            0,
            0
        );
    }
    catch (error) {

        console.error(
            error
        );


        showToast(
            error.message,
            "error"
        );
    }
}


// ============================================================
// Render Patient Details
// ============================================================

function renderPatientDetails(
    result
) {

    const patient =
        result.patient;


    selectedPatient =
        patient;


    // --------------------------------------------------------
    // Header
    // --------------------------------------------------------

    patientFullName.textContent =
        `${patient.firstName} ${patient.lastName}`;


    patientNationalCode.textContent =
        `کد ملی: ${patient.nationalCode}`;


    // --------------------------------------------------------
    // Fields
    // --------------------------------------------------------

    detailFirstName.textContent =
        patient.firstName ||
        "-";


    detailLastName.textContent =
        patient.lastName ||
        "-";


    detailNationalCode.textContent =
        patient.nationalCode ||
        "-";


    detailMobile.textContent =
        patient.mobile ||
        "-";


    detailBirthDate.textContent =
        formatPersianDate(
            patient.birthDate
        );


    detailIsActive.textContent =
        patient.isActive
            ? "فعال"
            : "غیرفعال";


    detailAddress.textContent =
        patient.address ||
        "-";


    detailDescription.textContent =
        patient.description ||
        "-";


    // --------------------------------------------------------
    // Summary
    // --------------------------------------------------------

    studyCount.textContent =
        result.studyCount;


    totalImageCount.textContent =
        result.totalImageCount;


    // --------------------------------------------------------
    // Study List
    // --------------------------------------------------------

    renderStudies(
        result.studies
    );


    // --------------------------------------------------------
    // Status Badge
    // --------------------------------------------------------

    patientStatusBadge.textContent =
        patient.isActive
            ? "فعال"
            : "غیرفعال";


    patientStatusBadge.className =
        `status-badge ${patient.isActive
            ? "active"
            : "inactive"
        }`;


    // --------------------------------------------------------
    // Active / Inactive Button
    // --------------------------------------------------------

    if (
        patient.isActive
    ) {

        deactivatePatientButton.textContent =
            "غیرفعال‌سازی";


        deactivatePatientButton
            .classList
            .add(
                "danger-button"
            );


        deactivatePatientButton
            .classList
            .remove(
                "success-button"
            );
    }
    else {

        deactivatePatientButton.textContent =
            "فعال‌سازی";


        deactivatePatientButton
            .classList
            .remove(
                "danger-button"
            );


        deactivatePatientButton
            .classList
            .add(
                "success-button"
            );
    }
}


// ============================================================
// Studies
// ============================================================

function renderStudies(
    studies
) {
    studiesContainer.innerHTML = "";

    if (!studies || studies.length === 0) {
        const message = document.createElement("div");
        message.className = "status-message";
        message.textContent = "برای این بیمار هنوز رادیولوژی ثبت نشده است.";
        studiesContainer.appendChild(message);
        return;
    }

    studies.forEach(study => {
        const card = document.createElement("div");
        card.className = "study-card";
        card.dataset.studyId = String(study.studyID);

        const header = document.createElement("div");
        header.className = "study-card-header";

        const title = document.createElement("div");
        title.className = "study-title";
        title.textContent = study.studyType || `رادیولوژی ${study.studyID}`;

        const buttons = document.createElement("div");
        buttons.className = "study-action-buttons";

        const imagesButton = document.createElement("button");
        imagesButton.classList.add("study-images-button");
        imagesButton.dataset.imageCount = String(study.imageCount || 0);
        imagesButton.textContent = `تصاویر (${study.imageCount || 0})`;

        const uploadButton = document.createElement("button");
        uploadButton.textContent = "افزودن تصویر";
        uploadButton.addEventListener("click", () => openUploadImageForm(study));

        const editButton = document.createElement("button");
        editButton.textContent = "ویرایش";
        editButton.className = "secondary-button";
        editButton.addEventListener("click", () => openEditStudyForm(study));

        buttons.appendChild(imagesButton);
        buttons.appendChild(uploadButton);
        buttons.appendChild(editButton);
        header.appendChild(title);
        header.appendChild(buttons);
        card.appendChild(header);

        const meta = document.createElement("div");
        meta.className = "study-meta";
        meta.appendChild(createInfoLine("تاریخ", formatPersianDateTime(study.studyDate)));
        meta.appendChild(createInfoLine("مطب / مرکز", study.organizationName || "-"));
        meta.appendChild(createInfoLine("دندانپزشک", study.dentistName || "-"));
        meta.appendChild(createInfoLine("ناحیه", study.bodyPart || "-"));
        meta.appendChild(createInfoLine("توضیحات", study.description || "-"));
        meta.appendChild(createInfoLine("گزارش", study.report || "-"));
        card.appendChild(meta);

        // تصاویر هر Study داخل همان کارت نمایش داده می‌شوند.
        const imageSection = document.createElement("div");
        imageSection.className = "study-inline-images hidden";

        const imageStatus = document.createElement("div");
        imageStatus.className = "status-message";

        const imageGrid = document.createElement("div");
        imageGrid.className = "images-grid";

        imageSection.appendChild(imageStatus);
        imageSection.appendChild(imageGrid);
        card.appendChild(imageSection);

        imagesButton.addEventListener("click", async () => {
            await toggleStudyImages(study, imageSection, imageStatus, imageGrid, imagesButton);
        });

        studiesContainer.appendChild(card);
    });
}


// ============================================================
// Toggle Study Images
// ============================================================
// فقط تصاویر همان Study را زیر کارت خودش باز می‌کند.
// برای جلوگیری از شلوغی، تصاویر Studyهای دیگر بسته می‌شوند.
// ============================================================

async function toggleStudyImages(study, imageSection, imageStatus, imageGrid, button) {
    if (!imageSection.classList.contains("hidden")) {
        imageSection.classList.add("hidden");
        button.textContent = `تصاویر (${button.dataset.imageCount || study.imageCount || 0})`;
        return;
    }

    document.querySelectorAll(".study-inline-images").forEach(section => {
        if (section !== imageSection) {
            section.classList.add("hidden");
            const otherCard = section.closest(".study-card");
            const otherButton = otherCard?.querySelector(".study-images-button");
            if (otherButton) {
                otherButton.textContent = `تصاویر (${otherButton.dataset.imageCount || 0})`;
            }
        }
    });

    imageSection.classList.remove("hidden");
    button.textContent = `بستن تصاویر (${button.dataset.imageCount || study.imageCount || 0})`;
    imageStatus.classList.remove("error");
    imageStatus.textContent = "در حال دریافت تصاویر...";
    imageGrid.innerHTML = "";

    try {
        const response = await fetch(`/api/radiologyimages/study/${study.studyID}`);
        const result = await response.json();

        if (!response.ok || !result.success) {
            throw new Error(getApiError(result, "خطا در دریافت تصاویر."));
        }

        button.dataset.imageCount = String(result.count);
        button.textContent = `بستن تصاویر (${result.count})`;
        renderImagesInGrid(result.images, imageGrid, study, imageStatus, button);
        imageStatus.textContent = result.count === 0
            ? "تصویری برای این رادیولوژی ثبت نشده است."
            : `${result.count} تصویر نمایش داده شد.`;
    }
    catch (error) {
        console.error(error);
        imageStatus.textContent = error.message;
        imageStatus.classList.add("error");
    }
}


// ============================================================
// Render Images In Study Grid
// ============================================================
// ============================================================

function renderImagesInGrid(images, imageGrid, study, imageStatus, imagesButton) {
    imageGrid.innerHTML = "";

    if (!images || images.length === 0) {
        return;
    }

    images.forEach(image => {
        const card = document.createElement("div");
        card.className = "image-card";

        const img = document.createElement("img");
        img.src = `/api/radiologyimages/${image.imageID}`;
        img.alt = image.fileName;
        img.loading = "lazy";
        img.addEventListener("click", () => openLargeImage(image));

        const title = document.createElement("div");
        title.className = "image-card-title";
        title.textContent = image.fileName;

        const actions = document.createElement("div");
        actions.className = "image-card-actions";

        const deleteButton = document.createElement("button");
        deleteButton.className = "image-delete-button";
        deleteButton.textContent = "حذف تصویر";
        deleteButton.addEventListener("click", async () => {
            await deleteImageInline(image, study, imageGrid, imageStatus, imagesButton);
        });

        actions.appendChild(deleteButton);
        card.appendChild(img);
        card.appendChild(title);
        card.appendChild(actions);
        imageGrid.appendChild(card);
    });
}


// ============================================================
// Delete Image Inline
// ============================================================
// بعد از حذف، فقط تصاویر همان Study دوباره دریافت می‌شوند.
// ============================================================

async function deleteImageInline(image, study, imageGrid, imageStatus, imagesButton) {
    const confirmed = await askConfirmation({
        title: "حذف تصویر",
        message: `آیا از حذف این تصویر مطمئن هستید؟\n\n${image.fileName}\n\nاین عملیات فایل فیزیکی و رکورد آن را حذف می‌کند.`,
        confirmText: "حذف تصویر",
        danger: true
    });

    if (!confirmed) return;

    try {
        const response = await fetch(`/api/radiologyimages/${image.imageID}`, { method: "DELETE" });
        const result = await response.json();

        if (!response.ok || !result.success) {
            throw new Error(getApiError(result, "حذف تصویر انجام نشد."));
        }

        if (result.cleanupWarning) {
            showToast("رکورد تصویر حذف شد، اما پاک‌سازی فایل موقت کامل نشد.", "warning", "هشدار پاک‌سازی");
        }
        else {
            showToast("تصویر با موفقیت حذف شد.", "success");
        }

        const imagesResponse = await fetch(`/api/radiologyimages/study/${study.studyID}`);
        const imagesResult = await imagesResponse.json();

        if (!imagesResponse.ok || !imagesResult.success) {
            throw new Error(getApiError(imagesResult, "دریافت مجدد تصاویر انجام نشد."));
        }

        imagesButton.dataset.imageCount = String(imagesResult.count);
        imagesButton.textContent = `بستن تصاویر (${imagesResult.count})`;
        renderImagesInGrid(imagesResult.images, imageGrid, study, imageStatus, imagesButton);
        imageStatus.textContent = imagesResult.count === 0
            ? "تصویری برای این رادیولوژی ثبت نشده است."
            : `${imagesResult.count} تصویر نمایش داده شد.`;

        const currentTotal = Number(totalImageCount.textContent) || 0;
        totalImageCount.textContent = Math.max(0, currentTotal - 1);
    }
    catch (error) {
        showToast(error.message, "error");
    }
}


// ============================================================
// New Patient
// ============================================================

function openNewPatientForm() {

    hideMainSections();


    newPatientSection
        .classList
        .remove(
            "hidden"
        );


    newPatientForm.reset();


    newPatientStatus.textContent =
        "";


    newPatientStatus
        .classList
        .remove(
            "error"
        );


    newFirstName.focus();


    window.scrollTo(
        0,
        0
    );
}


// ============================================================
// Create Patient
// ============================================================

async function createPatient() {

    try {

        const firstName =
            newFirstName
                .value
                .trim();


        const lastName =
            newLastName
                .value
                .trim();


        const nationalCode =
            normalizeDigits(
                newNationalCode
                    .value
                    .trim()
            );


        const mobile =
            normalizePhone(
                newMobile.value
            );


        // --------------------------------------------------------
        // Validation
        // --------------------------------------------------------

        validatePatientFields(
            firstName,
            lastName,
            nationalCode,
            mobile
        );


        // --------------------------------------------------------
        // BirthDate
        // --------------------------------------------------------

        const birthDate =
            parsePersianDateForBackend(
                newBirthDate.value,
                false
            );


        // --------------------------------------------------------
        // Request
        // --------------------------------------------------------

        const request = {

            nationalCode:
                nationalCode,

            firstName:
                firstName,

            lastName:
                lastName,

            birthDate:
                birthDate,

            gender:
                newGender.value === ""
                    ? null
                    : Number(
                        newGender.value
                    ),

            mobile:
                mobile,

            address:
                emptyToNull(
                    newAddress.value
                ),

            description:
                emptyToNull(
                    newDescription.value
                )
        };


        setFormStatus(
            newPatientStatus,
            "در حال ثبت بیمار...",
            false
        );


        // --------------------------------------------------------
        // POST
        // --------------------------------------------------------

        const response =
            await fetch(
                "/api/patients",
                {
                    method:
                        "POST",

                    headers: {
                        "Content-Type":
                            "application/json"
                    },

                    body:
                        JSON.stringify(
                            request
                        )
                }
            );


        const result =
            await response.json();


        if (
            !response.ok
        ) {

            throw new Error(
                getApiError(
                    result,
                    "ثبت بیمار انجام نشد."
                )
            );
        }


        // --------------------------------------------------------
        // Refresh
        // --------------------------------------------------------

        await loadPatients();


        await openPatient(
            result.patientID
        );


        showToast(
            "بیمار با موفقیت ثبت شد.",
            "success"
        );
    }
    catch (error) {

        setFormStatus(
            newPatientStatus,
            error.message,
            true
        );
    }
}


// ============================================================
// Edit Patient - Open Form
// ============================================================

function openEditPatientForm() {

    if (
        !selectedPatient
    ) {

        return;
    }


    editFirstName.value =
        selectedPatient.firstName ||
        "";


    editLastName.value =
        selectedPatient.lastName ||
        "";


    editNationalCode.value =
        selectedPatient.nationalCode ||
        "";


    editMobile.value =
        selectedPatient.mobile ||
        "";


    editBirthDate.value =
        formatPersianDateForInput(
            selectedPatient.birthDate
        );


    editGender.value =
        selectedPatient.gender == null
            ? ""
            : String(
                selectedPatient.gender
            );


    editAddress.value =
        selectedPatient.address ||
        "";


    editDescription.value =
        selectedPatient.description ||
        "";


    editPatientStatus.textContent =
        "";


    editPatientStatus
        .classList
        .remove(
            "error"
        );


    hideMainSections();


    editPatientSection
        .classList
        .remove(
            "hidden"
        );


    editFirstName.focus();


    window.scrollTo(
        0,
        0
    );
}


// ============================================================
// Update Patient
// ============================================================

async function updatePatient() {

    try {

        const firstName =
            editFirstName
                .value
                .trim();


        const lastName =
            editLastName
                .value
                .trim();


        const nationalCode =
            normalizeDigits(
                editNationalCode
                    .value
                    .trim()
            );


        const mobile =
            normalizePhone(
                editMobile.value
            );


        // --------------------------------------------------------
        // Validation
        // --------------------------------------------------------

        validatePatientFields(
            firstName,
            lastName,
            nationalCode,
            mobile
        );


        // --------------------------------------------------------
        // BirthDate
        // --------------------------------------------------------

        const birthDate =
            parsePersianDateForBackend(
                editBirthDate.value,
                false
            );


        // --------------------------------------------------------
        // Request
        // --------------------------------------------------------

        const request = {

            nationalCode:
                nationalCode,

            firstName:
                firstName,

            lastName:
                lastName,

            birthDate:
                birthDate,

            gender:
                editGender.value === ""
                    ? null
                    : Number(
                        editGender.value
                    ),

            mobile:
                mobile,

            address:
                emptyToNull(
                    editAddress.value
                ),

            description:
                emptyToNull(
                    editDescription.value
                )
        };


        setFormStatus(
            editPatientStatus,
            "در حال ذخیره تغییرات...",
            false
        );


        // --------------------------------------------------------
        // PUT
        // --------------------------------------------------------

        const response =
            await fetch(
                `/api/patients/${selectedPatientID}`,
                {
                    method:
                        "PUT",

                    headers: {
                        "Content-Type":
                            "application/json"
                    },

                    body:
                        JSON.stringify(
                            request
                        )
                }
            );


        const result =
            await response.json();


        if (
            !response.ok ||
            !result.success
        ) {

            throw new Error(
                getApiError(
                    result,
                    "ویرایش بیمار انجام نشد."
                )
            );
        }


        // --------------------------------------------------------
        // Refresh
        // --------------------------------------------------------

        await loadPatients();


        await openPatient(
            selectedPatientID
        );


        // --------------------------------------------------------
        // پیام موفقیت
        // --------------------------------------------------------

        let message =
            "اطلاعات بیمار با موفقیت ذخیره شد.";


        if (
            result.nationalCodeChanged
        ) {

            message +=
                ` ${result.renamedImages} تصویر نیز با کد ملی جدید هماهنگ شد.`;
        }


        showToast(
            message,
            "success"
        );
    }
    catch (error) {

        setFormStatus(
            editPatientStatus,
            error.message,
            true
        );
    }
}


// ============================================================
// Active / Inactive Patient
// ============================================================

async function togglePatientActiveStatus() {

    if (
        !selectedPatientID ||
        !selectedPatient
    ) {

        return;
    }


    const isActive =
        selectedPatient.isActive;


    // --------------------------------------------------------
    // Confirm
    // --------------------------------------------------------

    const confirmed =
        await askConfirmation({

            title:
                isActive
                    ? "غیرفعال‌سازی بیمار"
                    : "فعال‌سازی بیمار",

            message:
                isActive
                    ? `آیا از غیرفعال کردن ${selectedPatient.firstName} ${selectedPatient.lastName} مطمئن هستید؟\n\nاطلاعات و رادیولوژی‌های بیمار حذف نمی‌شوند.`
                    : `آیا بیمار ${selectedPatient.firstName} ${selectedPatient.lastName} دوباره فعال شود؟`,

            confirmText:
                isActive
                    ? "غیرفعال کن"
                    : "فعال کن",

            danger:
                isActive
        });


    if (
        !confirmed
    ) {

        return;
    }


    // --------------------------------------------------------
    // Endpoint
    // --------------------------------------------------------

    const endpoint =
        isActive
            ? `/api/patients/${selectedPatientID}/deactivate`
            : `/api/patients/${selectedPatientID}/activate`;


    try {

        const response =
            await fetch(
                endpoint,
                {
                    method:
                        "PUT"
                }
            );


        const result =
            await response.json();


        if (
            !response.ok ||
            !result.success
        ) {

            throw new Error(
                getApiError(
                    result,
                    isActive
                        ? "غیرفعال‌سازی انجام نشد."
                        : "فعال‌سازی انجام نشد."
                )
            );
        }


        await loadPatients();


        await openPatient(
            selectedPatientID
        );


        showToast(
            isActive
                ? "بیمار غیرفعال شد."
                : "بیمار دوباره فعال شد.",
            "success"
        );
    }
    catch (error) {

        showToast(
            error.message,
            "error"
        );
    }
}




// ============================================================
// Clinic / Dentist helpers for Study
// ============================================================
async function loadStudyOrganizations() {
    const response = await fetch("/api/organizations");
    if (!response.ok) throw new Error("دریافت فهرست مطب‌ها انجام نشد.");
    const items = await response.json();
    newStudyOrganization.innerHTML = '<option value="">انتخاب مطب...</option>';
    items.forEach(item => {
        const option = document.createElement("option");
        option.value = item.organizationID;
        option.textContent = item.name;
        newStudyOrganization.appendChild(option);
    });
    if (items.length === 1) {
        newStudyOrganization.value = String(items[0].organizationID);
        await loadStudyDentists(items[0].organizationID);
    } else {
        newStudyDentist.disabled = true;
        newStudyDentist.innerHTML = '<option value="">ابتدا مطب را انتخاب کنید</option>';
    }
}

async function loadStudyDentists(organizationID) {
    newStudyDentist.innerHTML = '<option value="">در حال دریافت...</option>';
    newStudyDentist.disabled = true;
    if (!organizationID) {
        newStudyDentist.innerHTML = '<option value="">ابتدا مطب را انتخاب کنید</option>';
        return;
    }
    const response = await fetch(`/api/organizations/${organizationID}/dentists`);
    if (!response.ok) throw new Error("دریافت فهرست دندانپزشکان انجام نشد.");
    const items = await response.json();
    newStudyDentist.innerHTML = '<option value="">انتخاب دندانپزشک...</option>';
    items.forEach(item => {
        const option = document.createElement("option");
        option.value = item.personID;
        option.textContent = `${item.firstName} ${item.lastName}`;
        newStudyDentist.appendChild(option);
    });
    newStudyDentist.disabled = items.length === 0;
    if (items.length === 1) newStudyDentist.value = String(items[0].personID);
}

newStudyOrganization.addEventListener("change", async () => {
    try { await loadStudyDentists(newStudyOrganization.value); }
    catch (error) { setFormStatus(newStudyStatus, error.message, true); }
});

// ============================================================
// New Study - Open Form
// ============================================================

async function openNewStudyForm() {

    if (
        !selectedPatientID
    ) {

        return;
    }


    newStudyForm.reset();

    try {
        await loadStudyOrganizations();
    }
    catch (error) {
        setFormStatus(newStudyStatus, error.message, true);
    }


    // تاریخ و زمان فعلی را به صورت شمسی در فرم قرار می‌دهیم.
    newStudyDate.value =
        formatPersianDateTimeForInput(
            new Date()
        );


    newStudyStatus.textContent =
        "";


    newStudyStatus
        .classList
        .remove(
            "error"
        );


    hideMainSections();


    newStudySection
        .classList
        .remove(
            "hidden"
        );


    newStudyType.focus();


    window.scrollTo(
        0,
        0
    );
}


// ============================================================
// Create Study
// ============================================================

async function createStudy() {

    try {

        const studyType =
            newStudyType
                .value
                .trim();


        if (
            studyType === ""
        ) {

            throw new Error(
                "نوع رادیولوژی را وارد کنید."
            );
        }


        if (
            studyType.length > 50
        ) {

            throw new Error(
                "نوع رادیولوژی نمی‌تواند بیشتر از ۵۰ کاراکتر باشد."
            );
        }


        // --------------------------------------------------------
        // StudyDate
        // --------------------------------------------------------

        const studyDate =
            parsePersianDateForBackend(
                newStudyDate.value,
                true
            );


        if (
            !studyDate
        ) {

            throw new Error(
                "تاریخ رادیولوژی را وارد کنید."
            );
        }


        // --------------------------------------------------------
        // Request
        // --------------------------------------------------------

        const request = {

            patientID:
                selectedPatientID,

            organizationID:
                newStudyOrganization.value ? Number(newStudyOrganization.value) : null,

            dentistPersonID:
                newStudyDentist.value ? Number(newStudyDentist.value) : null,

            studyDate:
                studyDate,

            studyType:
                studyType,

            bodyPart:
                emptyToNull(
                    newBodyPart.value
                ),

            description:
                emptyToNull(
                    newStudyDescription.value
                ),

            report:
                emptyToNull(
                    newStudyReport.value
                )
        };


        setFormStatus(
            newStudyStatus,
            "در حال ثبت رادیولوژی...",
            false
        );


        const response =
            await fetch(
                "/api/radiologystudies",
                {
                    method:
                        "POST",

                    headers: {
                        "Content-Type":
                            "application/json"
                    },

                    body:
                        JSON.stringify(
                            request
                        )
                }
            );


        const result =
            await response.json();


        if (
            !response.ok
        ) {

            throw new Error(
                getApiError(
                    result,
                    "ثبت رادیولوژی انجام نشد."
                )
            );
        }


        await openPatient(
            selectedPatientID
        );


        showToast(
            "رادیولوژی جدید با موفقیت ثبت شد.",
            "success"
        );
    }
    catch (error) {

        setFormStatus(
            newStudyStatus,
            error.message,
            true
        );
    }
}




async function loadEditStudyOrganizations(selectedOrganizationID, selectedDentistID) {
    const response = await fetch("/api/organizations");
    if (!response.ok) throw new Error("دریافت فهرست مطب‌ها انجام نشد.");
    const items = await response.json();
    editStudyOrganization.innerHTML = '<option value="">انتخاب مطب...</option>';
    items.forEach(item => {
        const option = document.createElement("option");
        option.value = item.organizationID; option.textContent = item.name;
        editStudyOrganization.appendChild(option);
    });
    if (selectedOrganizationID) {
        editStudyOrganization.value = String(selectedOrganizationID);
        await loadEditStudyDentists(selectedOrganizationID, selectedDentistID);
    } else {
        editStudyDentist.disabled = true;
        editStudyDentist.innerHTML = '<option value="">ابتدا مطب را انتخاب کنید</option>';
    }
}

async function loadEditStudyDentists(organizationID, selectedDentistID = null) {
    editStudyDentist.disabled = true;
    if (!organizationID) {
        editStudyDentist.innerHTML = '<option value="">ابتدا مطب را انتخاب کنید</option>'; return;
    }
    const response = await fetch(`/api/organizations/${organizationID}/dentists`);
    if (!response.ok) throw new Error("دریافت فهرست دندانپزشکان انجام نشد.");
    const items = await response.json();
    editStudyDentist.innerHTML = '<option value="">انتخاب دندانپزشک...</option>';
    items.forEach(item => {
        const option = document.createElement("option");
        option.value = item.personID; option.textContent = `${item.firstName} ${item.lastName}`;
        editStudyDentist.appendChild(option);
    });
    editStudyDentist.disabled = items.length === 0;
    if (selectedDentistID) editStudyDentist.value = String(selectedDentistID);
    else if (items.length === 1) editStudyDentist.value = String(items[0].personID);
}

editStudyOrganization.addEventListener("change", async () => {
    try { await loadEditStudyDentists(editStudyOrganization.value); }
    catch (error) { setFormStatus(editStudyStatus, error.message, true); }
});

// ============================================================
// Edit Study - Open Form
// ============================================================

async function openEditStudyForm(
    study
) {

    selectedStudyID =
        study.studyID;


    selectedStudy =
        study;

    try {
        await loadEditStudyOrganizations(study.organizationID, study.dentistPersonID);
    }
    catch (error) {
        setFormStatus(editStudyStatus, error.message, true);
    }


    editStudyType.value =
        study.studyType ||
        "";


    editBodyPart.value =
        study.bodyPart ||
        "";


    editStudyDate.value =
        formatPersianDateTimeForInput(
            study.studyDate
        );


    editStudyDescription.value =
        study.description ||
        "";


    editStudyReport.value =
        study.report ||
        "";


    editStudySubtitle.textContent =
        `رادیولوژی شماره ${study.studyID}`;


    editStudyStatus.textContent =
        "";


    editStudyStatus
        .classList
        .remove(
            "error"
        );


    hideMainSections();


    editStudySection
        .classList
        .remove(
            "hidden"
        );


    editStudyType.focus();


    window.scrollTo(
        0,
        0
    );
}


// ============================================================
// Update Study
// ============================================================

async function updateStudy() {

    try {

        if (
            !selectedStudyID
        ) {

            throw new Error(
                "رادیولوژی انتخاب‌شده معتبر نیست."
            );
        }


        const studyType =
            editStudyType
                .value
                .trim();


        if (
            studyType === ""
        ) {

            throw new Error(
                "نوع رادیولوژی را وارد کنید."
            );
        }


        if (
            studyType.length > 50
        ) {

            throw new Error(
                "نوع رادیولوژی نمی‌تواند بیشتر از ۵۰ کاراکتر باشد."
            );
        }


        // --------------------------------------------------------
        // Date
        // --------------------------------------------------------

        const studyDate =
            parsePersianDateForBackend(
                editStudyDate.value,
                true
            );


        if (
            !studyDate
        ) {

            throw new Error(
                "تاریخ رادیولوژی را وارد کنید."
            );
        }


        // --------------------------------------------------------
        // Request
        // --------------------------------------------------------

        const request = {

            organizationID:
                editStudyOrganization.value ? Number(editStudyOrganization.value) : null,

            dentistPersonID:
                editStudyDentist.value ? Number(editStudyDentist.value) : null,

            studyDate:
                studyDate,

            studyType:
                studyType,

            bodyPart:
                emptyToNull(
                    editBodyPart.value
                ),

            description:
                emptyToNull(
                    editStudyDescription.value
                ),

            report:
                emptyToNull(
                    editStudyReport.value
                )
        };


        setFormStatus(
            editStudyStatus,
            "در حال ذخیره تغییرات...",
            false
        );


        const response =
            await fetch(
                `/api/radiologystudies/${selectedStudyID}`,
                {
                    method:
                        "PUT",

                    headers: {
                        "Content-Type":
                            "application/json"
                    },

                    body:
                        JSON.stringify(
                            request
                        )
                }
            );


        const result =
            await response.json();


        if (
            !response.ok ||
            !result.success
        ) {

            throw new Error(
                getApiError(
                    result,
                    "ویرایش رادیولوژی انجام نشد."
                )
            );
        }


        await openPatient(
            selectedPatientID
        );


        showToast(
            "اطلاعات رادیولوژی با موفقیت ویرایش شد.",
            "success"
        );
    }
    catch (error) {

        setFormStatus(
            editStudyStatus,
            error.message,
            true
        );
    }
}


// ============================================================
// Upload Image - Open Form
// ============================================================

function openUploadImageForm(
    study
) {

    selectedStudyID =
        study.studyID;


    selectedStudy =
        study;


    imageFileInput.value =
        "";


    uploadImageStatus.textContent =
        "";


    uploadImageStatus
        .classList
        .remove(
            "error"
        );


    uploadImageStudyInfo.textContent =
        `رادیولوژی شماره ${study.studyID} — ${study.studyType || ""}`;


    hideMainSections();


    uploadImageSection
        .classList
        .remove(
            "hidden"
        );


    window.scrollTo(
        0,
        0
    );
}


// ============================================================
// Upload Image
// ============================================================

async function uploadImage() {

    try {

        if (
            !selectedStudyID
        ) {

            throw new Error(
                "رادیولوژی انتخاب نشده است."
            );
        }


        const file =
            imageFileInput
                .files[0];


        if (
            !file
        ) {

            throw new Error(
                "یک فایل تصویر انتخاب کنید."
            );
        }


        // --------------------------------------------------------
        // MIME
        // --------------------------------------------------------

        const allowedTypes = [
            "image/jpeg",
            "image/png"
        ];


        if (
            !allowedTypes.includes(
                file.type
            )
        ) {

            throw new Error(
                "فقط فایل‌های JPG، JPEG و PNG مجاز هستند."
            );
        }


        // --------------------------------------------------------
        // 25 MB
        // --------------------------------------------------------

        const maxSize =
            25 *
            1024 *
            1024;


        if (
            file.size >
            maxSize
        ) {

            throw new Error(
                "حجم تصویر بیشتر از ۲۵ مگابایت است."
            );
        }


        // --------------------------------------------------------
        // FormData
        // --------------------------------------------------------

        const formData =
            new FormData();


        formData.append(
            "file",
            file
        );


        setFormStatus(
            uploadImageStatus,
            "در حال ذخیره تصویر...",
            false
        );


        // --------------------------------------------------------
        // POST
        // --------------------------------------------------------

        const response =
            await fetch(
                `/api/radiologyimages?studyID=${selectedStudyID}`,
                {
                    method:
                        "POST",

                    body:
                        formData
                }
            );


        const result =
            await response.json();


        if (
            !response.ok ||
            !result.success
        ) {

            throw new Error(
                getApiError(
                    result,
                    "ذخیره تصویر انجام نشد."
                )
            );
        }


        await openPatient(
            selectedPatientID
        );


        // پس از Upload، همان Study را دوباره باز می‌کنیم
        // تا تصویر جدید بلافاصله زیر همان Study دیده شود.
        setTimeout(
            () => {
                const studyCard =
                    document.querySelector(
                        `[data-study-id="${selectedStudyID}"]`
                    );

                const imagesButton =
                    studyCard?.querySelector(
                        ".study-images-button"
                    );

                if (imagesButton) {
                    imagesButton.click();
                }
            },
            100
        );


        showToast(
            `تصویر با موفقیت ذخیره شد: ${result.fileName}`,
            "success"
        );
    }
    catch (error) {

        setFormStatus(
            uploadImageStatus,
            error.message,
            true
        );
    }
}


// ============================================================
// Merge - Open Form
// ============================================================

function openMergePatientForm() {

    if (
        !selectedPatient
    ) {

        return;
    }


    mergePatientForm.reset();


    mergePatientStatus
        .classList
        .remove(
            "error"
        );


    mergePatientStatus.textContent =
        `Source: ${selectedPatient.firstName} ${selectedPatient.lastName} — ${selectedPatient.nationalCode}`;


    hideMainSections();


    mergePatientSection
        .classList
        .remove(
            "hidden"
        );


    mergeTargetNationalCode.focus();


    window.scrollTo(
        0,
        0
    );
}


// ============================================================
// Merge Patient
// ============================================================

async function mergePatient() {

    try {

        const targetNationalCode =
            normalizeDigits(
                mergeTargetNationalCode
                    .value
                    .trim()
            );


        if (
            !/^\d+$/.test(
                targetNationalCode
            )
        ) {

            throw new Error(
                "کد ملی بیمار مقصد معتبر نیست."
            );
        }


        if (
            targetNationalCode ===
            selectedPatient.nationalCode
        ) {

            throw new Error(
                "بیمار Source و Target نمی‌توانند یک پرونده باشند."
            );
        }


        // --------------------------------------------------------
        // پیدا کردن Target
        // --------------------------------------------------------

        setFormStatus(
            mergePatientStatus,
            "در حال بررسی بیمار مقصد...",
            false
        );


        const targetResponse =
            await fetch(
                `/api/patients/${encodeURIComponent(targetNationalCode)}`
            );


        const target =
            await targetResponse.json();


        if (
            !targetResponse.ok
        ) {

            throw new Error(
                getApiError(
                    target,
                    "بیمار مقصد پیدا نشد."
                )
            );
        }


        // --------------------------------------------------------
        // Confirm
        // --------------------------------------------------------

        const confirmed =
            await askConfirmation({

                title:
                    "تأیید ادغام بیمار",

                message:
                    `Source:\n${selectedPatient.firstName} ${selectedPatient.lastName} — ${selectedPatient.nationalCode}\n\n` +
                    `Target:\n${target.firstName} ${target.lastName} — ${target.nationalCode}\n\n` +
                    "تمام رادیولوژی‌ها و تصاویر Source به Target منتقل می‌شوند و پرونده Source حذف خواهد شد.",

                confirmText:
                    "انجام Merge",

                danger:
                    true
            });


        if (
            !confirmed
        ) {

            setFormStatus(
                mergePatientStatus,
                "عملیات لغو شد.",
                false
            );


            return;
        }


        // --------------------------------------------------------
        // Merge
        // --------------------------------------------------------

        setFormStatus(
            mergePatientStatus,
            "در حال انجام Merge...",
            false
        );


        const response =
            await fetch(
                "/api/patients/merge",
                {
                    method:
                        "POST",

                    headers: {
                        "Content-Type":
                            "application/json"
                    },

                    body:
                        JSON.stringify({
                            sourcePatientID:
                                selectedPatientID,

                            targetPatientID:
                                target.patientID
                        })
                }
            );


        const result =
            await response.json();


        if (
            !response.ok ||
            !result.success
        ) {

            throw new Error(
                getApiError(
                    result,
                    "Merge انجام نشد."
                )
            );
        }


        // --------------------------------------------------------
        // Refresh
        // --------------------------------------------------------

        await loadPatients();


        await openPatient(
            target.patientID
        );


        showToast(
            `${result.transferredStudies} رادیولوژی منتقل شد و ${result.renamedImages} تصویر با پرونده مقصد هماهنگ شد.`,
            "success",
            "ادغام با موفقیت انجام شد"
        );
    }
    catch (error) {

        setFormStatus(
            mergePatientStatus,
            error.message,
            true
        );
    }
}


// ============================================================
// Large Image
// ============================================================

function openLargeImage(
    image
) {

    largeImage.src =
        `/api/radiologyimages/${image.imageID}`;


    largeImageCaption.textContent =
        image.fileName;


    imageModal
        .classList
        .remove(
            "hidden"
        );
}


// ============================================================
// Close Large Image
// ============================================================

function closeLargeImage() {

    imageModal
        .classList
        .add(
            "hidden"
        );


    largeImage.src =
        "";
}


// ============================================================
// Form Status Helper
// ============================================================

function setFormStatus(
    element,
    message,
    isError
) {

    element.textContent =
        message;


    element.classList.toggle(
        "error",
        isError
    );
}


// ============================================================
// Patient Validation
// ============================================================

function validatePatientFields(
    firstName,
    lastName,
    nationalCode,
    mobile
) {

    if (
        firstName === ""
    ) {

        throw new Error(
            "نام بیمار را وارد کنید."
        );
    }


    if (
        lastName === ""
    ) {

        throw new Error(
            "نام خانوادگی بیمار را وارد کنید."
        );
    }


    if (
        nationalCode === ""
    ) {

        throw new Error(
            "کد ملی بیمار را وارد کنید."
        );
    }


    if (
        !/^\d+$/.test(
            nationalCode
        )
    ) {

        throw new Error(
            "کد ملی فقط باید شامل عدد باشد."
        );
    }


    // Mobile اختیاری است.
    if (
        mobile !== null &&
        !/^\+?\d+$/.test(
            mobile
        )
    ) {

        throw new Error(
            "شماره موبایل معتبر نیست."
        );
    }
}


// ============================================================
// Optional String -> null
// ============================================================

function emptyToNull(
    value
) {

    const text =
        value.trim();


    return text === ""
        ? null
        : text;
}


// ============================================================
// Mobile Normalizer
// ============================================================

function normalizePhone(
    value
) {

    const text =
        normalizeDigits(
            value || ""
        )
            .replace(
                /\s+/g,
                ""
            )
            .trim();


    return text === ""
        ? null
        : text;
}


// ============================================================
// Table Cell
// ============================================================

function createCell(
    value
) {

    const cell =
        document.createElement(
            "td"
        );


    cell.textContent =
        value ??
        "";


    return cell;
}


// ============================================================
// Study Info Line
// ============================================================

function createInfoLine(
    label,
    value
) {

    const container =
        document.createElement(
            "div"
        );


    const labelElement =
        document.createElement(
            "strong"
        );


    labelElement.textContent =
        `${label}: `;


    const valueElement =
        document.createElement(
            "span"
        );


    valueElement.textContent =
        value;


    container.appendChild(
        labelElement
    );


    container.appendChild(
        valueElement
    );


    return container;
}


// ============================================================
// Main Status
// ============================================================

function showStatus(
    message,
    isError
) {

    statusMessage.textContent =
        message;


    statusMessage
        .classList
        .toggle(
            "error",
            isError
        );
}


// ============================================================
// Persian / Arabic Digits -> English Digits
// ============================================================

function normalizeDigits(
    value
) {

    if (
        value == null
    ) {

        return "";
    }


    const persianDigits =
        "۰۱۲۳۴۵۶۷۸۹";


    const arabicDigits =
        "٠١٢٣٤٥٦٧٨٩";


    return String(
        value
    )
        .replace(
            /[۰-۹]/g,
            digit =>
                String(
                    persianDigits.indexOf(
                        digit
                    )
                )
        )
        .replace(
            /[٠-٩]/g,
            digit =>
                String(
                    arabicDigits.indexOf(
                        digit
                    )
                )
        );
}


// ============================================================
// Persian Date Display
// ============================================================

function formatPersianDate(
    value
) {

    if (
        !value
    ) {

        return "-";
    }


    try {

        return new Intl.DateTimeFormat(
            "fa-IR-u-ca-persian",
            {
                year:
                    "numeric",

                month:
                    "2-digit",

                day:
                    "2-digit"
            }
        )
            .format(
                new Date(
                    value
                )
            );
    }
    catch {

        return value;
    }
}


// ============================================================
// Persian DateTime Display
// ============================================================

function formatPersianDateTime(
    value
) {

    if (
        !value
    ) {

        return "-";
    }


    try {

        return new Intl.DateTimeFormat(
            "fa-IR-u-ca-persian",
            {
                year:
                    "numeric",

                month:
                    "2-digit",

                day:
                    "2-digit",

                hour:
                    "2-digit",

                minute:
                    "2-digit"
            }
        )
            .format(
                new Date(
                    value
                )
            );
    }
    catch {

        return value;
    }
}


// ============================================================
// Jalali / Gregorian Conversion
// ============================================================
//
// این قسمت وظیفه تبدیل تاریخ شمسی واردشده توسط کاربر
// به تاریخ Gregorian موردنیاز Backend را دارد.
//
// الگوریتم مستقل است و نیاز به Library خارجی ندارد.
//
// ============================================================

function div(
    a,
    b
) {

    return ~~(
        a /
        b
    );
}


function mod(
    a,
    b
) {

    return (
        a -
        ~~(
            a /
            b
        ) *
        b
    );
}


// ============================================================
// jalCal
// ============================================================

function jalCal(
    jy
) {

    const breaks = [
        -61,
        9,
        38,
        199,
        426,
        686,
        756,
        818,
        1111,
        1181,
        1210,
        1635,
        2060,
        2097,
        2192,
        2262,
        2324,
        2394,
        2456,
        3178
    ];


    const bl =
        breaks.length;


    const gy =
        jy +
        621;


    let leapJ =
        -14;


    let jp =
        breaks[0];


    let jm;


    let jump =
        0;


    if (
        jy < jp ||
        jy >= breaks[
        bl - 1
        ]
    ) {

        throw new Error(
            "سال شمسی خارج از محدوده معتبر است."
        );
    }


    for (
        let i = 1;
        i < bl;
        i += 1
    ) {

        jm =
            breaks[i];


        jump =
            jm -
            jp;


        if (
            jy < jm
        ) {

            break;
        }


        leapJ =
            leapJ +
            div(
                jump,
                33
            ) *
            8 +
            div(
                mod(
                    jump,
                    33
                ),
                4
            );


        jp =
            jm;
    }


    let n =
        jy -
        jp;


    leapJ =
        leapJ +
        div(
            n,
            33
        ) *
        8 +
        div(
            mod(
                n,
                33
            ) +
            3,
            4
        );


    if (
        mod(
            jump,
            33
        ) === 4 &&
        jump - n === 4
    ) {

        leapJ +=
            1;
    }


    const leapG =
        div(
            gy,
            4
        ) -
        div(
            (
                div(
                    gy,
                    100
                ) +
                1
            ) *
            3,
            4
        ) -
        150;


    const march =
        20 +
        leapJ -
        leapG;


    if (
        jump -
        n <
        6
    ) {

        n =
            n -
            jump +
            div(
                jump +
                4,
                33
            ) *
            33;
    }


    let leap =
        mod(
            mod(
                n +
                1,
                33
            ) -
            1,
            4
        );


    if (
        leap === -1
    ) {

        leap =
            4;
    }


    return {
        leap:
            leap,

        gy:
            gy,

        march:
            march
    };
}


// ============================================================
// Gregorian -> Julian Day Number
// ============================================================

function g2d(
    gy,
    gm,
    gd
) {

    let d =
        div(
            (
                gy +
                div(
                    gm -
                    8,
                    6
                ) +
                100100
            ) *
            1461,
            4
        ) +
        div(
            153 *
            mod(
                gm +
                9,
                12
            ) +
            2,
            5
        ) +
        gd -
        34840408;


    d =
        d -
        div(
            div(
                gy +
                100100 +
                div(
                    gm -
                    8,
                    6
                ),
                100
            ) *
            3,
            4
        ) +
        752;


    return d;
}


// ============================================================
// Julian Day Number -> Gregorian
// ============================================================

function d2g(
    jdn
) {

    let j =
        4 *
        jdn +
        139361631;


    j =
        j +
        div(
            div(
                4 *
                jdn +
                183187720,
                146097
            ) *
            3,
            4
        ) *
        4 -
        3908;


    const i =
        div(
            mod(
                j,
                1461
            ),
            4
        ) *
        5 +
        308;


    const gd =
        div(
            mod(
                i,
                153
            ),
            5
        ) +
        1;


    const gm =
        mod(
            div(
                i,
                153
            ),
            12
        ) +
        1;


    const gy =
        div(
            j,
            1461
        ) -
        100100 +
        div(
            8 -
            gm,
            6
        );


    return {
        gy:
            gy,

        gm:
            gm,

        gd:
            gd
    };
}


// ============================================================
// Jalali -> Julian Day Number
// ============================================================

function j2d(
    jy,
    jm,
    jd
) {

    const r =
        jalCal(
            jy
        );


    return (
        g2d(
            r.gy,
            3,
            r.march
        ) +
        (
            jm -
            1
        ) *
        31 -
        div(
            jm,
            7
        ) *
        (
            jm -
            7
        ) +
        jd -
        1
    );
}


// ============================================================
// Julian Day Number -> Jalali
// ============================================================

function d2j(
    jdn
) {

    const g =
        d2g(
            jdn
        );


    let jy =
        g.gy -
        621;


    const r =
        jalCal(
            jy
        );


    const jdn1f =
        g2d(
            g.gy,
            3,
            r.march
        );


    let k =
        jdn -
        jdn1f;


    let jd;
    let jm;


    if (
        k >= 0
    ) {

        if (
            k <= 185
        ) {

            jm =
                1 +
                div(
                    k,
                    31
                );


            jd =
                mod(
                    k,
                    31
                ) +
                1;


            return {
                jy:
                    jy,

                jm:
                    jm,

                jd:
                    jd
            };
        }


        k -=
            186;
    }
    else {

        jy -=
            1;


        k +=
            179;


        if (
            r.leap === 1
        ) {

            k +=
                1;
        }
    }


    jm =
        7 +
        div(
            k,
            30
        );


    jd =
        mod(
            k,
            30
        ) +
        1;


    return {
        jy:
            jy,

        jm:
            jm,

        jd:
            jd
    };
}


// ============================================================
// Jalali -> Gregorian
// ============================================================

function toGregorian(
    jy,
    jm,
    jd
) {

    return d2g(
        j2d(
            jy,
            jm,
            jd
        )
    );
}


// ============================================================
// Gregorian -> Jalali
// ============================================================

function toJalaali(
    gy,
    gm,
    gd
) {

    return d2j(
        g2d(
            gy,
            gm,
            gd
        )
    );
}


// ============================================================
// Validation تاریخ شمسی
// ============================================================

function isValidPersianDate(
    jy,
    jm,
    jd
) {

    if (
        !Number.isInteger(
            jy
        ) ||
        !Number.isInteger(
            jm
        ) ||
        !Number.isInteger(
            jd
        )
    ) {

        return false;
    }


    if (
        jy < 1 ||
        jm < 1 ||
        jm > 12 ||
        jd < 1
    ) {

        return false;
    }


    let maxDay;


    if (
        jm <= 6
    ) {

        maxDay =
            31;
    }
    else if (
        jm <= 11
    ) {

        maxDay =
            30;
    }
    else {

        try {

            // leap === 0 یعنی سال کبیسه شمسی
            maxDay =
                jalCal(
                    jy
                ).leap === 0
                    ? 30
                    : 29;
        }
        catch {

            return false;
        }
    }


    return (
        jd <=
        maxDay
    );
}


// ============================================================
// Parse تاریخ شمسی برای Backend
// ============================================================
//
// includeTime = false:
//
// 1363/07/15
//
// خروجی:
//
// 1984-10-07
//
//
//
// includeTime = true:
//
// 1405/06/22 14:30
//
// خروجی:
//
// 2026-09-13T14:30:00
//
//
//
// نکته مهم:
//
// عمداً Z / UTC اضافه نمی‌کنیم.
//
// چون SQL Server ما DateTime2 محلی نگه می‌دارد و نمی‌خواهیم
// ساعت مطب به دلیل تبدیل TimeZone جابه‌جا شود.
//
// ============================================================

function parsePersianDateForBackend(
    value,
    includeTime
) {

    const raw =
        normalizeDigits(
            value ||
            ""
        )
            .trim();


    // تاریخ Optional
    if (
        raw === ""
    ) {

        return null;
    }


    const pattern =
        includeTime
            ? /^(\d{4})\/(\d{1,2})\/(\d{1,2})(?:\s+(\d{1,2}):(\d{2}))?$/
            : /^(\d{4})\/(\d{1,2})\/(\d{1,2})$/;


    const match =
        raw.match(
            pattern
        );


    if (
        !match
    ) {

        throw new Error(
            includeTime
                ? "فرمت تاریخ صحیح نیست. نمونه صحیح: 1405/06/22 14:30"
                : "فرمت تاریخ تولد صحیح نیست. نمونه صحیح: 1363/07/15"
        );
    }


    const jy =
        Number(
            match[1]
        );


    const jm =
        Number(
            match[2]
        );


    const jd =
        Number(
            match[3]
        );


    if (
        !isValidPersianDate(
            jy,
            jm,
            jd
        )
    ) {

        throw new Error(
            "تاریخ شمسی واردشده معتبر نیست."
        );
    }


    // --------------------------------------------------------
    // Time
    // --------------------------------------------------------

    let hour =
        0;


    let minute =
        0;


    if (
        includeTime &&
        match[4] != null
    ) {

        hour =
            Number(
                match[4]
            );


        minute =
            Number(
                match[5]
            );


        if (
            hour < 0 ||
            hour > 23 ||
            minute < 0 ||
            minute > 59
        ) {

            throw new Error(
                "ساعت واردشده معتبر نیست."
            );
        }
    }


    // --------------------------------------------------------
    // Convert
    // --------------------------------------------------------

    const gregorian =
        toGregorian(
            jy,
            jm,
            jd
        );


    const yearText =
        String(
            gregorian.gy
        )
            .padStart(
                4,
                "0"
            );


    const monthText =
        String(
            gregorian.gm
        )
            .padStart(
                2,
                "0"
            );


    const dayText =
        String(
            gregorian.gd
        )
            .padStart(
                2,
                "0"
            );


    // BirthDate
    if (
        !includeTime
    ) {

        return (
            `${yearText}-${monthText}-${dayText}`
        );
    }


    // StudyDate
    const hourText =
        String(
            hour
        )
            .padStart(
                2,
                "0"
            );


    const minuteText =
        String(
            minute
        )
            .padStart(
                2,
                "0"
            );


    return (
        `${yearText}-${monthText}-${dayText}` +
        `T${hourText}:${minuteText}:00`
    );
}


// ============================================================
// Date -> Persian Input
// ============================================================

function formatPersianDateForInput(
    value
) {

    if (
        !value
    ) {

        return "";
    }


    const date =
        parseBackendDate(
            value
        );


    if (
        !date
    ) {

        return "";
    }


    const jalali =
        toJalaali(
            date.getFullYear(),
            date.getMonth() + 1,
            date.getDate()
        );


    return (
        `${jalali.jy}/` +
        `${String(jalali.jm).padStart(2, "0")}/` +
        `${String(jalali.jd).padStart(2, "0")}`
    );
}


// ============================================================
// DateTime -> Persian Input
// ============================================================

function formatPersianDateTimeForInput(
    value
) {

    if (
        !value
    ) {

        return "";
    }


    let date;


    if (
        value instanceof Date
    ) {

        date =
            value;
    }
    else {

        date =
            parseBackendDate(
                value
            );
    }


    if (
        !date ||
        Number.isNaN(
            date.getTime()
        )
    ) {

        return "";
    }


    const jalali =
        toJalaali(
            date.getFullYear(),
            date.getMonth() + 1,
            date.getDate()
        );


    return (
        `${jalali.jy}/` +
        `${String(jalali.jm).padStart(2, "0")}/` +
        `${String(jalali.jd).padStart(2, "0")} ` +
        `${String(date.getHours()).padStart(2, "0")}:` +
        `${String(date.getMinutes()).padStart(2, "0")}`
    );
}


// ============================================================
// Parse Backend Date
// ============================================================
//
// SQL DateTime2 معمولاً بدون TimeZone برمی‌گردد.
//
// برای جلوگیری از رفتار متفاوت Browserها، اگر String فاقد
// Z یا Offset باشد آن را به عنوان تاریخ محلی می‌سازیم.
//
// ============================================================

function parseBackendDate(
    value
) {

    if (
        value instanceof Date
    ) {

        return value;
    }


    if (
        typeof value !== "string"
    ) {

        const date =
            new Date(
                value
            );


        return Number.isNaN(
            date.getTime()
        )
            ? null
            : date;
    }


    // --------------------------------------------------------
    // YYYY-MM-DD
    // --------------------------------------------------------

    let match =
        value.match(
            /^(\d{4})-(\d{2})-(\d{2})$/
        );


    if (
        match
    ) {

        return new Date(
            Number(match[1]),
            Number(match[2]) - 1,
            Number(match[3]),
            0,
            0,
            0,
            0
        );
    }


    // --------------------------------------------------------
    // YYYY-MM-DDTHH:mm:ss...
    // بدون Offset
    // --------------------------------------------------------

    match =
        value.match(
            /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2}))?(?:\.\d+)?$/
        );


    if (
        match
    ) {

        return new Date(
            Number(match[1]),
            Number(match[2]) - 1,
            Number(match[3]),
            Number(match[4]),
            Number(match[5]),
            Number(match[6] || 0),
            0
        );
    }


    // --------------------------------------------------------
    // اگر Offset یا Z داشت Browser خودش Parse می‌کند.
    // --------------------------------------------------------

    const date =
        new Date(
            value
        );


    return Number.isNaN(
        date.getTime()
    )
        ? null
        : date;
}


// ============================================================
// Events - Search
// ============================================================

searchButton.addEventListener(
    "click",
    () => {

        loadPatients(
            patientSearch.value
        );
    }
);


patientSearch.addEventListener(
    "keydown",
    event => {

        if (
            event.key ===
            "Enter"
        ) {

            loadPatients(
                patientSearch.value
            );
        }
    }
);


clearSearchButton.addEventListener(
    "click",
    () => {

        patientSearch.value =
            "";


        loadPatients();
    }
);


includeInactivePatients.addEventListener(
    "change",
    () => {

        loadPatients(
            patientSearch.value
        );
    }
);


// ============================================================
// Events - New Patient
// ============================================================

newPatientButton.addEventListener(
    "click",
    openNewPatientForm
);


newPatientForm.addEventListener(
    "submit",
    async event => {

        event.preventDefault();


        await createPatient();
    }
);


function cancelNewPatient() {

    showPatientsScreen();
}


cancelNewPatientButton.addEventListener(
    "click",
    cancelNewPatient
);


cancelNewPatientButtonBottom.addEventListener(
    "click",
    cancelNewPatient
);


// ============================================================
// Events - Patient Details
// ============================================================

backToPatientsButton.addEventListener(
    "click",
    showPatientsScreen
);


editPatientButton.addEventListener(
    "click",
    openEditPatientForm
);


deactivatePatientButton.addEventListener(
    "click",
    togglePatientActiveStatus
);


newStudyButton.addEventListener(
    "click",
    openNewStudyForm
);


mergePatientButton.addEventListener(
    "click",
    openMergePatientForm
);


// ============================================================
// Events - Edit Patient
// ============================================================

editPatientForm.addEventListener(
    "submit",
    async event => {

        event.preventDefault();


        await updatePatient();
    }
);


function cancelEditPatient() {

    openPatient(
        selectedPatientID
    );
}


cancelEditPatientButton.addEventListener(
    "click",
    cancelEditPatient
);


cancelEditPatientButtonBottom.addEventListener(
    "click",
    cancelEditPatient
);


// ============================================================
// Events - New Study
// ============================================================

newStudyForm.addEventListener(
    "submit",
    async event => {

        event.preventDefault();


        await createStudy();
    }
);


function cancelNewStudy() {

    openPatient(
        selectedPatientID
    );
}


cancelNewStudyButton.addEventListener(
    "click",
    cancelNewStudy
);


cancelNewStudyButtonBottom.addEventListener(
    "click",
    cancelNewStudy
);


// ============================================================
// Events - Edit Study
// ============================================================

editStudyForm.addEventListener(
    "submit",
    async event => {

        event.preventDefault();


        await updateStudy();
    }
);


function cancelEditStudy() {

    // برگشت از فرم ویرایش Study نباید به API وابسته باشد.
    hideMainSections();

    patientDetailsSection
        .classList
        .remove(
            "hidden"
        );

    window.scrollTo(
        0,
        0
    );
}


cancelEditStudyButton.addEventListener(
    "click",
    cancelEditStudy
);


cancelEditStudyButtonBottom.addEventListener(
    "click",
    cancelEditStudy
);


// ============================================================
// Events - Upload
// ============================================================

uploadImageForm.addEventListener(
    "submit",
    async event => {

        event.preventDefault();


        await uploadImage();
    }
);


function cancelUploadImage() {

    openPatient(
        selectedPatientID
    );
}


cancelUploadImageButton.addEventListener(
    "click",
    cancelUploadImage
);


cancelUploadImageButtonBottom.addEventListener(
    "click",
    cancelUploadImage
);


// ============================================================
// Events - Merge
// ============================================================

mergePatientForm.addEventListener(
    "submit",
    async event => {

        event.preventDefault();


        await mergePatient();
    }
);


function cancelMergePatient() {

    openPatient(
        selectedPatientID
    );
}


cancelMergePatientButton.addEventListener(
    "click",
    cancelMergePatient
);


cancelMergePatientButtonBottom.addEventListener(
    "click",
    cancelMergePatient
);


// ============================================================
// Events - Image Modal
// ============================================================

closeImageModalButton.addEventListener(
    "click",
    closeLargeImage
);


imageModal.addEventListener(
    "click",
    event => {

        if (
            event.target ===
            imageModal
        ) {

            closeLargeImage();
        }
    }
);


// ============================================================
// Escape
// ============================================================

document.addEventListener(
    "keydown",
    event => {

        if (
            event.key ===
            "Escape" &&
            !imageModal
                .classList
                .contains(
                    "hidden"
                )
        ) {

            closeLargeImage();
        }
    }
);


// ============================================================
// Start
// ============================================================
//
// قبل از بارگذاری اطلاعات برنامه، وضعیت Login بررسی می‌شود.
// در اولین اجرا، اگر هیچ کاربری وجود نداشته باشد، فرم ساخت
// مدیر اولیه نمایش داده خواهد شد.
// ============================================================

if (window.DentalRaySecurity) {

    window.DentalRaySecurity.start(
        loadPatients
    );
}
else {

    loadPatients();
}
