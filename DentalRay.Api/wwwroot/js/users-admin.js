// SuperAdmin-only user account management. Normal usernames are always Staff NationalCode.
(() => {
 const main=document.querySelector(".page-container"),header=document.querySelector(".header-content"); if(!main||!header)return;
 const section=document.createElement("section"); section.id="usersAdminSection"; section.className="card hidden";
 section.innerHTML=`<div class="section-header"><div><h2>مدیریت کاربران</h2><p>نام کاربری کاربران عادی همیشه کد ملی پرسنل است.</p></div><button id="newUserAdminButton" type="button">+ کاربر جدید</button></div>
 <div id="usersAdminStatus" class="status-message"></div>
 <div class="table-container"><table class="patient-table"><thead><tr><th>نام و نام خانوادگی</th><th>نام کاربری / کد ملی</th><th>نوع پرسنل</th><th>وضعیت</th><th>عملیات</th></tr></thead><tbody id="usersAdminBody"></tbody></table></div>
 <form id="userAdminForm" class="hidden" style="margin-top:20px"><input id="userAdminID" type="hidden">
 <div class="form-grid"><div class="form-field" id="userStaffField"><label for="userAdminStaff">پرسنل</label><select id="userAdminStaff"></select></div>
 <div class="form-field"><label for="userAdminPassword">رمز عبور <span id="userPasswordHint"></span></label><input id="userAdminPassword" type="password" autocomplete="new-password"></div>
 <div class="form-field"><label class="checkbox-row"><input id="userAdminActive" type="checkbox" checked><span>فعال</span></label></div></div>
 <div id="userAdminFormStatus" class="status-message"></div><div class="form-actions"><button type="submit">ذخیره</button><button id="cancelUserAdmin" type="button" class="secondary-button">انصراف</button></div></form>`;
 main.appendChild(section);
 const nav=document.createElement("button");nav.type="button";nav.className="secondary-button hidden";nav.textContent="مدیریت کاربران";header.appendChild(nav);
 const $=id=>document.getElementById(id); let editing=null;
 async function api(url,opt){const r=await fetch(url,opt);let x={};try{x=await r.json();}catch{}if(!r.ok||x.success===false)throw new Error(x.message||x.messageEn||"عملیات انجام نشد.");return x;}
 function sync(){nav.classList.toggle("hidden",window.dentalRayCurrentUser?.isSuperAdmin!==true);}
 window.addEventListener("dentalray-auth-changed",sync);sync();
 nav.onclick=async()=>{document.querySelectorAll(".page-container > section").forEach(x=>x.classList.add("hidden"));section.classList.remove("hidden");await load();};
 async function load(){try{const x=await api("/api/admin/users");$("usersAdminBody").innerHTML="";(x.users||[]).forEach(u=>{const tr=document.createElement("tr");tr.innerHTML="<td></td><td></td><td></td><td></td><td></td>";tr.children[0].textContent=u.firstName+" "+u.lastName;tr.children[1].textContent=u.nationalCode;tr.children[2].textContent=u.staffType===2?"دندانپزشک":"کارمند";tr.children[3].textContent=u.isActive?"فعال":"غیرفعال";const b=document.createElement("button");b.type="button";b.textContent="ویرایش / رمز عبور";b.onclick=()=>openEdit(u);tr.children[4].appendChild(b);$("usersAdminBody").appendChild(tr);});$("usersAdminStatus").textContent="";}catch(e){$("usersAdminStatus").textContent=e.message;}}
 async function openNew(){editing=null;const x=await api("/api/admin/users/available-staff");$("userAdminStaff").innerHTML='<option value="">انتخاب پرسنل</option>';(x.staff||[]).forEach(s=>{const o=document.createElement("option");o.value=s.staffID;o.textContent=s.firstName+" "+s.lastName+" — "+s.nationalCode;$("userAdminStaff").appendChild(o);});$("userStaffField").classList.remove("hidden");$("userAdminPassword").required=true;$("userPasswordHint").textContent="";$("userAdminPassword").value="";$("userAdminActive").checked=true;$("userAdminForm").classList.remove("hidden");}
 function openEdit(u){editing=u;$("userStaffField").classList.add("hidden");$("userAdminPassword").required=false;$("userPasswordHint").textContent="(برای عدم تغییر خالی بگذارید)";$("userAdminPassword").value="";$("userAdminActive").checked=u.isActive;$("userAdminForm").classList.remove("hidden");}
 $("newUserAdminButton").onclick=openNew;$("cancelUserAdmin").onclick=()=>$("userAdminForm").classList.add("hidden");
 $("userAdminForm").onsubmit=async e=>{e.preventDefault();try{const body=editing?{isActive:$("userAdminActive").checked,newPassword:$("userAdminPassword").value||null}:{staffID:Number($("userAdminStaff").value),password:$("userAdminPassword").value,isActive:$("userAdminActive").checked};await api(editing?"/api/admin/users/"+editing.userID:"/api/admin/users",{method:editing?"PUT":"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(body)});$("userAdminForm").classList.add("hidden");await load();}catch(err){$("userAdminFormStatus").textContent=err.message;}};
})();