// Handwritten legacy-card extraction and radiology analysis.
// Nothing extracted or analysed is ever written to the patient record by itself:
// every result is a review-only draft, and analysis is stored only to avoid sending
// the same picture out of the clinic twice.
(() => {
 "use strict";
 const esc=value=>{const d=document.createElement("div");d.textContent=value??"";return d.innerHTML;};
 const text=value=>value===null||value===undefined||value===""?"—":String(value);
 const row=(label,value)=>`<div class="card-extraction-field"><span>${esc(label)}</span><strong>${esc(text(value))}</strong></div>`;
 const dentalType=name=>/(cbct|opg|پانور|پری[‌ -]?اپیکال|بایت|اکلوز|سفال|داخل دهان|دندان)/i.test(String(name||""));
 function close(){document.getElementById("cardExtractionModal")?.remove();}

 // حریم خصوصی: تصویرِ بیمار بدونِ تأییدِ صریحِ کاربر به سرویسِ خارجی نمی‌رود.
 // تیکِ «دیگر نپرس» فقط برای همین نشستِ مرورگر معتبر می‌ماند.
 let consentGiven=false;
 try{consentGiven=sessionStorage.getItem("dentix-ai-consent")==="1";}catch{}
 function askConsent(){
  if(consentGiven)return Promise.resolve(true);
  return new Promise(resolve=>{
   const box=document.createElement("div");
   box.className="card-extraction-overlay";
   box.innerHTML=`<div class="card-extraction-dialog" role="dialog" aria-modal="true">
    <header><div><h3>ارسال تصویر به هوش مصنوعی</h3><span>تأیید شما لازم است</span></div><button type="button" class="card-extraction-close">×</button></header>
    <div class="card-extraction-body">
     <p class="card-extraction-warning">برای این کار، تصویر از این رایانه به سرورِ خارجی <strong>OpenRouter</strong> ارسال می‌شود و <strong>تصویرِ بیمار از مطب خارج خواهد شد.</strong></p>
     <section><h4>این یعنی چه؟</h4>
      <p style="margin:4px 0">• تصویر به سرویسی خارج از کشور می‌رود و ممکن است توسط همان سرویس دیده شود یا برای بهبودش استفاده گردد.</p>
      <p style="margin:4px 0">• نتیجه فقط <strong>پیش‌نویس</strong> است و جایگزین تشخیص دندانپزشک نیست.</p>
      <p style="margin:4px 0">• تا وقتی تأیید نکنید، هیچ تصویری ارسال نمی‌شود.</p>
     </section>
     <label class="ai-consent-more" style="display:flex;gap:8px;align-items:center;margin-top:12px;font-size:13px;cursor:pointer"><input type="checkbox" /> <span>در این نشست دیگر نپرس</span></label>
    </div>
    <footer><button type="button" class="secondary-button ai-consent-no">انصراف</button><button type="button" class="card-extraction-done ai-consent-yes">بله، ارسال شود</button></footer>
   </div>`;
   document.body.appendChild(box);
   const onKey=e=>{if(e.key==="Escape")done(false);};
   function done(ok){
    const checked=box.querySelector("input[type=checkbox]")?.checked;
    if(ok&&checked){consentGiven=true;try{sessionStorage.setItem("dentix-ai-consent","1");}catch{}}
    box.remove();document.removeEventListener("keydown",onKey);resolve(ok);
   }
   box.querySelector(".card-extraction-close").onclick=()=>done(false);
   box.querySelector(".ai-consent-no").onclick=()=>done(false);
   box.querySelector(".ai-consent-yes").onclick=()=>done(true);
   box.addEventListener("click",e=>{if(e.target===box)done(false);});
   document.addEventListener("keydown",onKey);
  });
 }

 function render(modal,data){
  const s=data.study||{},uncertain=data.uncertainFields||[];
  modal.querySelector(".card-extraction-body").innerHTML=`
   <p class="card-extraction-warning">اطلاعات زیر توسط هوش مصنوعی خوانده شده است. قبل از ثبت حتماً با کارت اصلی تطبیق دهید.</p>
   <section><h4>اطلاعات پیشنهادی مطالعه</h4><div class="card-extraction-grid">${row("تاریخ مطالعه",s.studyDate)}${row("دلیل مراجعه",s.studyType)}${row("ناحیه",s.bodyPart)}${row("توضیحات",s.description)}${row("گزارش",s.report)}</div></section>
   <section><h4>متن کامل خوانده‌شده</h4><pre>${esc(text(data.rawText))}</pre></section>
   ${uncertain.length?`<div class="card-extraction-uncertain"><strong>موارد نیازمند بررسی:</strong> ${esc(uncertain.join("، "))}</div>`:""}`;
  modal.querySelector(".card-extraction-copy").onclick=async()=>{await navigator.clipboard.writeText(data.rawText||"");};
 }
 async function open(image){
  if(!await askConsent())return;
  close();
  const modal=document.createElement("div");modal.id="cardExtractionModal";modal.className="card-extraction-overlay";
  modal.innerHTML='<div class="card-extraction-dialog" role="dialog" aria-modal="true"><header><div><h3>استخراج اطلاعات از کارت</h3><span>پیش‌نویس قابل بازبینی</span></div><button type="button" class="card-extraction-close">×</button></header><div class="card-extraction-body"><p>در حال خواندن نوشته‌های کارت...</p></div><footer><button type="button" class="secondary-button card-extraction-copy" disabled>کپی متن</button><button type="button" class="card-extraction-done">بستن</button></footer></div>';
  document.body.appendChild(modal);modal.querySelector(".card-extraction-close").onclick=close;modal.querySelector(".card-extraction-done").onclick=close;modal.addEventListener("click",e=>{if(e.target===modal)close();});
  try{const r=await fetch(`/api/ai/images/${image.imageID}/extract-card`,{method:"POST"});let x={};try{x=await r.json();}catch{}if(!r.ok||x.success===false)throw new Error(x.message||"استخراج اطلاعات انجام نشد.");render(modal,x.extraction||{});modal.querySelector(".card-extraction-copy").disabled=false;}
  catch(e){modal.querySelector(".card-extraction-body").innerHTML=`<p class="error">${esc(e.message||"استخراج اطلاعات انجام نشد.")}</p>`;}
 }

 // از کجا آمده؟ ذخیره‌شده یا تازه — و اگر با مدلِ فعلی فرق دارد، هشدار داده می‌شود.
 function sourceLine(meta){
  if(!meta)return "";
  const when=meta.analyzedAt?new Date(meta.analyzedAt).toLocaleString("fa-IR"):"—";
  const head=`<p style="margin:6px 0;font-size:12px;color:#4a6b78">`;
  if(meta.cached){
   const stale=meta.stale
    ?` <strong style="color:#b45309">قدیمی است: با مدلِ «${esc(meta.model||"نامشخص")}» ذخیره شده و مدلِ فعلی فرق دارد — «تحلیل دوباره» بزنید.</strong>`
    :"";
   return `${head}📄 تحلیلِ <strong>ذخیره‌شده</strong> — ${esc(when)} · مدل: ${esc(meta.model||"—")}${stale}</p>`;
  }
  return `${head}✨ تحلیلِ <strong>تازه</strong> — ${esc(when)} (ذخیره شد تا دوباره ارسال نشود)</p>`;
 }

 function renderRadiology(modal,data,meta){
  const teeth=data.problemTeeth||[],general=data.generalFindings||"";
  modal.querySelector(".card-extraction-body").innerHTML=`${sourceLine(meta)}<p class="card-extraction-warning">این تحلیل جایگزین نظر و تشخیص دندانپزشک نیست.</p>${general?`<section><h4>یافته‌های کلی</h4><pre>${esc(general)}</pre></section>`:""}<section><h4>دندان‌های نیازمند بررسی</h4>${teeth.length?teeth.map(t=>`<div class="radiology-finding"><strong>دندان ${esc(t.toothNumber??"؟")}</strong>${(t.findings||[]).map(x=>`<p>یافته: ${esc(x)}</p>`).join("")}${(t.previousWork||[]).map(x=>`<p>درمان قبلی: ${esc(x)}</p>`).join("")}${(t.dentistReview||[]).map(x=>`<p>بررسی دندانپزشک: ${esc(x)}</p>`).join("")}<small>اطمینان: ${esc(t.confidence||"نامشخص")}</small></div>`).join(""):"<p>مورد مشخصی گزارش نشد.</p>"}</section>`;
  modal.querySelector(".card-extraction-copy").onclick=async()=>navigator.clipboard.writeText(modal.querySelector(".card-extraction-body").innerText);
 }

 // ارسالِ تصویر فقط با تأییدِ سروری انجام می‌شود: سرور اگر ذخیره‌ای نداشته باشد
 // و consent نیامده باشد، درخواست را با needsConsent برمی‌گرداند و ما تأیید می‌گیریم.
 async function analyze(image,opts){
  opts=opts||{};
  close();
  const call=async qs=>{const r=await fetch(`/api/ai/images/${image.imageID}/analyze-radiology?${qs}`,{method:"POST"});let x={};try{x=await r.json();}catch{}return{r,x};};
  const makeModal=()=>{
   const modal=document.createElement("div");modal.id="cardExtractionModal";modal.className="card-extraction-overlay";
   modal.innerHTML='<div class="card-extraction-dialog" role="dialog" aria-modal="true"><header><div><h3>تحلیل رادیولوژی</h3><span>تحلیل همان تصویر انتخاب‌شده</span></div><button type="button" class="card-extraction-close">×</button></header><div class="card-extraction-body"><p>در حال تحلیل تصویر رادیولوژی...</p></div><footer><button type="button" class="secondary-button card-extraction-reanalyze">تحلیل دوباره</button><button type="button" class="secondary-button card-extraction-copy" disabled>کپی تحلیل</button><button type="button" class="card-extraction-done">بستن</button></footer></div>';
   document.body.appendChild(modal);
   modal.querySelector(".card-extraction-close").onclick=close;
   modal.querySelector(".card-extraction-done").onclick=close;
   modal.querySelector(".card-extraction-reanalyze").onclick=()=>analyze(image,{force:true});
   modal.addEventListener("click",e=>{if(e.target===modal)close();});
   return modal;
  };
  let modal=makeModal();
  try{
   let {r,x}=await call(opts.force?"force=1":"");
   if(r.status===400&&x&&x.needsConsent){
    modal.remove();
    if(!await askConsent())return;
    modal=makeModal();
    ({r,x}=await call((opts.force?"force=1&":"")+"consent=1"));
   }
   if(!r.ok||x.success===false)throw new Error(x.message||"تحلیل رادیولوژی انجام نشد.");
   renderRadiology(modal,x.analysis||{},x);
   modal.querySelector(".card-extraction-copy").disabled=false;
  }catch(e){modal.querySelector(".card-extraction-body").innerHTML=`<p class="error">${esc(e.message||"تحلیل رادیولوژی انجام نشد.")}</p>`;}
 }

 window.DentalRayImageAI={open,analyze,isDentalType:dentalType,askConsent};
 window.DentalRayCardExtraction={open};
 // تأییدِ حریم خصوصی، مشترک بینِ همهٔ دکمه‌های AI همین صفحه
 window.DentalRayAIConsent={ask:askConsent};
})();
