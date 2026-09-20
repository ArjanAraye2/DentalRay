(() => {
    const addStyles = () => {
        if (document.getElementById("communicationUiStyles")) return;
        const style = document.createElement("style");
        style.id = "communicationUiStyles";
        style.textContent = `
            .communication-channel-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px;margin-top:16px}
            .communication-channel-card{padding:16px;border:1px solid #e2e8f0;border-radius:12px;background:#fbfdff}
            .communication-channel-card strong{display:block;margin-bottom:5px}.communication-channel-card span{color:#64748b;font-size:13px}
            .communication-settings-actions{display:flex;gap:8px;flex-wrap:wrap;margin-top:18px}
            @media(max-width:720px){.communication-channel-grid{grid-template-columns:1fr}}
        `;
        document.head.appendChild(style);
    };
    function init() {
        addStyles();
        const nav = document.querySelector(".sidebar-nav");
        if (!nav || nav.querySelector('[data-nav="communications"]')) return;
        const link = document.createElement("a");
        link.href="#"; link.className="sidebar-link"; link.dataset.nav="communications";
        link.innerHTML="<span>✉</span>ارتباطات";
        nav.insertBefore(link, nav.querySelector('[data-nav="settings"]'));

        const main = document.querySelector(".page-container");
        const section=document.createElement("section");
        section.id="communicationsSection"; section.className="card hidden shell-page";
        section.innerHTML=`
          <div class="section-header"><div><h2>ارتباطات با بیماران</h2><p>مدیریت پیامک و کانال‌های ارتباطی Dentix</p></div></div>
          <div class="communication-channel-grid">
            <div class="communication-channel-card"><strong>SMS</strong><span id="commSmsStatus">در حال دریافت وضعیت...</span></div>
            <div class="communication-channel-card"><strong>Push Notification</strong><span id="commPushStatus">آماده برای دستگاه‌های ثبت‌شده</span></div>
            <div class="communication-channel-card"><strong>Email</strong><span id="commEmailStatus">اختیاری</span></div>
            <div class="communication-channel-card"><strong>WhatsApp / Telegram</strong><span>اختیاری و وابسته به دسترسی سرویس</span></div>
          </div>
          <div class="section-header" style="margin-top:28px"><div><h3>تنظیمات پیامک</h3><p>کاوه‌نگار به‌صورت پیش‌فرض انتخاب شده و ارائه‌دهنده بعداً قابل تغییر است.</p></div></div>
          <div class="form-grid">
            <div class="form-field"><label>ارائه‌دهنده</label><input id="commSmsProvider" value="Kavenegar" /></div>
            <div class="form-field"><label>API URL</label><input id="commSmsApiUrl" /></div>
            <div class="form-field"><label>API Key</label><input id="commSmsApiKey" type="password" autocomplete="off" /></div>
            <div class="form-field"><label>شماره / خط ارسال</label><input id="commSmsSender" /></div>
            <div class="form-field"><label>نام الگوی OTP (اختیاری)</label><input id="commSmsOtpTemplate" /></div>
          </div>
          <div class="checkbox-row"><input id="commSmsEnabled" type="checkbox"><span>ارسال پیامک فعال باشد</span></div>
          <div class="communication-settings-actions"><button id="commSaveButton" type="button">ذخیره تنظیمات</button><button id="commTestButton" type="button" class="secondary-button">تست ارسال</button></div>
          <div id="commStatus" class="status-message"></div>
          <div class="section-header" style="margin-top:28px"><div><h3>شماره بازیابی رمز کارکنان</h3><p>برای هر کاربر می‌توان یک شماره تأییدشده برای بازیابی رمز ثبت کرد.</p></div></div>
          <div class="form-grid">
            <div class="form-field"><label>کد ملی کاربر</label><input id="commRecoveryNationalCode" maxlength="10" inputmode="numeric"></div>
            <div class="form-field"><label>موبایل بازیابی</label><input id="commRecoveryMobile" maxlength="30" inputmode="tel"></div>
          </div>
          <button id="commRecoverySave" type="button">ذخیره شماره بازیابی</button>
          <div id="commRecoveryStatus" class="status-message"></div>`;
        main.appendChild(section);

        async function loadSettings(){
            const r=await fetch("/api/communications/settings"); if(!r.ok){section.querySelectorAll("input,button").forEach(x=>x.disabled=true);return;}
            const d=await r.json(), s=d.settings||{};
            document.getElementById("commSmsProvider").value=s.smsProvider||"Kavenegar";
            document.getElementById("commSmsApiUrl").value=s.smsApiUrl||"https://api.kavenegar.com/v1";
            document.getElementById("commSmsApiKey").value=s.smsApiKey||"";
            document.getElementById("commSmsSender").value=s.smsSender||"";
            document.getElementById("commSmsOtpTemplate").value=s.smsOtpTemplate||"";
            document.getElementById("commSmsEnabled").checked=!!s.smsEnabled;
            document.getElementById("commSmsStatus").textContent=s.smsEnabled?"فعال":"غیرفعال";
        }
        async function saveSettings(){
            const body={smsProvider:document.getElementById("commSmsProvider").value,smsApiUrl:document.getElementById("commSmsApiUrl").value,smsApiKey:document.getElementById("commSmsApiKey").value,smsSender:document.getElementById("commSmsSender").value,smsOtpTemplate:document.getElementById("commSmsOtpTemplate").value,smsEnabled:document.getElementById("commSmsEnabled").checked,pushEnabled:true,emailEnabled:false,whatsAppEnabled:false,telegramEnabled:false};
            const r=await fetch("/api/communications/settings",{method:"PUT",headers:{"Content-Type":"application/json"},body:JSON.stringify(body)});
            const d=await r.json(); document.getElementById("commStatus").textContent=d.message|| (r.ok?"تنظیمات ذخیره شد.":"ذخیره تنظیمات ناموفق بود.");
            if(r.ok) document.getElementById("commSmsStatus").textContent=body.smsEnabled?"فعال":"غیرفعال";
        }
        document.getElementById("commSaveButton").onclick=saveSettings;
        document.getElementById("commTestButton").onclick=async()=>{const mobile=prompt("شماره موبایل تست را وارد کنید:");if(!mobile)return;const r=await fetch("/api/communications/sms/test",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({mobile,message:"تست اتصال DentalRay"})});const d=await r.json();document.getElementById("commStatus").textContent=d.message||"نتیجه تست دریافت نشد.";};
        document.getElementById("commRecoverySave").onclick=async()=>{const nationalCode=document.getElementById("commRecoveryNationalCode").value.trim(),mobile=document.getElementById("commRecoveryMobile").value.trim();const r=await fetch("/api/communications/recovery-mobile",{method:"PUT",headers:{"Content-Type":"application/json"},body:JSON.stringify({nationalCode,mobile})});const d=await r.json();document.getElementById("commRecoveryStatus").textContent=d.message||(r.ok?"شماره بازیابی ذخیره شد.":"ذخیره انجام نشد.");};
        link.addEventListener("click",e=>{e.preventDefault();document.querySelectorAll(".page-container > section").forEach(x=>x.classList.add("hidden"));section.classList.remove("hidden");document.querySelectorAll(".sidebar-link").forEach(x=>x.classList.toggle("active",x===link));loadSettings();window.scrollTo(0,0);});
    }
    if(document.readyState==="loading") document.addEventListener("DOMContentLoaded",init); else init();
})();