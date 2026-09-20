// ============================================================
// DentalRay - Runtime AI Study Analysis UI
// ============================================================
// This module intentionally stores NOTHING in the DentalRay database.
// The backend AI endpoint is called only when the dentist requests analysis.
// Results live only in this page's DOM and disappear on refresh/reload.
(function(){
'use strict';

function esc(value){const d=document.createElement('div');d.textContent=value??'';return d.innerHTML;}
function renderTooth(tooth){
 const number=tooth.toothNumber??tooth.fdi??'?';
 const findings=(tooth.findings||[]).map(x=>`<li>${esc(x)}</li>`).join('');
 const previous=(tooth.previousWork||[]).map(x=>`<li>${esc(x)}</li>`).join('');
 const review=(tooth.dentistReview||tooth.suggestedReview||[]).map(x=>`<li>${esc(x)}</li>`).join('');
 const confidence=tooth.confidence==null?'':`<p><strong>میزان اطمینان AI:</strong> ${esc(tooth.confidence)}</p>`;
 return `<details class="ai-tooth-panel"><summary>دندان ${esc(number)}</summary><div class="ai-tooth-body">${findings?`<h5>یافته‌ها</h5><ul>${findings}</ul>`:''}${previous?`<h5>کارهای قبلی قابل مشاهده</h5><ul>${previous}</ul>`:''}${review?`<h5>موارد پیشنهادی برای بررسی دندانپزشک</h5><ul>${review}</ul>`:''}${confidence}</div></details>`;
}
function renderResult(host,result){
 const teeth=result.problemTeeth||result.teeth||[];
 const general=result.generalFindings||result.summary||'';
 host.innerHTML=`<div class="ai-analysis-result"><h4>توضیحات هوش مصنوعی</h4><p class="field-hint">این تحلیل Runtime است، در Database ذخیره نمی‌شود و جایگزین تشخیص دندانپزشک نیست.</p>${general?`<div class="ai-general-findings">${esc(general)}</div>`:''}${teeth.length?teeth.map(renderTooth).join(''):'<p>AI مورد قابل تفکیکی برای دندان‌ها گزارش نکرد.</p>'}</div>`;
}
async function analyze(card,button){
 const studyID=Number(card.dataset.studyId);if(!studyID)return;
 let host=card.querySelector('.ai-study-analysis');
 // The Study body is now ".study-scroll-body" and exists as soon as the row is
 // built, so the result has a home whether or not the row has been opened yet.
 if(!host){host=document.createElement('div');host.className='ai-study-analysis';(card.querySelector('.study-scroll-body')||card).appendChild(host);}
 button.disabled=true;host.innerHTML='<p>در حال تحلیل همه تصاویر این Study با هوش مصنوعی...</p>';
 try{
   const response=await fetch(`/api/ai/studies/${studyID}/analyze`,{method:'POST'});
   const result=await response.json();
   if(!response.ok||result.success===false)throw new Error(result.message||'تحلیل هوش مصنوعی انجام نشد.');
   renderResult(host,result.analysis||result);
 }catch(error){host.innerHTML=`<p class="error">${esc(error.message||'تحلیل هوش مصنوعی انجام نشد.')}</p>`;}
 finally{button.disabled=false;}
}
// Attach to the current collapsible row markup. The actions live in the header, so
// the button is visible without opening the Study, and the click must not toggle it.
function enhance(){document.querySelectorAll('.study-scroll-card').forEach(card=>{if(card.querySelector('.ai-analyze-button'))return;const actions=card.querySelector('.study-scroll-actions');if(!actions)return;const button=document.createElement('button');button.type='button';button.className='secondary-button ai-analyze-button';button.textContent='تحلیل با هوش مصنوعی';button.addEventListener('click',e=>{e.stopPropagation();analyze(card,button);});actions.appendChild(button);});}
const container=document.getElementById('studiesContainer');if(container){new MutationObserver(enhance).observe(container,{childList:true,subtree:true});enhance();}
})();