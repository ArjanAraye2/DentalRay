// DentalRay Frontend
// Patient/Study forms plus patient-owned radiology image workflow.

function byId(id) { return document.getElementById(id); }
let selectedPatientID=null, selectedPatient=null, selectedStudyID=null, selectedStudy=null;
// A fast second click must cancel/obsolete the first patient request. Otherwise a
// slower response for the previous row can overwrite the newly selected patient.
let patientOpenRequestVersion=0,patientOpenAbortController=null;
function beginPatientOpenRequest(){patientOpenAbortController?.abort();patientOpenAbortController=new AbortController();return{version:++patientOpenRequestVersion,signal:patientOpenAbortController.signal};}
function isCurrentPatientOpenRequest(version,id){return version===patientOpenRequestVersion&&Number(selectedPatientID)===Number(id);}
function showPatientLoadingState(){selectedPatient=null;publishSelectedPatient(null);selectedStudyID=null;selectedStudy=null;E.patientFullName.textContent="در حال دریافت پرونده...";E.patientDisplayCode.textContent="";E.patientNationalCode.textContent="";E.studiesContainer.textContent="در حال دریافت اطلاعات...";E.recentStudiesSummary.replaceChildren();E.studyCount.textContent="—";E.totalImageCount.textContent="—";E.lastStudyDateSummary.textContent="—";if(E.patientProfilePhoto){E.patientProfilePhoto.onerror=null;E.patientProfilePhoto.removeAttribute("src");E.patientProfilePhoto.classList.add("empty");}}
let pendingCameraFile=null, cameraPreviewUrl=null;
// Lookups used by the study panel. Fetched once and reused; the dentist list is
// needed to suggest a waiting stage from the dentist's specialty.
let dentistsCache=null, waitStagesCache=null;
let studyDetailsSaveInProgress=false;

const ids=["newStatus","newFollowUpDate","newFollowUpNote","newFollowUpBox","openStudiesOnly","dueFollowUpOnly","statPatientsWithOpenStudies","studyDetailsStatus2","studyDetailsWaitBox","studyDetailsWaitStage","studyDetailsFollowUpBox","studyDetailsFollowUpDate","studyDetailsFollowUpNote","studyDetailsDentist","studyDetailsStatusBadge","patientsSection","patientStatistics","statTotalPatients","statActivePatients","statInactivePatients","statPatientsWithStudies","patientSearch","searchButton","clearSearchButton","includeInactivePatients","newPatientButton","patientsTableBody","statusMessage","patientDetailsSection","studyDetailsSection","studyImagesSection","backToPatientDetailsButton","backToStudyDetailsButton","studyDetailsTitle","studyDetailsDate","studyDetailsForm","studyDetailsType","studyDetailsBodyPart","studyDetailsStudyDate","studyDetailsDescription","studyDetailsReport","studyDetailsDentalChart","studyDetailsUploadButton","studyDetailsEditButton","studyDetailsImagesButton","studyDetailsSaveButton","studyDetailsCancelButton","studyImagesTitle","studyDetailsImagesStatus","studyDetailsImagesGrid","backToPatientsButton","editPatientButton","newStudyButton","printPatientButton","mergePatientButton","deactivatePatientButton","patientFullName","patientDisplayCode","patientNationalCode","patientStatusBadge","patientProfilePhoto","patientPhotoInput","patientPhotoButton","detailPatientCode","detailFirstName","detailLastName","detailNationalCode","detailMobile","detailBirthDate","detailGender","detailIsActive","detailAddress","detailDescription","studyCount","totalImageCount","studiesContainer","newPatientSection","newPatientForm","cancelNewPatientButton","cancelNewPatientButtonBottom","newFirstName","newLastName","newNationalCode","newMobile","newBirthDate","newGender","newAddress","newDescription","newPatientStatus","editPatientSection","editPatientForm","cancelEditPatientButton","cancelEditPatientButtonBottom","editFirstName","editLastName","editNationalCode","editMobile","editBirthDate","editGender","editAddress","editDescription","editPatientStatus","newStudySection","newStudyForm","cancelNewStudyButton","cancelNewStudyButtonBottom","newStudyType","newBodyPart","newStudyDate","newStudyDescription","newStudyReport","newStudyStatus","uploadImageSection","uploadImageForm","cancelUploadImageButton","cancelUploadImageButtonBottom","uploadImageStudyInfo","uploadImageType","imageFileInput","cameraFileInput","cameraPreviewPanel","cameraPreviewImage","confirmCameraButton","retakeCameraButton","uploadImageStatus","mergePatientSection","mergePatientForm","cancelMergePatientButton","cancelMergePatientButtonBottom","mergeTargetNationalCode","mergePatientStatus","imageModal","closeImageModalButton","zoomOutImageButton","zoomInImageButton","rotateLeftImageButton","rotateRightImageButton","flipHorizontalImageButton","resetImageViewButton","largeImage","largeImageCaption","confirmModal","confirmTitle","confirmMessage","confirmYesButton","confirmNoButton","toastContainer"];
const E={}; ids.forEach(id=>E[id]=byId(id));
E.lastStudyDateSummary=byId("lastStudyDateSummary");
E.recentStudiesSummary=byId("recentStudiesSummary");

function hideMainSections(){[E.patientsSection,E.patientDetailsSection,E.studyDetailsSection,E.studyImagesSection,E.newPatientSection,E.editPatientSection,E.newStudySection,E.uploadImageSection,E.mergePatientSection].forEach(x=>x?.classList.add("hidden"));}
function showPatientsScreen(){hideMainSections();E.patientsSection.classList.remove("hidden");window.scrollTo(0,0);}
function showToast(message,type="success",title=""){const t=document.createElement("div");t.className=`toast ${type}`;t.innerHTML=`<div class="toast-title"></div><div class="toast-message"></div>`;t.children[0].textContent=title||(type==="success"?"انجام شد":type==="error"?"خطا":"توجه");t.children[1].textContent=message;E.toastContainer.appendChild(t);setTimeout(()=>t.remove(),4300);}
function askConfirmation({title="تأیید عملیات",message,confirmText="تأیید",danger=true}){return new Promise(resolve=>{E.confirmTitle.textContent=title;E.confirmMessage.textContent=message;E.confirmYesButton.textContent=confirmText;E.confirmYesButton.classList.toggle("danger-button",danger);E.confirmModal.classList.remove("hidden");const done=v=>{E.confirmModal.classList.add("hidden");E.confirmYesButton.onclick=E.confirmNoButton.onclick=null;resolve(v);};E.confirmYesButton.onclick=()=>done(true);E.confirmNoButton.onclick=()=>done(false);});}
function getApiError(r,f){const m=r?.message||r?.messageEn||r?.error;if(!m)return f;const translations={"A patient with this NationalCode already exists.":"بیماری با این کد ملی قبلاً ثبت شده است.","NationalCode must contain only digits.":"کد ملی فقط باید شامل عدد باشد.","PatientID must be greater than zero.":"شناسه بیمار معتبر نیست.","Patient not found.":"بیمار پیدا نشد.","StudyType is required.":"نوع رادیولوژی را وارد کنید.","StudyType cannot be longer than 50 characters.":"نوع رادیولوژی نمی‌تواند بیشتر از ۵۰ نویسه باشد.","BodyPart cannot be longer than 100 characters.":"ناحیه نمی‌تواند بیشتر از ۱۰۰ نویسه باشد.","Description cannot be longer than 1000 characters.":"توضیحات نمی‌تواند بیشتر از ۱۰۰۰ نویسه باشد.","Study creation failed.":"ثبت رادیولوژی انجام نشد.","StudyDate is required.":"تاریخ رادیولوژی الزامی است.","Study not found.":"رادیولوژی پیدا نشد."};return translations[m]||m;}
// A 401 from the API has an EMPTY body, so a plain r.json() throws the cryptic
// "JSON.parse: unexpected character at line 1 column 1" and the user sees a
// technical English error instead of a Persian one. Read the body safely: an
// unparsable or missing body becomes {}, and the HTTP status decides the message.
// A 401/403 without a body always means the login session is gone, and a session
// that ends while the page is open should tell the user to log in again rather
// than look like a broken patient form.
async function readApiJson(r){let x={};try{x=await r.json();}catch{x={};}return x;}
function apiErrorMessage(r,x,fallback){if(r.status===401||r.status===403){const m=x?.message||x?.messageEn||x?.error;if(m)return getApiError(x,fallback);return"نشست ورود شما پایان یافته است. لطفاً از سیستم خارج شده و دوباره وارد شوید.";}return getApiError(x,fallback);}
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
// Navigation's global search opens a patient record directly.
window.openPatientInline=openPatientInline;
// The dashboard reuses the application image viewer instead of opening a bare URL.
window.openLargeImage=openLargeImage;
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
// Two cells: how many Studies are still open and how many are finished. An open
// count above zero is tinted so the eye lands on the patients with work left.
function createStudyCountCell(open,completed){
 const openCell=document.createElement("td");
 const openValue=Number(open||0);
 openCell.textContent=String(openValue);
 if(openValue>0){
  openCell.classList.add("study-count-open");
  openCell.title=`${openValue} مطالعه باز`;
 }
 const doneCell=document.createElement("td");
 doneCell.textContent=String(Number(completed||0));
 doneCell.classList.add("study-count-done");
 return [openCell,doneCell];
}
function createPatientStatusCell(patient){
 const td=document.createElement("td"),badge=document.createElement("span");
 badge.className=`status-badge ${patient.isActive?"active":"inactive"}`;badge.textContent=patient.isActive?"فعال":"غیرفعال";td.appendChild(badge);return td;
}
async function loadPatients(search=""){
 try{
  setFormStatus(E.statusMessage,"در حال دریافت اطلاعات...",false);
  const q=new URLSearchParams();if(search.trim())q.set("search",search.trim());q.set("includeInactive",E.includeInactivePatients.checked);
  // Reminder filters: patients with work outstanding, and follow-ups that are due.
  if(E.openStudiesOnly?.checked)q.set("openOnly","true");
  if(E.dueFollowUpOnly?.checked)q.set("dueOnly","true");
  const r=await fetch(`/api/patients?${q}`),x=await readApiJson(r);if(!r.ok)throw new Error(apiErrorMessage(r,x,"خطا در دریافت بیماران."));
  const s=x.statistics||{};E.statTotalPatients.textContent=s.totalPatients??0;E.statActivePatients.textContent=s.activePatients??0;E.statInactivePatients.textContent=s.inactivePatients??0;E.statPatientsWithStudies.textContent=s.patientsWithStudies??0;E.statPatientsWithOpenStudies.textContent=s.patientsWithOpenStudies??0;
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
    // Open and completed counts, colour-coded so outstanding work stands out.
    // The helper returns two cells, so they are spread into append().
    ...createStudyCountCell(p.openStudyCount,p.completedStudyCount),
    createCell(formatPersianDate(p.lastStudyDate)),
    createPatientStatusCell(p)
   );
   const td=document.createElement("td");td.className="patient-row-actions";
   const edit=document.createElement("button");edit.type="button";edit.className="patient-edit-button secondary-button";edit.textContent="ویرایش";
   const remove=document.createElement("button");remove.type="button";remove.className="patient-delete-button danger-button";remove.textContent="حذف";
   const hasStudies=Number(p.studyCount||0)>0;remove.disabled=hasStudies;remove.title=hasStudies?"بیمار دارای مطالعه قابل حذف نیست.":"حذف دائمی بیمار";
   edit.addEventListener("click",async e=>{e.stopPropagation();try{if(await openPatientInline(p.patientID,tr))openEditPatientForm();}catch(err){showToast(err.message||"پرونده بیمار دریافت نشد.","error");}});
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
 const request=beginPatientOpenRequest();
 document.querySelectorAll(".patient-list-row.selected").forEach(x=>x.classList.remove("selected"));
 row?.classList.add("selected");
 E.patientDetailsSection.classList.remove("hidden");
 showPatientLoadingState();
 try{
  const r=await fetch(`/api/patients/${id}/details`,{cache:"no-store",signal:request.signal});let x={};try{x=await r.json();}catch{}
  if(!isCurrentPatientOpenRequest(request.version,id))return false;
  if(!r.ok||!x.success)throw new Error(getApiError(x,`پرونده بیمار دریافت نشد. (HTTP ${r.status})`));
  selectedPatient=x.patient;publishSelectedPatient(x.patient);renderPatientDetails(x);
  E.patientDetailsSection.scrollIntoView({behavior:"smooth",block:"start"});
  return true;
 }catch(err){
  if(err?.name==="AbortError"||!isCurrentPatientOpenRequest(request.version,id))return false;
  console.error("Patient inline record error:",err);E.patientFullName.textContent="خطا در دریافت پرونده";
  E.studiesContainer.textContent=err.message||"پرونده بیمار دریافت نشد.";throw err;
 }
}
async function openPatient(id){selectedPatientID=id;const request=beginPatientOpenRequest();hideMainSections();E.patientDetailsSection.classList.remove("hidden");showPatientLoadingState();window.scrollTo(0,0);try{const r=await fetch(`/api/patients/${id}/details`,{cache:"no-store",signal:request.signal});let x={};try{x=await r.json();}catch{}if(!isCurrentPatientOpenRequest(request.version,id))return false;if(!r.ok||!x.success)throw new Error(getApiError(x,`پرونده بیمار دریافت نشد. (HTTP ${r.status})`));selectedPatient=x.patient;publishSelectedPatient(x.patient);renderPatientDetails(x);return true;}catch(err){if(err?.name==="AbortError"||!isCurrentPatientOpenRequest(request.version,id))return false;console.error("Patient record error:",err);E.patientFullName.textContent="خطا در دریافت پرونده";E.studiesContainer.textContent=err.message||"پرونده بیمار دریافت نشد.";showToast(err.message||"پرونده بیمار دریافت نشد.","error");return false;}}
// Other modules (patient messaging, printing helpers) need the open patient.
// Publishing it here keeps one source of truth: whenever the record renders,
// window.selectedPatient matches what is on screen.
function publishSelectedPatient(p){window.selectedPatient=p||null;window.selectedPatientID=Number(p?.patientID)||null;}
function renderPatientDetails(x){const p=x.patient,patientCode=formatPatientCode(p.patientID),studies=x.studies||[];E.patientFullName.textContent=`${p.firstName} ${p.lastName}`;E.patientDisplayCode.textContent=`شناسه پرونده: ${patientCode}`;E.patientNationalCode.textContent=`کد ملی: ${p.nationalCode}`;E.detailPatientCode.textContent=patientCode;E.detailFirstName.textContent=p.firstName||"-";E.detailLastName.textContent=p.lastName||"-";E.detailNationalCode.textContent=p.nationalCode||"-";E.detailMobile.textContent=p.mobile||"-";E.detailBirthDate.textContent=formatPersianDate(p.birthDate);E.detailGender.textContent=formatPatientGender(p.gender);E.detailIsActive.textContent=p.isActive?"فعال":"غیرفعال";E.detailAddress.textContent=p.address||"-";E.detailDescription.textContent=p.description||"-";E.studyCount.textContent=x.studyCount;E.totalImageCount.textContent=x.totalImageCount;E.lastStudyDateSummary.textContent=studies.length?formatPersianDate(studies[0].studyDate):"-";E.patientStatusBadge.textContent=p.isActive?"فعال":"غیرفعال";E.patientStatusBadge.className=`status-badge ${p.isActive?"active":"inactive"}`;E.deactivatePatientButton.textContent=p.isActive?"غیرفعال کردن":"فعال کردن";if(E.patientProfilePhoto){E.patientProfilePhoto.src=`/api/patients/${p.patientID}/photo?v=${Date.now()}`;E.patientProfilePhoto.onerror=()=>{E.patientProfilePhoto.removeAttribute("src");E.patientProfilePhoto.classList.add("empty");};E.patientProfilePhoto.classList.remove("empty");}renderRecentStudiesSummary(studies);renderStudiesSafe(studies);}

async function renderRecentStudiesSummary(studies){
 E.recentStudiesSummary.replaceChildren();const recent=(studies||[]).slice(0,3);
 if(!recent.length){E.recentStudiesSummary.textContent="هنوز مطالعه‌ای ثبت نشده است.";return;}
 for(const study of recent){const row=document.createElement("button");row.type="button";row.className="recent-study-row";const thumb=document.createElement("span");thumb.className="recent-study-thumb";thumb.textContent="🦷";const text=document.createElement("span");text.className="recent-study-text";const name=document.createElement("strong");name.textContent=study.studyTypeName||study.studyType||`مطالعه ${study.studyID}`;const date=document.createElement("small");date.textContent=formatPersianDate(study.studyDate);text.append(name,date);row.append(thumb,text);row.onclick=()=>document.querySelector(`.study-scroll-card[data-study-id="${study.studyID}"]`)?.scrollIntoView({behavior:"smooth",block:"start"});E.recentStudiesSummary.appendChild(row);
  try{const r=await fetch(`/api/radiologyimages/study/${study.studyID}`),image=undefined;let x=await readApiJson(r);(x.images||[]).find(i=>i.contentType!=="application/pdf");if(r.ok&&x.success&&image){const img=document.createElement("img");img.src=`/api/radiologyimages/${image.imageID}`;img.alt="";img.loading="lazy";thumb.replaceChildren(img);}}catch{}
 }
}
async function loadStudyDetailsImages(study){
 E.studyDetailsImagesGrid.replaceChildren();setFormStatus(E.studyDetailsImagesStatus,"در حال دریافت فایل‌ها...",false);
 try{const r=await fetch(`/api/radiologyimages/study/${study.studyID}`),x=await readApiJson(r);if(!r.ok||!x.success)throw new Error(apiErrorMessage(r,x,"فایل‌های Study دریافت نشد."));
  renderImagesInGrid(x.images||[],E.studyDetailsImagesGrid);setFormStatus(E.studyDetailsImagesStatus,x.count?"":"هنوز فایلی به این Study متصل نشده است.",false);
 }catch(e){setFormStatus(E.studyDetailsImagesStatus,e.message||"فایل‌های Study دریافت نشد.",true);}
}
function openStudyImages(study){selectedStudyID=study.studyID;selectedStudy=study;hideMainSections();E.studyImagesSection.classList.remove("hidden");E.studyImagesTitle.textContent="تصاویر — "+(study.studyTypeName||("Study "+study.studyID));loadStudyDetailsImages(study);window.scrollTo(0,0);}
// Dentists with their specialty, fetched once.
async function loadDentists(){
 if(dentistsCache)return dentistsCache;
 try{const r=await fetch("/api/staff/dentists",{cache:"no-store"}),x=await readApiJson(r);
  dentistsCache=r.ok&&x.success?(x.dentists||[]):[];
 }catch{dentistsCache=[];}
 return dentistsCache;
}
// Waiting stages, optionally narrowed to a specialty. The unfiltered list is
// cached; a specialty filter is cheap because the response is small.
async function loadWaitStages(specialtyID){
 if(!specialtyID&&waitStagesCache)return waitStagesCache;
 try{
  const url=specialtyID?`/api/waitstages?specialtyID=${specialtyID}`:"/api/waitstages";
  const r=await fetch(url,{cache:"no-store"}),x=await readApiJson(r);
  const rows=r.ok&&x.success?(x.waitStages||[]):[];
  if(!specialtyID)waitStagesCache=rows;
  return rows;
 }catch{return [];}
}
// Fills the dentist dropdown and keeps the current value selectable even if that
// dentist is no longer in the active list.
function fillDentistSelect(selectedID){
 const sel=E.studyDetailsDentist;if(!sel)return;
 sel.replaceChildren();
 const none=document.createElement("option");none.value="";none.textContent="انتخاب نشده";sel.appendChild(none);
 (dentistsCache||[]).forEach(d=>{
  const o=document.createElement("option");o.value=String(d.staffID);
  o.textContent=`${d.firstName||""} ${d.lastName||""}`.trim()+((d.specialtyName)?` — ${d.specialtyName}`:"");
  sel.appendChild(o);
 });
 const value=selectedID?String(selectedID):"";
 if(value&&!Array.from(sel.options).some(o=>o.value===value)){
  const cur=document.createElement("option");cur.value=value;cur.textContent=`${selectedStudy?.dentistName||"دندانپزشک فعلی"} (غیرفعال)`;
  sel.appendChild(cur);
 }
 sel.value=value;
}
// Fills the waiting-stage dropdown for the selected dentist's specialty.
async function fillWaitStageSelect(selectedStageID){
 const sel=E.studyDetailsWaitStage;if(!sel)return;
 const dentistID=Number(E.studyDetailsDentist?.value)||0;
 const specialtyID=(dentistsCache||[]).find(d=>Number(d.staffID)===dentistID)?.specialtyID||null;
 const rows=await loadWaitStages(specialtyID);
 sel.replaceChildren();
 const none=document.createElement("option");none.value="";none.textContent="انتخاب نشده";sel.appendChild(none);
 rows.forEach(w=>{const o=document.createElement("option");o.value=String(w.waitStageID);o.textContent=w.name;sel.appendChild(o);});
 const value=selectedStageID?String(selectedStageID):"";
 if(value&&!Array.from(sel.options).some(o=>o.value===value)){
  const cur=document.createElement("option");cur.value=value;cur.textContent=(selectedStudy?.waitStageName||"مرحله فعلی")+" (غیرفعال)";
  sel.appendChild(cur);
 }
 sel.value=value;
}
// The status and waiting fields only apply together, so they are shown together.
function syncStudyDetailsStatusFields(){
 const status=Number(E.studyDetailsStatus2?.value)||2;
 const waiting=status===3;
 E.studyDetailsWaitBox?.classList.toggle("hidden",!waiting);
 E.studyDetailsFollowUpBox?.classList.toggle("hidden",!waiting);
}
async function ensureStudyDetailsTypes(selectedID){
 try{const r=await fetch("/api/studytypes",{cache:"no-store"}),x=await readApiJson(r);if(!r.ok||!x.success)throw new Error();E.studyDetailsType.replaceChildren();(x.studyTypes||[]).forEach(t=>{const o=document.createElement("option");o.value=String(t.studyTypeID);o.textContent=t.studyTypeName;E.studyDetailsType.appendChild(o);});if(selectedID&&!Array.from(E.studyDetailsType.options).some(o=>Number(o.value)===Number(selectedID))){const current=document.createElement("option");current.value=String(selectedID);current.textContent=`${selectedStudy?.studyTypeName||"نوع فعلی"} (غیرفعال)`;E.studyDetailsType.prepend(current);}E.studyDetailsType.value=String(selectedID||"");}catch{E.studyDetailsType.replaceChildren();const o=document.createElement("option");o.value=String(selectedID||"");o.textContent=selectedStudy?.studyTypeName||"تعیین نشده";E.studyDetailsType.appendChild(o);}
}
// Marks a study complete straight from its card, without opening the panel.
//
// It reuses the update endpoint with the fields the API requires, keeping the
// study's own values so nothing is overwritten. The confirm step exists because
// completing a study also clears its waiting stage and follow-up date.
async function completeStudyFromCard(study,button){
 const wasWaiting=Number(study.status)===3;
 const message=wasWaiting
  ?"این مطالعه «تمام‌شده» شود؟\nمرحله انتظار و تاریخ پیگیری آن پاک می‌شود."
  :"این مطالعه «تمام‌شده» شود؟";
 if(!await askConfirmation({title:"تمام شدن مطالعه",message,confirmText:"بله، تمام شد",danger:false}))return;
 const original=button.textContent;button.disabled=true;button.textContent="در حال ثبت...";
 try{
  const body={
   studyDate:study.studyDate,
   studyTypeID:study.studyTypeID,
   bodyPart:study.bodyPart||null,
   description:study.description||null,
   report:study.report||null,
   toothNumbers:[],
   status:2,
   waitStageID:null,
   followUpDate:null,
   followUpNote:null,
   dentistStaffID:study.dentistStaffID??null
  };
  const r=await fetch(`/api/radiologystudies/${study.studyID}`,{method:"PUT",headers:{"Content-Type":"application/json"},body:JSON.stringify(body)});
  let x={};try{x=await r.json();}catch{}
  if(!r.ok||!x.success)throw new Error(getApiError(x,"تغییر وضعیت مطالعه انجام نشد."));
  showToast("مطالعه تمام‌شده شد.");
  // Re-render from the server so the badge, counts and any waiting date update.
  await openPatient(selectedPatientID);
  // A finished study with an outstanding balance is the moment to remind the
  // patient, so the offer is made here with the real balance.
  try{
   const fr=await fetch(`/api/patients/${selectedPatientID}/finance`,{cache:"no-store"}),fx=await fr.json();
   if(fr.ok&&fx.success&&Number(fx.balanceAmount)>0){
    offerPatientMessage(selectedPatientID,"این بیمار مانده حساب دارد. یادآوری مانده فرستاده شود؟","balance-due");
   }
  }catch{/* the finance lookup is optional */}
 }catch(e){
  showToast(e.message||"تغییر وضعیت مطالعه انجام نشد.","error");
  button.disabled=false;button.textContent=original;
 }
}
async function openStudyDetails(study){
 selectedStudyID=study.studyID;selectedStudy=study;hideMainSections();E.studyDetailsSection.classList.remove("hidden");
 E.studyDetailsTitle.textContent=study.studyTypeName||("Study "+study.studyID);E.studyDetailsDate.textContent=formatPersianDateTime(study.studyDate);
 // Status badge so the state is visible without entering edit mode.
 const badge=createStudyStatusBadge(study);
 if(E.studyDetailsStatusBadge){E.studyDetailsStatusBadge.replaceChildren();if(badge)E.studyDetailsStatusBadge.appendChild(badge);}
 await ensureStudyDetailsTypes(study.studyTypeID);
 E.studyDetailsBodyPart.value=study.bodyPart||"";E.studyDetailsStudyDate.value=formatPersianDateTimeForInput(study.studyDate);
 E.studyDetailsDescription.value=study.description||"";E.studyDetailsReport.value=study.report||"";
 // Dentist and status, then the waiting stage narrowed to that dentist's specialty.
 await loadDentists();
 fillDentistSelect(study.dentistStaffID);
 E.studyDetailsStatus2.value=String(Number(study.status)||2);
 await fillWaitStageSelect(study.waitStageID);
 E.studyDetailsFollowUpDate.value=study.followUpDate?formatPersianDateForInput(study.followUpDate):"";
 E.studyDetailsFollowUpNote.value=study.followUpNote||"";
 syncStudyDetailsStatusFields();
 setStudyDetailsEditing(false);setFormStatus(E.studyDetailsStatus,"",false);
 try{const r=await fetch(`/api/radiologystudies/${study.studyID}`,{cache:"no-store"}),x=await readApiJson(r);if(!r.ok||!x.success)throw new Error(apiErrorMessage(r,x,"اطلاعات Study دریافت نشد."));const teeth=x.toothNumbers||x.study?.toothNumbers||[];if(window.DentalRayDentalChart)window.DentalRayDentalChart.render(E.studyDetailsDentalChart,teeth);}catch{if(window.DentalRayDentalChart)window.DentalRayDentalChart.render(E.studyDetailsDentalChart,[]);}finally{E.studyDetailsDentalChart?.classList.add("study-chart-readonly");}
 window.scrollTo(0,0);
}
function setStudyDetailsEditing(editing){
 [E.studyDetailsBodyPart,E.studyDetailsStudyDate,E.studyDetailsDescription,E.studyDetailsReport,
  E.studyDetailsFollowUpDate,E.studyDetailsFollowUpNote].forEach(x=>{if(x)x.readOnly=!editing;});
 E.studyDetailsType.disabled=!editing;
 // The dentist and the status are part of the record, so they unlock with the rest.
 if(E.studyDetailsDentist)E.studyDetailsDentist.disabled=!editing;
 if(E.studyDetailsStatus2)E.studyDetailsStatus2.disabled=!editing;
 if(E.studyDetailsWaitStage)E.studyDetailsWaitStage.disabled=!editing;
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
  const status=Number(E.studyDetailsStatus2?.value)||2;
  const body={studyDate,studyTypeID,bodyPart:emptyToNull(E.studyDetailsBodyPart.value),description:emptyToNull(E.studyDetailsDescription.value),report:emptyToNull(E.studyDetailsReport.value),toothNumbers:window.DentalRayDentalChart?.getSelected(E.studyDetailsDentalChart)||[],
   status,
   waitStageID:status===3?(Number(E.studyDetailsWaitStage?.value)||null):null,
   followUpDate:status===3?parsePersianDateForBackend(E.studyDetailsFollowUpDate.value,false):null,
   followUpNote:status===3?emptyToNull(E.studyDetailsFollowUpNote.value):null,
   dentistStaffID:Number(E.studyDetailsDentist?.value)||null};
  const r=await fetch(`/api/radiologystudies/${studyID}`,{method:"PUT",headers:{"Content-Type":"application/json"},body:JSON.stringify(body)});let x={};try{x=await r.json();}catch{}if(!r.ok||!x.success)throw new Error(getApiError(x,`ویرایش Study انجام نشد. (HTTP ${r.status})`));
  const updated={...selectedStudy,...(x.study||{}),studyID,studyTypeID,studyTypeName:E.studyDetailsType.options[E.studyDetailsType.selectedIndex]?.text||selectedStudy?.studyTypeName,studyDate,bodyPart:body.bodyPart,description:body.description,report:body.report,status:body.status,waitStageID:body.waitStageID,followUpDate:body.followUpDate,followUpNote:body.followUpNote,dentistStaffID:body.dentistStaffID,dentistName:E.studyDetailsDentist?.options[E.studyDetailsDentist.selectedIndex]?.text||selectedStudy?.dentistName,waitStageName:E.studyDetailsWaitStage?.options[E.studyDetailsWaitStage.selectedIndex]?.text||selectedStudy?.waitStageName};selectedStudy=updated;
  setStudyDetailsEditing(false);E.studyDetailsTitle.textContent=updated.studyTypeName||`Study ${studyID}`;E.studyDetailsDate.textContent=formatPersianDateTime(studyDate);setFormStatus(E.studyDetailsStatus,"تغییرات مطالعه با موفقیت ذخیره شد.",false);showToast("مطالعه با موفقیت ویرایش شد.");
  try{const pr=await fetch(`/api/patients/${selectedPatientID}/details`,{cache:"no-store"}),pd=await pr.json();if(pr.ok&&pd.success){const fresh=(pd.studies||[]).find(s=>s.studyID===studyID);if(fresh)selectedStudy=fresh;}}catch(refreshError){console.warn("Study saved, but patient workspace refresh failed:",refreshError);}
 }catch(e){setFormStatus(E.studyDetailsStatus,e.message||"ویرایش Study انجام نشد.",true);
 }finally{studyDetailsSaveInProgress=false;E.studyDetailsSaveButton.disabled=false;E.studyDetailsSaveButton.textContent="ذخیره تغییرات";}
}
window.DentalRaySaveStudyDetails=event=>{event?.preventDefault?.();return saveStudyDetails();};

 // Studies as collapsible rows.
 //
 // A study card used to render everything at once - details, a full odontogram and
 // an image grid - which made each row tall enough to need its own scrolling. Now
 // the header carries a one-line summary and the body opens on demand, so a patient
 // with several studies stays readable.
 function studySummary(study){
  const parts=[];
  if(study.bodyPart)parts.push(study.bodyPart);
  const toothCount=Array.isArray(study.toothNumbers)?study.toothNumbers.length:0;
  if(toothCount)parts.push(`${toothCount} دندان`);
  if(study.imageCount)parts.push(`${study.imageCount} تصویر`);
  if(study.report&&String(study.report).trim())parts.push("دارای گزارش");
  if(Number(study.status)===3&&study.followUpDate)parts.push(`پیگیری ${formatPersianDate(study.followUpDate)}`);
  return parts.join(" · ")||"بدون جزئیات";
 }
 function renderStudiesSafe(studies){
  E.studiesContainer.replaceChildren();
  selectedStudyID=null;selectedStudy=null;
  if(!studies?.length){E.studiesContainer.textContent="برای این بیمار هنوز مطالعه‌ای ثبت نشده است.";return;}
  const ordered=[...studies].sort((a,b)=>new Date(b.studyDate||0)-new Date(a.studyDate||0));
  ordered.forEach(study=>{
   const card=document.createElement("article");
   card.className="study-scroll-card";
   card.dataset.studyId=String(study.studyID);

   // --- header: a clickable summary row -------------------------------------
   const header=document.createElement("header");
   header.className="study-scroll-header study-collapsible-header";
   header.tabIndex=0;
   header.setAttribute("role","button");
   header.setAttribute("aria-expanded","false");

   const heading=document.createElement("div");
   heading.className="study-header-main";
   const title=document.createElement("h4");
   title.textContent=study.studyTypeName||study.studyType||`مطالعه ${study.studyID}`;
   const date=document.createElement("time");
   date.textContent=formatPersianDateTime(study.studyDate);
   title.append(" ", date);
   heading.appendChild(title);
   const statusBadge=createStudyStatusBadge(study);
   if(statusBadge)heading.appendChild(statusBadge);
   // The one-line summary is what makes a collapsed list useful.
   const summaryLine=document.createElement("p");
   summaryLine.className="study-summary-line";
   summaryLine.textContent=studySummary(study);
   heading.appendChild(summaryLine);

   const actions=document.createElement("div");
   actions.className="study-scroll-actions";
   const toggle=document.createElement("button");
   toggle.type="button";toggle.className="study-toggle-button secondary-button";toggle.textContent="نمایش";
   toggle.setAttribute("aria-label","نمایش جزئیات مطالعه");
   if(Number(study.status)!==2){
    const complete=document.createElement("button");complete.type="button";complete.className="study-complete-button";complete.textContent="✓ تمام شد";
    complete.title="علامت‌گذاری این مطالعه به‌عنوان تمام‌شده";complete.onclick=e=>{e.stopPropagation();completeStudyFromCard(study,complete);};
    actions.appendChild(complete);
   }
   const edit=document.createElement("button");edit.type="button";edit.className="secondary-button";edit.textContent="مشاهده / ویرایش";
   edit.onclick=e=>{e.stopPropagation();openStudyDetails(study);};
   const addImage=document.createElement("button");addImage.type="button";addImage.textContent="+ افزودن تصویر";
   addImage.onclick=e=>{e.stopPropagation();openUploadImageForm(study);};
   actions.append(toggle,edit,addImage);

   // --- body: built on first open, so a closed study costs nothing ----------
   const body=document.createElement("div");
   body.className="study-scroll-body hidden";
   let hydrated=false;

   const setOpen=open=>{
    body.classList.toggle("hidden",!open);
    header.setAttribute("aria-expanded",open?"true":"false");
    toggle.textContent=open?"بستن":"نمایش";
    card.classList.toggle("study-open",open);
    if(open&&!hydrated){
     hydrated=true;
     const details=document.createElement("div");details.className="study-scroll-details";
     details.append(createInfoLine("ناحیه",study.bodyPart||"-"),createInfoLine("توضیحات",study.description||"-"),createInfoLine("گزارش",study.report||"-"));
     details.querySelectorAll(":scope > div").forEach(x=>x.classList.add("info-line"));
     const chartSection=document.createElement("section");chartSection.className="study-scroll-chart";
     const chartTitle=document.createElement("strong");chartTitle.textContent="نمودار دندان‌های این مطالعه";
     const chart=document.createElement("div");chart.className="study-card-dental-chart study-chart-readonly";
     chartSection.append(chartTitle,chart);
     body.append(details,chartSection);
     const imagesSection=document.createElement("section");imagesSection.className="study-scroll-images";
     const imagesTitle=document.createElement("div");imagesTitle.className="study-scroll-images-title";imagesTitle.textContent="تصاویر مطالعه";
     const status=document.createElement("div");status.className="status-message";status.textContent="در حال دریافت تصاویر...";
     const grid=document.createElement("div");grid.className="images-grid";
     imagesSection.append(imagesTitle,status,grid);
     body.appendChild(imagesSection);
     hydrateStudyCard(study,chart,status,grid);
    }
   };
   toggle.onclick=e=>{e.stopPropagation();setOpen(body.classList.contains("hidden"));};
   header.addEventListener("click",()=>setOpen(body.classList.contains("hidden")));
   header.addEventListener("keydown",e=>{
    if(e.key==="Enter"||e.key===" "){e.preventDefault();setOpen(body.classList.contains("hidden"));}
   });

   header.append(heading,actions);
   card.append(header,body);
   E.studiesContainer.appendChild(card);
  });
 }
async function hydrateStudyCard(study,chart,status,grid){
 const [imagesResult,studyResult]=await Promise.allSettled([fetch(`/api/radiologyimages/study/${study.studyID}`).then(async r=>({r,x:await readApiJson(r)})),fetch(`/api/radiologystudies/${study.studyID}`).then(async r=>({r,x:await readApiJson(r)}))]);
 if(studyResult.status==="fulfilled"&&studyResult.value.r.ok){const x=studyResult.value.x,teeth=x.toothNumbers||x.study?.toothNumbers||[];if(window.DentalRayDentalChart)window.DentalRayDentalChart.render(chart,teeth);}
 else if(window.DentalRayDentalChart)window.DentalRayDentalChart.render(chart,[]);
 if(imagesResult.status==="fulfilled"){const {r,x}=imagesResult.value;if(r.ok&&x.success){renderImagesInGrid(x.images||[],grid);status.textContent=x.count?`${x.count} تصویر / فایل`:`برای این مطالعه هنوز تصویری ثبت نشده است.`;return;}status.textContent=getApiError(x,"تصاویر مطالعه دریافت نشد.");}
 else status.textContent="تصاویر مطالعه دریافت نشد.";status.classList.toggle("error",!imagesResult.value?.r?.ok);
}

function renderImagesInGrid(images,grid){grid.innerHTML="";(images||[]).forEach(image=>{const card=document.createElement("div");card.className="image-card";let media;if(image.contentType==="application/pdf"){media=document.createElement("div");media.className="pdf-thumbnail";media.textContent="PDF";}else{media=document.createElement("img");media.src=`/api/radiologyimages/${image.imageID}`;media.alt=image.fileName;media.loading="lazy";}media.onclick=()=>openLargeImage(image);const title=document.createElement("div");title.className="image-card-title";title.textContent=image.fileName;const type=document.createElement("div");type.className="field-hint";type.textContent=image.imageTypeName?`نوع تصویر: ${image.imageTypeName}`:"نوع تصویر: تعیین نشده";card.append(media,title,type);if(image.contentType?.startsWith("image/")){const ai=document.createElement("button"),isDental=window.DentalRayImageAI?.isDentalType(image.imageTypeName)??/(cbct|opg|پانور|پری[‌ -]?اپیکال|بایت|اکلوز|سفال|داخل دهان|دندان)/i.test(image.imageTypeName||"");ai.type="button";ai.className="card-extraction-button";ai.textContent=isDental?"تحلیل رادیولوژی":"استخراج اطلاعات از کارت";ai.onclick=ev=>{ev.stopPropagation();isDental?window.DentalRayImageAI?.analyze(image):window.DentalRayImageAI?.open(image);};card.appendChild(ai);}if(!image.imageTypeID&&window.dentalRayCurrentUser?.isSuperAdmin===true){const classify=document.createElement("button");classify.type="button";classify.className="secondary-button";classify.textContent="تعیین نوع تصویر";classify.onclick=async ev=>{ev.stopPropagation();try{const r=await fetch("/api/imagetypes"),x=await readApiJson(r);if(!r.ok||!x.success)throw new Error(apiErrorMessage(r,x,"انواع تصویر دریافت نشد."));const choices=(x.imageTypes||[]).map(t=>`${t.imageTypeID}: ${t.imageTypeName}`).join("\n");const answer=window.prompt("ImageTypeID را انتخاب کنید:\n"+choices);if(answer===null)return;const imageTypeID=Number(answer);if(!Number.isInteger(imageTypeID)||(x.imageTypes||[]).every(t=>t.imageTypeID!==imageTypeID))throw new Error("نوع تصویر معتبر انتخاب نشده است.");const u=await fetch(`/api/radiologyimages/${image.imageID}/type`,{method:"PATCH",headers:{"Content-Type":"application/json"},body:JSON.stringify({imageTypeID})}),y=await u.json();if(!u.ok||!y.success)throw new Error(getApiError(y,"نوع تصویر ذخیره نشد."));image.imageTypeID=y.imageTypeID;image.imageTypeName=y.imageTypeName;type.textContent=`نوع تصویر: ${y.imageTypeName}`;classify.remove();showToast("نوع تصویر ذخیره شد.","success");}catch(e){showToast(e.message||"نوع تصویر ذخیره نشد.","error");}};card.appendChild(classify);}grid.appendChild(card);});}
let imageViewScale=1,imageViewRotation=0,imageViewFlipX=1,imageViewX=0,imageViewY=0,imageDragging=false,imageDragStartX=0,imageDragStartY=0;
function applyImageView(){E.largeImage.style.transform=`translate(${imageViewX}px,${imageViewY}px) scale(${imageViewScale}) rotate(${imageViewRotation}deg) scaleX(${imageViewFlipX})`;}
function resetImageView(){imageViewScale=1;imageViewRotation=0;imageViewFlipX=1;imageViewX=0;imageViewY=0;applyImageView();}
function openLargeImage(image){if(image.contentType==="application/pdf"){window.open(`/api/radiologyimages/${image.imageID}`,"_blank","noopener");return;}resetImageView();E.largeImage.src=`/api/radiologyimages/${image.imageID}`;E.largeImageCaption.textContent=image.imageTypeName?`${image.fileName} — ${image.imageTypeName}`:image.fileName;E.imageModal.classList.remove("hidden");}
function closeLargeImage(){E.imageModal.classList.add("hidden");E.largeImage.src="";resetImageView();}

function resetCameraCapture(){pendingCameraFile=null;if(cameraPreviewUrl){URL.revokeObjectURL(cameraPreviewUrl);cameraPreviewUrl=null;}E.cameraFileInput.value="";E.cameraPreviewImage.removeAttribute("src");E.cameraPreviewPanel.classList.add("hidden");E.imageFileInput.required=true;}
async function loadImageTypes(){E.uploadImageType.innerHTML=`<option value="">انتخاب نوع تصویر...</option>`;const r=await fetch("/api/imagetypes"),x=await readApiJson(r);if(!r.ok||!x.success)throw new Error(apiErrorMessage(r,x,"انواع تصویر دریافت نشد."));const types=x.imageTypes||[];types.forEach(t=>{const o=document.createElement("option");o.value=t.imageTypeID;o.textContent=t.imageTypeName;E.uploadImageType.appendChild(o);});/* The placeholder must stop looking like a loader, otherwise nobody notices that a choice is required and the Save button silently refuses to run. */const placeholder=E.uploadImageType.querySelector('option[value=""]');if(placeholder)placeholder.textContent=types.length?"نوع تصویر را انتخاب کنید...":"هنوز نوع تصویری ثبت نشده است.";}
async function openUploadImageForm(study){selectedStudyID=study.studyID;selectedStudy=study;E.imageFileInput.value="";E.uploadImageType.value="";resetCameraCapture();setFormStatus(E.uploadImageStatus,"",false);E.uploadImageStudyInfo.textContent=`رادیولوژی شماره ${study.studyID} — ${study.studyType||""}`;hideMainSections();E.uploadImageSection.classList.remove("hidden");try{await loadImageTypes();}catch(e){setFormStatus(E.uploadImageStatus,e.message,true);}window.scrollTo(0,0);}
E.cameraFileInput?.addEventListener("change",()=>{const f=E.cameraFileInput.files?.[0];if(!f)return;pendingCameraFile=f;if(cameraPreviewUrl)URL.revokeObjectURL(cameraPreviewUrl);cameraPreviewUrl=URL.createObjectURL(f);E.cameraPreviewImage.src=cameraPreviewUrl;E.cameraPreviewPanel.classList.remove("hidden");setFormStatus(E.uploadImageStatus,"پیش‌نمایش را بررسی و سپس «تأیید تصویر» را انتخاب کنید.",false);});
E.confirmCameraButton?.addEventListener("click",()=>{if(!pendingCameraFile)return;E.imageFileInput.value="";setFormStatus(E.uploadImageStatus,E.uploadImageType.value?"تصویر دوربین تأیید شد و آماده ذخیره است.":"تصویر تأیید شد؛ حالا «نوع تصویر» را انتخاب و «ذخیره و اتصال به Study» را بزنید.",false);});
E.uploadImageType?.addEventListener("change",()=>E.uploadImageType.classList.remove("field-missing"));
E.retakeCameraButton?.addEventListener("click",()=>{resetCameraCapture();E.cameraFileInput.click();});
async function uploadImage(){try{if(!selectedStudyID)throw new Error("رادیولوژی انتخاب نشده است.");const imageTypeID=Number(E.uploadImageType.value);if(!imageTypeID)throw new Error("ابتدا «نوع تصویر» را از فهرست انتخاب کنید؛ بدون آن ذخیره ممکن نیست.");const file=pendingCameraFile||E.imageFileInput.files?.[0];if(!file)throw new Error("یک فایل یا تصویر دوربین انتخاب کنید.");const isImage=(file.type||"").toLowerCase().startsWith("image/"),isPdf=(file.type||"").toLowerCase()==="application/pdf"||(file.name||"").toLowerCase().endsWith(".pdf");if(!isImage&&!isPdf)throw new Error("فایل انتخاب‌شده باید تصویر یا PDF باشد.");const fd=new FormData();fd.append("file",file);setFormStatus(E.uploadImageStatus,"در حال ذخیره و اتصال فایل...",false);const r=await fetch(`/api/radiologyimages?studyID=${selectedStudyID}&imageTypeID=${imageTypeID}`,{method:"POST",body:fd}),x=await readApiJson(r);if(!r.ok||!x.success)throw new Error(apiErrorMessage(r,x,"ذخیره فایل انجام نشد."));resetCameraCapture();E.uploadImageType.classList.remove("field-missing");if(selectedStudy){selectedStudy.imageCount=(selectedStudy.imageCount||0)+1;openStudyImages(selectedStudy);}else await openPatient(selectedPatientID);showToast(`فایل با موفقیت ذخیره شد: ${x.fileName}`);}catch(e){setFormStatus(E.uploadImageStatus,e.message,true);/* The status line sits above the buttons and is easy to miss on a phone, so failures are repeated as a toast. */showToast(e.message,"error");if(!Number(E.uploadImageType?.value))E.uploadImageType?.classList.add("field-missing");}}

function patientPayload(prefix){const f=E[`${prefix}FirstName`].value.trim(),l=E[`${prefix}LastName`].value.trim(),n=normalizeDigits(E[`${prefix}NationalCode`].value.trim()),m=normalizePhone(E[`${prefix}Mobile`].value);validatePatientFields(f,l,n,m);return{nationalCode:n,firstName:f,lastName:l,birthDate:parsePersianDateForBackend(E[`${prefix}BirthDate`].value,false),gender:E[`${prefix}Gender`].value===""?null:+E[`${prefix}Gender`].value,mobile:m,address:emptyToNull(E[`${prefix}Address`].value),description:emptyToNull(E[`${prefix}Description`].value)};}
function openNewPatientForm(){E.newPatientForm.reset();setFormStatus(E.newPatientStatus,"",false);hideMainSections();E.newPatientSection.classList.remove("hidden");E.newFirstName.focus();}
async function createPatient(){try{const r=await fetch("/api/patients",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(patientPayload("new"))}),x=await readApiJson(r);if(!r.ok||!x.success)throw new Error(apiErrorMessage(r,x,"ثبت بیمار انجام نشد."));await loadPatients();showPatientsScreen();showToast("بیمار ثبت شد.");}catch(e){setFormStatus(E.newPatientStatus,e.message,true);}}
function openEditPatientForm(){if(!selectedPatient)return;const p=selectedPatient;E.editFirstName.value=p.firstName||"";E.editLastName.value=p.lastName||"";E.editNationalCode.value=p.nationalCode||"";E.editMobile.value=p.mobile||"";E.editBirthDate.value=formatPersianDateForInput(p.birthDate);E.editGender.value=p.gender??"";E.editAddress.value=p.address||"";E.editDescription.value=p.description||"";hideMainSections();E.editPatientSection.classList.remove("hidden");}
async function updatePatient(){try{const r=await fetch(`/api/patients/${selectedPatientID}`,{method:"PUT",headers:{"Content-Type":"application/json"},body:JSON.stringify(patientPayload("edit"))}),x=await readApiJson(r);if(!r.ok||!x.success)throw new Error(apiErrorMessage(r,x,"ویرایش بیمار انجام نشد."));await openPatient(selectedPatientID);showToast("اطلاعات بیمار ذخیره شد.");}catch(e){setFormStatus(E.editPatientStatus,e.message,true);}}
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
 printWindow.document.write(`<!doctype html><html lang="fa" dir="rtl"><head><meta charset="utf-8"><title>اطلاعات بیمار - ${escapeHtml(patientCode)}</title><style>body{font-family:Tahoma,Arial,sans-serif;margin:28px;color:#17365d}h1{font-size:22px;margin:0 0 6px}.subtitle{color:#60758c;margin-bottom:22px}.sheet{border:1px solid #cfe0ee;border-radius:12px;padding:18px}.row{display:grid;grid-template-columns:150px 1fr;gap:14px;padding:9px 4px;border-bottom:1px solid #e7eff6}.row:last-child{border-bottom:0}.label{font-weight:700;color:#285b8f}.footer{margin-top:20px;font-size:11px;color:#718397}@media print{body{margin:12mm}.sheet{break-inside:avoid}}</style></head><body><h1>Dentix — اطلاعات بیمار</h1><div class="subtitle">${escapeHtml(patientCode)}</div><div class="sheet">${rows.map(([label,value])=>`<div class="row"><div class="label">${escapeHtml(label)}</div><div>${escapeHtml(value)}</div></div>`).join("")}</div><div class="footer">چاپ‌شده از سامانه Dentix</div><script>window.addEventListener("load",()=>window.print());<\/script></body></html>`);
 printWindow.document.close();
}
async function togglePatientActiveStatus(){
 if(!selectedPatient)return;
 const isDeactivating=selectedPatient.isActive;
 if(isDeactivating&&!await askConfirmation({title:"غیرفعال کردن بیمار",message:`آیا پرونده ${selectedPatient.firstName} ${selectedPatient.lastName} غیرفعال شود؟ اطلاعات و مطالعات بیمار حذف نخواهند شد.`,confirmText:"غیرفعال کردن"}))return;
 // Both corresponding controller actions use HttpPut; matching that verb prevents an HTTP 405 response.
 try{const action=isDeactivating?"deactivate":"activate";const r=await fetch(`/api/patients/${selectedPatientID}/${action}`,{method:"PUT"}),x=await readApiJson(r);if(!r.ok||!x.success)throw new Error(apiErrorMessage(r,x,"عملیات انجام نشد."));await openPatient(selectedPatientID);showToast(isDeactivating?"بیمار غیرفعال شد.":"بیمار فعال شد.");}catch(e){showToast(e.message||"عملیات انجام نشد.","error");}
}
async function openNewStudyForm(){
 if(!Number.isInteger(Number(selectedPatientID))||Number(selectedPatientID)<=0){
  showToast("ابتدا یک بیمار را انتخاب کنید.","error");
  return;
 }
 E.newStudyForm.reset();
 setFormStatus(E.newStudyStatus,"",false);
 E.newStudyDate.value=toEnglishJalaliInput(new Date(),true);
 // A brand-new Study starts open: the work is by definition not finished yet.
 setStudyStatusFields("new",1,null,null);
 hideMainSections();
 E.newStudySection.classList.remove("hidden");
 E.newStudySection.scrollIntoView({behavior:"smooth",block:"start"});
 E.newStudyType.innerHTML='<option value="">در حال دریافت انواع مطالعه...</option>';
 E.newStudyType.disabled=true;
 try{
  const r=await fetch("/api/studytypes",{cache:"no-store"}),x=await readApiJson(r);
  if(!r.ok||!x.success)throw new Error(apiErrorMessage(r,x,"انواع مطالعه دریافت نشد."));
  E.newStudyType.innerHTML='<option value="">انتخاب نوع مطالعه</option>';
  (x.studyTypes||[]).forEach(t=>{const o=document.createElement("option");o.value=String(t.studyTypeID);o.textContent=t.studyTypeName;E.newStudyType.appendChild(o);});
  E.newStudyType.disabled=false;E.newStudyType.focus();
 }catch(e){E.newStudyType.innerHTML='<option value="">دریافت انواع مطالعه ناموفق بود</option>';setFormStatus(E.newStudyStatus,e.message||"انواع مطالعه دریافت نشد.",true);}
}
// Public entry point keeps this primary action independent from later optional bindings.
window.DentalRayOpenNewStudy=event=>{event?.preventDefault?.();return openNewStudyForm();};
// Study status helpers. The follow-up fields only make sense for "needs another
// study", so they are shown and hidden with the status choice.
function studyStatusLabel(status){return({1:"باز",2:"تمام‌شده",3:"در انتظار"})[Number(status)]||"-";}
function createStudyStatusBadge(study){
 const status=Number(study?.status)||2;
 // Completed is the normal case and needs no badge; only work still outstanding
 // is worth flagging.
 if(status===2)return null;
 const wrap=document.createElement("span");
 wrap.className=`study-status-badge status-${status}`;
 wrap.textContent=studyStatusLabel(status);
 if(status===3&&study.followUpDate){
  const due=new Date(study.followUpDate);
  const overdue=due<=new Date(new Date().toDateString());
  wrap.textContent=`${studyStatusLabel(status)} — ${formatPersianDate(study.followUpDate)}`;
  if(overdue)wrap.classList.add("overdue");
 }
 if(study?.followUpNote)wrap.title=study.followUpNote;
 return wrap;
}
function syncFollowUpVisibility(prefix){
 const sel=E[`${prefix}Status`],box=E[`${prefix}FollowUpBox`];
 if(!sel||!box)return;
 box.classList.toggle("hidden",sel.value!=="3");
}
function setStudyStatusFields(prefix,status,followUpDate,followUpNote){
 const sel=E[`${prefix}Status`];
 if(sel)sel.value=String(status||2);
 if(E[`${prefix}FollowUpDate`])E[`${prefix}FollowUpDate`].value=followUpDate?formatPersianDateForInput(followUpDate):"";
 if(E[`${prefix}FollowUpNote`])E[`${prefix}FollowUpNote`].value=followUpNote||"";
 syncFollowUpVisibility(prefix);
}
function attachStatusToggle(prefix){E[`${prefix}Status`]?.addEventListener("change",()=>syncFollowUpVisibility(prefix));}

function studyPayload(prefix){
 const studyTypeID=Number(E[`${prefix}StudyType`].value);
 if(!Number.isInteger(studyTypeID)||studyTypeID<=0)throw new Error("نوع رادیولوژی را انتخاب کنید.");
 const chart=byId(prefix==="new"?"newStudyDentalChart":"editStudyDentalChart");
 return{
  studyDate:parsePersianDateForBackend(E[`${prefix}StudyDate`].value,true),
  studyTypeID,
  bodyPart:emptyToNull(E[`${prefix}BodyPart`].value),
  description:emptyToNull(E[`${prefix}StudyDescription`].value),
  report:emptyToNull(E[`${prefix}StudyReport`].value),
  toothNumbers:window.DentalRayDentalChart?.getSelected(chart)||[],
  status:Number(E[`${prefix}Status`]?.value)||2,
  followUpDate:E[`${prefix}Status`]?.value==="3"?parsePersianDateForBackend(E[`${prefix}FollowUpDate`].value,false):null,
  followUpNote:E[`${prefix}Status`]?.value==="3"?emptyToNull(E[`${prefix}FollowUpNote`].value):null
 };
}
// Offers to message the patient after something worth telling them about.
//
// It is a bar under the form, not a dialog, so it never blocks the user: they can
// ignore it and it simply disappears on the next action. Nothing is sent without
// an explicit click, which keeps the clinic in control of every message.
function offerPatientMessage(patientID,reason,preferredTemplate){
 const host=document.querySelector(".page-container");
 if(!host||!patientID)return;
 document.getElementById("patientMessageOffer")?.remove();

 const bar=document.createElement("div");
 bar.id="patientMessageOffer";
 bar.className="message-offer";
 bar.innerHTML='<span class="message-offer-text"></span><button type="button" class="message-offer-send">ارسال پیامک</button><button type="button" class="secondary-button message-offer-dismiss">بعداً</button>';
 bar.querySelector(".message-offer-text").textContent=reason;
 host.prepend(bar);

 const close=()=>bar.remove();
 bar.querySelector(".message-offer-dismiss").onclick=close;
 bar.querySelector(".message-offer-send").onclick=()=>{
  close();
  const study=null;
  const patient=window.selectedPatient;
  if(!patient){
   // The patient record is the anchor the messaging dialog needs.
   openPatient(patientID).then(()=>{
    window.dispatchEvent(new CustomEvent("dentalray-offer-message",{detail:{patientID,templateKey:preferredTemplate}}));
   });
   return;
  }
  window.dispatchEvent(new CustomEvent("dentalray-offer-message",{detail:{patientID,templateKey:preferredTemplate}}));
 };
}
async function createStudy(){try{const body={...studyPayload("new"),patientID:selectedPatientID};const r=await fetch("/api/radiologystudies",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(body)}),x=await readApiJson(r);if(!r.ok||x.success===false)throw new Error(apiErrorMessage(r,x,"ثبت رادیولوژی انجام نشد."));const savedPatientID=selectedPatientID;await openPatient(savedPatientID);showToast("رادیولوژی ثبت شد.");
 // A study recorded for a future date is a booked visit, so a reminder makes sense.
 const saved=x.study||x;
 if(Number(saved.status)===3&&saved.followUpDate){
  offerPatientMessage(savedPatientID,"برای نوبت پیگیری این مطالعه، به بیمار یادآوری بفرستیم؟","appointment-reminder");
 } else if(Number(saved.status)===1){
  offerPatientMessage(savedPatientID,"برای این مطالعه جدید به بیمار اطلاع بفرستیم؟","images-ready");
 }
}catch(e){setFormStatus(E.newStudyStatus,getApiError({message:e.message},"ثبت رادیولوژی انجام نشد."),true);}}
function openMergePatientForm(){E.mergePatientForm.reset();setFormStatus(E.mergePatientStatus,`Source: ${selectedPatient.firstName} ${selectedPatient.lastName} — ${selectedPatient.nationalCode}`,false);hideMainSections();E.mergePatientSection.classList.remove("hidden");}
async function mergePatient(){try{const code=normalizeDigits(E.mergeTargetNationalCode.value.trim());const tr=await fetch(`/api/patients/${encodeURIComponent(code)}`),target=await readApiJson(tr);if(!tr.ok)throw new Error(apiErrorMessage(tr,target,"بیمار مقصد پیدا نشد."));if(!await askConfirmation({title:"تأیید ادغام بیمار",message:`Source: ${selectedPatient.nationalCode}\nTarget: ${target.nationalCode}\nتمام Studyها و تصاویر منتقل می‌شوند.`,confirmText:"انجام Merge"}))return;const r=await fetch("/api/patients/merge",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({sourcePatientID:selectedPatientID,targetPatientID:target.patientID})}),x=await readApiJson(r);if(!r.ok||!x.success)throw new Error(apiErrorMessage(r,x,"Merge انجام نشد."));await loadPatients();await openPatient(target.patientID);showToast("ادغام با موفقیت انجام شد.");}catch(e){setFormStatus(E.mergePatientStatus,e.message,true);}}

enableJalaliDateMask(E.newBirthDate);enableJalaliDateMask(E.editBirthDate);enableJalaliDateTimeMask(E.newStudyDate);enableJalaliDateMask(E.newFollowUpDate);enableJalaliDateMask(E.studyDetailsFollowUpDate);attachStatusToggle("new");attachStatusToggle("edit");window.DentalRayJalali?.enhanceAll(document);syncFollowUpVisibility("new");syncFollowUpVisibility("edit");
E.searchButton.onclick=()=>loadPatients(E.patientSearch.value);E.clearSearchButton.onclick=()=>{E.patientSearch.value="";loadPatients();};E.patientSearch.onkeydown=e=>{if(e.key==="Enter")loadPatients(E.patientSearch.value);};E.includeInactivePatients.onchange=()=>loadPatients(E.patientSearch.value);
E.openStudiesOnly.onchange=()=>loadPatients(E.patientSearch.value);
E.dueFollowUpOnly.onchange=()=>{E.openStudiesOnly.checked=E.dueFollowUpOnly.checked||E.openStudiesOnly.checked;loadPatients(E.patientSearch.value);};E.newPatientButton.onclick=openNewPatientForm;E.newPatientForm.onsubmit=e=>{e.preventDefault();createPatient();};E.backToPatientsButton.addEventListener("click",e=>{e.preventDefault();e.stopPropagation();showPatientsScreen();});
E.backToPatientDetailsButton?.addEventListener("click",()=>openPatient(selectedPatientID));
E.backToStudyDetailsButton?.addEventListener("click",()=>{if(selectedStudy)openStudyDetails(selectedStudy);});
E.studyDetailsImagesButton?.addEventListener("click",()=>{if(selectedStudy)openStudyImages(selectedStudy);});
E.studyDetailsEditButton?.addEventListener("click",()=>setStudyDetailsEditing(true));
E.studyDetailsCancelButton?.addEventListener("click",()=>{if(selectedStudy)openStudyDetails(selectedStudy);});
E.studyDetailsForm?.addEventListener("submit",e=>{e.preventDefault();saveStudyDetails();});
E.studyDetailsSaveButton?.addEventListener("click",e=>{e.preventDefault();saveStudyDetails();});
// The status decides whether the waiting fields apply, and the dentist decides
// which waiting stages are offered.
E.studyDetailsStatus2?.addEventListener("change",syncStudyDetailsStatusFields);
E.studyDetailsDentist?.addEventListener("change",()=>fillWaitStageSelect(null));
E.newStudyButton?.addEventListener("click",e=>{e.preventDefault();openNewStudyForm();});
E.studyDetailsUploadButton?.addEventListener("click",()=>{if(selectedStudy)openUploadImageForm(selectedStudy);});
E.editPatientButton?.addEventListener("click",openEditPatientForm);
E.printPatientButton?.addEventListener("click",printPatientInformation);
E.deactivatePatientButton?.addEventListener("click",togglePatientActiveStatus);
E.mergePatientButton?.addEventListener("click",openMergePatientForm);
E.editPatientForm?.addEventListener("submit",e=>{e.preventDefault();updatePatient();});
E.newStudyForm?.addEventListener("submit",e=>{e.preventDefault();createStudy();});

E.uploadImageForm?.addEventListener("submit",e=>{e.preventDefault();uploadImage();});
E.mergePatientForm?.addEventListener("submit",e=>{e.preventDefault();mergePatient();});
[[E.cancelNewPatientButton,E.cancelNewPatientButtonBottom]].flat().forEach(b=>b.onclick=showPatientsScreen);[E.cancelEditPatientButton,E.cancelEditPatientButtonBottom,E.cancelNewStudyButton,E.cancelNewStudyButtonBottom,E.cancelMergePatientButton,E.cancelMergePatientButtonBottom].forEach(b=>b.onclick=()=>openPatient(selectedPatientID));[E.cancelUploadImageButton,E.cancelUploadImageButtonBottom].forEach(b=>b.onclick=()=>selectedStudy?openStudyImages(selectedStudy):openPatient(selectedPatientID));
E.patientPhotoButton?.addEventListener("click",()=>E.patientPhotoInput?.click());
E.patientPhotoInput?.addEventListener("change",async()=>{const file=E.patientPhotoInput.files?.[0];if(!file||!selectedPatientID)return;try{const fd=new FormData();fd.append("file",file);const r=await fetch(`/api/patients/${selectedPatientID}/photo`,{method:"POST",body:fd}),x=await readApiJson(r);if(!r.ok||!x.success)throw new Error(apiErrorMessage(r,x,"ذخیره تصویر بیمار انجام نشد."));E.patientProfilePhoto.src=`/api/patients/${selectedPatientID}/photo?v=${Date.now()}`;E.patientProfilePhoto.classList.remove("empty");showToast("تصویر بیمار ذخیره شد.");}catch(e){showToast(e.message||"ذخیره تصویر بیمار انجام نشد.","error");}finally{E.patientPhotoInput.value="";}});
E.zoomInImageButton.onclick=()=>{imageViewScale=Math.min(5,imageViewScale+0.25);applyImageView();};E.zoomOutImageButton.onclick=()=>{imageViewScale=Math.max(0.25,imageViewScale-0.25);applyImageView();};E.rotateLeftImageButton.onclick=()=>{imageViewRotation-=90;applyImageView();};E.rotateRightImageButton.onclick=()=>{imageViewRotation+=90;applyImageView();};E.flipHorizontalImageButton.onclick=()=>{imageViewFlipX*=-1;applyImageView();};E.resetImageViewButton.onclick=resetImageView;E.imageModal.addEventListener("pointerdown",e=>{if(e.target!==E.largeImage)return;e.preventDefault();imageDragging=true;imageDragStartX=e.clientX-imageViewX;imageDragStartY=e.clientY-imageViewY;try{E.imageModal.setPointerCapture(e.pointerId);}catch{}});E.imageModal.addEventListener("pointermove",e=>{if(!imageDragging)return;e.preventDefault();imageViewX=e.clientX-imageDragStartX;imageViewY=e.clientY-imageDragStartY;applyImageView();});const endImageDrag=e=>{if(!imageDragging)return;imageDragging=false;try{E.imageModal.releasePointerCapture(e.pointerId);}catch{}};E.imageModal.addEventListener("pointerup",endImageDrag);E.imageModal.addEventListener("pointercancel",endImageDrag);E.imageModal.addEventListener("lostpointercapture",()=>{imageDragging=false;});E.closeImageModalButton.onclick=closeLargeImage;E.imageModal.onclick=e=>{if(imageDragging){e.preventDefault();e.stopPropagation();return;}/* The viewer closes only with the explicit close button or Escape. This prevents a completed image drag from being interpreted as a backdrop click. */};document.addEventListener("keydown",e=>{if(e.key==="Escape")closeLargeImage();});
loadPatients();
