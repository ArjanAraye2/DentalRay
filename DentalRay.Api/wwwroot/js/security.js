// ============================================================
// DentalRay Security UI
// ============================================================
//
// این فایل وظایف زیر را انجام می‌دهد:
//
// - Login / Logout
// - ساخت مدیر اولیه در اولین اجرای برنامه
// - مدیریت کاربران توسط Admin
// - نمایش و مدیریت Audit Log
//
// Backend نیز تمام APIهای اصلی را کنترل می‌کند؛ بنابراین
// مخفی‌کردن فرم‌ها به تنهایی مبنای امنیت نیست.
// ============================================================

(() => {

    "use strict";

    const nativeFetch =
        window.fetch.bind(window);

    let currentUser =
        null;

    let startApplication =
        null;

    let editingUserID =
        null;


    function byId(id) {

        return document.getElementById(id);
    }


    function escapeHtml(value) {

        return String(
            value ?? ""
        )
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#039;");
    }


    async function readJson(
        response
    ) {

        try {

            return await response.json();
        }
        catch {

            return {};
        }
    }


    async function api(
        url,
        options = {}
    ) {

        const headers = {
            ...(options.headers || {})
        };


        if (
            options.body &&
            !(options.body instanceof FormData) &&
            !headers["Content-Type"]
        ) {

            headers["Content-Type"] =
                "application/json";
        }


        const response =
            await nativeFetch(
                url,
                {
                    ...options,
                    headers
                }
            );


        const data =
            await readJson(
                response
            );


        if (
            !response.ok
        ) {

            throw new Error(
                data.message ||
                "عملیات انجام نشد."
            );
        }


        return data;
    }


    // --------------------------------------------------------
    // کنترل پایان Session برای درخواست‌های قدیمی app.js
    // --------------------------------------------------------

    window.fetch =
        async function (
            input,
            init
        ) {

            const response =
                await nativeFetch(
                    input,
                    init
                );


            const url =
                typeof input === "string"
                    ? input
                    : input.url;


            if (
                response.status === 401 &&
                !url.includes(
                    "/api/auth/"
                )
            ) {

                showLogin(
                    "نشست کاربری پایان یافته است. دوباره وارد شوید."
                );
            }


            return response;
        };


    function buildUi() {

        document.body
            .classList
            .add(
                "security-pending"
            );


        const header =
            document.querySelector(
                ".header-content"
            );


        header?.insertAdjacentHTML(
            "beforeend",
            `
            <div id="securityHeader"
                 class="security-header hidden">

                <span id="securityUserLabel">
                </span>

                <button id="securityAdminButton"
                        type="button"
                        class="secondary-button hidden">

                    مدیریت کاربران و لاگ

                </button>

                <button id="securityLogoutButton"
                        type="button"
                        class="secondary-button">

                    خروج

                </button>

            </div>
            `
        );


        document.body
            .insertAdjacentHTML(
                "beforeend",
                `
                <div id="securityLoginOverlay"
                     class="security-overlay">

                    <div class="security-login-card">

                        <div class="security-login-brand">

                            <div class="brand-mark">
                                DR
                            </div>

                            <div>

                                <h2>
                                    DentalRay
                                </h2>

                                <p id="securityLoginSubtitle">
                                    ورود به برنامه
                                </p>

                            </div>

                        </div>


                        <form id="securityLoginForm">

                            <div class="form-field">

                                <label for="securityLoginUserName">
                                    نام کاربری
                                </label>

                                <input id="securityLoginUserName"
                                       type="text"
                                       autocomplete="username"
                                       maxlength="50"
                                       required />

                            </div>


                            <div class="form-field">

                                <label for="securityLoginPassword">
                                    رمز عبور
                                </label>

                                <input id="securityLoginPassword"
                                       type="password"
                                       autocomplete="current-password"
                                       required />

                            </div>


                            <div id="securityLoginStatus"
                                 class="status-message">
                            </div>


                            <div class="form-actions">

                                <button type="submit">
                                    ورود
                                </button>

                            </div>

                        </form>


                        <form id="securityBootstrapForm"
                              class="hidden">

                            <div class="security-notice">

                                هنوز هیچ کاربری تعریف نشده است.
                                اولین حساب به عنوان مدیر سیستم ایجاد می‌شود.

                            </div>


                            <div class="form-field">

                                <label for="securityBootstrapDisplayName">
                                    نام و نام خانوادگی
                                </label>

                                <input id="securityBootstrapDisplayName"
                                       type="text"
                                       maxlength="100"
                                       required />

                            </div>


                            <div class="form-field">

                                <label for="securityBootstrapUserName">
                                    نام کاربری مدیر
                                </label>

                                <input id="securityBootstrapUserName"
                                       type="text"
                                       maxlength="50"
                                       autocomplete="username"
                                       required />

                            </div>


                            <div class="form-field">

                                <label for="securityBootstrapPassword">
                                    رمز عبور
                                </label>

                                <input id="securityBootstrapPassword"
                                       type="password"
                                       minlength="8"
                                       autocomplete="new-password"
                                       required />

                            </div>


                            <div class="form-field">

                                <label for="securityBootstrapConfirmPassword">
                                    تکرار رمز عبور
                                </label>

                                <input id="securityBootstrapConfirmPassword"
                                       type="password"
                                       minlength="8"
                                       autocomplete="new-password"
                                       required />

                            </div>


                            <div id="securityBootstrapStatus"
                                 class="status-message">
                            </div>


                            <div class="form-actions">

                                <button type="submit">
                                    ایجاد مدیر و ورود
                                </button>

                            </div>

                        </form>

                    </div>

                </div>


                <div id="securityAdminOverlay"
                     class="security-overlay hidden">

                    <div class="security-admin-card">

                        <div class="section-header">

                            <div>

                                <h2>
                                    مدیریت سیستم
                                </h2>

                                <p>
                                    کاربران و سابقه فعالیت
                                </p>

                            </div>


                            <button id="securityAdminCloseButton"
                                    type="button"
                                    class="secondary-button">

                                بستن

                            </button>

                        </div>


                        <div class="security-tabs">

                            <button id="securityUsersTabButton"
                                    type="button"
                                    class="active">

                                کاربران

                            </button>

                            <button id="securityLogsTabButton"
                                    type="button">

                                لاگ فعالیت

                            </button>

                        </div>


                        <section id="securityUsersPanel">

                            <form id="securityUserForm"
                                  class="security-user-form">

                                <div class="form-grid">

                                    <div class="form-field">

                                        <label for="securityUserName">
                                            نام کاربری
                                        </label>

                                        <input id="securityUserName"
                                               type="text"
                                               maxlength="50"
                                               required />

                                    </div>


                                    <div class="form-field">

                                        <label for="securityDisplayName">
                                            نام نمایشی
                                        </label>

                                        <input id="securityDisplayName"
                                               type="text"
                                               maxlength="100"
                                               required />

                                    </div>


                                    <div class="form-field">

                                        <label for="securityRole">
                                            نقش
                                        </label>

                                        <select id="securityRole">

                                            <option value="User">
                                                کاربر
                                            </option>

                                            <option value="Admin">
                                                مدیر
                                            </option>

                                        </select>

                                    </div>


                                    <div class="form-field">

                                        <label for="securityUserPassword">
                                            رمز عبور
                                        </label>

                                        <input id="securityUserPassword"
                                               type="password"
                                               minlength="8"
                                               autocomplete="new-password" />


                                        <small class="field-hint">

                                            برای کاربر جدید الزامی است؛
                                            هنگام ویرایش خالی بماند یعنی تغییر نکند.

                                        </small>

                                    </div>


                                    <label class="checkbox-row">

                                        <input id="securityUserIsActive"
                                               type="checkbox"
                                               checked />

                                        <span>
                                            کاربر فعال است
                                        </span>

                                    </label>

                                </div>


                                <div id="securityUserFormStatus"
                                     class="status-message">
                                </div>


                                <div class="form-actions">

                                    <button type="submit">
                                        ذخیره کاربر
                                    </button>

                                    <button id="securityUserCancelButton"
                                            type="button"
                                            class="secondary-button">

                                        کاربر جدید

                                    </button>

                                </div>

                            </form>


                            <div class="table-container">

                                <table class="patient-table">

                                    <thead>

                                        <tr>

                                            <th>
                                                نام کاربری
                                            </th>

                                            <th>
                                                نام نمایشی
                                            </th>

                                            <th>
                                                نقش
                                            </th>

                                            <th>
                                                وضعیت
                                            </th>

                                            <th>
                                                آخرین ورود
                                            </th>

                                            <th>
                                                عملیات
                                            </th>

                                        </tr>

                                    </thead>


                                    <tbody id="securityUsersBody">
                                    </tbody>

                                </table>

                            </div>

                        </section>


                        <section id="securityLogsPanel"
                                 class="hidden">

                            <div class="search-panel">

                                <div class="search-container">

                                    <input id="securityLogSearch"
                                           type="text"
                                           placeholder="کاربر، عملیات یا مسیر..." />

                                    <button id="securityLogRefreshButton"
                                            type="button">

                                        جستجو / بروزرسانی

                                    </button>

                                    <button id="securityLogCleanupButton"
                                            type="button"
                                            class="warning-button">

                                        حذف لاگ‌های قدیمی‌تر از ۹۰ روز

                                    </button>

                                </div>

                            </div>


                            <div id="securityLogStatus"
                                 class="status-message">
                            </div>


                            <div class="table-container">

                                <table class="patient-table security-log-table">

                                    <thead>

                                        <tr>

                                            <th>
                                                زمان
                                            </th>

                                            <th>
                                                کاربر
                                            </th>

                                            <th>
                                                عملیات
                                            </th>

                                            <th>
                                                وضعیت
                                            </th>

                                            <th>
                                                جزئیات
                                            </th>

                                            <th>
                                            </th>

                                        </tr>

                                    </thead>


                                    <tbody id="securityLogsBody">
                                    </tbody>

                                </table>

                            </div>

                        </section>

                    </div>

                </div>
                `
            );


        byId(
            "securityLoginForm"
        )
            .addEventListener(
                "submit",
                login
            );


        byId(
            "securityBootstrapForm"
        )
            .addEventListener(
                "submit",
                bootstrap
            );


        byId(
            "securityLogoutButton"
        )
            .addEventListener(
                "click",
                logout
            );


        byId(
            "securityAdminButton"
        )
            .addEventListener(
                "click",
                openAdmin
            );


        byId(
            "securityAdminCloseButton"
        )
            .addEventListener(
                "click",
                closeAdmin
            );


        byId(
            "securityUsersTabButton"
        )
            .addEventListener(
                "click",
                () =>
                    showAdminTab(
                        "users"
                    )
            );


        byId(
            "securityLogsTabButton"
        )
            .addEventListener(
                "click",
                () =>
                    showAdminTab(
                        "logs"
                    )
            );


        byId(
            "securityUserForm"
        )
            .addEventListener(
                "submit",
                saveUser
            );


        byId(
            "securityUserCancelButton"
        )
            .addEventListener(
                "click",
                resetUserForm
            );


        byId(
            "securityLogRefreshButton"
        )
            .addEventListener(
                "click",
                loadLogs
            );


        byId(
            "securityLogCleanupButton"
        )
            .addEventListener(
                "click",
                cleanupLogs
            );


        byId(
            "securityLogSearch"
        )
            .addEventListener(
                "keydown",
                event => {

                    if (
                        event.key === "Enter"
                    ) {

                        loadLogs();
                    }
                }
            );
    }


    function setStatus(
        element,
        message,
        error = false
    ) {

        element.textContent =
            message ||
            "";


        element
            .classList
            .toggle(
                "error",
                error
            );
    }


    function showLogin(
        message = ""
    ) {

        currentUser =
            null;


        document.body
            .classList
            .add(
                "security-pending"
            );


        byId(
            "securityHeader"
        )
            .classList
            .add(
                "hidden"
            );


        byId(
            "securityAdminOverlay"
        )
            .classList
            .add(
                "hidden"
            );


        byId(
            "securityLoginOverlay"
        )
            .classList
            .remove(
                "hidden"
            );


        byId(
            "securityBootstrapForm"
        )
            .classList
            .add(
                "hidden"
            );


        byId(
            "securityLoginForm"
        )
            .classList
            .remove(
                "hidden"
            );


        byId(
            "securityLoginSubtitle"
        )
            .textContent =
            "ورود به برنامه";


        setStatus(
            byId(
                "securityLoginStatus"
            ),
            message,
            Boolean(
                message
            )
        );


        setTimeout(
            () =>
                byId(
                    "securityLoginUserName"
                )
                    .focus(),
            0
        );
    }


    function showBootstrap() {

        document.body
            .classList
            .add(
                "security-pending"
            );


        byId(
            "securityHeader"
        )
            .classList
            .add(
                "hidden"
            );


        byId(
            "securityLoginOverlay"
        )
            .classList
            .remove(
                "hidden"
            );


        byId(
            "securityLoginForm"
        )
            .classList
            .add(
                "hidden"
            );


        byId(
            "securityBootstrapForm"
        )
            .classList
            .remove(
                "hidden"
            );


        byId(
            "securityLoginSubtitle"
        )
            .textContent =
            "راه‌اندازی اولیه";


        setStatus(
            byId(
                "securityBootstrapStatus"
            ),
            ""
        );


        setTimeout(
            () =>
                byId(
                    "securityBootstrapDisplayName"
                )
                    .focus(),
            0
        );
    }


    function unlockApplication(
        user
    ) {

        currentUser =
            user;


        document.body
            .classList
            .remove(
                "security-pending"
            );


        byId(
            "securityLoginOverlay"
        )
            .classList
            .add(
                "hidden"
            );


        byId(
            "securityHeader"
        )
            .classList
            .remove(
                "hidden"
            );


        byId(
            "securityUserLabel"
        )
            .textContent =
            `${user.displayName || user.userName} (${user.role === "Admin" ? "مدیر" : "کاربر"})`;


        byId(
            "securityAdminButton"
        )
            .classList
            .toggle(
                "hidden",
                user.role !== "Admin"
            );


        if (
            startApplication
        ) {

            const callback =
                startApplication;


            startApplication =
                null;


            callback();
        }
    }


    async function login(
        event
    ) {

        event.preventDefault();


        const status =
            byId(
                "securityLoginStatus"
            );


        try {

            setStatus(
                status,
                "در حال ورود..."
            );


            const result =
                await api(
                    "/api/auth/login",
                    {
                        method:
                            "POST",

                        body:
                            JSON.stringify({
                                userName:
                                    byId(
                                        "securityLoginUserName"
                                    )
                                        .value
                                        .trim(),

                                password:
                                    byId(
                                        "securityLoginPassword"
                                    )
                                        .value
                            })
                    }
                );


            byId(
                "securityLoginPassword"
            )
                .value =
                "";


            unlockApplication(
                result.user
            );
        }
        catch (error) {

            setStatus(
                status,
                error.message,
                true
            );
        }
    }


    async function bootstrap(
        event
    ) {

        event.preventDefault();


        const password =
            byId(
                "securityBootstrapPassword"
            )
                .value;


        const confirmPassword =
            byId(
                "securityBootstrapConfirmPassword"
            )
                .value;


        const status =
            byId(
                "securityBootstrapStatus"
            );


        if (
            password !==
            confirmPassword
        ) {

            setStatus(
                status,
                "رمز عبور و تکرار آن یکسان نیست.",
                true
            );


            return;
        }


        try {

            setStatus(
                status,
                "در حال ایجاد مدیر..."
            );


            const result =
                await api(
                    "/api/auth/bootstrap",
                    {
                        method:
                            "POST",

                        body:
                            JSON.stringify({
                                displayName:
                                    byId(
                                        "securityBootstrapDisplayName"
                                    )
                                        .value
                                        .trim(),

                                userName:
                                    byId(
                                        "securityBootstrapUserName"
                                    )
                                        .value
                                        .trim(),

                                password:
                                    password
                            })
                    }
                );


            unlockApplication(
                result.user
            );
        }
        catch (error) {

            setStatus(
                status,
                error.message,
                true
            );
        }
    }


    async function logout() {

        try {

            await api(
                "/api/auth/logout",
                {
                    method:
                        "POST"
                }
            );
        }
        catch {

            // حتی در صورت خطای شبکه، UI محلی قفل می‌شود.
        }


        showLogin(
            "از برنامه خارج شدید."
        );
    }


    async function openAdmin() {

        if (
            !currentUser ||
            currentUser.role !==
            "Admin"
        ) {

            return;
        }


        byId(
            "securityAdminOverlay"
        )
            .classList
            .remove(
                "hidden"
            );


        showAdminTab(
            "users"
        );


        await loadUsers();
    }


    function closeAdmin() {

        byId(
            "securityAdminOverlay"
        )
            .classList
            .add(
                "hidden"
            );
    }


    async function showAdminTab(
        tab
    ) {

        const users =
            tab ===
            "users";


        byId(
            "securityUsersPanel"
        )
            .classList
            .toggle(
                "hidden",
                !users
            );


        byId(
            "securityLogsPanel"
        )
            .classList
            .toggle(
                "hidden",
                users
            );


        byId(
            "securityUsersTabButton"
        )
            .classList
            .toggle(
                "active",
                users
            );


        byId(
            "securityLogsTabButton"
        )
            .classList
            .toggle(
                "active",
                !users
            );


        if (
            !users
        ) {

            await loadLogs();
        }
    }


    async function loadUsers() {

        try {

            const result =
                await api(
                    "/api/users"
                );


            const body =
                byId(
                    "securityUsersBody"
                );


            body.innerHTML =
                "";


            for (
                const user
                of result.users
            ) {

                const tr =
                    document.createElement(
                        "tr"
                    );


                tr.innerHTML =
                    `
                    <td>
                        ${escapeHtml(user.userName)}
                    </td>

                    <td>
                        ${escapeHtml(user.displayName)}
                    </td>

                    <td>
                        ${user.role === "Admin" ? "مدیر" : "کاربر"}
                    </td>

                    <td>
                        ${user.isActive ? "فعال" : "غیرفعال"}
                    </td>

                    <td>
                        ${formatDate(user.lastLoginDate)}
                    </td>

                    <td>

                        <button type="button"
                                data-edit-user="${user.userID}">

                            ویرایش

                        </button>

                    </td>
                    `;


                tr
                    .querySelector(
                        "[data-edit-user]"
                    )
                    .addEventListener(
                        "click",
                        () =>
                            editUser(
                                user
                            )
                    );


                body.appendChild(
                    tr
                );
            }
        }
        catch (error) {

            setStatus(
                byId(
                    "securityUserFormStatus"
                ),
                error.message,
                true
            );
        }
    }


    function editUser(
        user
    ) {

        editingUserID =
            user.userID;


        byId(
            "securityUserName"
        )
            .value =
            user.userName;


        byId(
            "securityDisplayName"
        )
            .value =
            user.displayName;


        byId(
            "securityRole"
        )
            .value =
            user.role;


        byId(
            "securityUserIsActive"
        )
            .checked =
            user.isActive;


        byId(
            "securityUserPassword"
        )
            .value =
            "";


        setStatus(
            byId(
                "securityUserFormStatus"
            ),
            "در حال ویرایش کاربر."
        );
    }


    function resetUserForm() {

        editingUserID =
            null;


        byId(
            "securityUserForm"
        )
            .reset();


        byId(
            "securityUserIsActive"
        )
            .checked =
            true;


        byId(
            "securityRole"
        )
            .value =
            "User";


        setStatus(
            byId(
                "securityUserFormStatus"
            ),
            ""
        );
    }


    async function saveUser(
        event
    ) {

        event.preventDefault();


        const status =
            byId(
                "securityUserFormStatus"
            );


        const payload = {

            userName:
                byId(
                    "securityUserName"
                )
                    .value
                    .trim(),

            displayName:
                byId(
                    "securityDisplayName"
                )
                    .value
                    .trim(),

            role:
                byId(
                    "securityRole"
                )
                    .value,

            isActive:
                byId(
                    "securityUserIsActive"
                )
                    .checked
        };


        const password =
            byId(
                "securityUserPassword"
            )
                .value;


        try {

            if (
                !editingUserID
            ) {

                if (
                    password.length <
                    8
                ) {

                    throw new Error(
                        "برای کاربر جدید رمز عبور حداقل ۸ کاراکتری وارد کنید."
                    );
                }


                await api(
                    "/api/users",
                    {
                        method:
                            "POST",

                        body:
                            JSON.stringify({
                                ...payload,
                                password
                            })
                    }
                );
            }
            else {

                await api(
                    `/api/users/${editingUserID}`,
                    {
                        method:
                            "PUT",

                        body:
                            JSON.stringify(
                                payload
                            )
                    }
                );


                if (
                    password
                ) {

                    await api(
                        `/api/users/${editingUserID}/reset-password`,
                        {
                            method:
                                "POST",

                            body:
                                JSON.stringify({
                                    password
                                })
                        }
                    );
                }
            }


            resetUserForm();


            setStatus(
                status,
                "اطلاعات کاربر ذخیره شد."
            );


            await loadUsers();
        }
        catch (error) {

            setStatus(
                status,
                error.message,
                true
            );
        }
    }


    async function loadLogs() {

        const status =
            byId(
                "securityLogStatus"
            );


        try {

            setStatus(
                status,
                "در حال دریافت لاگ‌ها..."
            );


            const search =
                byId(
                    "securityLogSearch"
                )
                    .value
                    .trim();


            const result =
                await api(
                    "/api/audit?take=500&search=" +
                    encodeURIComponent(
                        search
                    )
                );


            const body =
                byId(
                    "securityLogsBody"
                );


            body.innerHTML =
                "";


            for (
                const log
                of result.logs
            ) {

                const tr =
                    document.createElement(
                        "tr"
                    );


                tr.innerHTML =
                    `
                    <td>
                        ${formatDate(log.createdDate)}
                    </td>

                    <td>
                        ${escapeHtml(log.userName || "-")}
                    </td>

                    <td>

                        <strong>
                            ${escapeHtml(log.action)}
                        </strong>

                        <div class="security-log-path">
                            ${escapeHtml(log.path || "")}
                        </div>

                    </td>

                    <td class="${log.isSuccess ? "security-success" : "security-error"}">
                        ${log.statusCode ?? ""}
                    </td>

                    <td>
                        ${escapeHtml(log.details || "")}
                    </td>

                    <td>

                        <button type="button"
                                class="danger-button"
                                data-delete-log="${log.auditLogID}">

                            حذف

                        </button>

                    </td>
                    `;


                tr
                    .querySelector(
                        "[data-delete-log]"
                    )
                    .addEventListener(
                        "click",
                        () =>
                            deleteLog(
                                log.auditLogID
                            )
                    );


                body.appendChild(
                    tr
                );
            }


            setStatus(
                status,
                `${result.logs.length} رکورد نمایش داده شد.`
            );
        }
        catch (error) {

            setStatus(
                status,
                error.message,
                true
            );
        }
    }


    async function deleteLog(
        id
    ) {

        if (
            !window.confirm(
                "این رکورد لاگ حذف شود؟"
            )
        ) {

            return;
        }


        try {

            await api(
                `/api/audit/${id}`,
                {
                    method:
                        "DELETE"
                }
            );


            await loadLogs();
        }
        catch (error) {

            setStatus(
                byId(
                    "securityLogStatus"
                ),
                error.message,
                true
            );
        }
    }


    async function cleanupLogs() {

        if (
            !window.confirm(
                "لاگ‌های قدیمی‌تر از ۹۰ روز حذف شوند؟"
            )
        ) {

            return;
        }


        try {

            const result =
                await api(
                    "/api/audit/cleanup?olderThanDays=90",
                    {
                        method:
                            "DELETE"
                    }
                );


            setStatus(
                byId(
                    "securityLogStatus"
                ),
                `${result.deleted} رکورد قدیمی حذف شد.`
            );


            await loadLogs();
        }
        catch (error) {

            setStatus(
                byId(
                    "securityLogStatus"
                ),
                error.message,
                true
            );
        }
    }


    function formatDate(
        value
    ) {

        if (
            !value
        ) {

            return "-";
        }


        const date =
            new Date(
                value
            );


        if (
            Number.isNaN(
                date.getTime()
            )
        ) {

            return String(
                value
            );
        }


        return date.toLocaleString(
            "fa-IR"
        );
    }


    async function start(
        callback
    ) {

        startApplication =
            callback;


        try {

            const result =
                await api(
                    "/api/auth/status"
                );


            if (
                result.needsBootstrap
            ) {

                showBootstrap();


                return;
            }


            if (
                result.authenticated
            ) {

                unlockApplication(
                    result.user
                );


                return;
            }


            showLogin();
        }
        catch (error) {

            showLogin(
                "اتصال به سرویس DentalRay برقرار نشد: " +
                error.message
            );
        }
    }


    buildUi();


    window.DentalRaySecurity = {
        start,
        getCurrentUser: () => currentUser
    };

})();
