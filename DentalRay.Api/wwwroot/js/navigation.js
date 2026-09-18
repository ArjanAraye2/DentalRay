// DentalRay shell navigation.
// Keeps global navigation separate from Patient and Study actions and moves
// administrative maintenance commands out of the application header.
(() => {
    const main = document.querySelector(".page-container");
    const sidebarLinks = [...document.querySelectorAll(".sidebar-link[data-nav]")];
    if (!main || !sidebarLinks.length) return;

    const hidePages = () => document.querySelectorAll(".page-container > section").forEach(x => x.classList.add("hidden"));
    const setActive = name => sidebarLinks.forEach(x => x.classList.toggle("active", x.dataset.nav === name));

    const dashboard = document.createElement("section");
    dashboard.id = "dashboardSection";
    dashboard.className = "card hidden shell-page";
    dashboard.innerHTML = `
      <div class="section-header"><div><h2>داشبورد</h2><p>نمای کلی سامانه DentalRay</p></div></div>
      <div class="dashboard-summary-grid">
        <div class="dashboard-summary-card"><strong id="dashboardTotalPatients">-</strong><span>کل بیماران</span></div>
        <div class="dashboard-summary-card"><strong id="dashboardActivePatients">-</strong><span>بیماران فعال</span></div>
        <div class="dashboard-summary-card"><strong id="dashboardPatientsWithStudies">-</strong><span>بیماران دارای مطالعه</span></div>
      </div>
      <div class="dashboard-shortcuts"><button type="button" data-open-nav="patients">مدیریت بیماران</button><button type="button" data-open-nav="studies" class="secondary-button">مطالعات</button><button type="button" data-open-nav="images" class="secondary-button">تصاویر</button></div>`;
    main.appendChild(dashboard);

    const settings = document.createElement("section");
    settings.id = "settingsSection";
    settings.className = "card hidden shell-page";
    settings.innerHTML = `
      <div class="section-header"><div><h2>تنظیمات و مدیریت سیستم</h2><p>تعاریف پایه و دسترسی‌های مدیریتی DentalRay</p></div></div>
      <div id="settingsAdminActions" class="settings-admin-actions"></div>`;
    main.appendChild(settings);

    const placeholders = {
        studies: ["مطالعات", "جستجو و مشاهدهٔ سراسری مطالعات همهٔ بیماران"],
        images: ["تصاویر", "جستجو و مشاهدهٔ سراسری تصاویر رادیولوژی"],
        reports: ["گزارش‌ها", "گزارش‌های مدیریتی و آماری سامانه"]
    };
    const placeholderSections = {};
    Object.entries(placeholders).forEach(([name, [title, description]]) => {
        const section = document.createElement("section");
        section.id = `${name}WorkspaceSection`;
        section.className = "card hidden shell-page";
        section.innerHTML = `<div class="section-header"><div><h2>${title}</h2><p>${description}</p></div></div><div class="shell-empty-state">این بخش در مرحلهٔ بعد تکمیل می‌شود.</div>`;
        main.appendChild(section);
        placeholderSections[name] = section;
    });

    function moveAdministrativeButtons() {
        const target = document.getElementById("settingsAdminActions");
        const administrativeLabels = new Set(["اشخاص / پرسنل", "مدیریت تخصص‌ها", "مدیریت انواع Study", "مدیریت انواع تصویر", "مدیریت کاربران"]);
        document.querySelectorAll(".header-content button").forEach(button => {
            if (!administrativeLabels.has(button.textContent.trim())) return;
            button.classList.add("settings-action-button");
            target.appendChild(button);
        });
    }

    async function openDashboard() {
        hidePages(); dashboard.classList.remove("hidden"); setActive("dashboard");
        try {
            const response = await fetch("/api/patients?includeInactive=true");
            const result = await response.json();
            if (!response.ok) throw new Error();
            const statistics = result.statistics || {};
            document.getElementById("dashboardTotalPatients").textContent = statistics.totalPatients ?? 0;
            document.getElementById("dashboardActivePatients").textContent = statistics.activePatients ?? 0;
            document.getElementById("dashboardPatientsWithStudies").textContent = statistics.patientsWithStudies ?? 0;
        } catch {
            dashboard.querySelectorAll(".dashboard-summary-card strong").forEach(x => x.textContent = "-");
        }
    }

    function openPatients() {
        hidePages();
        document.getElementById("patientsSection")?.classList.remove("hidden");
        setActive("patients");
        if (typeof window.loadPatients === "function") window.loadPatients();
        window.scrollTo(0, 0);
    }

    function openSettings() {
        hidePages(); settings.classList.remove("hidden"); setActive("settings"); window.scrollTo(0, 0);
    }

    function openPlaceholder(name) {
        hidePages(); placeholderSections[name]?.classList.remove("hidden"); setActive(name); window.scrollTo(0, 0);
    }

    function navigate(name) {
        if (name === "dashboard") return openDashboard();
        if (name === "patients") return openPatients();
        if (name === "settings") return openSettings();
        if (placeholderSections[name]) return openPlaceholder(name);
    }

    sidebarLinks.forEach(link => link.addEventListener("click", event => {
        event.preventDefault();
        navigate(link.dataset.nav);
    }));
    dashboard.addEventListener("click", event => {
        const button = event.target.closest("[data-open-nav]");
        if (button) navigate(button.dataset.openNav);
    });

    // Admin scripts run before this file and create their own working buttons.
    // Moving those same nodes preserves every original click handler.
    moveAdministrativeButtons();
    window.DentalRayNavigation = { navigate, moveAdministrativeButtons };
})();
