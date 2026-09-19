// DentalRay Login UI — approved reference recreation
(function () {
  'use strict';

  const appHeader=()=>document.querySelector('.main-header');
  const appMain=()=>document.querySelector('.page-container');
  const appSidebar=()=>document.querySelector('.app-sidebar');

  const toothSvg=(stroke='#159bb6')=>'<svg viewBox="0 0 48 56" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true"><path d="M13.3 4.8C8.2 5.7 4.9 10 5.4 15.1c.4 4.4 3 7.3 4.8 10.8 1.9 3.6 1.9 10.6 2.9 16.5.6 3.7 2 6.2 4.7 6.2 3.3 0 3.9-5.2 4.4-9.6.5-4.2 1.2-7.1 2.8-7.1s2.3 2.9 2.8 7.1c.5 4.4 1.1 9.6 4.4 9.6 2.7 0 4.1-2.5 4.7-6.2 1-5.9 1-12.9 2.9-16.5 1.8-3.5 4.4-6.4 4.8-10.8.5-5.1-2.8-9.4-7.9-10.3-3.5-.6-6.2.8-8.1 2.2-1.4 1-2.5 1-3.9 0-1.9-1.4-4.6-2.8-8.1-2.2Z" fill="#fff" stroke="'+stroke+'" stroke-width="2.2"/></svg>';

  const iconSvg=type=>{
    if(type==='id') return '<svg viewBox="0 0 24 24" fill="none"><rect x="5" y="3.5" width="14" height="17" rx="2.5" stroke="currentColor" stroke-width="1.7"/><circle cx="12" cy="9" r="2.2" stroke="currentColor" stroke-width="1.6"/><path d="M8.5 16c1.1-2 5.9-2 7 0" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"/></svg>';
    if(type==='lock') return '<svg viewBox="0 0 24 24" fill="none"><rect x="5" y="10" width="14" height="10" rx="2.2" stroke="currentColor" stroke-width="1.7"/><path d="M8 10V7a4 4 0 0 1 8 0v3" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/></svg>';
    if(type==='eye') return '<svg viewBox="0 0 24 24" fill="none"><path d="M2.5 12s3.4-5 9.5-5 9.5 5 9.5 5-3.4 5-9.5 5-9.5-5-9.5-5Z" stroke="currentColor" stroke-width="1.7"/><circle cx="12" cy="12" r="2.4" stroke="currentColor" stroke-width="1.7"/></svg>';
    return '<svg viewBox="0 0 24 24" fill="none"><path d="M12 3l7 3v5c0 4.4-2.9 7.9-7 10-4.1-2.1-7-5.6-7-10V6l7-3Z" stroke="currentColor" stroke-width="1.7"/><path d="m9 12 2 2 4-4" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/></svg>';
  };

  function setLoginMode(active){
    document.body.classList.toggle('dentalray-login-active',active);
    [appHeader(),appMain(),appSidebar()].forEach(el=>el?.classList.toggle('login-app-hidden',active));
  }

  function ensureLogoutControl(user){
    const header=appHeader(); if(!header)return;
    let control=document.getElementById('dentalRayUserControl');
    if(!control){
      control=document.createElement('div');
      control.id='dentalRayUserControl';
      control.style.cssText='display:flex;align-items:center;gap:10px;margin-right:auto;';
      control.innerHTML='<span id="dentalRayCurrentUserName" style="font-size:13px;color:#64748b"></span><button id="dentalRayLogoutButton" type="button" class="secondary-button" style="white-space:nowrap">خروج</button>';
      const headerContent=header.querySelector('.header-content')||header;
      headerContent.style.display='flex';headerContent.style.alignItems='center';headerContent.style.gap='16px';headerContent.appendChild(control);
      document.getElementById('dentalRayLogoutButton').addEventListener('click',window.dentalRayLogout);
    }
    const name=document.getElementById('dentalRayCurrentUserName');
    if(name)name.textContent=user.isSuperAdmin?'مدیر سیستم':`${user.firstName||''} ${user.lastName||''}`.trim();
    control.style.display='flex';
  }

  function showApplication(user){
    window.dentalRayCurrentUser=user;
    document.getElementById('dentalRayLoginScreen')?.remove();
    setLoginMode(false);
    ensureLogoutControl(user);
    window.dispatchEvent(new CustomEvent('dentalray-auth-changed',{detail:user}));
  }

  async function restoreSession(){
    setLoginMode(true);
    try{
      const r=await fetch('/api/auth/me',{credentials:'same-origin',cache:'no-store'});
      if(!r.ok)return false;
      const d=await r.json();
      if(!d.success||!d.user)return false;
      showApplication(d.user); return true;
    }catch(_){return false;}
  }

  function createLogin(){
    if(document.getElementById('dentalRayLoginScreen'))return;
    setLoginMode(true);
    const control=document.getElementById('dentalRayUserControl'); if(control)control.style.display='none';

    const screen=document.createElement('div');
    screen.id='dentalRayLoginScreen';
    screen.className='login-screen';
    screen.innerHTML=`
      <div class="login-shell">
        <section class="login-form-panel" aria-label="فرم ورود">
          <div class="login-form-logo">
            <span class="login-logo-tooth">${toothSvg()}</span>
            <span class="login-logo-name"><strong>Dental<span style="color:#1ba7c0">Ray</span></strong><small>Dental Imaging System</small></span>
          </div>
          <div class="login-form-heading">
            <h2>ورود به DentalRay</h2>
            <p>برای ورود، اطلاعات حساب خود را وارد کنید.</p>
          </div>
          <form id="dentalRayLoginForm" autocomplete="on">
            <div class="login-field">
              <label for="loginUserName">کد ملی</label>
              <span class="login-field-icon">${iconSvg('id')}</span>
              <input id="loginUserName" name="username" type="text" inputmode="numeric" autocomplete="username" maxlength="10" pattern="[0-9]{10}" required placeholder="کد ملی خود را وارد کنید" />
            </div>
            <div class="login-field">
              <label for="loginPassword">رمز عبور</label>
              <div class="login-password-row">
                <span class="login-field-icon">${iconSvg('lock')}</span>
                <input id="loginPassword" name="password" type="password" autocomplete="current-password" required placeholder="رمز عبور خود را وارد کنید" />
                <button id="toggleLoginPassword" type="button" class="login-password-toggle" aria-label="نمایش رمز عبور">${iconSvg('eye')}</button>
              </div>
            </div>
            <div id="loginStatus" class="login-status" role="status"></div>
            <button id="loginSubmit" class="login-submit" type="submit"><span>ورود</span><span class="login-submit-arrow">←</span></button>
          </form>
          <button id="forgotPasswordButton" type="button" class="login-recovery-link"><span class="login-recovery-help">؟</span><span>رمز عبور را فراموش کرده‌اید؟</span></button>
          <p class="login-footer"><span class="login-shield">${iconSvg('shield')}</span>ورود شما به معنای پذیرش قوانین و مقررات DentalRay است.</p>
        </section>

        <section class="login-brand-panel" aria-label="DentalRay">
          <div class="login-brand-top">
            <span class="login-logo-tooth">${toothSvg()}</span>
            <span class="login-logo-name"><strong>Dental<span style="color:#1ba7c0">Ray</span></strong><small>Dental Imaging System</small></span>
          </div>
          <div class="login-brand-copy">
            <h1>تصویر واضح‌تر، تصمیم‌های بهتر</h1>
            <p>مدیریت هوشمند تصاویر و پرونده‌های دندانپزشکی</p>
          </div>
          <img class="login-clinic-art" src="/images/login-reference-right.jpg?v=20260920.1" alt="محیط تصویربرداری دندانپزشکی" />
          <div class="login-brand-footer">
            <span class="login-logo-name"><strong>Dental<span style="color:#1ba7c0">Ray</span></strong><small>Dental Imaging System</small></span>
            <span class="login-logo-tooth">${toothSvg()}</span>
          </div>
        </section>
      </div>`;

    document.body.prepend(screen);

    const form=document.getElementById('dentalRayLoginForm');
    const userName=document.getElementById('loginUserName');
    const password=document.getElementById('loginPassword');
    const toggle=document.getElementById('toggleLoginPassword');
    const status=document.getElementById('loginStatus');
    const submit=document.getElementById('loginSubmit');

    userName.addEventListener('input',()=>userName.value=userName.value.replace(/\D/g,'').slice(0,10));
    toggle.onclick=()=>{
      const show=password.type==='password';
      password.type=show?'text':'password';
      toggle.innerHTML=iconSvg('eye');
      toggle.setAttribute('aria-label',show?'پنهان کردن رمز عبور':'نمایش رمز عبور');
    };

    form.onsubmit=async e=>{
      e.preventDefault();
      if(submit.disabled)return;
      status.className='login-status info';
      status.textContent='در حال ورود...';
      submit.disabled=true;
      try{
        const r=await fetch('/api/auth/login',{method:'POST',credentials:'same-origin',headers:{'Content-Type':'application/json'},body:JSON.stringify({userName:userName.value.trim(),password:password.value})});
        const d=await r.json();
        if(!r.ok||!d.success)throw new Error(d?.message||'کد ملی یا رمز عبور صحیح نیست.');
        password.value=''; showApplication(d.user);
      }catch(err){
        password.value=''; password.focus();
        status.textContent=err.message||'ورود انجام نشد.';
        status.className='login-status error';
      }finally{submit.disabled=false;}
    };
    document.getElementById('forgotPasswordButton').onclick=()=>showRecovery(screen,userName.value.trim());
    userName.focus();
  }

  function showRecovery(screen,nationalCode){
    const panel=screen.querySelector('.login-form-panel');
    panel.innerHTML=`
      <div class="login-form-logo"><span class="login-logo-tooth">${toothSvg()}</span><span class="login-logo-name"><strong>Dental<span style="color:#1ba7c0">Ray</span></strong><small>Dental Imaging System</small></span></div>
      <div class="login-form-heading"><h2>بازیابی رمز عبور</h2><p>کد ملی خود را وارد کنید. اگر شماره بازیابی معتبر ثبت شده باشد، کد برای شما ارسال می‌شود.</p></div>
      <form id="recoveryRequestForm">
        <div class="login-field"><label for="recoveryNationalCode">کد ملی</label><span class="login-field-icon">${iconSvg('id')}</span><input id="recoveryNationalCode" type="text" inputmode="numeric" maxlength="10" value="${String(nationalCode).replace(/"/g,'&quot;')}" required /></div>
        <div id="recoveryStatus" class="login-status"></div>
        <button class="login-submit" type="submit"><span>ارسال کد بازیابی</span></button>
      </form>
      <button id="backToLogin" type="button" class="login-recovery-link">بازگشت به ورود</button>
      <p class="login-footer"><span class="login-shield">${iconSvg('shield')}</span>ورود شما به معنای پذیرش قوانین و مقررات DentalRay است.</p>`;
    const input=document.getElementById('recoveryNationalCode');
    input.addEventListener('input',()=>input.value=input.value.replace(/\D/g,'').slice(0,10));
    document.getElementById('backToLogin').onclick=()=>{screen.remove();createLogin();};
    document.getElementById('recoveryRequestForm').onsubmit=async e=>{
      e.preventDefault();
      const status=document.getElementById('recoveryStatus');
      status.className='login-status info'; status.textContent='در حال ارسال...';
      try{
        const r=await fetch('/api/auth/forgot-password',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({nationalCode:input.value.trim()})});
        const d=await r.json(); status.textContent=d.message||'درخواست ثبت شد.';
        setTimeout(()=>showVerify(screen,input.value.trim()),700);
      }catch(_){status.textContent='درخواست انجام نشد. دوباره امتحان کنید.';status.className='login-status error';}
    };
  }

  function showVerify(screen,nationalCode){
    const panel=screen.querySelector('.login-form-panel');
    panel.innerHTML=`
      <div class="login-form-logo"><span class="login-logo-tooth">${toothSvg()}</span><span class="login-logo-name"><strong>Dental<span style="color:#1ba7c0">Ray</span></strong><small>Dental Imaging System</small></span></div>
      <div class="login-form-heading"><h2>تأیید بازیابی</h2><p>کد ۶ رقمی ارسال‌شده را وارد کنید و سپس رمز جدید را تعیین کنید.</p></div>
      <form id="verifyRecoveryForm">
        <div class="login-field"><label for="recoveryCode">کد تأیید</label><input id="recoveryCode" type="text" inputmode="numeric" maxlength="6" required /></div>
        <div class="login-field"><label for="newRecoveryPassword">رمز عبور جدید</label><input id="newRecoveryPassword" type="password" minlength="8" required /></div>
        <div class="login-field"><label for="newRecoveryPassword2">تکرار رمز عبور</label><input id="newRecoveryPassword2" type="password" minlength="8" required /></div>
        <div id="recoveryVerifyStatus" class="login-status"></div>
        <button class="login-submit" type="submit">تغییر رمز عبور</button>
      </form>`;
    document.getElementById('recoveryCode').addEventListener('input',e=>e.target.value=e.target.value.replace(/\D/g,'').slice(0,6));
    document.getElementById('verifyRecoveryForm').onsubmit=async e=>{
      e.preventDefault();
      const status=document.getElementById('recoveryVerifyStatus');
      const p1=document.getElementById('newRecoveryPassword').value,p2=document.getElementById('newRecoveryPassword2').value;
      if(p1!==p2){status.textContent='دو رمز عبور یکسان نیستند.';status.className='login-status error';return;}
      const r=await fetch('/api/auth/reset-password',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({nationalCode,code:document.getElementById('recoveryCode').value,newPassword:p1})});
      const d=await r.json();
      if(!r.ok||!d.success){status.textContent=d.message||'کد معتبر نیست.';status.className='login-status error';return;}
      status.textContent='رمز عبور تغییر کرد. در حال بازگشت به ورود...';status.className='login-status info';
      setTimeout(()=>{screen.remove();createLogin();},1000);
    };
    document.getElementById('recoveryCode').focus();
  }

  window.dentalRayLogout=async function(){
    try{await fetch('/api/auth/logout',{method:'POST',credentials:'same-origin'});}
    finally{window.dentalRayCurrentUser=null;window.dispatchEvent(new CustomEvent('dentalray-auth-changed'));createLogin();}
  };

  function loadCommunicationUi(){
    if(document.getElementById('communicationUiScript'))return;
    const s=document.createElement('script');s.id='communicationUiScript';s.src='/js/communication-ui.js';document.body.appendChild(s);
  }

  async function initialize(){
    setLoginMode(true);
    const authenticated=await restoreSession();
    if(!authenticated)createLogin();
    else loadCommunicationUi();
  }

  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',initialize);else initialize();
})();