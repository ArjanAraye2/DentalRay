// DentalRay - mobile camera workflow hardening.
// Keeps camera capture separate from ordinary file selection and requires
// an explicit preview confirmation before the form may upload the photo.
(function(){
'use strict';
let confirmed=false;
function el(id){return document.getElementById(id);}
function init(){
 const form=el('uploadImageForm'), gallery=el('imageFileInput'), camera=el('cameraFileInput');
 const preview=el('cameraPreviewPanel'), image=el('cameraPreviewImage');
 const confirm=el('confirmCameraButton'), retake=el('retakeCameraButton'), status=el('uploadImageStatus');
 if(!form||!gallery||!camera||!confirm||!retake)return;
 // Camera is an alternative to the normal file input, therefore the gallery
 // input itself must not be HTML-required.
 gallery.required=false;
 camera.setAttribute('accept','image/jpeg,image/png,image/*');
 camera.setAttribute('capture','environment');
 camera.addEventListener('change',()=>{confirmed=false;if(camera.files?.length)gallery.value='';},true);
 gallery.addEventListener('change',()=>{if(gallery.files?.length){confirmed=false;camera.value='';preview?.classList.add('hidden');}},true);
 confirm.addEventListener('click',()=>{
   if(!camera.files?.length)return;
   confirmed=true;
   if(status){status.textContent='عکس تأیید شد و آماده ذخیره و اتصال به Study است.';status.classList.remove('error');}
 },true);
 retake.addEventListener('click',e=>{
   // Stop app.js from opening the ordinary file picker; reopen the camera.
   e.preventDefault();e.stopImmediatePropagation();confirmed=false;camera.value='';preview?.classList.add('hidden');camera.click();
 },true);
 form.addEventListener('submit',e=>{
   const hasCamera=!!camera.files?.length,hasGallery=!!gallery.files?.length;
   if(!hasCamera&&!hasGallery){e.preventDefault();e.stopImmediatePropagation();if(status){status.textContent='ابتدا یک فایل انتخاب کنید یا با دوربین عکس بگیرید.';status.classList.add('error');}return;}
   if(hasCamera&&!confirmed){e.preventDefault();e.stopImmediatePropagation();if(status){status.textContent='ابتدا پیش‌نمایش عکس را بررسی و «تأیید تصویر» را انتخاب کنید.';status.classList.add('error');}return;}
 },true);
 // On supported mobile browsers capture=environment asks for the rear camera.
 // The browser/OS may still present its own camera/file chooser by design.
}
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',init);else init();
})();