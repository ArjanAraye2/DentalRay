// DentalRay Login UI
// Frontend-only first step. Authentication will be connected after the User
// credential fields and backend login policy are finalized.
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
                            <input id="loginUserName" name="username" type="text"
                                   autocomplete="username" maxlength="100" required
                                   placeholder="نام کاربری" />
                        </div>

                        <div class="login-field">
                            <label for="loginPassword">رمز عبور</label>
                            <div class="login-password-row">
                                <input id="loginPassword" name="password" type="password"
                                       autocomplete="current-password" required
                                       placeholder="رمز عبور" />
                                <button id="toggleLoginPassword" type="button"
                                        class="login-password-toggle" aria-label="نمایش رمز عبور">نمایش</button>
                            </div>
                        </div>

                        <div id="loginStatus" class="login-status" role="status"></div>
                        <button class="login-submit" type="submit">ورود</button>
                    </form>

                    <p class="login-footer">DentalRay</p>
                </section>
            </div>`;

        document.body.prepend(screen);

        const form = document.getElementById('dentalRayLoginForm');
        const password = document.getElementById('loginPassword');
        const toggle = document.getElementById('toggleLoginPassword');
        const status = document.getElementById('loginStatus');

        toggle.addEventListener('click', () => {
            const show = password.type === 'password';
            password.type = show ? 'text' : 'password';
            toggle.textContent = show ? 'پنهان' : 'نمایش';
        });

        form.addEventListener('submit', event => {
            event.preventDefault();
            // Do not simulate a successful login. The real authentication endpoint
            // will be connected after password storage/security is implemented.
            status.textContent = 'فرم ورود آماده است؛ اتصال امن به Backend در مرحله بعد انجام می‌شود.';
            status.classList.add('info');
        });
    }

    if (document.readyState === 'loading')
        document.addEventListener('DOMContentLoaded', createLogin);
    else
        createLogin();
})();
