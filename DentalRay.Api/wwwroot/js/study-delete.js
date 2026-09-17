// ============================================================
// DentalRay Frontend - Study Delete + Dental Chart bootstrap
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
            const button = document.createElement("button");
            button.type = "button";
            button.className = "danger-button study-delete-button";
            button.textContent = "حذف Study";
            button.addEventListener("click", e => { e.stopPropagation(); deleteStudyFromFrontend(studyID); });
            buttons.appendChild(button);
        });
    }

    async function getDeletePreview(studyID) {
        const response = await fetch(`/api/radiologystudies/${studyID}/delete-preview`);
        const result = await response.json();
        if (!response.ok || !result.success) throw new Error(getApiError(result, "اطلاعات لازم برای حذف Study دریافت نشد."));
        return result;
    }

    async function chooseStudyOnlyImages(images) {
        if (!images || images.length === 0) return [];
        const names = images.map(x => `${x.imageID}: ${x.fileName}`).join("\n");
        const answer = window.prompt("این تصاویر فقط به همین Study متصل هستند:\n\n" + names + "\n\nبرای نگهداری همه، کادر را خالی بگذارید.\nبرای حذف همه، all را وارد کنید.\nبرای حذف انتخابی، ImageIDها را با کاما جدا کنید.");
        if (answer === null || answer.trim() === "") return [];
        if (answer.trim().toLowerCase() === "all") return images.map(x => x.imageID);
        const allowed = new Set(images.map(x => Number(x.imageID)));
        return [...new Set(answer.split(",").map(x => Number(x.trim())).filter(x => Number.isInteger(x) && allowed.has(x)))];
    }

    async function deleteStudyFromFrontend(studyID) {
        try {
            const preview = await getDeletePreview(studyID);
            const sharedCount = preview.sharedImages?.length || 0;
            const studyOnly = preview.studyOnlyImages || [];
            let message = "آیا از حذف این Study مطمئن هستید؟";
            if (sharedCount > 0) message += `\n\n${sharedCount} تصویر مشترک حذف نخواهد شد و برای Studyهای دیگر باقی می‌ماند.`;
            if (studyOnly.length > 0) message += `\n\n${studyOnly.length} تصویر فقط به همین Study متصل است.`;
            const confirmed = await askConfirmation({ title: "حذف Study", message, confirmText: "ادامه حذف", danger: true });
            if (!confirmed) return;
            const deleteIDs = await chooseStudyOnlyImages(studyOnly);
            const query = deleteIDs.map(id => `deleteImageIDs=${encodeURIComponent(id)}`).join("&");
            const response = await fetch(`/api/radiologystudies/${studyID}${query ? `?${query}` : ""}`, { method: "DELETE" });
            const result = await response.json();
            if (!response.ok || !result.success) throw new Error(getApiError(result, "حذف Study انجام نشد."));
            showToast("Study با موفقیت حذف شد.", "success");
            if (selectedPatientID) await openPatient(selectedPatientID);
        } catch (error) { console.error(error); showToast(error.message || "حذف Study انجام نشد.", "error"); }
    }

    function installStudyDentalStyles() {
        if (document.getElementById("studyDentalStylesheet")) return;
        const link = document.createElement("link"); link.id="studyDentalStylesheet"; link.rel="stylesheet"; link.href="/css/study-dental.css"; document.head.appendChild(link);
    }

    function createDentalChartField(containerId) {
        const panel=document.createElement("div"); panel.className="study-dental-panel";
        const chart=document.createElement("div"); chart.id=containerId;
        const hint=document.createElement("small"); hint.className="field-hint"; hint.textContent="دندان‌های مربوط به این Study را انتخاب کنید.";
        panel.append(chart,hint); return panel;
    }

    function injectDentalCharts() {
        if (!window.DentalRayDentalChart) return;
        const newGrid=document.querySelector("#newStudyForm .form-grid");
        if(newGrid&&!document.getElementById("newStudyDentalChart")){const f=createDentalChartField("newStudyDentalChart");newGrid.appendChild(f);window.DentalRayDentalChart.render(f.querySelector("#newStudyDentalChart"),[]);}
        const editGrid=document.querySelector("#editStudyForm .form-grid");
        if(editGrid&&!document.getElementById("editStudyDentalChart")){const f=createDentalChartField("editStudyDentalChart");editGrid.appendChild(f);window.DentalRayDentalChart.render(f.querySelector("#editStudyDentalChart"),[]);}
    }

    // Convert every Study on the Patient screen to a closed accordion.
    // The original card contents remain intact inside the expandable body.
    function enhanceStudyAccordions() {
        document.querySelectorAll(".study-card").forEach(card => {
            if(card.dataset.accordionReady==="1") return;
            const header=card.querySelector(".study-card-header");
            if(!header) return;
            const title=header.querySelector(".study-title");
            const meta=card.querySelector(".study-meta");
            if(meta){
                const dateLine=Array.from(meta.children).find(x => (x.querySelector("strong")?.textContent||"").includes("تاریخ"));
                const dateText=dateLine?.querySelector("span")?.textContent;
                if(title&&dateText) title.textContent=dateText;
            }
            const body=document.createElement("div"); body.className="study-accordion-body hidden";
            Array.from(card.children).filter(x=>x!==header).forEach(x=>body.appendChild(x));
            const chart=document.createElement("div"); chart.className="study-readonly-odontogram";
            body.insertBefore(chart,body.firstChild);
            card.appendChild(body);
            const arrow=document.createElement("span"); arrow.className="study-accordion-arrow"; arrow.textContent="⌄"; header.insertBefore(arrow,header.firstChild);
            header.classList.add("study-accordion-header");
            header.addEventListener("click",e=>{
                if(e.target.closest("button")) return;
                const opening=body.classList.contains("hidden"); body.classList.toggle("hidden",!opening); card.classList.toggle("study-open",opening); arrow.textContent=opening?"⌃":"⌄";
                if(opening&&window.DentalRayDentalChart&&!chart.dataset.rendered){window.DentalRayDentalChart.render(chart,[]);chart.dataset.rendered="1";}
            });
            card.dataset.accordionReady="1";
        });
    }

    function loadDentalChart() {
        installStudyDentalStyles();
        if(window.DentalRayDentalChart){injectDentalCharts();enhanceStudyAccordions();return;}
        const script=document.createElement("script"); script.src="/js/dental-chart.js";
        script.onload=()=>{injectDentalCharts();enhanceStudyAccordions();};
        script.onerror=()=>console.error("DentalRay: dental-chart.js could not be loaded."); document.body.appendChild(script);
    }

    const container=document.getElementById("studiesContainer");
    if(container){const observer=new MutationObserver(()=>{addDeleteButtonsToStudyCards();enhanceStudyAccordions();});observer.observe(container,{childList:true,subtree:true});addDeleteButtonsToStudyCards();}
    loadDentalChart();
})();