// Shared Persian (Jalali) date picker for DentalRay.
// Keeps manual typing available and adds a calendar button beside every Jalali date field.
(() => {
 const norm=v=>String(v??"").replace(/[۰-۹]/g,d=>"۰۱۲۳۴۵۶۷۸۹".indexOf(d)).replace(/[٠-٩]/g,d=>"٠١٢٣٤٥٦٧٨٩".indexOf(d));
 const parts=d=>{const p=new Intl.DateTimeFormat("en-US-u-ca-persian",{year:"numeric",month:"numeric",day:"numeric"}).formatToParts(d),g=t=>+p.find(x=>x.type===t).value;return [g("year"),g("month"),g("day")];};
 function greg(jy,jm,jd){const target=jy*10000+jm*100+jd,start=new Date(jy+621,2,1);for(let i=0;i<370;i++){const d=new Date(start);d.setDate(start.getDate()+i);const [y,m,day]=parts(d);if(y*10000+m*100+day===target)return d;}return null;}
 function enhance(input,withTime=false){if(!input||input.dataset.jalaliPicker)return;input.dataset.jalaliPicker="1";input.type="text";input.inputMode="numeric";input.maxLength=withTime?16:10;input.placeholder=withTime?"1405/06/27 14:30":"1405/06/27";
   const b=document.createElement("button");b.type="button";b.className="secondary-button";b.textContent="📅";b.title="انتخاب از تقویم شمسی";input.insertAdjacentElement("afterend",b);
   b.onclick=()=>open(input,withTime);
 }
 function open(input,withTime){document.getElementById("jalaliPickerModal")?.remove();let now=parts(new Date()),m=norm(input.value).match(/^(\d{4})\/(\d{1,2})\/(\d{1,2})/);let y=m?+m[1]:now[0],mo=m?+m[2]:now[1];
   const modal=document.createElement("div");modal.id="jalaliPickerModal";modal.className="modal-overlay";const box=document.createElement("div");box.className="modal-content";modal.appendChild(box);document.body.appendChild(modal);
   const draw=()=>{box.innerHTML="";const h=document.createElement("div");h.className="section-header";const prev=document.createElement("button"),next=document.createElement("button"),title=document.createElement("strong");prev.textContent="ماه قبل";next.textContent="ماه بعد";title.textContent=y+"/"+String(mo).padStart(2,"0");h.append(prev,title,next);box.appendChild(h);
     const weekdays=document.createElement("div");weekdays.style.display="grid";weekdays.style.gridTemplateColumns="repeat(7,1fr)";weekdays.style.gap="6px";["ش","ی","د","س","چ","پ","ج"].forEach(n=>{const x=document.createElement("strong");x.textContent=n;x.style.textAlign="center";weekdays.appendChild(x);});box.appendChild(weekdays);const grid=document.createElement("div");grid.style.display="grid";grid.style.gridTemplateColumns="repeat(7,1fr)";grid.style.gap="6px";const first=greg(y,mo,1);if(first){const offset=(first.getDay()+1)%7;for(let i=0;i<offset;i++)grid.appendChild(document.createElement("span"));}for(let d=1;d<=31;d++){const gd=greg(y,mo,d);if(!gd)break;const [yy,mm,dd]=parts(gd);if(yy!==y||mm!==mo)break;const bt=document.createElement("button");bt.type="button";bt.textContent=String(d);bt.onclick=()=>{const date=y+"/"+String(mo).padStart(2,"0")+"/"+String(d).padStart(2,"0");if(withTime){const old=norm(input.value).match(/\s(\d{1,2}:\d{2})$/);input.value=date+" "+(old?old[1]:"00:00");}else input.value=date;modal.remove();input.dispatchEvent(new Event("input",{bubbles:true}));};grid.appendChild(bt);}box.appendChild(grid);
     const close=document.createElement("button");close.type="button";close.className="secondary-button";close.textContent="انصراف";close.onclick=()=>modal.remove();box.appendChild(close);
     prev.onclick=()=>{if(--mo<1){mo=12;y--;}draw();};next.onclick=()=>{if(++mo>12){mo=1;y++;}draw();};};
   draw();modal.onclick=e=>{if(e.target===modal)modal.remove();};
 }
 window.DentalRayJalali={enhance,enhanceAll(root=document){root.querySelectorAll('[data-jalali-date]').forEach(x=>enhance(x,false));root.querySelectorAll('[data-jalali-datetime]').forEach(x=>enhance(x,true));}};
 window.DentalRayJalali.enhanceAll();new MutationObserver(()=>window.DentalRayJalali.enhanceAll()).observe(document.body,{childList:true,subtree:true});
})();