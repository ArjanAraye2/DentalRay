// ============================================================
// DentalRay Frontend - Study Delete + Dental Chart integration
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
            const button = document.createElement("button"); button.type="button"; button.className="danger-button study-delete-button"; button.textContent="حذف Study";
            button.addEventListener("click",e=>{e.stopPropagation();deleteStudyFromFrontend(studyID);}); buttons.appendChild(button);
        });
    }

    async function getDeletePreview(studyID){const response=await fetch(`/api/radiologystudies/${studyID}/delete-preview`);const result=await response.json();if(!response.ok||!result.success)throw new Error(getApiError(result,"اطلاعات لازم برای حذف Study دریافت نشد."));return result;}
    async function chooseStudyOnlyImages(images){if(!images||!images.length)return[];const names=images.map(x=>`${x.imageID}: ${x.fileName}`).join("\n");const answer=window.prompt("این تصاویر فقط به همین Study متصل هستند:\n\n"+names+"\n\nبرای نگهداری همه، کادر را خالی بگذارید.\nبرای حذف همه، all را وارد کنید.\nبرای حذف انتخابی، ImageIDها را با کاما جدا کنید.");if(answer===null||answer.trim()==="")return[];if(answer.trim().toLowerCase()==="all")return images.map(x=>x.imageID);const allowed=new Set(images.map(x=>Number(x.imageID)));return[...new Set(answer.split(",").map(x=>Number(x.trim())).filter(x=>Number.isInteger(x)&&allowed.has(x)))];}
    async function deleteStudyFromFrontend(studyID){try{const preview=await getDeletePreview(studyID),studyOnly=preview.studyOnlyImages||[];let message="آیا از حذف این Study مطمئن هستید؟";if(preview.sharedImages?.length)message+=`\n\n${preview.sharedImages.length} تصویر مشترک برای Studyهای دیگر باقی می‌ماند.`;const confirmed=await askConfirmation({title:"حذف Study",message,confirmText:"ادامه حذف",danger:true});if(!confirmed)return;const deleteIDs=await chooseStudyOnlyImages(studyOnly),query=deleteIDs.map(id=>`deleteImageIDs=${encodeURIComponent(id)}`).join("&");const response=await fetch(`/api/radiologystudies/${studyID}${query?`?${query}`:""}`,{method:"DELETE"}),result=await response.json();if(!response.ok||!result.success)throw new Error(getApiError(result,"حذف Study انجام نشد."));showToast("Study با موفقیت حذف شد.","success");if(selectedPatientID)await openPatient(selectedPatientID);}catch(error){console.error(error);showToast(error.message||"حذف Study انجام نشد.","error");}}

    function installStudyDentalStyles(){if(document.getElementById("studyDentalStylesheet"))return;const link=document.createElement("link");link.id="studyDentalStylesheet";link.rel="stylesheet";link.href="/css/study-dental.css";document.head.appendChild(link);}
    function createDentalChartField(containerId){const panel=document.createElement("div");panel.className="study-dental-panel";const chart=document.createElement("div");chart.id=containerId;const hint=document.createElement("small");hint.className="field-hint";hint.textContent="دندان‌های مربوط به این Study را انتخاب کنید.";panel.append(chart,hint);return panel;}
    function injectDentalCharts(){if(!window.DentalRayDentalChart)return;const newGrid=document.querySelector("#newStudyForm .form-grid");if(newGrid&&!document.getElementById("newStudyDentalChart")){const f=createDentalChartField("newStudyDentalChart");newGrid.appendChild(f);window.DentalRayDentalChart.render(f.querySelector("#newStudyDentalChart"),[]);}const editGrid=document.querySelector("#editStudyForm .form-grid");if(editGrid&&!document.getElementById("editStudyDentalChart")){const f=createDentalChartField("editStudyDentalChart");editGrid.appendChild(f);window.DentalRayDentalChart.render(f.querySelector("#editStudyDentalChart"),[]);}}
    async function loadStudyTeeth(studyID){try{const r=await fetch(`/api/radiologystudies/${studyID}`),x=await r.json();if(!r.ok)return[];return x.toothNumbers||x.study?.toothNumbers||[];}catch{return[];}}

    // Do not replace window.fetch globally. A global fetch wrapper makes every
    // request in DentalRay appear to originate from this file and can interfere
    // with unrelated responses such as Patient Details. Tooth selections are
    // added only to the two Study forms immediately before their own request.
    function addTeethToStudyRequestBody(body, formKind) {
        if (typeof body !== "string") return body;
        try {
            const data = JSON.parse(body);
            const chart = document.getElementById(formKind === "new" ? "newStudyDentalChart" : "editStudyDentalChart");
            data.toothNumbers = window.DentalRayDentalChart?.getSelected(chart) || [];
            return JSON.stringify(data);
        } catch (e) {
            console.error("DentalRay: could not add tooth selections to Study request.", e);
            return body;
        }
    }

    window.dentalRayAddStudyTeeth = addTeethToStudyRequestBody;

    // A closed Study shows only its date/title. All action buttons, including
    // Images and Add file, live inside the collapsible body and therefore become
    // visible only after the Study is opened.
    function enhanceStudyAccordions(){document.querySelectorAll(".study-card").forEach(card=>{if(card.dataset.accordionReady==="1")return;const header=card.querySelector(".study-card-header");if(!header)return;const title=header.querySelector(".study-title"),meta=card.querySelector(".study-meta"),actions=header.querySelector(".study-action-buttons");if(meta){const dateLine=Array.from(meta.children).find(x=>(x.querySelector("strong")?.textContent||"").includes("تاریخ"));const dateText=dateLine?.querySelector("span")?.textContent;if(title&&dateText)title.textContent=dateText;}const body=document.createElement("div");body.className="study-accordion-body hidden";Array.from(card.children).filter(x=>x!==header).forEach(x=>body.appendChild(x));if(actions){actions.classList.add("study-body-actions");body.insertBefore(actions,body.firstChild);}const chart=document.createElement("div");chart.className="study-readonly-odontogram";body.insertBefore(chart,actions?actions.nextSibling:body.firstChild);card.appendChild(body);const arrow=document.createElement("span");arrow.className="study-accordion-arrow";arrow.textContent="⌄";header.insertBefore(arrow,header.firstChild);header.classList.add("study-accordion-header");header.addEventListener("click",async e=>{const opening=body.classList.contains("hidden");body.classList.toggle("hidden",!opening);card.classList.toggle("study-open",opening);arrow.textContent=opening?"⌃":"⌄";if(opening&&window.DentalRayDentalChart&&!chart.dataset.rendered){const teeth=await loadStudyTeeth(Number(card.dataset.studyId));window.DentalRayDentalChart.render(chart,teeth);chart.dataset.rendered="1";}});card.dataset.accordionReady="1";});}

    function wireFormCharts(){document.getElementById("newStudyButton")?.addEventListener("click",()=>setTimeout(()=>{const c=document.getElementById("newStudyDentalChart");if(c&&window.DentalRayDentalChart)window.DentalRayDentalChart.render(c,[]);},0));document.getElementById("studiesContainer")?.addEventListener("click",e=>{const button=e.target.closest("button");if(!button||button.textContent.trim()!=="ویرایش")return;const card=button.closest(".study-card"),id=Number(card?.dataset.studyId);if(!id)return;setTimeout(async()=>{const c=document.getElementById("editStudyDentalChart");if(c&&window.DentalRayDentalChart)window.DentalRayDentalChart.render(c,await loadStudyTeeth(id));},0);},true);}
    function loadDentalChart(){installStudyDentalStyles();const ready=()=>{injectDentalCharts();enhanceStudyAccordions();wireFormCharts();};if(window.DentalRayDentalChart){ready();return;}const script=document.createElement("script");script.src="/js/dental-chart.js";script.onload=ready;script.onerror=()=>console.error("DentalRay: dental-chart.js could not be loaded.");document.body.appendChild(script);}
    const container=document.getElementById("studiesContainer");if(container){const observer=new MutationObserver(()=>{addDeleteButtonsToStudyCards();enhanceStudyAccordions();});observer.observe(container,{childList:true,subtree:true});addDeleteButtonsToStudyCards();}
    loadDentalChart();
})();