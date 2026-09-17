// DentalRay - ensures the mobile camera workflow is active on every frontend load.
(function(){
'use strict';
if(document.getElementById('dentalrayMobileCamera'))return;
const script=document.createElement('script');
script.id='dentalrayMobileCamera';
script.src='/js/mobile-camera.js';
script.onerror=()=>console.error('DentalRay: mobile-camera.js could not be loaded.');
document.body.appendChild(script);
})();