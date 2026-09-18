// Shared Persian (Jalali) date picker for DentalRay.
(() => {
 const norm=v=>String(v??"").replace(/[۰-۹]/g,d=>"۰۱۲۳۴۵۶۷۸۹".indexOf(d)).replace(/[٠-٩]/g,d=>"٠١٢٣٤٥٦٧٨٩".indexOf(d));
 const fmt=new Intl.DateTimeFormat("en-US-u-ca-persian",{year:"numeric",month:"numeric",day:"numeric"});
 const parts=d=>{const p=fmt.formatToParts(d),g=t=>+p.find(x=>x.type===t).value;return [g("year"),g("month"),g("day")];};
 function greg(jy,jm,jd){
   // Convert Jalali to Gregorian with the arithmetic Jalaali algorithm.
   // This avoids browser Intl search-range edge cases, especially Esfand.
   const div=(a,b)=>Math.trunc(a/b),mod=(a,b)=>a-Math.trunc(a/b)*b;
   const breaks=[-61,9,38,199,426,686,756,818,1111,1181,1210,1635,2060,2097,2192,2262,2324,2394,2456,3178];
   let gy=jy+621,leapJ=-14,jp=breaks[0],jump=0;
   for(let i=1;i<breaks.length;i++){const jm2=breaks[i];jump=jm2-jp;if(jy<jm2)break;leapJ+=div(jump,33)*8+div(mod(jump,33),4);jp=jm2;}
   let n=jy-jp;leapJ+=div(n,33)*8+div(mod(n,33)+3,4);if(mod(jump,33)===4&&jump-n===4)leapJ++;
   const leapG=div(gy,4)-div((div(gy,100)+1)*3,4)-150;
   const march=20+leapJ-leapG;
   if(jump-n<6)n=n-jump+div(jump+4,33)*33;
   let leap=mod(mod(n+1,33)-1,4);if(leap===-1)leap=4;
   const g0=new Date(gy,2,march);
   const dayOfYear=(jm-1)*31-Math.max(0,jm-7)*(jm-7)+(jd-1);
   const d=new Date(g0);d.setDate(g0.getDate()+dayOfYear);return d;
 }
 function enhance(input,withTime=false){if(!input||input.dataset.jalaliPicker)return;input.dataset.jalaliPicker="1";input.type="text";input.inputMode="numeric";input.dir="ltr";input.maxLength=withTime?16:10;
   const wrap=document.createElement("div");wrap.className="jalali-input-wrap";input.parentNode.insertBefore(wrap,input);wrap.appendChild(input);
   const b=document.createElement("button");b.type="button";b.className="jalali-calendar-button";b.textContent="📅";b.title="انتخاب از تقویم شمسی";wrap.appendChild(b);
   b.addEventListener("click",e=>{e.preventDefault();e.stopPropagation();open(input,withTime);});
 }
 function open(input,withTime){document.getElementById("jalaliPickerModal")?.remove();const now=parts(new Date()),m=norm(input.value).match(/^(\d{4})\/(\d{1,2})\/(\d{1,2})/);let y=m?+m[1]:now[0],mo=m?+m[2]:now[1];
   const modal=document.createElement("div");modal.id="jalaliPickerModal";modal.className="jalali-picker-overlay";modal.innerHTML='<div class="jalali-picker-dialog" role="dialog" aria-modal="true"></div>';document.body.appendChild(modal);const box=modal.firstElementChild;
   const draw=()=>{box.innerHTML="";const head=document.createElement("div");head.className="jalali-picker-header";const prev=document.createElement("button"),next=document.createElement("button"),title=document.createElement("strong");prev.type=next.type="button";prev.textContent="‹";next.textContent="›";title.textContent=y+"/"+String(mo).padStart(2,"0");head.append(next,title,prev);box.appendChild(head);
     const names=document.createElement("div");names.className="jalali-picker-grid jalali-weekdays";["ش","ی","د","س","چ","پ","ج"].forEach(n=>{const x=document.createElement("span");x.textContent=n;names.appendChild(x);});box.appendChild(names);
     const grid=document.createElement("div");grid.className="jalali-picker-grid";grid.dir="rtl";const first=greg(y,mo,1);if(first){const saturdayFirst=(first.getDay()+1)%7;for(let i=0;i<saturdayFirst;i++)grid.appendChild(document.createElement("span"));}
     for(let d=1;d<=31;d++){const gd=greg(y,mo,d);if(!gd)break;const [yy,mm]=parts(gd);if(yy!==y||mm!==mo)break;const bt=document.createElement("button");bt.type="button";bt.textContent=String(d);bt.onclick=()=>{const date=y+"/"+String(mo).padStart(2,"0")+"/"+String(d).padStart(2,"0");input.value=withTime?date+" "+((norm(input.value).match(/\s(\d{1,2}:\d{2})$/)||[])[1]||"00:00"):date;modal.remove();input.dispatchEvent(new Event("input",{bubbles:true}));input.dispatchEvent(new Event("change",{bubbles:true}));};grid.appendChild(bt);}box.appendChild(grid);
     const foot=document.createElement("div");foot.className="jalali-picker-footer";const today=document.createElement("button"),close=document.createElement("button");today.type=close.type="button";today.textContent="امروز";close.textContent="انصراف";today.onclick=()=>{const [ty,tm,td]=parts(new Date());input.value=ty+"/"+String(tm).padStart(2,"0")+"/"+String(td).padStart(2,"0")+(withTime?" "+String(new Date().getHours()).padStart(2,"0")+":"+String(new Date().getMinutes()).padStart(2,"0"):"");modal.remove();input.dispatchEvent(new Event("input",{bubbles:true}));};close.onclick=()=>modal.remove();foot.append(today,close);box.appendChild(foot);
     prev.onclick=()=>{if(--mo<1){mo=12;y--;}draw();};next.onclick=()=>{if(++mo>12){mo=1;y++;}draw();};
   };draw();modal.addEventListener("click",e=>{if(e.target===modal)modal.remove();});
 }
 window.DentalRayJalali={enhance,enhanceAll(root=document){if(root.matches?.("[data-jalali-date]"))enhance(root,false);else if(root.matches?.("[data-jalali-datetime]"))enhance(root,true);root.querySelectorAll?.("[data-jalali-date]").forEach(x=>enhance(x,false));root.querySelectorAll?.("[data-jalali-datetime]").forEach(x=>enhance(x,true));}};
 const run=()=>window.DentalRayJalali.enhanceAll();if(document.readyState==="loading")document.addEventListener("DOMContentLoaded",run);else run();
 // Dynamic admin forms explicitly call enhanceAll after rendering. A global DOM
 // observer is intentionally avoided because enhancing a date input wraps/moves
 // that same input, which can retrigger DOM observation and freeze the UI.
})();