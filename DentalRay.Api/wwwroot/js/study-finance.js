// Dentix Study finance: multiple billable actions and multiple payments per Study.
(() => {
 "use strict";
 const money=value=>Number(value||0).toLocaleString("fa-IR",{maximumFractionDigits:2});
 const api=async(url,options)=>{const r=await fetch(url,options);let x={};try{x=await r.json();}catch{}if(!r.ok||x.success===false)throw new Error(x.message||`خطای مالی (HTTP ${r.status})`);return x;};
 const field=(placeholder,type="text")=>{const i=document.createElement("input");i.type=type;i.placeholder=placeholder;if(type==="number"){i.min="0";i.step="0.01";i.inputMode="decimal";}return i;};
 const paymentMethodSelect=()=>{const s=document.createElement("select");s.innerHTML='<option value="">نوع دریافت</option><option value="1">پوز</option><option value="2">کارت به کارت</option><option value="3">نقدی</option>';return s;};
 const paymentMethodName=value=>({1:"پوز",2:"کارت به کارت",3:"نقدی"})[Number(value)]||"تعیین نشده";
 const button=(text,className="")=>{const b=document.createElement("button");b.type="button";b.textContent=text;if(className)b.className=className;return b;};

 function createPanel(studyID){
  const root=document.createElement("section");root.className="study-finance";root.dataset.studyId=studyID;
  root.innerHTML='<div class="study-finance-title"><h5>امور مالی مطالعه</h5><span>اقدامات، تخفیف‌ها و دریافت‌ها</span></div><div class="study-finance-summary"><div><span>جمع هزینه</span><strong data-s="gross">۰</strong></div><div><span>جمع تخفیف</span><strong data-s="discount">۰</strong></div><div><span>مبلغ خالص</span><strong data-s="net">۰</strong></div><div><span>دریافتی</span><strong data-s="received">۰</strong></div><div class="finance-balance"><span>مانده</span><strong data-s="balance">۰</strong></div></div><div class="study-finance-columns"></div><div class="study-finance-status"></div>';
  const cols=root.querySelector(".study-finance-columns");
  cols.append(createActionBox(root),createPaymentBox(root));return root;
 }
 function createActionBox(root){
  const box=document.createElement("section");box.className="study-finance-box";box.innerHTML="<h6>اقدامات Study</h6>";
  const form=document.createElement("div");form.className="study-finance-form";const desc=field("شرح اقدام"),amount=field("هزینه","number"),discount=field("تخفیف","number"),add=button("+ افزودن");discount.value="0";form.append(desc,amount,discount,add);
  const list=document.createElement("div");list.className="study-finance-list";list.dataset.list="actions";box.append(form,list);
  add.onclick=()=>saveAction(root,{description:desc.value,amount:num(amount),discountAmount:num(discount)},()=>{desc.value="";amount.value="";discount.value="0";});return box;
 }
 function createPaymentBox(root){
  const box=document.createElement("section");box.className="study-finance-box";box.innerHTML="<h6>دریافت‌های Study</h6>";
  const form=document.createElement("div");form.className="study-finance-form payment";
  const date=field("تاریخ شمسی"),method=paymentMethodSelect(),amount=field("مبلغ دریافت","number"),desc=field("شرح دریافت");
  const isRefund=document.createElement("label");isRefund.className="finance-refund-toggle";
  isRefund.innerHTML='<input type="checkbox" /> <span>بازپرداخت</span>';
  const refundBox=isRefund.querySelector("input");
  const add=button("+ دریافت");
  date.dataset.jalaliDatetime="";date.value=window.toEnglishJalaliInput?.(new Date(),true)||"";
  form.append(date,method,amount,desc,isRefund,add);
  window.DentalRayJalali?.enhanceAll(date);

  // A refund reverses money, so the wording and the button change with it.
  const applyRefundMode=()=>{
    const on=refundBox.checked;
    amount.placeholder=on?"مبلغ بازپرداخت":"مبلغ دریافت";
    desc.placeholder=on?"دلیل بازپرداخت (الزامی)":"شرح دریافت";
    add.textContent=on?"− بازپرداخت":"+ دریافت";
    add.classList.toggle("danger-button",on);
    box.classList.toggle("is-refund-mode",on);
  };
  refundBox.addEventListener("change",applyRefundMode);applyRefundMode();

  const list=document.createElement("div");list.className="study-finance-list";list.dataset.list="payments";box.append(form,list);
  add.onclick=()=>{let paymentDate;try{paymentDate=window.parsePersianDateForBackend(date.value,true);}catch(e){setStatus(root,e.message,true);return;}
    savePayment(root,{paymentDate,paymentMethod:Number(method.value),amount:num(amount),description:desc.value,isRefund:refundBox.checked},
      ()=>{method.value="";amount.value="";desc.value="";refundBox.checked=false;applyRefundMode();});};
  return box;
 }
 function setStatus(root,message,error=false){const s=root.querySelector(".study-finance-status");s.textContent=message||"";s.classList.toggle("error",error);}
 // Empty input must become 0, not NaN: Number("") is 0 but Number(" ") is also 0,
 // while Number("abc") is NaN which would be sent to the API.
 const num=el=>{const v=Number(String(el.value).replace(/[^\d.\-]/g,""));return Number.isFinite(v)?v:0;};
 async function saveAction(root,data,done,id){try{if(!data.description.trim())throw new Error("شرح اقدام را وارد کنید.");setStatus(root,"در حال ذخیره...");const base=`/api/studies/${root.dataset.studyId}/finance/actions`;await api(id?`${base}/${id}`:base,{method:id?"PUT":"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(data)});done?.();await load(root);}catch(e){setStatus(root,e.message,true);}}
 async function savePayment(root,data,done,id){try{if(!(data.amount>0))throw new Error(data.isRefund?"مبلغ بازپرداخت را وارد کنید.":"مبلغ دریافت را وارد کنید.");setStatus(root,"در حال ذخیره...");const base=`/api/studies/${root.dataset.studyId}/finance/payments`;await api(id?`${base}/${id}`:base,{method:id?"PUT":"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(data)});done?.();await load(root);}catch(e){setStatus(root,e.message,true);}}
 function renderActions(root,items){const list=root.querySelector('[data-list="actions"]');list.replaceChildren();if(!items.length){list.innerHTML='<div class="study-finance-empty">اقدامی ثبت نشده است.</div>';return;}items.forEach(x=>{const row=document.createElement("div");row.className="study-finance-row";[x.description,`هزینه: ${money(x.amount)}`,`تخفیف: ${money(x.discountAmount)}`,`خالص: ${money(x.amount-x.discountAmount)}`].forEach(v=>{const s=document.createElement("span");s.textContent=v;row.appendChild(s);});const actions=document.createElement("span");actions.className="row-actions";const edit=button("ویرایش","secondary-button"),del=button("حذف","danger-button");edit.onclick=()=>{const description=prompt("شرح اقدام",x.description);if(description===null)return;const amount=prompt("مبلغ هزینه",x.amount);if(amount===null)return;const discountAmount=prompt("مبلغ تخفیف",x.discountAmount);if(discountAmount===null)return;saveAction(root,{description,amount:Number(amount)||0,discountAmount:Number(discountAmount)||0},null,x.studyActionID);};del.onclick=()=>remove(root,`actions/${x.studyActionID}`);actions.append(edit,del);row.append(actions);list.append(row);});}
 function renderPayments(root,items){
  const list=root.querySelector('[data-list="payments"]');list.replaceChildren();
  if(!items.length){list.innerHTML='<div class="study-finance-empty">دریافتی ثبت نشده است.</div>';return;}
  items.forEach(x=>{
    const isRefund=!!x.isRefund;
    const row=document.createElement("div");
    row.className="study-finance-row payment"+(isRefund?" is-refund":"");

    // Date, kind, amount and description. A refund is labelled, not shown as a
    // negative number, so the stored amount always stays positive.
    const cells=[
      window.formatPersianDateTime?.(x.paymentDate)||x.paymentDate,
      isRefund?"بازپرداخت":`نوع: ${paymentMethodName(x.paymentMethod)}`,
      `${isRefund?"−":"+"} ${money(x.amount)}`,
      isRefund?`دلیل: ${x.description||"-"}`:(x.description||"-")
    ];
    cells.forEach(v=>{const s=document.createElement("span");s.textContent=v;row.appendChild(s);});

    // POS dispatch state, so a failed send can be retried.
    if(x.posSentAt){
      const state=document.createElement("span");
      state.className="finance-pos-state "+(x.posSuccess?"ok":"err");
      state.textContent=x.posSuccess?"پوز: تأیید شد":"پوز: ناموفق";
      state.title=`${window.formatPersianDateTime?.(x.posSentAt)||x.posSentAt}${x.posMessage?" — "+x.posMessage:""}`;
      row.appendChild(state);
    }

    const actions=document.createElement("span");actions.className="row-actions";
    if(!isRefund){
      const send=button(x.posSentAt?"ارسال مجدد":"ارسال به پوز","secondary-button");
      send.onclick=()=>sendToPos(root,x,send);
      actions.appendChild(send);
    }
    const edit=button("ویرایش","secondary-button"),del=button("حذف","danger-button");
    edit.onclick=()=>openEditPayment(root,row,x);
    del.onclick=()=>remove(root,`payments/${x.studyPaymentID}`);
    actions.append(edit,del);
    row.append(actions);list.append(row);
  });
 }

 // Inline edit of an existing payment, including the refund flag.
 function openEditPayment(root,row,x){
  const form=document.createElement("div");
  form.className="study-finance-row payment edit-row"+(x.isRefund?" is-refund":"");
  const date=field("تاریخ شمسی");date.dataset.jalaliDatetime="";
  date.value=window.toEnglishJalaliInput?.(new Date(x.paymentDate),true)||"";
  const method=paymentMethodSelect();method.value=String(x.paymentMethod||1);
  const amount=field("مبلغ","number");amount.value=x.amount;
  const desc=field("شرح");desc.value=x.description||"";
  const refundLabel=document.createElement("label");refundLabel.className="finance-refund-toggle";
  refundLabel.innerHTML='<input type="checkbox" /> <span>بازپرداخت</span>';
  const refundBox=refundLabel.querySelector("input");refundBox.checked=!!x.isRefund;
  const save=button("ذخیره"),cancel=button("انصراف","secondary-button");
  form.append(date,method,amount,desc,refundLabel,save,cancel);
  row.replaceWith(form);
  window.DentalRayJalali?.enhanceAll(form);
  save.onclick=()=>{let paymentDate;try{paymentDate=window.parsePersianDateForBackend(date.value,true);}catch(e){setStatus(root,e.message,true);return;}
    savePayment(root,{paymentDate,paymentMethod:Number(method.value),amount:num(amount),description:desc.value,isRefund:refundBox.checked},null,x.studyPaymentID);};
  cancel.onclick=()=>form.replaceWith(row);
 }

 // Sends a recorded payment to the card reader. The payment is already stored,
 // so a failure here never loses the money - it is offered again as a retry.
 async function sendToPos(root,payment,buttonEl){
  const original=buttonEl.textContent;
  buttonEl.disabled=true;buttonEl.textContent="در حال ارسال...";
  setStatus(root,"در حال ارسال مبلغ به پوز...",false);
  try{
    const x=await api(`/api/studies/${root.dataset.studyId}/finance/payments/${payment.studyPaymentID}/send-to-pos`,{method:"POST"});
    setStatus(root,x.ok?`پوز «${x.posName}» مبلغ را تأیید کرد.`:`پوز پاسخ داد: ${x.message}${x.raw?" — "+x.raw:""}`,!x.ok);
    await load(root);
  }catch(e){setStatus(root,e.message,true);}
  finally{buttonEl.disabled=false;buttonEl.textContent=original;}
 }
 async function remove(root,path){if(!confirm("این رکورد مالی حذف شود؟"))return;try{await api(`/api/studies/${root.dataset.studyId}/finance/${path}`,{method:"DELETE"});await load(root);}catch(e){setStatus(root,e.message,true);}}
 async function load(root){try{setStatus(root,"در حال دریافت اطلاعات مالی...");const x=await api(`/api/studies/${root.dataset.studyId}/finance`),s=x.summary||{};root.querySelector('[data-s="gross"]').textContent=money(s.grossAmount);root.querySelector('[data-s="discount"]').textContent=money(s.discountAmount);root.querySelector('[data-s="net"]').textContent=money(s.netAmount);root.querySelector('[data-s="received"]').textContent=money(s.receivedAmount);root.querySelector('[data-s="balance"]').textContent=money(s.balanceAmount);renderActions(root,x.actions||[]);renderPayments(root,x.payments||[]);setStatus(root,"");}catch(e){setStatus(root,e.message,true);}}
 function enhance(){document.querySelectorAll(".study-scroll-card").forEach(card=>{if(card.dataset.financeReady)return;card.dataset.financeReady="1";const root=createPanel(Number(card.dataset.studyId));card.appendChild(root);load(root);});}
 const observer=new MutationObserver(enhance);const host=document.getElementById("studiesContainer");if(host)observer.observe(host,{childList:true,subtree:true});enhance();
})();
