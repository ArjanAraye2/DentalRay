// ============================================================
// Dentix Frontend - Study Delete + Dental Chart integration
// ============================================================
//
// The Study rows are now built by app.js as collapsible ".study-scroll-card"
// elements with the actions in ".study-scroll-actions". This file used to attach to
// the old ".study-card" markup, so the delete button silently stopped appearing and
// the accordion enhancer re-wrapped rows that were already collapsible.
(function () {
    "use strict";

    function addDeleteButtonsToStudyCards() {
        document.querySelectorAll(".study-scroll-card").forEach(card => {
            if (card.querySelector(".study-delete-button")) return;
            const studyID = Number(card.dataset.studyId);
            if (!Number.isInteger(studyID) || studyID <= 0) return;
            const buttons = card.querySelector(".study-scroll-actions");
            if (!buttons) return;
            const button = document.createElement("button");
            button.type = "button";
            button.className = "danger-button study-delete-button";
            button.textContent = "حذف Study";
            // The whole header toggles the Study, so the delete click must not bubble.
            button.addEventListener("click", e => { e.stopPropagation(); deleteStudyFromFrontend(studyID); });
            buttons.appendChild(button);
        });
    }

    async function getDeletePreview(studyID){const response=await fetch(`/api/radiologystudies/${studyID}/delete-preview`);const result=await response.json();if(!response.ok||!result.success)throw new Error(getApiError(result,"اطلاعات لازم برای حذف Study دریافت نشد."));return result;}
    async function chooseStudyOnlyImages(images){if(!images||!images.length)return[];const names=images.map(x=>`${x.imageID}: ${x.fileName}`).join("\n");const answer=window.prompt("این تصاویر فقط به همین Study متصل هستند:\n\n"+names+"\n\nبرای نگهداری همه، کادر را خالی بگذارید.\nبرای حذف همه، all را وارد کنید.\nبرای حذف انتخابی، ImageIDها را با کاما جدا کنید.");if(answer===null||answer.trim()==="")return[];if(answer.trim().toLowerCase()==="all")return images.map(x=>x.imageID);const allowed=new Set(images.map(x=>Number(x.imageID)));return[...new Set(answer.split(",").map(x=>Number(x.trim())).filter(x=>Number.isInteger(x)&&allowed.has(x)))];}
    async function deleteStudyFromFrontend(studyID){try{const preview=await getDeletePreview(studyID),studyOnly=preview.studyOnlyImages||[];let message="آیا از حذف این Study مطمئن هستید؟";if(preview.sharedImages?.length)message+=`\n\n${preview.sharedImages.length} تصویر مشترک برای Studyهای دیگر باقی می‌ماند.`;const confirmed=await askConfirmation({title:"حذف Study",message,confirmText:"ادامه حذف",danger:true});if(!confirmed)return;const deleteIDs=await chooseStudyOnlyImages(studyOnly),query=deleteIDs.map(id=>`deleteImageIDs=${encodeURIComponent(id)}`).join("&");const response=await fetch(`/api/radiologystudies/${studyID}${query?`?${query}`:""}`,{method:"DELETE"}),result=await response.json();if(!response.ok||!result.success)throw new Error(getApiError(result,"حذف Study انجام نشد."));showToast("Study با موفقیت حذف شد.","success");if(selectedPatientID)await openPatient(selectedPatientID);}catch(error){console.error(error);showToast(error.message||"حذف Study انجام نشد.","error");}}

    function installStudyDentalStyles(){if(document.getElementById("studyDentalStylesheet"))return;const link=document.createElement("link");link.id="studyDentalStylesheet";link.rel="stylesheet";link.href="/css/study-dental.css";document.head.appendChild(link);}

    // The tooth picker for a new Study. app.js reads "newStudyDentalChart" when it
    // builds the request (studyPayload), so the field only has to exist in the form.
    function createDentalChartField(containerId){const panel=document.createElement("div");panel.className="study-dental-panel";const chart=document.createElement("div");chart.id=containerId;const hint=document.createElement("small");hint.className="field-hint";hint.textContent="دندان‌های مربوط به این Study را انتخاب کنید.";panel.append(chart,hint);return panel;}
    function injectDentalCharts(){if(!window.DentalRayDentalChart)return;const newGrid=document.querySelector("#newStudyForm .form-grid");if(newGrid&&!document.getElementById("newStudyDentalChart")){const f=createDentalChartField("newStudyDentalChart");newGrid.appendChild(f);window.DentalRayDentalChart.render(f.querySelector("#newStudyDentalChart"),[]);}}

    function loadDentalChart(){installStudyDentalStyles();const ready=()=>{injectDentalCharts();};if(window.DentalRayDentalChart){ready();return;}const script=document.createElement("script");script.src="/js/dental-chart.js?v=20260921.5";script.onload=ready;script.onerror=()=>console.error("Dentix: dental-chart.js could not be loaded.");document.body.appendChild(script);}

    const container=document.getElementById("studiesContainer");
    if(container){
        // app.js rebuilds the rows on every patient load, so watch for new ones.
        new MutationObserver(addDeleteButtonsToStudyCards).observe(container,{childList:true,subtree:true});
        addDeleteButtonsToStudyCards();
    }
    loadDentalChart();
})();
