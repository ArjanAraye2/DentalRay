// Handwritten legacy-card extraction. Results are review-only and are never auto-saved.
(() => {
 "use strict";
 const esc=value=>{const d=document.createElement("div");d.textContent=value??"";return d.innerHTML;};
 const text=value=>value===null||value===undefined||value===""?"—":String(value);
 const row=(label,value)=>`<div class="card-extraction-field"><span>${esc(label)}</span><strong>${esc(text(value))}</strong></div>`;
 function close(){document.getElementById("cardExtractionModal")?.remove();}
 function render(modal,data){
  const s=data.study||{},uncertain=data.uncertainFields||[];
  modal.querySelector(".card-extraction-body").innerHTML=`
   <p class="card-extraction-warning">اطلاعات زیر توسط هوش مصنوعی خوانده شده است. قبل از ثبت حتماً با کارت اصلی تطبیق دهید.</p>
   <section><h4>اطلاعات پیشنهادی مطالعه</h4><div class="card-extraction-grid">${row("تاریخ مطالعه",s.studyDate)}${row("نوع مطالعه",s.studyType)}${row("ناحیه",s.bodyPart)}${row("توضیحات",s.description)}${row("گزارش",s.report)}</div></section>
   <section><h4>متن کامل خوانده‌شده</h4><pre>${esc(text(data.rawText))}</pre></section>
   ${uncertain.length?`<div class="card-extraction-uncertain"><strong>موارد نیازمند بررسی:</strong> ${esc(uncertain.join("، "))}</div>`:""}`;
  modal.querySelector(".card-extraction-copy").onclick=async()=>{await navigator.clipboard.writeText(data.rawText||"");};
 }
 async function open(image){
  close();const modal=document.createElement("div");modal.id="cardExtractionModal";modal.className="card-extraction-overlay";
  modal.innerHTML='<div class="card-extraction-dialog" role="dialog" aria-modal="true"><header><div><h3>استخراج اطلاعات از کارت</h3><span>پیش‌نویس قابل بازبینی</span></div><button type="button" class="card-extraction-close">×</button></header><div class="card-extraction-body"><p>در حال خواندن نوشته‌های کارت...</p></div><footer><button type="button" class="secondary-button card-extraction-copy" disabled>کپی متن</button><button type="button" class="card-extraction-done">بستن</button></footer></div>';
  document.body.appendChild(modal);modal.querySelector(".card-extraction-close").onclick=close;modal.querySelector(".card-extraction-done").onclick=close;modal.addEventListener("click",e=>{if(e.target===modal)close();});
  try{const r=await fetch(`/api/ai/images/${image.imageID}/extract-card`,{method:"POST"});let x={};try{x=await r.json();}catch{}if(!r.ok||x.success===false)throw new Error(x.message||"استخراج اطلاعات انجام نشد.");render(modal,x.extraction||{});modal.querySelector(".card-extraction-copy").disabled=false;}
  catch(e){modal.querySelector(".card-extraction-body").innerHTML=`<p class="error">${esc(e.message||"استخراج اطلاعات انجام نشد.")}</p>`;}
 }
 window.DentalRayCardExtraction={open};
})();
