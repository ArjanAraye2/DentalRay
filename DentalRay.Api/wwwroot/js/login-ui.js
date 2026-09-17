// DentalRay Login UI
// Connects the login screen to /api/auth/login. Passwords are sent only to the
// backend for verification and are never stored in browser storage.
(function () {
    'use strict';

    function createLogin() {
        if (document.getElementById('dentalRayLoginScreen')) return;

        const appHeader = document.querySelector('.main-header');
        const appMain = document.querySelector('.page-container');
        if (appHeader) appHeader.classList.add('login-app-hidden');
        if (appMain) appMain.classList.add('login-app-hidden');

        const screen = document.createElement('div');
        screen.id = 'dentalRayLoginScreen';
        screen.className = 'login-screen';
        screen.innerHTML = `
            <div class="login-shell">
                <section class="login-brand-panel">
                    <div class="login-brand-mark">DR</div>
                    <h1>DentalRay</h1>
                    <p>سامانه مدیریت پرونده و تصاویر دندانپزشکی</p>
                    <div class="login-brand-decoration" aria-hidden="true">🦷</div>
                </section>
                <section class="login-form-panel">
                    <div class="login-form-heading">
                        <h2>ورود به DentalRay</h2>
                        <p>برای ادامه، اطلاعات کاربری خود را وارد کنید.</p>
                    </div>
                    <form id="dentalRayLoginForm" autocomplete="on">
                        <div class="login-field">
                            <label for="loginUserName">نام کاربری</label>
                            <input id="loginUserName" name="username" type="text" autocomplete="username" maxlength="100" required placeholder="نام کاربری" />
                        </div>
                        <div class="login-field">
                            <label for="loginPassword">رمز عبور</label>
                            <div class="login-password-row">
                                <input id="loginPassword" name="password" type="password" autocomplete="current-password" required placeholder="رمز عبور" />
                                <button id="toggleLoginPassword" type="button" class="login-password-toggle">نمایش</button>
                            </div>
                        </div>
                        <div id="loginStatus" class="login-status" role="status"></div>
                        <button id="loginSubmit" class="login-submit" type="submit">ورود</button>
                    </form>
                    <p class="login-footer">DentalRay</p>
                </section>
            </div>`;
        document.body.prepend(screen);

        const form = document.getElementById('dentalRayLoginForm');
        const userName = document.getElementById('loginUserName');
        const password = document.getElementById('loginPassword');
        const toggle = document.getElementById('toggleLoginPassword');
        const status = document.getElementById('loginStatus');
        const submit = document.getElementById('loginSubmit');

        toggle.addEventListener('click', () => {
            const show = password.type === 'password';
            password.type = show ? 'text' : 'password';
            toggle.textContent = show ? 'پنهان' : 'نمایش';
        });

        form.addEventListener('submit', async event => {
            event.preventDefault();
            status.className = 'login-status';
            status.textContent = 'در حال بررسی اطلاعات...';
            submit.disabled = true;

            try {
                const response = await fetch('/api/auth/login', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ userName: userName.value.trim(), password: password.value })
                });
                const result = await response.json();
                if (!response.ok || !result.success)
                    throw new Error('نام کاربری یا رمز عبور صحیح نیست.');

                // Store only non-secret identity information for the current browser
                // session. The password itself is immediately discarded.
                sessionStorage.setItem('dentalRayCurrentUser', JSON.stringify(result.user));
                password.value = '';
                screen.remove();
                if (appHeader) appHeader.classList.remove('login-app-hidden');
                if (appMain) appMain.classList.remove('login-app-hidden');
            } catch (error) {
                password.value = '';
                password.focus();
                status.textContent = error.message || 'ورود انجام نشد.';
                status.className = 'login-status error';
            } finally {
                submit.disabled = false;
            }
        });

        userName.focus();
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', createLogin);
    else createLogin();
})();
