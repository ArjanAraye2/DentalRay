// SuperAdmin-only Dental Specialty management screen.
(() => {
    const main = document.querySelector(".page-container");
    const header = document.querySelector(".header-content");
    if (!main || !header) return;

    const section = document.createElement("section");
    section.id = "specialtyAdminSection";
    section.className = "card hidden";
    section.innerHTML = `
      <div class="section-header"><div><h2>مدیریت تخصص‌ها</h2><p>این بخش فقط برای مدیر سیستم است.</p></div><button id="newSpecialtyButton" type="button">+ تخصص جدید</button></div>
      <div id="specialtyAdminStatus" class="status-message"></div>
      <div class="table-container"><table class="patient-table"><thead><tr><th>تخصص</th><th>وضعیت</th><th>عملیات</th></tr></thead><tbody id="specialtyAdminBody"></tbody></table></div>
      <form id="specialtyAdminForm" class="hidden" style="margin-top:20px">
        <input id="specialtyAdminID" type="hidden">
        <div class="form-grid"><div class="form-field"><label for="specialtyAdminName">نام تخصص</label><input id="specialtyAdminName" maxlength="150" required></div>
        <div class="form-field"><label class="checkbox-row"><input id="specialtyAdminActive" type="checkbox" checked><span>فعال</span></label></div></div>
        <div id="specialtyAdminFormStatus" class="status-message"></div>
        <div class="form-actions"><button type="submit">ذخیره</button><button id="cancelSpecialtyAdmin" type="button" class="secondary-button">انصراف</button></div>
      </form>`;
    main.appendChild(section);

    const nav = document.createElement("button");
    nav.id = "specialtyAdminNav";
    nav.type = "button";
    nav.className = "secondary-button hidden";
    nav.textContent = "مدیریت تخصص‌ها";
    header.appendChild(nav);

    const $ = id => document.getElementById(id);
    async function api(url, options) {
        const r = await fetch(url, options);
        let x = {}; try { x = await r.json(); } catch {}
        if (!r.ok || x.success === false) throw new Error(x.message || x.messageEn || "عملیات انجام نشد.");
        return x;
    }
    function isSuper() { return window.dentalRayCurrentUser?.isSuperAdmin === true; }
    function syncVisibility() { nav.classList.toggle("hidden", !isSuper()); }
    window.addEventListener("dentalray-auth-changed", syncVisibility);
    setTimeout(syncVisibility, 0);

    nav.onclick = async () => {
        if (!isSuper()) return;
        document.querySelectorAll(".page-container > section").forEach(x => x.classList.add("hidden"));
        section.classList.remove("hidden");
        await load();
    };

    async function load() {
        try {
            $("specialtyAdminStatus").textContent = "در حال دریافت تخصص‌ها...";
            const x = await api("/api/admin/specialties");
            $("specialtyAdminBody").innerHTML = "";
            (x.specialties || []).forEach(s => {
                const tr = document.createElement("tr");
                const n = document.createElement("td"); n.textContent = s.specialtyName;
                const a = document.createElement("td"); a.textContent = s.isActive ? "فعال" : "غیرفعال";
                const actions = document.createElement("td");
                const edit = document.createElement("button"); edit.type="button"; edit.textContent="ویرایش"; edit.onclick=()=>openForm(s);
                const toggle = document.createElement("button"); toggle.type="button"; toggle.className="secondary-button"; toggle.textContent=s.isActive?"غیرفعال‌سازی":"فعال‌سازی";
                toggle.onclick=async()=>{await api(`/api/admin/specialties/${s.specialtyID}/active`,{method:"PATCH",headers:{"Content-Type":"application/json"},body:JSON.stringify({isActive:!s.isActive})});await load();};
                actions.append(edit, document.createTextNode(" "), toggle); tr.append(n,a,actions); $("specialtyAdminBody").appendChild(tr);
            });
            $("specialtyAdminStatus").textContent = "";
        } catch(e) { $("specialtyAdminStatus").textContent=e.message; $("specialtyAdminStatus").classList.add("error"); }
    }
    function openForm(s=null) {
        $("specialtyAdminID").value=s?.specialtyID||"";
        $("specialtyAdminName").value=s?.specialtyName||"";
        $("specialtyAdminActive").checked=s?.isActive??true;
        $("specialtyAdminFormStatus").textContent="";
        $("specialtyAdminForm").classList.remove("hidden");
        $("specialtyAdminName").focus();
    }
    $("newSpecialtyButton").onclick=()=>openForm();
    $("cancelSpecialtyAdmin").onclick=()=>$("specialtyAdminForm").classList.add("hidden");
    $("specialtyAdminForm").onsubmit=async e=>{
        e.preventDefault();
        try {
            const id=Number($("specialtyAdminID").value||0);
            const body={specialtyID:id,specialtyName:$("specialtyAdminName").value.trim(),isActive:$("specialtyAdminActive").checked};
            await api(id?`/api/admin/specialties/${id}`:"/api/admin/specialties",{method:id?"PUT":"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(body)});
            $("specialtyAdminForm").classList.add("hidden"); await load();
        } catch(e2) { $("specialtyAdminFormStatus").textContent=e2.message; $("specialtyAdminFormStatus").classList.add("error"); }
    };
})();
