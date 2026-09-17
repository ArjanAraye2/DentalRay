// DentalRay Login UI
// Authentication is owned by the backend. The browser never stores a password
// or an authentication token; ASP.NET Core keeps the session in an HttpOnly cookie.
(function () {
    'use strict';

    const appHeader = () => document.querySelector('.main-header');
    const appMain = () => document.querySelector('.page-container');

    function hideApplication() {
        if (appHeader()) appHeader().classList.add('login-app-hidden');
        if (appMain()) appMain().classList.add('login-app-hidden');
    }

    function ensureLogoutControl(user) {
        const header = appHeader();
        if (!header) return;
        let control = document.getElementById('dentalRayUserControl');
        if (!control) {
            control = document.createElement('div');
            control.id = 'dentalRayUserControl';
            control.style.cssText = 'display:flex;align-items:center;gap:10px;margin-right:auto;';
            control.innerHTML = '<span id="dentalRayCurrentUserName" style="font-size:13px;color:#64748b"></span><button id="dentalRayLogoutButton" type="button" class="secondary-button" style="white-space:nowrap">خروج</button>';
            const headerContent = header.querySelector('.header-content') || header;
            headerContent.style.display = 'flex';
            headerContent.style.alignItems = 'center';
            headerContent.style.gap = '16px';
            headerContent.appendChild(control);
            document.getElementById('dentalRayLogoutButton').addEventListener('click', window.dentalRayLogout);
        }
        const name = document.getElementById('dentalRayCurrentUserName');
        if (name) name.textContent = user.isSuperAdmin ? 'مدیر سیستم' : `${user.firstName || ''} ${user.lastName || ''}`.trim();
        control.style.display = 'flex';
    }

    function showApplication(user) {
        window.dentalRayCurrentUser = user;
        const screen = document.getElementById('dentalRayLoginScreen');
        if (screen) screen.remove();
        if (appHeader()) appHeader().classList.remove('login-app-hidden');
        if (appMain()) appMain().classList.remove('login-app-hidden');
        ensureLogoutControl(user);
        window.dispatchEvent(new CustomEvent('dentalray-auth-changed', { detail: user }));
    }

    async function restoreSession() {
        hideApplication();
        try {
            const response = await fetch('/api/auth/me', { credentials: 'same-origin', cache: 'no-store' });
            if (!response.ok) return false;
            const result = await response.json();
            if (!result.success || !result.user) return false;
            showApplication(result.user);
            return true;
        } catch (_) { return false; }
    }

    function createLogin() {
        if (document.getElementById('dentalRayLoginScreen')) return;
        hideApplication();
        const control = document.getElementById('dentalRayUserControl');
        if (control) control.style.display = 'none';

        const screen = document.createElement('div');
        screen.id = 'dentalRayLoginScreen'; screen.className = 'login-screen';
        screen.innerHTML = `<div class="login-shell"><section class="login-brand-panel"><div class="login-brand-mark">DR</div><h1>DentalRay</h1><p>سامانه مدیریت پرونده و تصاویر دندانپزشکی</p><div class="login-brand-decoration" aria-hidden="true">🦷</div></section><section class="login-form-panel"><div class="login-form-heading"><h2>ورود به DentalRay</h2><p>برای ادامه، اطلاعات کاربری خود را وارد کنید.</p></div><form id="dentalRayLoginForm" autocomplete="on"><div class="login-field"><label for="loginUserName">نام کاربری</label><input id="loginUserName" name="username" type="text" autocomplete="username" maxlength="100" required placeholder="نام کاربری" /></div><div class="login-field"><label for="loginPassword">رمز عبور</label><div class="login-password-row"><input id="loginPassword" name="password" type="password" autocomplete="current-password" required placeholder="رمز عبور" /><button id="toggleLoginPassword" type="button" class="login-password-toggle">نمایش</button></div></div><div id="loginStatus" class="login-status" role="status"></div><button id="loginSubmit" class="login-submit" type="submit">ورود</button></form><p class="login-footer">DentalRay</p></section></div>`;
        document.body.prepend(screen);

        const form = document.getElementById('dentalRayLoginForm'); const userName = document.getElementById('loginUserName'); const password = document.getElementById('loginPassword'); const toggle = document.getElementById('toggleLoginPassword'); const status = document.getElementById('loginStatus'); const submit = document.getElementById('loginSubmit');
        toggle.addEventListener('click', () => { const show = password.type === 'password'; password.type = show ? 'text' : 'password'; toggle.textContent = show ? 'پنهان' : 'نمایش'; });
        form.addEventListener('submit', async event => {
            event.preventDefault(); status.className = 'login-status'; status.textContent = 'در حال بررسی اطلاعات...'; submit.disabled = true;
            try {
                const response = await fetch('/api/auth/login', { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ userName: userName.value.trim(), password: password.value }) });
                const result = await response.json();
                if (!response.ok || !result.success) throw new Error('نام کاربری یا رمز عبور صحیح نیست.');
                password.value = ''; showApplication(result.user);
            } catch (error) { password.value = ''; password.focus(); status.textContent = error.message || 'ورود انجام نشد.'; status.className = 'login-status error'; }
            finally { submit.disabled = false; }
        });
        userName.focus();
    }

    window.dentalRayLogout = async function () {
        try { await fetch('/api/auth/logout', { method: 'POST', credentials: 'same-origin' }); }
        finally { window.dentalRayCurrentUser = null; window.dispatchEvent(new CustomEvent('dentalray-auth-changed')); createLogin(); }
    };

    async function initialize() { const authenticated = await restoreSession(); if (!authenticated) createLogin(); }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', initialize); else initialize();
})();
