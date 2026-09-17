(() => {
    let section, navButton, list, nameInput, saveButton, cancelButton, status, editing=null;

    function apiError(x,fallback){ return x?.message || x?.messageEn || fallback; }
    function toast(m,type){ if(window.showToast) window.showToast(m,type); }

    function ensureUi(){
        if(section) return;
        section=document.createElement("section"); section.id="imageTypesAdminSection"; section.className="card hidden";
        section.innerHTML=`
          <div class="section-header"><div><h2>مدیریت انواع تصویر</h2><p>تعریف و مدیریت انواع تصاویر رادیولوژی مانند پانورامیک، CBCT و بایت‌وینگ</p></div><button id="newImageTypeAdminButton" type="button">+ نوع تصویر جدید</button></div>
          <form id="imageTypeAdminForm" class="hidden" style="margin-top:20px">
            <div class="form-grid"><div class="form-field"><label for="imageTypeAdminName">نام نوع تصویر</label><input id="imageTypeAdminName" maxlength="150" required /></div><div class="form-field"><label class="checkbox-row"><input id="imageTypeAdminActive" type="checkbox" checked /><span>فعال</span></label></div></div>
            <div class="form-actions"><button type="submit" class="primary-button">ذخیره</button><button type="button" class="secondary-button" id="imageTypeAdminCancel">انصراف از ویرایش</button></div>
          </form>
          <div id="imageTypeAdminStatus" class="status-message"></div>
          <div class="table-container"><table class="patient-table"><thead><tr><th>نوع تصویر</th><th>وضعیت</th><th>عملیات</th></tr></thead><tbody id="imageTypeAdminList"></tbody></table></div>`;
        document.querySelector(".page-container")?.appendChild(section);
        list=section.querySelector("#imageTypeAdminList"); nameInput=section.querySelector("#imageTypeAdminName");
        saveButton=section.querySelector('button[type="submit"]'); cancelButton=section.querySelector("#imageTypeAdminCancel"); status=section.querySelector("#imageTypeAdminStatus");
        section.querySelector("form").onsubmit=e=>{e.preventDefault();save();}; cancelButton.onclick=reset; section.querySelector("#newImageTypeAdminButton").onclick=()=>openForm();

        navButton=document.createElement("button"); navButton.type="button"; navButton.textContent="مدیریت انواع تصویر"; navButton.className="secondary-button"; navButton.hidden=true;
        navButton.onclick=()=>{document.querySelectorAll(".page-container > section").forEach(x=>x.classList.add("hidden"));section.classList.remove("hidden");load();};
        document.querySelector(".header-content")?.appendChild(navButton);
    }
    function syncAuth(){ensureUi(); const ok=window.dentalRayCurrentUser?.isSuperAdmin===true; navButton.hidden=!ok;if(!ok)section.classList.add("hidden");}
    function reset(){editing=null;nameInput.value="";saveButton.textContent="ذخیره";section.querySelector("#imageTypeAdminActive").checked=true;section.querySelector("#imageTypeAdminForm").classList.add("hidden");}\n    function openForm(item=null){editing=item;nameInput.value=item?.imageTypeName||"";section.querySelector("#imageTypeAdminActive").checked=item?.isActive??true;saveButton.textContent=item?"ذخیره ویرایش":"ذخیره";section.querySelector("#imageTypeAdminForm").classList.remove("hidden");nameInput.focus();}
    async function load(){
        status.textContent="در حال دریافت انواع تصویر...";
        const r=await fetch("/api/admin/imagetypes"),x=await r.json(); if(!r.ok){status.textContent=apiError(x,"خطا در دریافت اطلاعات.");return;}
        list.innerHTML=""; (x.imageTypes||[]).forEach(item=>{
            const tr=document.createElement("tr");
            tr.innerHTML=`<td></td><td>${item.isActive?"فعال":"غیرفعال"}</td><td></td>`; tr.children[0].textContent=item.imageTypeName;
            const edit=document.createElement("button");edit.type="button";edit.textContent="ویرایش";edit.onclick=()=>openForm(item);
            const active=document.createElement("button");active.type="button";active.className="secondary-button";active.textContent=item.isActive?"غیرفعال‌سازی":"فعال‌سازی";active.onclick=()=>setActive(item,!item.isActive);
            tr.children[2].append(edit,document.createTextNode(" "),active);list.appendChild(tr);
        }); status.textContent=x.count? "": "نوع تصویری ثبت نشده است."; reset();
    }
    async function save(){
        const name=nameInput.value.trim();if(!name)return;
        const url=editing?`/api/admin/imagetypes/${editing.imageTypeID}`:"/api/admin/imagetypes";
        const isActive=section.querySelector("#imageTypeAdminActive").checked;\n        const body=editing?{imageTypeID:editing.imageTypeID,imageTypeName:name,isActive}:{imageTypeName:name,isActive};
        const r=await fetch(url,{method:editing?"PUT":"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(body)}),x=await r.json();
        if(!r.ok){status.textContent=apiError(x,"ذخیره انجام نشد.");return;} toast("نوع تصویر ذخیره شد.");await load();
    }
    async function setActive(item,value){
        const r=await fetch(`/api/admin/imagetypes/${item.imageTypeID}/active`,{method:"PATCH",headers:{"Content-Type":"application/json"},body:JSON.stringify({isActive:value})}),x=await r.json();
        if(!r.ok){status.textContent=apiError(x,"عملیات انجام نشد.");return;}await load();
    }
    document.addEventListener("dentalray-auth-changed",syncAuth);ensureUi();syncAuth();
})();