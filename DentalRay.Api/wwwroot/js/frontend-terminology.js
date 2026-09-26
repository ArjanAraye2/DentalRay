// DentalRay frontend terminology policy.
//
// واژهٔ این برنامه برای «کارهای ثبت‌شده برای یک بیمار» (چه تصویر داشته باشد
// چه نداشته باشد) از این به بعد «مراجعه» است؛ قبلاً Study / مطالعه به کار
// می‌رفت که در فارسی معنای «خواندن و درس خواندن» می‌داد.
//
// این فایل همهٔ متن‌های دیده‌شده در صفحه را در لحظهٔ رندر جایگزین می‌کند،
// پس نیازی نیست صدها رشته در کد عوض شوند و خطر شکستن کد هم نیست.
// فقط متن‌های داخل <script> و <style> دست نمی‌خورند و کدها دست‌نخورده می‌مانند.
(function(){
'use strict';
const replacements=[
  // ---- واژهٔ اصلی ----
  ['Studyها و تصاویر','مراجعات و تصاویر'],
  ['Studyهای','مراجعات'],
  ['Studyها','مراجعات'],
  ['Study شمارهٔ','مراجعهٔ شمارهٔ'],
  ['Study شماره','مراجعهٔ شماره'],
  ['این Study','این مراجعه'],
  ['همین Study','همین مراجعه'],
  ['Study','مراجعه'],
  ['مطالعه‌ها','مراجعات'],
  ['مطالعه ها','مراجعات'],
  ['مطالعات','مراجعات'],

  // ---- «نوع» در مراجعه یعنی دلیل مراجعه (عصب‌کشی، روکش، ...)؛
  //      «نوع تصویر» (OPG، CBCT) چیز دیگری است و دست نمی‌خورد. ----
  ['انواع مطالعه','دلایل مراجعه'],
  ['نوع مطالعه','دلیل مراجعه'],

  ['مطالعه','مراجعه'],

  // ---- اضافهٔ (ه‌ای) در عبارت‌های پرکاربرد ----
  ['مراجعه جدید','مراجعهٔ جدید'],
  ['مراجعه ثبت نشده','مراجعهٔ ثبت نشده'],
  ['مراجعه پیدا نشد','مراجعه‌ای پیدا نشد'],
  ['مراجعه انجام نشد','مراجعه انجام نشد'],

  // ---- متون قدیمی که با «رادیولوژی» ساخته شده بودند ----
  ['ثبت رادیولوژی جدید','مراجعهٔ جدید'],
  ['ثبت رادیولوژی','ثبت مراجعه'],
  ['ویرایش رادیولوژی','ویرایش مراجعه'],
  ['نوع رادیولوژی','دلیل مراجعه'],
  ['تاریخ رادیولوژی','تاریخ مراجعه'],
  ['تعداد رادیولوژی','تعداد مراجعه'],
  ['رادیولوژی‌ها و تصاویر','مراجعات و تصاویر'],
  ['رادیولوژی‌ها','مراجعات'],
  ['رادیولوژی ثبت نشده است','مراجعه ثبت نشده است'],
  ['رادیولوژی پیدا نشد','مراجعه پیدا نشد'],
  ['رادیولوژی انجام نشد','مراجعه انجام نشد'],
  ['رادیولوژی بیماران','مراجعات بیماران']
];
function replaceText(text){let result=text;for(const [from,to] of replacements)result=result.split(from).join(to);return result;}
function apply(root=document.body){if(!root)return;const walker=document.createTreeWalker(root,NodeFilter.SHOW_TEXT);const nodes=[];while(walker.nextNode())nodes.push(walker.currentNode);for(const node of nodes){if(node.parentElement?.closest('script,style'))continue;const next=replaceText(node.nodeValue);if(next!==node.nodeValue)node.nodeValue=next;}for(const el of root.querySelectorAll?.('[placeholder],[title],[aria-label]')||[]){for(const attr of ['placeholder','title','aria-label'])if(el.hasAttribute(attr))el.setAttribute(attr,replaceText(el.getAttribute(attr)));}}
// به‌جای requestAnimationFrame که در پنجرهٔ غیرفعال هرگز اجرا نمی‌شود و صف را
// برای همیشه قفل می‌کند، از setTimeout استفاده می‌کنیم تا تغییرات همیشه اعمال شوند.
let queued=false;const observer=new MutationObserver(()=>{if(queued)return;queued=true;setTimeout(()=>{try{apply();}finally{queued=false;}},0);});
// قلّاب تست: برای بررسی اینکه چرا تغییرات بعدی اعمال نمی‌شوند
window.DentalRayTerminology = { apply, replaceText, observer };
observer.observe(document.body,{childList:true,subtree:true,characterData:true});
apply();
// Load the mobile-camera hardening after the main app has created its handlers.
if(!document.getElementById('dentalrayMobileCamera')){const s=document.createElement('script');s.id='dentalrayMobileCamera';s.src='/js/mobile-camera.js';document.body.appendChild(s);}
})();
