// Input language hints and numeric normalization for all current and dynamically-created forms.
// Browsers cannot switch the Windows keyboard layout programmatically. lang/dir/inputmode
// provide the correct language/keyboard hint, especially on mobile/touch devices.
(() => {
 const numericRx=/(nationalcode|mobile|phone|tel|serial|count|amount|price|number|date|time|year|month|day)/i;
 const persianRx=/(firstname|lastname|address|description|report|bodypart|name|title|specialty|clinic)/i;
 const toLatin=v=>String(v??"").replace(/[۰-۹]/g,d=>"۰۱۲۳۴۵۶۷۸۹".indexOf(d)).replace(/[٠-٩]/g,d=>"٠١٢٣٤٥٦٧٨٩".indexOf(d));
 function apply(root=document){
  root.querySelectorAll("input,textarea").forEach(el=>{
   if(el.type==="file"||el.type==="checkbox"||el.type==="radio"||el.type==="password"||el.type==="hidden")return;
   const key=(el.id||"")+" "+(el.name||"")+" "+(el.getAttribute("inputmode")||"");
   const numeric=el.hasAttribute("data-jalali-date")||el.hasAttribute("data-jalali-datetime")||numericRx.test(key)||el.type==="number";
   if(numeric){el.lang="en";el.dir="ltr";if(el.inputMode!=="tel")el.inputMode=el.hasAttribute("data-jalali-datetime")?"text":"numeric";if(!el.dataset.latinDigits){el.dataset.latinDigits="1";el.addEventListener("input",()=>{const p=el.selectionStart,v=el.value,n=toLatin(v);if(v!==n){el.value=n;try{el.setSelectionRange(p,p);}catch{}}});}}
   else if(persianRx.test(key)||el.tagName==="TEXTAREA"){el.lang="fa";el.dir="rtl";el.setAttribute("inputmode","text");el.setAttribute("spellcheck","true");}
  });
 }
 window.DentalRayInputLanguage={apply};const run=()=>apply();if(document.readyState==="loading")document.addEventListener("DOMContentLoaded",run);else run();const observer=new MutationObserver(mutations=>{for(const m of mutations){for(const node of m.addedNodes){if(node.nodeType!==1)continue;if(node.matches?.("input,textarea"))apply(node.parentElement||document);else if(node.querySelector?.("input,textarea"))apply(node);}}});observer.observe(document.documentElement,{childList:true,subtree:true});
})();