// Staff/person management UI for DentalRay.
// Kept separate from app.js so Patient/Study behavior is not disturbed.

(() => {
    const main = document.querySelector(".page-container");
    if (!main) return;

    const section = document.createElement("section");
    section.id = "staffSection";
    section.className = "card hidden";
    section.innerHTML = `
        <div class="section-header">
            <div><h2>اشخاص و پرسنل</h2><p>ثبت و ویرایش کارمندان و دندانپزشکان</p></div>
            <button id="newStaffButton" type="button">+ شخص جدید</button>
        </div>
        <div class="search-container">
            <input id="staffSearch" type="text" placeholder="نام، نام خانوادگی یا کد ملی..." autocomplete="off">
            <button id="staffSearchButton" type="button">جستجو</button>
        </div>
        <div id="staffStatus" class="status-message"></div>
        <div class="table-container"><table class="patient-table">
            <thead><tr><th>نام</th><th>نام خانوادگی</th><th>کد ملی</th><th>نوع</th><th>عملیات</th></tr></thead>
            <tbody id="staffTableBody"></tbody>
        </table></div>
        <form id="staffForm" class="hidden" style="margin-top:20px">
            <input id="staffID" type="hidden">
            <div class="form-grid">
                <div class="form-field"><label for="staffFirstName">نام</label><input id="staffFirstName" maxlength="100" required></div>
                <div class="form-field"><label for="staffLastName">نام خانوادگی</label><input id="staffLastName" maxlength="100" required></div>
                <div class="form-field"><label for="staffNationalCode">کد ملی</label><input id="staffNationalCode" maxlength="10" inputmode="numeric" pattern="[0-9۰-۹٠-٩]{10}" required><small class="field-hint">کد ملی معتبر ۱۰ رقمی</small></div>
                <div class="form-field"><label for="staffType">نوع شخص</label><select id="staffType" required><option value="1">کارمند</option><option value="2">دندانپزشک</option></select></div>
                <div id="staffSpecialtyField" class="form-field hidden"><label for="staffSpecialtyID">تخصص</label><select id="staffSpecialtyID"></select></div>
                <div class="form-field"><label for="staffStartDate">تاریخ شروع</label><input id="staffStartDate" type="date" required></div>
                <div class="form-field"><label for="staffEndDate">تاریخ پایان</label><input id="staffEndDate" type="date"></div>
            </div>
            <div id="staffFormStatus" class="status-message"></div>
            <div class="form-actions"><button type="submit">ذخیره</button><button id="cancelStaffButton" type="button" class="secondary-button">انصراف</button></div>
        </form>`;
    main.appendChild(section);

    const header = document.querySelector(".header-content");
    if (header) {
        const b = document.createElement("button");
        b.id = "staffNavButton";
        b.type = "button";
        b.textContent = "اشخاص / پرسنل";
        b.className = "secondary-button";
        header.appendChild(b);
        b.onclick = () => {
            document.querySelectorAll(".page-container > section").forEach(x => x.classList.add("hidden"));
            section.classList.remove("hidden");
            loadStaff();
        };
    }

    const $ = id => document.getElementById(id);
    const normalize = value => String(value ?? "").replace(/[۰-۹]/g, d => "۰۱۲۳۴۵۶۷۸۹".indexOf(d)).replace(/[٠-٩]/g, d => "٠١٢٣٤٥٦٧٨٩".indexOf(d));

    async function apiJson(url, options) {
        const r = await fetch(url, options);
        let x = {};
        try { x = await r.json(); } catch {}
        if (!r.ok || x.success === false) throw new Error(x.message || x.messageEn || "عملیات انجام نشد.");
        return x;
    }

    async function loadStaff() {
        try {
            $("staffStatus").textContent = "در حال دریافت اطلاعات...";
            const q = $("staffSearch").value.trim();
            const x = await apiJson("/api/staff" + (q ? "?search=" + encodeURIComponent(q) : ""));
            $("staffTableBody").innerHTML = "";
            (x.staff || []).forEach(s => {
                const tr = document.createElement("tr");
                [s.firstName, s.lastName, s.nationalCode, s.staffType === 2 ? "دندانپزشک" : "کارمند"].forEach(v => {
                    const td = document.createElement("td"); td.textContent = v ?? ""; tr.appendChild(td);
                });
                const td = document.createElement("td"), edit = document.createElement("button");
                edit.type = "button"; edit.textContent = "ویرایش"; edit.onclick = () => openForm(s);
                td.appendChild(edit); tr.appendChild(td); $("staffTableBody").appendChild(tr);
            });
            $("staffStatus").textContent = x.count ? "" : "شخصی ثبت نشده است.";
        } catch (e) { $("staffStatus").textContent = e.message; $("staffStatus").classList.add("error"); }
    }

    async function loadSpecialties(selected) {
        const select = $("staffSpecialtyID");
        select.innerHTML = '<option value="">در حال دریافت تخصص‌ها...</option>';

        try {
            const x = await apiJson("/api/staff/specialties");
            select.innerHTML = '<option value="">انتخاب تخصص</option>';

            (x.specialties || []).forEach(s => {
                const option = document.createElement("option");
                option.value = String(s.specialtyID);
                option.textContent = s.specialtyName;
                select.appendChild(option);
            });

            if (selected != null)
                select.value = String(selected);
        } catch (e) {
            select.innerHTML = '<option value="">دریافت تخصص‌ها ناموفق بود</option>';
            $("staffFormStatus").textContent = e.message;
            $("staffFormStatus").classList.add("error");
        }
    }

    function updateSpecialtyVisibility() {
        const dentist = $("staffType").value === "2";
        $("staffSpecialtyField").classList.toggle("hidden", !dentist);
        $("staffSpecialtyID").required = dentist;
        if (!dentist) $("staffSpecialtyID").value = "";
    }

    function openForm(s = null) {
        $("staffForm").reset();
        $("staffID").value = s?.staffID || "";
        $("staffFirstName").value = s?.firstName || "";
        $("staffLastName").value = s?.lastName || "";
        $("staffNationalCode").value = s?.nationalCode || "";
        $("staffType").value = String(s?.staffType || 1);
        $("staffStartDate").value = s?.startDate ? String(s.startDate).slice(0,10) : new Date().toISOString().slice(0,10);
        $("staffEndDate").value = s?.endDate ? String(s.endDate).slice(0,10) : "";
        updateSpecialtyVisibility();
        loadSpecialties(s?.specialtyID);
        $("staffFormStatus").textContent = "";
        $("staffForm").classList.remove("hidden");
        $("staffFirstName").focus();
    }

    $("staffType").onchange = updateSpecialtyVisibility;
    $("newStaffButton").onclick = () => openForm();
    $("cancelStaffButton").onclick = () => $("staffForm").classList.add("hidden");
    $("staffSearchButton").onclick = loadStaff;
    $("staffSearch").onkeydown = e => { if (e.key === "Enter") loadStaff(); };

    $("staffForm").onsubmit = async e => {
        e.preventDefault();
        try {
            const nationalCode = normalize($("staffNationalCode").value.trim());
            if (!/^\d{10}$/.test(nationalCode)) throw new Error("کد ملی باید دقیقاً ۱۰ رقم باشد.");
            const id = Number($("staffID").value || 0);
            const body = {
                staffID: id,
                nationalCode,
                firstName: $("staffFirstName").value.trim(),
                lastName: $("staffLastName").value.trim(),
                staffType: Number($("staffType").value),
                specialtyID: $("staffType").value === "2" ? Number($("staffSpecialtyID").value) || null : null,
                startDate: $("staffStartDate").value,
                endDate: $("staffEndDate").value || null
            };
            $("staffFormStatus").textContent = "در حال ذخیره...";
            await apiJson(id ? `/api/staff/${id}` : "/api/staff", {
                method: id ? "PUT" : "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(body)
            });
            $("staffForm").classList.add("hidden");
            await loadStaff();
            if (window.showToast) window.showToast("اطلاعات شخص ذخیره شد.");
        } catch (e2) {
            $("staffFormStatus").textContent = e2.message;
            $("staffFormStatus").classList.add("error");
        }
    };
})();
