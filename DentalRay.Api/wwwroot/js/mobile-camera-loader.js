// DentalRay - mobile-only camera UI and mobile terminology.
(function(){
'use strict';

function isMobileDevice(){
    // Prefer UA Client Hints when available, with a conservative touch/screen fallback.
    if(navigator.userAgentData && typeof navigator.userAgentData.mobile==='boolean')return navigator.userAgentData.mobile;
    return /Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent) ||
        (navigator.maxTouchPoints>1 && Math.min(screen.width,screen.height)<=1024);
}

function applyMobileUi(){
    const mobile=isMobileDevice();
    const camera= document.getElementById('cameraFileInput');
    const cameraField=camera?.closest('.form-field');
    // Desktop has no camera capture; hide that field with a class instead of inline styles.
    if(cameraField)cameraField.classList.toggle('hidden',!mobile);

    // On mobile the action describes both supported sources clearly.
    document.querySelectorAll('button').forEach(button=>{
        if(button.textContent.trim()==='افزودن فایل'){
            button.textContent=mobile?'افزودن تصویر/فایل':'افزودن فایل';
        }
    });
}

function loadCameraWorkflow(){
    if(!isMobileDevice() || document.getElementById('dentalrayMobileCamera'))return;
    const script=document.createElement('script');
    script.id='dentalrayMobileCamera';
    script.src='/js/mobile-camera.js';
    script.onerror=()=>console.error('DentalRay: mobile-camera.js could not be loaded.');
    document.body.appendChild(script);
}

function init(){applyMobileUi();loadCameraWorkflow();
    // Study cards are rendered dynamically, so re-apply wording after DOM changes.
    const container=document.getElementById('studiesContainer');
    if(container)new MutationObserver(applyMobileUi).observe(container,{childList:true,subtree:true});
}
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',init);else init();
})();