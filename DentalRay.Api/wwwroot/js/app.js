// DentalRay Frontend
// Patient/Study forms plus patient-owned radiology image workflow.

function byId(id) { return document.getElementById(id); }
let selectedPatientID=null, selectedPatient=null, selectedStudyID=null, selectedStudy=null;
let pendingCameraFile=null, cameraPreviewUrl=null;
let studyDetailsSaveInProgress=false;

const ids=["patientsSection","patientStatistics","statTotalPatients","statActivePatients","statInactivePatients","statPatientsWithStudies","patientSearch","searchButton","clearSearchButton","includeInactivePatients","newPatientButton","patientsTableBody","statusMessage","patientDetailsSection","studyDetailsSection","studyImagesSection","backToPatientDetailsButton","backToStudyDetailsButton","studyDetailsTitle","studyDetailsDate","studyDetailsForm","studyDetailsType","studyDetailsBodyPart","studyDetailsStudyDate","studyDetailsDescription","studyDetailsReport","studyDetailsDentalChart","studyDetailsStatus","studyDetailsUploadButton","studyDetailsEditButton","studyDetailsImagesButton","studyDetailsSaveButton","studyDetailsCancelButton","studyImagesTitle","studyDetailsImagesStatus","studyDetailsImagesGrid","backToPatientsButton","editPatientButton","newStudyButton","printPatientButton","mergePatientButton","deactivatePatientButton","patientFullName","patientDisplayCode","patientNationalCode","patientStatusBadge","patientProfilePhoto","patientPhotoInput","patientPhotoButton","detailPatientCode","detailFirstName","detailLastName","detailNationalCode","detailMobile","detailBirthDate","detailGender","detailIsActive","detailAddress","detailDescription","studyCount","totalImageCount","studiesContainer","newPatientSection","newPatientForm","cancelNewPatientButton","cancelNewPatientButtonBottom","newFirstName","newLastName","newNationalCode","newMobile","newBirthDate","newGender","newAddress","newDescription","newPatientStatus","editPatientSection","editPatientForm","cancelEditPatientButton","cancelEditPatientButtonBottom","editFirstName","editLastName","editNationalCode","editMobile","editBirthDate","editGender","editAddress","editDescription","editPatientStatus","newStudySection","newStudyForm","cancelNewStudyButton","cancelNewStudyButtonBottom","newStudyType","newBodyPart","newStudyDate","newStudyDescription","newStudyReport","newStudyStatus","editStudySection","editStudyForm","cancelEditStudyButton","cancelEditStudyButtonBottom","editStudySubtitle","editStudyType","editBodyPart","editStudyDate","editStudyDescription","editStudyReport","editStudyStatus","uploadImageSection","uploadImageForm","cancelUploadImageButton","cancelUploadImageButtonBottom","uploadImageStudyInfo","uploadImageType","imageFileInput","cameraFileInput","cameraPreviewPanel","cameraPreviewImage","confirmCameraButton","retakeCameraButton","uploadImageStatus","mergePatientSection","mergePatientForm","cancelMergePatientButton","cancelMergePatientButtonBottom","mergeTargetNationalCode","mergePatientStatus","imageModal","closeImageModalButton","zoomOutImageButton","zoomInImageButton","rotateLeftImageButton","rotateRightImageButton","flipHorizontalImageButton","resetImageViewButton","largeImage","largeImageCaption","confirmModal","confirmTitle","confirmMessage","confirmYesButton","confirmNoButton","toastContainer"];
const E={}; ids.forEach(id=>E[id]=byId(id));
E.lastStudyDateSummary=byId("lastStudyDateSummary");
E.recentStudiesSummary=byId("recentStudiesSummary");

function hideMainSections(){[E.patientsSection,E.patientDetailsSection,E.studyDetailsSection,E.studyImagesSection,E.newPatientSection,E.editPatientSection,E.newStudySection,E.editStudySection,E.uploadImageSection,E.mergePatientSection].forEach(x=>x?.classList.add("hidden"));}
function showPatientsScreen(){hideMainSections();E.patientsSection.classList.remove("hidden");window.scrollTo(0,0);}
function showToast(message,type="success",title=""){const t=document.createElement("div");t.className=`toast ${type}`;t.innerHTML=`<div class="toast-title"></div><div class="toast-message"></div>`;t.children[0].textContent=title||(type==="success"?"انجام شد":type==="error"?"خطا":"توجه");t.children[1].textContent=message;E.toastContainer.appendChild(t);setTimeout(()=>t.remove(),4300);}
function askConfirmation({title="تأیید عملیات",message,confirmText="تأیید",danger=true}){return new Promise(resolve=>{E.confirmTitle.textContent=title;E.confirmMessage.textContent=message;E.confirmYesButton.textContent=confirmText;E.confirmYesButton.classList.toggle("danger-button",danger);E.confirmModal.classList.remove("hidden");const done=v=>{E.confirmModal.classList.add("hidden");E.confirmYesButton.onclick=E.confirmNoButton.onclick=null;resolve(v);};E.confirmYesButton.onclick=()=>done(true);E.confirmNoButton.onclick=()=>done(false);});}
function getApiError(r,f){const m=r?.message||r?.messageEn||r?.error;if(!m)return f;const translations={"A patient with this NationalCode already exists.":"بیماری با این کد ملی قبلاً ثبت شده است.","NationalCode must contain only digits.":"کد ملی فقط باید شامل عدد باشد.","PatientID must be greater than zero.":"شناسه بیمار معتبر نیست.","Patient not found.":"بیمار پیدا نشد.","StudyType is required.":"نوع رادیولوژی را وارد کنید.","StudyType cannot be longer than 50 characters.":"نوع رادیولوژی نمی‌تواند بیشتر از ۵۰ نویسه باشد.","BodyPart cannot be longer than 100 characters.":"ناحیه نمی‌تواند بیشتر از ۱۰۰ نویسه باشد.","Description cannot be longer than 1000 characters.":"توضیحات نمی‌تواند بیشتر از ۱۰۰۰ نویسه باشد.","Study creation failed.":"ثبت رادیولوژی انجام نشد.","StudyDate is required.":"تاریخ رادیولوژی الزامی است.","Study not found.":"رادیولوژی پیدا نشد."};return translations[m]||m;}
function setFormStatus(el,msg,error){el.textContent=msg;el.classList.toggle("error",!!error);}
function normalizeDigits(v){const p="۰۱۲۳۴۵۶۷۸۹",a="٠١٢٣٤٥٦٧٨٩";return String(v??"").replace(/[۰-۹]/g,d=>p.indexOf(d)).replace(/[٠-٩]/g,d=>a.indexOf(d));}
function normalizePhone(v){const s=normalizeDigits(v).replace(/\s+/g,"").trim();return s||null;}
function emptyToNull(v){const s=v.trim();return s||null;}
function createCell(v){const td=document.createElement("td");td.textContent=v??"";return td;}
function createInfoLine(l,v){const d=document.createElement("div"),b=document.createElement("strong"),s=document.createElement("span");b.textContent=`${l}: `;s.textContent=v;d.append(b,s);return d;}
function formatPersianDate(v){if(!v)return "-";try{return new Intl.DateTimeFormat("fa-IR-u-ca-persian",{year:"numeric",month:"2-digit",day:"2-digit"}).format(new Date(v));}catch{return v;}}
function formatPersianDateTime(v){if(!v)return "-";try{return new Intl.DateTimeFormat("fa-IR-u-ca-persian",{year:"numeric",month:"2-digit",day:"2-digit",hour:"2-digit",minute:"2-digit"}).format(new Date(v));}catch{return v;}}
// Design D shows a friendly code (P-001), while PatientID remains the real database key.
function formatPatientCode(patientID){const id=Number(patientID);return Number.isInteger(id)&&id>0?`P-${String(id).padStart(3,"0")}`:"-";}
function formatPatientGender(gender){const value=Number(gender);if(value===1)return "مرد";if(value===2)return "زن";return "-";}
function escapeHtml(value){return String(value??"").replace(/[&<>"']/g,char=>({"&":"&amp;","<":"&lt;",">":"&gt;","\"":"&quot;","'":"&#39;"})[char]);}
function formatJalaliDateInput(value){const d=normalizeDigits(value).replace(/\D/g,"").slice(0,8);if(d.length<=4)return d;if(d.length<=6)return `${d.slice(0,4)}/${d.slice(4)}`;return `${d.slice(0,4)}/${d.slice(4,6)}/${d.slice(6)}`;}
function enableJalaliDateMask(input){input?.addEventListener("input",()=>{input.value=formatJalaliDateInput(input.value);});}
// Study date/time mask: user types digits; DentalRay inserts /, space and : automatically.
function formatJalaliDateTimeInput(value){const d=normalizeDigits(value).replace(/\D/g,"").slice(0,12);if(d.length<=4)return d;if(d.length<=6)return `${d.slice(0,4)}/${d.slice(4)}`;if(d.length<=8)return `${d.slice(0,4)}/${d.slice(4,6)}/${d.slice(6)}`;if(d.length<=10)return `${d.slice(0,4)}/${d.slice(4,6)}/${d.slice(6,8)} ${d.slice(8)}`;return `${d.slice(0,4)}/${d.slice(4,6)}/${d.slice(6,8)} ${d.slice(8,10)}:${d.slice(10)}`;}
function enableJalaliDateTimeMask(input){input?.addEventListener("input",()=>{input.value=formatJalaliDateTimeInput(input.value);});}
function toEnglishJalaliInput(date=new Date(),withTime=false){const parts=new Intl.DateTimeFormat("en-US-u-ca-persian",{year:"numeric",month:"2-digit",day:"2-digit"}).formatToParts(date);const val=t=>parts.find(x=>x.type===t)?.value;let r=`${val("year")}/${val("month")}/${val("day")}`;if(withTime)r+=` ${String(date.getHours()).padStart(2,"0")}:${String(date.getMinutes()).padStart(2,"0")}`;return r;}
function parsePersianDateForBackend(value,includeTime){const s=normalizeDigits(value).trim();if(!s)return null;const m=s.match(/^(\d{4})\/(\d{1,2})\/(\d{1,2})(?:\s+(\d{1,2}):(\d{1,2}))?$/);if(!m)throw new Error("فرمت تاریخ شمسی معتبر نیست.");const jy=+m[1],jm=+m[2],jd=+m[3],hh=+(m[4]||0),mm=+(m[5]||0);if(jm<1||jm>12||jd<1||jd>31||hh>23||mm>59)throw new Error("تاریخ یا ساعت معتبر نیست.");const target=`${jy}-${String(jm).padStart(2,"0")}-${String(jd).padStart(2,"0")}`;let start=new Date(jy+621,2,1);for(let i=0;i<370;i++){const d=new Date(start);d.setDate(start.getDate()+i);const p=new Intl.DateTimeFormat("en-US-u-ca-persian",{year:"numeric",month:"2-digit",day:"2-digit"}).formatToParts(d);const get=t=>p.find(x=>x.type===t)?.value;if(`${get("year")}-${get("month")}-${get("day")}`===target){const y=d.getFullYear(),mo=String(d.getMonth()+1).padStart(2,"0"),da=String(d.getDate()).padStart(2,"0");return includeTime?`${y}-${mo}-${da}T${String(hh).padStart(2,"0")}:${String(mm).padStart(2,"0")}:00`:`${y}-${mo}-${da}`;}}throw new Error("تاریخ شمسی خارج از محدوده معتبر است.");}
function formatPersianDateForInput(v){return v?toEnglishJalaliInput(new Date(v),false):"";}
function formatPersianDateTimeForInput(v){return v?toEnglishJalaliInput(new Date(v),true):"";}
// Shared by dynamically-created forms such as Staff management.
window.toEnglishJalaliInput=toEnglishJalaliInput;
window.parsePersianDateForBackend=parsePersianDateForBackend;
window.formatPersianDateForInput=formatPersianDateForInput;
window.formatPersianDateTimeForInput=formatPersianDateTimeForInput;
function validatePatientFields(f,l,n,m){if(!f)throw new Error("نام بیمار را وارد کنید.");if(!l)throw new Error("نام خانوادگی بیمار را وارد کنید.");if(!/^\d{10}$/.test(n))throw new Error("کد ملی باید دقیقاً ۱۰ رقم باشد.");if(m&&!/^\+?\d+$/.test(m))throw new Error("شماره موبایل معتبر نیست.");}

function createPatientIdentityCell(patient){
 const td=document.createElement("td");td.className="patient-identity-cell";
 const avatar=document.createElement("span");avatar.className="patient-row-avatar";
 const img=document.createElement("img");img.alt=`تصویر ${patient.firstName||""} ${patient.lastName||""}`;img.loading="lazy";img.src=`/api/patients/${patient.patientID}/photo`;
 img.onerror=()=>{img.remove();avatar.classList.add("patient-row-avatar-empty");avatar.textContent="👤";};
 avatar.appendChild(img);
 const text=document.createElement("span");text.className="patient-row-name";
 const name=document.createElement("strong");name.textContent=`${patient.firstName||""} ${patient.lastName||""}`.trim()||"-";
 const code=document.createElement("small");code.textContent=formatPatientCode(patient.patientID);
 text.append(name,code);td.append(avatar,text);return td;
}
function createPatientStatusCell(patient){
 const td=document.createElement("td"),badge=document.createElement("span");
 badge.className=`status-badge ${patient.isActive?"active":"inactive"}`;badge.textContent=patient.isActive?"فعال":"غیرفعال";td.appendChild(badge);return td;
}
async function loadPatients(search=""){
 try{
  setFormStatus(E.statusMessage,"در حال دریافت اطلاعات...",false);
  const q=new URLSearchParams();if(search.trim())q.set("search",search.trim());q.set("includeInactive",E.includeInactivePatients.checked);
  const r=await fetch(`/api/patients?${q}`),x=await r.json();if(!r.ok)throw new Error(getApiError(x,"خطا در دریافت بیماران."));
  const s=x.statistics||{};E.statTotalPatients.textContent=s.totalPatients??0;E.statActivePatients.textContent=s.activePatients??0;E.statInactivePatients.textContent=s.inactivePatients??0;E.statPatientsWithStudies.textContent=s.patientsWithStudies??0;
  E.patientsTableBody.replaceChildren();
  (x.patients||x||[]).forEach(p=>{
   const tr=document.createElement("tr");tr.tabIndex=0;tr.className="patient-list-row";tr.title="نمایش پرونده و مطالعات بیمار";
   tr.append(
    createPatientIdentityCell(p),
    createCell(formatPatientCode(p.patientID)),
    createCell(formatPatientGender(p.gender)),
    createCell(formatPersianDate(p.birthDate)),
    createCell(p.nationalCode||"-"),
    createCell(p.mobile||"-"),
    createCell(p.studyCount??0),
    createCell(formatPersianDate(p.lastStudyDate)),
    createPatientStatusCell(p)
   );
   const td=document.createElement("td");td.className="patient-row-actions";
   const edit=document.createElement("button");edit.type="button";edit.className="patient-edit-button secondary-button";edit.textContent="ویرایش";
   const remove=document.createElement("button");remove.type="button";remove.className="patient-delete-button danger-button";remove.textContent="حذف";
   const hasStudies=Number(p.studyCount||0)>0;remove.disabled=hasStudies;remove.title=hasStudies?"بیمار دارای مطالعه قابل حذف نیست.":"حذف دائمی بیمار";
   edit.addEventListener("click",async e=>{e.stopPropagation();try{await openPatientInline(p.patientID,tr);openEditPatientForm();}catch(err){showToast(err.message||"پرونده بیمار دریافت نشد.","error");}});
   remove.addEventListener("click",async e=>{e.stopPropagation();if(!remove.disabled)await deletePatient(p);});
   td.append(edit,remove);tr.appendChild(td);
   const select=async()=>{try{await openPatientInline(p.patientID,tr);}catch(e){showToast(e.message||"پرونده بیمار دریافت نشد.","error");}};
   tr.onclick=e=>{if(e.target.closest("button"))e.stopPropagation();select();};tr.onkeydown=e=>{if(e.key==="Enter"||e.key===" "){e.preventDefault();select();}};
   E.patientsTableBody.appendChild(tr);
  });
  setFormStatus(E.statusMessage,"",false);
 }catch(e){setFormStatus(E.statusMessage,e.message,true);}
}
async function deletePatient(patient){
 const fullName=`${patient.firstName||""} ${patient.lastName||""}`.trim();
 if(!await askConfirmation({title:"حذف دائمی بیمار",message:`آیا بیمار «${fullName}» برای همیشه حذف شود؟ این عملیات قابل بازگشت نیست.`,confirmText:"حذف بیمار"}))return;
 try{const r=await fetch(`/api/patients/${patient.patientID}`,{method:"DELETE"});let x={};try{x=await r.json();}catch{}if(!r.ok||!x.success)throw new Error(getApiError(x,"حذف بیمار انجام نشد."));
  if(selectedPatientID===patient.patientID){selectedPatientID=null;selectedPatient=null;selectedStudyID=null;selectedStudy=null;E.patientDetailsSection.classList.add("hidden");}
  await loadPatients(E.patientSearch.value);showToast("بیمار با موفقیت حذف شد.","success");
 }catch(e){showToast(e.message||"حذف بیمار انجام نشد.","error");}
}
async function openPatientInline(id,row){
 selectedPatientID=id;
 document.querySelectorAll(".patient-list-row.selected").forEach(x=>x.classList.remove("selected"));
 row?.classList.add("selected");
 E.patientDetailsSection.classList.remove("hidden");
 E.patientFullName.textContent="در حال دریافت پرونده...";
 E.studiesContainer.textContent="در حال دریافت اطلاعات...";
 try{
  const r=await fetch(`/api/patients/${id}/details`,{cache:"no-store"});let x={};try{x=await r.json();}catch{}
  if(!r.ok||!x.success)throw new Error(getApiError(x,`پرونده بیمار دریافت نشد. (HTTP ${r.status})`));
  selectedPatient=x.patient;renderPatientDetails(x);
  E.patientDetailsSection.scrollIntoView({behavior:"smooth",block:"start"});
 }catch(err){
  console.error("Patient inline record error:",err);E.patientFullName.textContent="خطا در دریافت پرونده";
  E.studiesContainer.textContent=err.message||"پرونده بیمار دریافت نشد.";throw err;
 }
}
async function openPatient(id){selectedPatientID=id;hideMainSections();E.patientDetailsSection.classList.remove("hidden");E.patientFullName.textContent="در حال دریافت پرونده...";E.studiesContainer.textContent="در حال دریافت اطلاعات...";window.scrollTo(0,0);try{const r=await fetch(`/api/patients/${id}/details`,{cache:"no-store"});let x={};try{x=await r.json();}catch{}if(!r.ok||!x.success)throw new Error(getApiError(x,`پرونده بیمار دریافت نشد. (HTTP ${r.status})`));selectedPatient=x.patient;renderPatientDetails(x);}catch(err){console.error("Patient record error:",err);E.patientFullName.textContent="خطا در دریافت پرونده";E.studiesContainer.textContent=err.message||"پرونده بیمار دریافت نشد.";showToast(err.message||"پرونده بیمار دریافت نشد.","error");}}
function renderPatientDetails(x){const p=x.patient,patientCode=formatPatientCode(p.patientID),studies=x.studies||[];E.patientFullName.textContent=`${p.firstName} ${p.lastName}`;E.patientDisplayCode.textContent=`شناسه پرونده: ${patientCode}`;E.patientNationalCode.textContent=`کد ملی: ${p.nationalCode}`;E.detailPatientCode.textContent=patientCode;E.detailFirstName.textContent=p.firstName||"-";E.detailLastName.textContent=p.lastName||"-";E.detailNationalCode.textContent=p.nationalCode||"-";E.detailMobile.textContent=p.mobile||"-";E.detailBirthDate.textContent=formatPersianDate(p.birthDate);E.detailGender.textContent=formatPatientGender(p.gender);E.detailIsActive.textContent=p.isActive?"فعال":"غیرفعال";E.detailAddress.textContent=p.address||"-";E.detailDescription.textContent=p.description||"-";E.studyCount.textContent=x.studyCount;E.totalImageCount.textContent=x.totalImageCount;E.lastStudyDateSummary.textContent=studies.length?formatPersianDate(studies[0].studyDate):"-";E.patientStatusBadge.textContent=p.isActive?"فعال":"غیرفعال";E.patientStatusBadge.className=`status-badge ${p.isActive?"active":"inactive"}`;E.deactivatePatientButton.textContent=p.isActive?"غیرفعال کردن":"فعال کردن";if(E.patientProfilePhoto){E.patientProfilePhoto.src=`/api/patients/${p.patientID}/photo?v=${Date.now()}`;E.patientProfilePhoto.onerror=()=>{E.patientProfilePhoto.removeAttribute("src");E.patientProfilePhoto.classList.add("empty");};E.patientProfilePhoto.classList.remove("empty");}renderRecentStudiesSummary(studies);renderStudiesSafe(studies);}

async function renderRecentStudiesSummary(studies){
 E.recentStudiesSummary.replaceChildren();const recent=(studies||[]).slice(0,3);
 if(!recent.length){E.recentStudiesSummary.textContent="هنوز مطالعه‌ای ثبت نشده است.";return;}
 for(const study of recent){const row=document.createElement("button");row.type="button";row.className="recent-study-row";const thumb=document.createElement("span");thumb.className="recent-study-thumb";thumb.textContent="🦷";const text=document.createElement("span");text.className="recent-study-text";const name=document.createElement("strong");name.textContent=study.studyTypeName||study.studyType||`مطالعه ${study.studyID}`;const date=document.createElement("small");date.textContent=formatPersianDate(study.studyDate);text.append(name,date);row.append(thumb,text);row.onclick=()=>document.querySelector(`.study-scroll-card[data-study-id="${study.studyID}"]`)?.scrollIntoView({behavior:"smooth",block:"start"});E.recentStudiesSummary.appendChild(row);
  try{const r=await fetch(`/api/radiologyimages/study/${study.studyID}`),x=await r.json(),image=(x.images||[]).find(i=>i.contentType!=="application/pdf");if(r.ok&&x.success&&image){const img=document.createElement("img");img.src=`/api/radiologyimages/${image.imageID}`;img.alt="";img.loading="lazy";thumb.replaceChildren(img);}}catch{}
 }
}
async function loadStudyDetailsImages(study){
 E.studyDetailsImagesGrid.replaceChildren();setFormStatus(E.studyDetailsImagesStatus,"در حال دریافت فایل‌ها...",false);
 try{const r=await fetch(`/api/radiologyimages/study/${study.studyID}`),x=await r.json();if(!r.ok||!x.success)throw new Error(getApiError(x,"فایل‌های Study دریافت نشد."));
  renderImagesInGrid(x.images||[],E.studyDetailsImagesGrid);setFormStatus(E.studyDetailsImagesStatus,x.count?"":"هنوز فایلی به این Study متصل نشده است.",false);
 }catch(e){setFormStatus(E.studyDetailsImagesStatus,e.message||"فایل‌های Study دریافت نشد.",true);}
}
function openStudyImages(study){selectedStudyID=study.studyID;selectedStudy=study;hideMainSections();E.studyImagesSection.classList.remove("hidden");E.studyImagesTitle.textContent="تصاویر — "+(study.studyTypeName||("Study "+study.studyID));loadStudyDetailsImages(study);window.scrollTo(0,0);}
async function ensureStudyDetailsTypes(selectedID){
 try{const r=await fetch("/api/studytypes",{cache:"no-store"}),x=await r.json();if(!r.ok||!x.success)throw new Error();E.studyDetailsType.replaceChildren();(x.studyTypes||[]).forEach(t=>{const o=document.createElement("option");o.value=String(t.studyTypeID);o.textContent=t.studyTypeName;E.studyDetailsType.appendChild(o);});if(selectedID&&!Array.from(E.studyDetailsType.options).some(o=>Number(o.value)===Number(selectedID))){const current=document.createElement("option");current.value=String(selectedID);current.textContent=`${selectedStudy?.studyTypeName||"نوع فعلی"} (غیرفعال)`;E.studyDetailsType.prepend(current);}E.studyDetailsType.value=String(selectedID||"");}catch{E.studyDetailsType.replaceChildren();const o=document.createElement("option");o.value=String(selectedID||"");o.textContent=selectedStudy?.studyTypeName||"تعیین نشده";E.studyDetailsType.appendChild(o);}
}
async function openStudyDetails(study){
 selectedStudyID=study.studyID;selectedStudy=study;hideMainSections();E.studyDetailsSection.classList.remove("hidden");
 E.studyDetailsTitle.textContent=study.studyTypeName||("Study "+study.studyID);E.studyDetailsDate.textContent=formatPersianDateTime(study.studyDate);
 await ensureStudyDetailsTypes(study.studyTypeID);
 E.studyDetailsBodyPart.value=study.bodyPart||"";E.studyDetailsStudyDate.value=formatPersianDateTimeForInput(study.studyDate);
 E.studyDetailsDescription.value=study.description||"";E.studyDetailsReport.value=study.report||"";
 setStudyDetailsEditing(false);setFormStatus(E.studyDetailsStatus,"",false);
 try{const r=await fetch(`/api/radiologystudies/${study.studyID}`,{cache:"no-store"}),x=await r.json();if(!r.ok||!x.success)throw new Error(getApiError(x,"اطلاعات Study دریافت نشد."));const teeth=x.toothNumbers||x.study?.toothNumbers||[];if(window.DentalRayDentalChart)window.DentalRayDentalChart.render(E.studyDetailsDentalChart,teeth);}catch{if(window.DentalRayDentalChart)window.DentalRayDentalChart.render(E.studyDetailsDentalChart,[]);}finally{E.studyDetailsDentalChart?.classList.add("study-chart-readonly");}
 window.scrollTo(0,0);
}
function setStudyDetailsEditing(editing){
 [E.studyDetailsBodyPart,E.studyDetailsStudyDate,E.studyDetailsDescription,E.studyDetailsReport].forEach(x=>x.readOnly=!editing);E.studyDetailsType.disabled=!editing;
 E.studyDetailsDentalChart?.classList.toggle("study-chart-readonly",!editing);
 E.studyDetailsEditButton.classList.toggle("hidden",editing);E.studyDetailsImagesButton.classList.toggle("hidden",editing);
 E.studyDetailsSaveButton.classList.toggle("hidden",!editing);E.studyDetailsCancelButton.classList.toggle("hidden",!editing);
}
async function saveStudyDetails(){
 if(studyDetailsSaveInProgress)return;
 const studyID=Number(selectedStudyID);
 if(!Number.isInteger(studyID)||studyID<=0){setFormStatus(E.studyDetailsStatus,"مطالعه معتبر انتخاب نشده است.",true);return;}
 try{
  studyDetailsSaveInProgress=true;E.studyDetailsSaveButton.disabled=true;E.studyDetailsSaveButton.textContent="در حال ذخیره...";setFormStatus(E.studyDetailsStatus,"در حال ذخیره تغییرات...",false);
  const studyTypeID=Number(E.studyDetailsType.value);if(!Number.isInteger(studyTypeID)||studyTypeID<=0)throw new Error("نوع مطالعه را انتخاب کنید.");
  const studyDate=parsePersianDateForBackend(E.studyDetailsStudyDate.value,true);if(!studyDate)throw new Error("تاریخ مطالعه را وارد کنید.");
  const body={studyDate,studyTypeID,bodyPart:emptyToNull(E.studyDetailsBodyPart.value),description:emptyToNull(E.studyDetailsDescription.value),report:emptyToNull(E.studyDetailsReport.value),toothNumbers:window.DentalRayDentalChart?.getSelected(E.studyDetailsDentalChart)||[]};
  const r=await fetch(`/api/radiologystudies/${studyID}`,{method:"PUT",headers:{"Content-Type":"application/json"},body:JSON.stringify(body)});let x={};try{x=await r.json();}catch{}if(!r.ok||!x.success)throw new Error(getApiError(x,`ویرایش Study انجام نشد. (HTTP ${r.status})`));
  const updated={...selectedStudy,...(x.study||{}),studyID,studyTypeID,studyTypeName:E.studyDetailsType.options[E.studyDetailsType.selectedIndex]?.text||selectedStudy?.studyTypeName,studyDate,bodyPart:body.bodyPart,description:body.description,report:body.report};selectedStudy=updated;
  setStudyDetailsEditing(false);E.studyDetailsTitle.textContent=updated.studyTypeName||`Study ${studyID}`;E.studyDetailsDate.textContent=formatPersianDateTime(studyDate);setFormStatus(E.studyDetailsStatus,"تغییرات مطالعه با موفقیت ذخیره شد.",false);showToast("مطالعه با موفقیت ویرایش شد.");
  try{const pr=await fetch(`/api/patients/${selectedPatientID}/details`,{cache:"no-store"}),pd=await pr.json();if(pr.ok&&pd.success){const fresh=(pd.studies||[]).find(s=>s.studyID===studyID);if(fresh)selectedStudy=fresh;}}catch(refreshError){console.warn("Study saved, but patient workspace refresh failed:",refreshError);}
 }catch(e){setFormStatus(E.studyDetailsStatus,e.message||"ویرایش Study انجام نشد.",true);
 }finally{studyDetailsSaveInProgress=false;E.studyDetailsSaveButton.disabled=false;E.studyDetailsSaveButton.textContent="ذخیره تغییرات";}
}
window.DentalRaySaveStudyDetails=event=>{event?.preventDefault?.();return saveStudyDetails();};

function renderStudiesSafe(studies){
 E.studiesContainer.replaceChildren();
 selectedStudyID=null;selectedStudy=null;
 if(!studies?.length){E.studiesContainer.textContent="برای این بیمار هنوز مطالعه‌ای ثبت نشده است.";return;}
 const ordered=[...studies].sort((a,b)=>new Date(b.studyDate||0)-new Date(a.studyDate||0));
 ordered.forEach(study=>{
  const card=document.createElement("article");card.className="study-scroll-card";card.dataset.studyId=String(study.studyID);
  const header=document.createElement("header");header.className="study-scroll-header";
  const heading=document.createElement("div");const title=document.createElement("h4");title.textContent=study.studyTypeName||study.studyType||`مطالعه ${study.studyID}`;const date=document.createElement("time");date.textContent=formatPersianDateTime(study.studyDate);heading.append(title,date);
  const actions=document.createElement("div");actions.className="study-scroll-actions";
  const edit=document.createElement("button");edit.type="button";edit.className="secondary-button";edit.textContent="مشاهده / ویرایش";edit.onclick=()=>openStudyDetails(study);
  const addImage=document.createElement("button");addImage.type="button";addImage.textContent="+ افزودن تصویر";addImage.onclick=()=>openUploadImageForm(study);actions.append(edit,addImage);header.append(heading,actions);
  const body=document.createElement("div");body.className="study-scroll-body";
  const details=document.createElement("div");details.className="study-scroll-details";details.append(createInfoLine("ناحیه",study.bodyPart||"-"),createInfoLine("توضیحات",study.description||"-"),createInfoLine("گزارش",study.report||"-"));details.querySelectorAll(":scope > div").forEach(x=>x.classList.add("info-line"));
  const chartSection=document.createElement("section");chartSection.className="study-scroll-chart";const chartTitle=document.createElement("strong");chartTitle.textContent="نمودار دندان‌های این مطالعه";const chart=document.createElement("div");chart.className="study-card-dental-chart study-chart-readonly";chartSection.append(chartTitle,chart);body.append(details,chartSection);
  const imagesSection=document.createElement("section");imagesSection.className="study-scroll-images";const imagesTitle=document.createElement("div");imagesTitle.className="study-scroll-images-title";imagesTitle.textContent="تصاویر مطالعه";const status=document.createElement("div");status.className="status-message";status.textContent="در حال دریافت تصاویر...";const grid=document.createElement("div");grid.className="images-grid";imagesSection.append(imagesTitle,status,grid);
  card.append(header,body,imagesSection);E.studiesContainer.appendChild(card);hydrateStudyCard(study,chart,status,grid);
 });
 }

async function hydrateStudyCard(study,chart,status,grid){
 const [imagesResult,studyResult]=await Promise.allSettled([fetch(`/api/radiologyimages/study/${study.studyID}`).then(async r=>({r,x:await r.json()})),fetch(`/api/radiologystudies/${study.studyID}`).then(async r=>({r,x:await r.json()}))]);
 if(studyResult.status==="fulfilled"&&studyResult.value.r.ok){const x=studyResult.value.x,teeth=x.toothNumbers||x.study?.toothNumbers||[];if(window.DentalRayDentalChart)window.DentalRayDentalChart.render(chart,teeth);}
 else if(window.DentalRayDentalChart)window.DentalRayDentalChart.render(chart,[]);
 if(imagesResult.status==="fulfilled"){const {r,x}=imagesResult.value;if(r.ok&&x.success){renderImagesInGrid(x.images||[],grid);status.textContent=x.count?`${x.count} تصویر / فایل`:`برای این مطالعه هنوز تصویری ثبت نشده است.`;return;}status.textContent=getApiError(x,"تصاویر مطالعه دریافت نشد.");}
 else status.textContent="تصاویر مطالعه دریافت نشد.";status.classList.toggle("error",!imagesResult.value?.r?.ok);
}

function renderStudies(studies){E.studiesContainer.innerHTML="";if(!studies?.length){E.studiesContainer.textContent="برای این بیمار هنوز رادیولوژی ثبت نشده است.";return;}studies.forEach(study=>{const card=document.createElement("div");card.className="study-card";card.dataset.studyId=study.studyID;const h=document.createElement("div");h.className="study-card-header";const title=document.createElement("div");title.className="study-title";title.textContent=study.studyTypeName||study.studyType||`رادیولوژی ${study.studyID}`;const buttons=document.createElement("div");buttons.className="study-action-buttons";const images=document.createElement("button");images.className="study-images-button";images.dataset.imageCount=study.imageCount||0;images.textContent=`تصاویر (${study.imageCount||0})`;const upload=document.createElement("button");upload.textContent="افزودن فایل";upload.onclick=()=>openUploadImageForm(study);const edit=document.createElement("button");edit.textContent="ویرایش";edit.className="secondary-button";edit.onclick=()=>openEditStudyForm(study);buttons.append(images,upload,edit);h.append(title,buttons);card.appendChild(h);const meta=document.createElement("div");meta.className="study-meta";meta.append(createInfoLine("تاریخ",formatPersianDateTime(study.studyDate)),createInfoLine("ناحیه",study.bodyPart||"-"),createInfoLine("توضیحات",study.description||"-"),createInfoLine("گزارش",study.report||"-"));card.appendChild(meta);const sec=document.createElement("div");sec.className="study-inline-images hidden";const status=document.createElement("div");status.className="status-message";const grid=document.createElement("div");grid.className="images-grid";sec.append(status,grid);card.appendChild(sec);images.onclick=()=>toggleStudyImages(study,sec,status,grid,images);E.studiesContainer.appendChild(card);});}
async function toggleStudyImages(study,sec,status,grid,button){if(!sec.classList.contains("hidden")){sec.classList.add("hidden");return;}sec.classList.remove("hidden");status.textContent="در حال دریافت تصاویر...";const r=await fetch(`/api/radiologyimages/study/${study.studyID}`),x=await r.json();if(!r.ok||!x.success){status.textContent=getApiError(x,"خطا در دریافت تصاویر.");return;}button.dataset.imageCount=x.count;button.textContent=`تصاویر (${x.count})`;renderImagesInGrid(x.images,grid);status.textContent=x.count?`${x.count} فایل نمایش داده شد.`:"فایلی متصل نیست.";}
function renderImagesInGrid(images,grid){grid.innerHTML="";(images||[]).forEach(image=>{const card=document.createElement("div");card.className="image-card";let media;if(image.contentType==="application/pdf"){media=document.createElement("div");media.className="pdf-thumbnail";media.textContent="PDF";}else{media=document.createElement("img");media.src=`/api/radiologyimages/${image.imageID}`;media.alt=image.fileName;media.loading="lazy";}media.onclick=()=>openLargeImage(image);const title=document.createElement("div");title.className="image-card-title";title.textContent=image.fileName;const type=document.createElement("div");type.className="field-hint";type.textContent=image.imageTypeName?`نوع تصویر: ${image.imageTypeName}`:"نوع تصویر: تعیین نشده";card.append(media,title,type);if(!image.imageTypeID&&window.dentalRayCurrentUser?.isSuperAdmin===true){const classify=document.createElement("button");classify.type="button";classify.className="secondary-button";classify.textContent="تعیین نوع تصویر";classify.onclick=async ev=>{ev.stopPropagation();try{const r=await fetch("/api/imagetypes"),x=await r.json();if(!r.ok||!x.success)throw new Error(getApiError(x,"انواع تصویر دریافت نشد."));const choices=(x.imageTypes||[]).map(t=>`${t.imageTypeID}: ${t.imageTypeName}`).join("\n");const answer=window.prompt("ImageTypeID را انتخاب کنید:\n"+choices);if(answer===null)return;const imageTypeID=Number(answer);if(!Number.isInteger(imageTypeID)||(x.imageTypes||[]).every(t=>t.imageTypeID!==imageTypeID))throw new Error("نوع تصویر معتبر انتخاب نشده است.");const u=await fetch(`/api/radiologyimages/${image.imageID}/type`,{method:"PATCH",headers:{"Content-Type":"application/json"},body:JSON.stringify({imageTypeID})}),y=await u.json();if(!u.ok||!y.success)throw new Error(getApiError(y,"نوع تصویر ذخیره نشد."));image.imageTypeID=y.imageTypeID;image.imageTypeName=y.imageTypeName;type.textContent=`نوع تصویر: ${y.imageTypeName}`;classify.remove();showToast("نوع تصویر ذخیره شد.","success");}catch(e){showToast(e.message||"نوع تصویر ذخیره نشد.","error");}};card.appendChild(classify);}grid.appendChild(card);});}
let imageViewScale=1,imageViewRotation=0,imageViewFlipX=1,imageViewX=0,imageViewY=0,imageDragging=false,imageDragStartX=0,imageDragStartY=0;
function applyImageView(){E.largeImage.style.transform=`translate(${imageViewX}px,${imageViewY}px) scale(${imageViewScale}) rotate(${imageViewRotation}deg) scaleX(${imageViewFlipX})`;}
function resetImageView(){imageViewScale=1;imageViewRotation=0;imageViewFlipX=1;imageViewX=0;imageViewY=0;applyImageView();}
function openLargeImage(image){if(image.contentType==="application/pdf"){window.open(`/api/radiologyimages/${image.imageID}`,"_blank","noopener");return;}resetImageView();E.largeImage.src=`/api/radiologyimages/${image.imageID}`;E.largeImageCaption.textContent=image.imageTypeName?`${image.fileName} — ${image.imageTypeName}`:image.fileName;E.imageModal.classList.remove("hidden");}
function closeLargeImage(){E.imageModal.classList.add("hidden");E.largeImage.src="";resetImageView();}

function resetCameraCapture(){pendingCameraFile=null;if(cameraPreviewUrl){URL.revokeObjectURL(cameraPreviewUrl);cameraPreviewUrl=null;}E.cameraFileInput.value="";E.cameraPreviewImage.removeAttribute("src");E.cameraPreviewPanel.classList.add("hidden");}
async function loadImageTypes(){E.uploadImageType.innerHTML=`<option value="">انتخاب نوع تصویر...</option>`;const r=await fetch("/api/imagetypes"),x=await r.json();if(!r.ok||!x.success)throw new Error(getApiError(x,"انواع تصویر دریافت نشد."));(x.imageTypes||[]).forEach(t=>{const o=document.createElement("option");o.value=t.imageTypeID;o.textContent=t.imageTypeName;E.uploadImageType.appendChild(o);});}
async function openUploadImageForm(study){selectedStudyID=study.studyID;selectedStudy=study;E.imageFileInput.value="";E.uploadImageType.value="";resetCameraCapture();setFormStatus(E.uploadImageStatus,"",false);E.uploadImageStudyInfo.textContent=`رادیولوژی شماره ${study.studyID} — ${study.studyType||""}`;hideMainSections();E.uploadImageSection.classList.remove("hidden");try{await loadImageTypes();}catch(e){setFormStatus(E.uploadImageStatus,e.message,true);}window.scrollTo(0,0);}
E.cameraFileInput?.addEventListener("change",()=>{const f=E.cameraFileInput.files?.[0];if(!f)return;pendingCameraFile=f;if(cameraPreviewUrl)URL.revokeObjectURL(cameraPreviewUrl);cameraPreviewUrl=URL.createObjectURL(f);E.cameraPreviewImage.src=cameraPreviewUrl;E.cameraPreviewPanel.classList.remove("hidden");setFormStatus(E.uploadImageStatus,"پیش‌نمایش را بررسی و سپس «تأیید تصویر» را انتخاب کنید.",false);});
E.confirmCameraButton?.addEventListener("click",()=>{if(!pendingCameraFile)return;E.imageFileInput.value="";setFormStatus(E.uploadImageStatus,"تصویر دوربین تأیید شد و آماده ذخیره است.",false);});
E.retakeCameraButton?.addEventListener("click",()=>{resetCameraCapture();E.cameraFileInput.click();});
async function uploadImage(){try{if(!selectedStudyID)throw new Error("رادیولوژی انتخاب نشده است.");const imageTypeID=Number(E.uploadImageType.value);if(!imageTypeID)throw new Error("نوع تصویر را انتخاب کنید.");const file=pendingCameraFile||E.imageFileInput.files?.[0];if(!file)throw new Error("یک فایل یا تصویر دوربین انتخاب کنید.");const isImage=(file.type||"").toLowerCase().startsWith("image/"),isPdf=(file.type||"").toLowerCase()==="application/pdf"||(file.name||"").toLowerCase().endsWith(".pdf");if(!isImage&&!isPdf)throw new Error("فایل انتخاب‌شده باید تصویر یا PDF باشد.");const fd=new FormData();fd.append("file",file);setFormStatus(E.uploadImageStatus,"در حال ذخیره و اتصال فایل...",false);const r=await fetch(`/api/radiologyimages?studyID=${selectedStudyID}&imageTypeID=${imageTypeID}`,{method:"POST",body:fd}),x=await r.json();if(!r.ok||!x.success)throw new Error(getApiError(x,"ذخیره فایل انجام نشد."));resetCameraCapture();if(selectedStudy){selectedStudy.imageCount=(selectedStudy.imageCount||0)+1;openStudyImages(selectedStudy);}else await openPatient(selectedPatientID);showToast(`فایل با موفقیت ذخیره شد: ${x.fileName}`);}catch(e){setFormStatus(E.uploadImageStatus,e.message,true);}}

function patientPayload(prefix){const f=E[`${prefix}FirstName`].value.trim(),l=E[`${prefix}LastName`].value.trim(),n=normalizeDigits(E[`${prefix}NationalCode`].value.trim()),m=normalizePhone(E[`${prefix}Mobile`].value);validatePatientFields(f,l,n,m);return{nationalCode:n,firstName:f,lastName:l,birthDate:parsePersianDateForBackend(E[`${prefix}BirthDate`].value,false),gender:E[`${prefix}Gender`].value===""?null:+E[`${prefix}Gender`].value,mobile:m,address:emptyToNull(E[`${prefix}Address`].value),description:emptyToNull(E[`${prefix}Description`].value)};}
function openNewPatientForm(){E.newPatientForm.reset();setFormStatus(E.newPatientStatus,"",false);hideMainSections();E.newPatientSection.classList.remove("hidden");E.newFirstName.focus();}
async function createPatient(){try{const r=await fetch("/api/patients",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(patientPayload("new"))}),x=await r.json();if(!r.ok||!x.success)throw new Error(getApiError(x,"ثبت بیمار انجام نشد."));await loadPatients();showPatientsScreen();showToast("بیمار ثبت شد.");}catch(e){setFormStatus(E.newPatientStatus,e.message,true);}}
function openEditPatientForm(){if(!selectedPatient)return;const p=selectedPatient;E.editFirstName.value=p.firstName||"";E.editLastName.value=p.lastName||"";E.editNationalCode.value=p.nationalCode||"";E.editMobile.value=p.mobile||"";E.editBirthDate.value=formatPersianDateForInput(p.birthDate);E.editGender.value=p.gender??"";E.editAddress.value=p.address||"";E.editDescription.value=p.description||"";hideMainSections();E.editPatientSection.classList.remove("hidden");}
async function updatePatient(){try{const r=await fetch(`/api/patients/${selectedPatientID}`,{method:"PUT",headers:{"Content-Type":"application/json"},body:JSON.stringify(patientPayload("edit"))}),x=await r.json();if(!r.ok||!x.success)throw new Error(getApiError(x,"ویرایش بیمار انجام نشد."));await openPatient(selectedPatientID);showToast("اطلاعات بیمار ذخیره شد.");}catch(e){setFormStatus(E.editPatientStatus,e.message,true);}}
function printPatientInformation(){
 if(!selectedPatient){showToast("ابتدا یک بیمار را انتخاب کنید.","error");return;}
 // A separate, escaped document keeps the printed page clean and prevents patient text from becoming HTML.
 const p=selectedPatient,patientCode=formatPatientCode(p.patientID),printWindow=window.open("","_blank");
 if(!printWindow){showToast("مرورگر پنجره چاپ را مسدود کرده است.","error");return;}
 printWindow.opener=null;
 const rows=[
  ["شناسه پرونده",patientCode],["نام و نام خانوادگی",`${p.firstName||""} ${p.lastName||""}`.trim()||"-"],
  ["کد ملی",p.nationalCode||"-"],["موبایل",p.mobile||"-"],["تاریخ تولد",formatPersianDate(p.birthDate)],
  ["جنسیت",formatPatientGender(p.gender)],["وضعیت",p.isActive?"فعال":"غیرفعال"],["آدرس",p.address||"-"],
  ["توضیحات",p.description||"-"],["تعداد مطالعات",E.studyCount.textContent||"0"],["تعداد تصاویر",E.totalImageCount.textContent||"0"]
 ];
 printWindow.document.write(`<!doctype html><html lang="fa" dir="rtl"><head><meta charset="utf-8"><title>اطلاعات بیمار - ${escapeHtml(patientCode)}</title><style>body{font-family:Tahoma,Arial,sans-serif;margin:28px;color:#17365d}h1{font-size:22px;margin:0 0 6px}.subtitle{color:#60758c;margin-bottom:22px}.sheet{border:1px solid #cfe0ee;border-radius:12px;padding:18px}.row{display:grid;grid-template-columns:150px 1fr;gap:14px;padding:9px 4px;border-bottom:1px solid #e7eff6}.row:last-child{border-bottom:0}.label{font-weight:700;color:#285b8f}.footer{margin-top:20px;font-size:11px;color:#718397}@media print{body{margin:12mm}.sheet{break-inside:avoid}}</style></head><body><h1>DentalRay — اطلاعات بیمار</h1><div class="subtitle">${escapeHtml(patientCode)}</div><div class="sheet">${rows.map(([label,value])=>`<div class="row"><div class="label">${escapeHtml(label)}</div><div>${escapeHtml(value)}</div></div>`).join("")}</div><div class="footer">چاپ‌شده از سامانه DentalRay</div><script>window.addEventListener("load",()=>window.print());<\/script></body></html>`);
 printWindow.document.close();
}
async function togglePatientActiveStatus(){
 if(!selectedPatient)return;
 const isDeactivating=selectedPatient.isActive;
 if(isDeactivating&&!await askConfirmation({title:"غیرفعال کردن بیمار",message:`آیا پرونده ${selectedPatient.firstName} ${selectedPatient.lastName} غیرفعال شود؟ اطلاعات و مطالعات بیمار حذف نخواهند شد.`,confirmText:"غیرفعال کردن"}))return;
 // Both corresponding controller actions use HttpPut; matching that verb prevents an HTTP 405 response.
 try{const action=isDeactivating?"deactivate":"activate";const r=await fetch(`/api/patients/${selectedPatientID}/${action}`,{method:"PUT"}),x=await r.json();if(!r.ok||!x.success)throw new Error(getApiError(x,"عملیات انجام نشد."));await openPatient(selectedPatientID);showToast(isDeactivating?"بیمار غیرفعال شد.":"بیمار فعال شد.");}catch(e){showToast(e.message||"عملیات انجام نشد.","error");}
}
function openNewStudyForm(){
 if(!Number.isInteger(Number(selectedPatientID))||Number(selectedPatientID)<=0){
  showToast("ابتدا یک بیمار را انتخاب کنید.","error");
  return;
 }
 E.newStudyForm.reset();
 setFormStatus(E.newStudyStatus,"",false);
 E.newStudyDate.value=toEnglishJalaliInput(new Date(),true);
 hideMainSections();
 E.newStudySection.classList.remove("hidden");
 E.newStudySection.scrollIntoView({behavior:"smooth",block:"start"});
 E.newStudyType.focus();
}
// Public entry point keeps this primary action independent from later optional bindings.
window.DentalRayOpenNewStudy=event=>{event?.preventDefault?.();return openNewStudyForm();};
function studyPayload(prefix){const type=E[`${prefix}StudyType`].value.trim();if(!type)throw new Error("نوع رادیولوژی را وارد کنید.");return{studyDate:parsePersianDateForBackend(E[`${prefix}StudyDate`].value,true),studyType:type,bodyPart:emptyToNull(E[`${prefix}BodyPart`].value),description:emptyToNull(E[`${prefix}StudyDescription`].value),report:emptyToNull(E[`${prefix}StudyReport`].value)};}
async function createStudy(){try{const body={...studyPayload("new"),patientID:selectedPatientID};const r=await fetch("/api/radiologystudies",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(body)}),x=await r.json();if(!r.ok||x.success===false)throw new Error(getApiError(x,"ثبت رادیولوژی انجام نشد."));await openPatient(selectedPatientID);showToast("رادیولوژی ثبت شد.");}catch(e){setFormStatus(E.newStudyStatus,getApiError({message:e.message},"ثبت رادیولوژی انجام نشد."),true);}}
function openEditStudyForm(s){selectedStudyID=s.studyID;selectedStudy=s;setFormStatus(E.editStudyStatus,"",false);E.editStudyType.value=s.studyTypeID?String(s.studyTypeID):(s.studyType||"");E.editBodyPart.value=s.bodyPart||"";E.editStudyDate.value=formatPersianDateTimeForInput(s.studyDate);E.editStudyDescription.value=s.description||"";E.editStudyReport.value=s.report||"";E.editStudySubtitle.textContent=`رادیولوژی شماره ${s.studyID}`;hideMainSections();E.editStudySection.classList.remove("hidden");}
async function updateStudy(){try{const r=await fetch(`/api/radiologystudies/${selectedStudyID}`,{method:"PUT",headers:{"Content-Type":"application/json"},body:JSON.stringify(studyPayload("edit"))}),x=await r.json();if(!r.ok||!x.success)throw new Error(getApiError(x,"ویرایش رادیولوژی انجام نشد."));const patientResponse=await fetch(`/api/patients/${selectedPatientID}/details`),patientData=await patientResponse.json();if(!patientResponse.ok||!patientData.success)throw new Error(getApiError(patientData,"اطلاعات Study به‌روز نشد."));const refreshed=(patientData.studies||[]).find(s=>s.studyID===selectedStudyID);if(refreshed){selectedStudy=refreshed;openStudyDetails(refreshed);}else await openPatient(selectedPatientID);showToast("رادیولوژی ویرایش شد.");}catch(e){setFormStatus(E.editStudyStatus,getApiError({message:e.message},"ویرایش رادیولوژی انجام نشد."),true);}}
function openMergePatientForm(){E.mergePatientForm.reset();setFormStatus(E.mergePatientStatus,`Source: ${selectedPatient.firstName} ${selectedPatient.lastName} — ${selectedPatient.nationalCode}`,false);hideMainSections();E.mergePatientSection.classList.remove("hidden");}
async function mergePatient(){try{const code=normalizeDigits(E.mergeTargetNationalCode.value.trim());const tr=await fetch(`/api/patients/${encodeURIComponent(code)}`),target=await tr.json();if(!tr.ok)throw new Error(getApiError(target,"بیمار مقصد پیدا نشد."));if(!await askConfirmation({title:"تأیید ادغام بیمار",message:`Source: ${selectedPatient.nationalCode}\nTarget: ${target.nationalCode}\nتمام Studyها و تصاویر منتقل می‌شوند.`,confirmText:"انجام Merge"}))return;const r=await fetch("/api/patients/merge",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({sourcePatientID:selectedPatientID,targetPatientID:target.patientID})}),x=await r.json();if(!r.ok||!x.success)throw new Error(getApiError(x,"Merge انجام نشد."));await loadPatients();await openPatient(target.patientID);showToast("ادغام با موفقیت انجام شد.");}catch(e){setFormStatus(E.mergePatientStatus,e.message,true);}}

enableJalaliDateMask(E.newBirthDate);enableJalaliDateMask(E.editBirthDate);enableJalaliDateTimeMask(E.newStudyDate);enableJalaliDateTimeMask(E.editStudyDate);
E.searchButton.onclick=()=>loadPatients(E.patientSearch.value);E.clearSearchButton.onclick=()=>{E.patientSearch.value="";loadPatients();};E.patientSearch.onkeydown=e=>{if(e.key==="Enter")loadPatients(E.patientSearch.value);};E.includeInactivePatients.onchange=()=>loadPatients(E.patientSearch.value);E.newPatientButton.onclick=openNewPatientForm;E.newPatientForm.onsubmit=e=>{e.preventDefault();createPatient();};E.backToPatientsButton.addEventListener("click",e=>{e.preventDefault();e.stopPropagation();showPatientsScreen();});
E.backToPatientDetailsButton?.addEventListener("click",()=>openPatient(selectedPatientID));
E.backToStudyDetailsButton?.addEventListener("click",()=>{if(selectedStudy)openStudyDetails(selectedStudy);});
E.studyDetailsImagesButton?.addEventListener("click",()=>{if(selectedStudy)openStudyImages(selectedStudy);});
E.studyDetailsEditButton?.addEventListener("click",()=>setStudyDetailsEditing(true));
E.studyDetailsCancelButton?.addEventListener("click",()=>{if(selectedStudy)openStudyDetails(selectedStudy);});
E.studyDetailsForm?.addEventListener("submit",e=>{e.preventDefault();saveStudyDetails();});
E.studyDetailsUploadButton?.addEventListener("click",()=>{if(selectedStudy)openUploadImageForm(selectedStudy);});
E.editPatientButton?.addEventListener("click",openEditPatientForm);
E.printPatientButton?.addEventListener("click",printPatientInformation);
E.deactivatePatientButton?.addEventListener("click",togglePatientActiveStatus);
E.mergePatientButton?.addEventListener("click",openMergePatientForm);
E.editPatientForm?.addEventListener("submit",e=>{e.preventDefault();updatePatient();});
E.newStudyForm?.addEventListener("submit",e=>{e.preventDefault();createStudy();});
E.editStudyForm?.addEventListener("submit",e=>{e.preventDefault();updateStudy();});
E.uploadImageForm?.addEventListener("submit",e=>{e.preventDefault();uploadImage();});
E.mergePatientForm?.addEventListener("submit",e=>{e.preventDefault();mergePatient();});
[[E.cancelNewPatientButton,E.cancelNewPatientButtonBottom]].flat().forEach(b=>b.onclick=showPatientsScreen);[E.cancelEditPatientButton,E.cancelEditPatientButtonBottom,E.cancelNewStudyButton,E.cancelNewStudyButtonBottom,E.cancelMergePatientButton,E.cancelMergePatientButtonBottom].forEach(b=>b.onclick=()=>openPatient(selectedPatientID));[E.cancelEditStudyButton,E.cancelEditStudyButtonBottom].forEach(b=>b.onclick=()=>selectedStudy?openStudyDetails(selectedStudy):openPatient(selectedPatientID));[E.cancelUploadImageButton,E.cancelUploadImageButtonBottom].forEach(b=>b.onclick=()=>selectedStudy?openStudyImages(selectedStudy):openPatient(selectedPatientID));
E.patientPhotoButton?.addEventListener("click",()=>E.patientPhotoInput?.click());
E.patientPhotoInput?.addEventListener("change",async()=>{const file=E.patientPhotoInput.files?.[0];if(!file||!selectedPatientID)return;try{const fd=new FormData();fd.append("file",file);const r=await fetch(`/api/patients/${selectedPatientID}/photo`,{method:"POST",body:fd}),x=await r.json();if(!r.ok||!x.success)throw new Error(getApiError(x,"ذخیره تصویر بیمار انجام نشد."));E.patientProfilePhoto.src=`/api/patients/${selectedPatientID}/photo?v=${Date.now()}`;E.patientProfilePhoto.classList.remove("empty");showToast("تصویر بیمار ذخیره شد.");}catch(e){showToast(e.message||"ذخیره تصویر بیمار انجام نشد.","error");}finally{E.patientPhotoInput.value="";}});
E.zoomInImageButton.onclick=()=>{imageViewScale=Math.min(5,imageViewScale+0.25);applyImageView();};E.zoomOutImageButton.onclick=()=>{imageViewScale=Math.max(0.25,imageViewScale-0.25);applyImageView();};E.rotateLeftImageButton.onclick=()=>{imageViewRotation-=90;applyImageView();};E.rotateRightImageButton.onclick=()=>{imageViewRotation+=90;applyImageView();};E.flipHorizontalImageButton.onclick=()=>{imageViewFlipX*=-1;applyImageView();};E.resetImageViewButton.onclick=resetImageView;E.imageModal.addEventListener("pointerdown",e=>{if(e.target!==E.largeImage)return;e.preventDefault();imageDragging=true;imageDragStartX=e.clientX-imageViewX;imageDragStartY=e.clientY-imageViewY;try{E.imageModal.setPointerCapture(e.pointerId);}catch{}});E.imageModal.addEventListener("pointermove",e=>{if(!imageDragging)return;e.preventDefault();imageViewX=e.clientX-imageDragStartX;imageViewY=e.clientY-imageDragStartY;applyImageView();});const endImageDrag=e=>{if(!imageDragging)return;imageDragging=false;try{E.imageModal.releasePointerCapture(e.pointerId);}catch{}};E.imageModal.addEventListener("pointerup",endImageDrag);E.imageModal.addEventListener("pointercancel",endImageDrag);E.imageModal.addEventListener("lostpointercapture",()=>{imageDragging=false;});E.closeImageModalButton.onclick=closeLargeImage;E.imageModal.onclick=e=>{if(imageDragging){e.preventDefault();e.stopPropagation();return;}/* The viewer closes only with the explicit close button or Escape. This prevents a completed image drag from being interpreted as a backdrop click. */};document.addEventListener("keydown",e=>{if(e.key==="Escape")closeLargeImage();});
loadPatients();
