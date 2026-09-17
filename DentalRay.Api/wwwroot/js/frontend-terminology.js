// DentalRay frontend terminology policy: the clinical record is called Study.
(function(){
'use strict';
const replacements=[
['ثبت رادیولوژی جدید','Study جدید'],['ثبت رادیولوژی','ثبت Study'],['ویرایش رادیولوژی','ویرایش Study'],['نوع رادیولوژی','نوع Study'],['تاریخ رادیولوژی','تاریخ Study'],['تعداد رادیولوژی','تعداد Study'],['رادیولوژی‌ها','Studyها'],['رادیولوژی ثبت نشده است','Study ثبت نشده است'],['رادیولوژی پیدا نشد','Study پیدا نشد'],['رادیولوژی انجام نشد','Study انجام نشد'],['رادیولوژی بیماران','Studyهای بیماران'],['رادیولوژی‌ها و تصاویر','Studyها و تصاویر']
];
function replaceText(text){let result=text;for(const [from,to] of replacements)result=result.split(from).join(to);return result;}
function apply(root=document.body){if(!root)return;const walker=document.createTreeWalker(root,NodeFilter.SHOW_TEXT);const nodes=[];while(walker.nextNode())nodes.push(walker.currentNode);for(const node of nodes){if(node.parentElement?.closest('script,style'))continue;const next=replaceText(node.nodeValue);if(next!==node.nodeValue)node.nodeValue=next;}for(const el of root.querySelectorAll?.('[placeholder],[title],[aria-label]')||[]){for(const attr of ['placeholder','title','aria-label'])if(el.hasAttribute(attr))el.setAttribute(attr,replaceText(el.getAttribute(attr)));}}
let queued=false;const observer=new MutationObserver(()=>{if(queued)return;queued=true;requestAnimationFrame(()=>{queued=false;apply();});});
apply();observer.observe(document.body,{childList:true,subtree:true,characterData:true});
// Load the mobile-camera hardening after the main app has created its handlers.
if(!document.getElementById('dentalrayMobileCamera')){const s=document.createElement('script');s.id='dentalrayMobileCamera';s.src='/js/mobile-camera.js';document.body.appendChild(s);}
})();