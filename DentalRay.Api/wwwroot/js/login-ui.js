// DentalRay Login UI
(function () {
    'use strict';

    const appHeader = () => document.querySelector('.main-header');
    const appMain = () => document.querySelector('.page-container');
    function hideApplication(){ if(appHeader()) appHeader().classList.add('login-app-hidden'); if(appMain()) appMain().classList.add('login-app-hidden'); }

    function ensureLogoutControl(user){
        const header=appHeader(); if(!header)return;
        let control=document.getElementById('dentalRayUserControl');
        if(!control){
            control=document.createElement('div'); control.id='dentalRayUserControl';
            control.style.cssText='display:flex;align-items:center;gap:10px;margin-right:auto;';
            control.innerHTML='<span id="dentalRayCurrentUserName" style="font-size:13px;color:#64748b"></span><button id="dentalRayLogoutButton" type="button" class="secondary-button" style="white-space:nowrap">خروج</button>';
            const headerContent=header.querySelector('.header-content')||header; headerContent.style.display='flex'; headerContent.style.alignItems='center'; headerContent.style.gap='16px'; headerContent.appendChild(control);
            document.getElementById('dentalRayLogoutButton').addEventListener('click',window.dentalRayLogout);
        }
        const name=document.getElementById('dentalRayCurrentUserName');
        if(name)name.textContent=user.isSuperAdmin?'مدیر سیستم':`${user.firstName||''} ${user.lastName||''}`.trim();
        control.style.display='flex';
    }
    function showApplication(user){
        window.dentalRayCurrentUser=user; document.getElementById('dentalRayLoginScreen')?.remove();
        appHeader()?.classList.remove('login-app-hidden'); appMain()?.classList.remove('login-app-hidden');
        ensureLogoutControl(user); window.dispatchEvent(new CustomEvent('dentalray-auth-changed',{detail:user}));
    }
    async function restoreSession(){
        hideApplication();
        try{const r=await fetch('/api/auth/me',{credentials:'same-origin',cache:'no-store'});if(!r.ok)return false;const d=await r.json();if(!d.success||!d.user)return false;showApplication(d.user);return true;}catch(_){return false;}
    }

    function createLogin(){
        if(document.getElementById('dentalRayLoginScreen'))return;
        hideApplication(); const control=document.getElementById('dentalRayUserControl'); if(control)control.style.display='none';
        const screen=document.createElement('div'); screen.id='dentalRayLoginScreen'; screen.className='login-screen';
        screen.innerHTML=`<div class="login-shell"><section class="login-brand-panel"><div class="login-brand-mark">DR</div><h1>DentalRay</h1><p>سامانه مدیریت پرونده و تصاویر دندانپزشکی</p><div class="login-brand-decoration" aria-hidden="true">🦷</div></section><section class="login-form-panel"><div class="login-form-heading"><h2>ورود به DentalRay</h2><p>کد ملی و رمز عبور خود را وارد کنید.</p></div><form id="dentalRayLoginForm" autocomplete="on"><div class="login-field"><label for="loginUserName">کد ملی</label><input id="loginUserName" name="username" type="text" inputmode="numeric" autocomplete="username" maxlength="10" pattern="[0-9]{10}" required placeholder="کد ملی ۱۰ رقمی" /></div><div class="login-field"><label for="loginPassword">رمز عبور</label><div class="login-password-row"><input id="loginPassword" name="password" type="password" autocomplete="current-password" required placeholder="رمز عبور" /><button id="toggleLoginPassword" type="button" class="login-password-toggle">نمایش</button></div></div><div id="loginStatus" class="login-status" role="status"></div><button id="loginSubmit" class="login-submit" type="submit">ورود</button></form><button id="forgotPasswordButton" type="button" class="login-recovery-link">رمز عبور را فراموش کرده‌اید؟</button><p class="login-footer">DentalRay</p></section></div>`;
        document.body.prepend(screen);

        const form=document.getElementById('dentalRayLoginForm'), userName=document.getElementById('loginUserName'), password=document.getElementById('loginPassword'), toggle=document.getElementById('toggleLoginPassword'), status=document.getElementById('loginStatus'), submit=document.getElementById('loginSubmit');
        toggle.onclick=()=>{const show=password.type==='password';password.type=show?'text':'password';toggle.textContent=show?'پنهان':'نمایش';};
        form.onsubmit=async e=>{
            e.preventDefault(); status.className='login-status info'; status.textContent='در حال بررسی اطلاعات...'; submit.disabled=true;
            try{const r=await fetch('/api/auth/login',{method:'POST',credentials:'same-origin',headers:{'Content-Type':'application/json'},body:JSON.stringify({userName:userName.value.trim(),password:password.value})});const d=await r.json();if(!r.ok||!d.success)throw new Error(d?.message||'کد ملی یا رمز عبور صحیح نیست.');password.value='';showApplication(d.user);}
            catch(err){password.value='';password.focus();status.textContent=err.message||'ورود انجام نشد.';status.className='login-status error';}
            finally{submit.disabled=false;}
        };
        document.getElementById('forgotPasswordButton').onclick=()=>showRecovery(screen,userName.value.trim());
        userName.focus();
    }

    function showRecovery(screen,nationalCode){
        const panel=screen.querySelector('.login-form-panel');
        panel.innerHTML=`<div class="login-form-heading"><h2>بازیابی رمز عبور</h2><p>کد ملی خود را وارد کنید. اگر شماره بازیابی معتبر ثبت شده باشد، کد برای شما ارسال می‌شود.</p></div><form id="recoveryRequestForm"><div class="login-field"><label>کد ملی</label><input id="recoveryNationalCode" type="text" inputmode="numeric" maxlength="10" value="${nationalCode.replace(/"/g,'&quot;')}" required /></div><div id="recoveryStatus" class="login-status"></div><button class="login-submit" type="submit">ارسال کد بازیابی</button></form><button id="backToLogin" type="button" class="login-recovery-link">بازگشت به ورود</button>`;
        document.getElementById('backToLogin').onclick=()=>{screen.remove();createLogin();};
        document.getElementById('recoveryRequestForm').onsubmit=async e=>{
            e.preventDefault(); const input=document.getElementById('recoveryNationalCode'), status=document.getElementById('recoveryStatus'); status.className='login-status info'; status.textContent='در حال ارسال...';
            try{const r=await fetch('/api/auth/forgot-password',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({nationalCode:input.value.trim()})});const d=await r.json();status.textContent=d.message||'درخواست ثبت شد.';setTimeout(()=>showVerify(screen,input.value.trim()),700);}
            catch(_){status.textContent='درخواست انجام نشد. دوباره امتحان کنید.';status.className='login-status error';}
        };
    }
    function showVerify(screen,nationalCode){
        const panel=screen.querySelector('.login-form-panel');
        panel.innerHTML=`<div class="login-form-heading"><h2>تأیید بازیابی</h2><p>کد ۶ رقمی ارسال‌شده را وارد کنید و سپس رمز جدید را تعیین کنید.</p></div><form id="verifyRecoveryForm"><div class="login-field"><label>کد تأیید</label><input id="recoveryCode" type="text" inputmode="numeric" maxlength="6" required /></div><div class="login-field"><label>رمز عبور جدید</label><input id="newRecoveryPassword" type="password" minlength="8" required /></div><div class="login-field"><label>تکرار رمز عبور</label><input id="newRecoveryPassword2" type="password" minlength="8" required /></div><div id="recoveryVerifyStatus" class="login-status"></div><button class="login-submit" type="submit">تغییر رمز عبور</button></form>`;
        document.getElementById('verifyRecoveryForm').onsubmit=async e=>{
            e.preventDefault(); const status=document.getElementById('recoveryVerifyStatus'), p1=document.getElementById('newRecoveryPassword').value,p2=document.getElementById('newRecoveryPassword2').value;
            if(p1!==p2){status.textContent='دو رمز عبور یکسان نیستند.';status.className='login-status error';return;}
            const r=await fetch('/api/auth/reset-password',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({nationalCode,code:document.getElementById('recoveryCode').value,newPassword:p1})});const d=await r.json();
            if(!r.ok||!d.success){status.textContent=d.message||'کد معتبر نیست.';status.className='login-status error';return;}
            status.textContent='رمز عبور تغییر کرد. در حال بازگشت به ورود...';status.className='login-status info';setTimeout(()=>{screen.remove();createLogin();},1000);
        };
        document.getElementById('recoveryCode').focus();
    }

    window.dentalRayLogout=async function(){try{await fetch('/api/auth/logout',{method:'POST',credentials:'same-origin'});}finally{window.dentalRayCurrentUser=null;window.dispatchEvent(new CustomEvent('dentalray-auth-changed'));createLogin();}};
    function loadCommunicationUi(){if(document.getElementById('communicationUiScript'))return;const s=document.createElement('script');s.id='communicationUiScript';s.src='/js/communication-ui.js';document.body.appendChild(s);}
    async function initialize(){loadCommunicationUi();const authenticated=await restoreSession();if(!authenticated)createLogin();}
    if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',initialize);else initialize();
})();