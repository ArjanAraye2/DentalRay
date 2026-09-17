// DentalRay - mobile camera workflow.
// On mobile, choosing "Use Photo" means the user has accepted the camera shot.
// DentalRay therefore previews it briefly and immediately uploads/attaches it
// to the currently selected Study. Retake remains available only before save
// if the browser keeps the capture UI open.
(function(){
'use strict';
let autoSaving=false;
function el(id){return document.getElementById(id);}
function setStatus(status,text,isError=false){if(!status)return;status.textContent=text;status.classList.toggle('error',isError);}
function init(){
 const form=el('uploadImageForm'), gallery=el('imageFileInput'), camera=el('cameraFileInput');
 const preview=el('cameraPreviewPanel'), image=el('cameraPreviewImage');
 const confirm=el('confirmCameraButton'), retake=el('retakeCameraButton'), status=el('uploadImageStatus');
 if(!form||!gallery||!camera)return;
 gallery.required=false;
 camera.setAttribute('accept','image/jpeg,image/png,image/*');
 camera.setAttribute('capture','environment');

 // A normal file selection still uses the existing Save button.
 gallery.addEventListener('change',()=>{if(gallery.files?.length){camera.value='';preview?.classList.add('hidden');}},true);

 // Returning from the native camera after "Use Photo" fires change. At that
 // point the photo is already accepted by the user, so save it immediately.
 camera.addEventListener('change',async()=>{
   const file=camera.files?.[0];
   if(!file||autoSaving)return;
   gallery.value='';
   if(image){try{image.src=URL.createObjectURL(file);}catch{} }
   preview?.classList.remove('hidden');
   setStatus(status,'عکس دریافت شد؛ در حال ذخیره و اتصال به Study...',false);
   autoSaving=true;
   try{
     // app.js keeps the selected camera File in pendingCameraFile and uploadImage
     // performs the existing validated API upload and returns to the patient/Study.
     await new Promise(resolve=>setTimeout(resolve,0));
     if(typeof uploadImage!=='function')throw new Error('تابع ذخیره تصویر در دسترس نیست.');
     await uploadImage();
   }catch(error){
     console.error(error);
     setStatus(status,error?.message||'ذخیره عکس انجام نشد.',true);
   }finally{autoSaving=false;}
 },false);

 // Keep these controls for desktop/browser fallbacks, but they are no longer
 // required after the native mobile camera has returned a photo.
 confirm?.addEventListener('click',()=>{if(camera.files?.length&&!autoSaving)uploadImage();},true);
 retake?.addEventListener('click',e=>{e.preventDefault();e.stopImmediatePropagation();camera.value='';preview?.classList.add('hidden');camera.click();},true);
}
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',init);else init();
})();