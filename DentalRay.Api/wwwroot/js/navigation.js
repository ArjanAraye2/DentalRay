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
      <h3 class="dashboard-group-title">آمار کلی</h3>
      <div class="dashboard-summary-grid dashboard-overall-grid">
        <div class="dashboard-summary-card metric-blue"><span class="dashboard-metric-icon">👥</span><strong id="dashboardTotalPatients">-</strong><span>کل بیماران</span></div>
        <div class="dashboard-summary-card metric-green"><span class="dashboard-metric-icon">✓</span><strong id="dashboardActivePatients">-</strong><span>بیماران فعال</span></div>
        <div class="dashboard-summary-card metric-gray"><span class="dashboard-metric-icon">○</span><strong id="dashboardInactivePatients">-</strong><span>بیماران غیرفعال</span></div>
        <div class="dashboard-summary-card metric-purple"><span class="dashboard-metric-icon">▣</span><strong id="dashboardTotalStudies">-</strong><span>کل مطالعات</span></div>
        <div class="dashboard-summary-card metric-cyan"><span class="dashboard-metric-icon">▧</span><strong id="dashboardTotalImages">-</strong><span>کل تصاویر</span></div>
      </div>
      <h3 class="dashboard-group-title">امروز</h3>
      <div class="dashboard-summary-grid dashboard-today-grid">
        <div class="dashboard-summary-card today-patients"><strong id="dashboardPatientsToday">-</strong><span>بیماران امروز</span></div>
        <div class="dashboard-summary-card today-studies"><strong id="dashboardStudiesToday">-</strong><span>مطالعات امروز</span></div>
        <div class="dashboard-summary-card today-images"><strong id="dashboardImagesToday">-</strong><span>تصاویر امروز</span></div>
        <div class="dashboard-summary-card today-new"><strong id="dashboardNewPatientsToday">-</strong><span>بیماران جدید امروز</span></div>
      </div>
      <div class="dashboard-detail-grid">
        <section class="dashboard-panel"><div class="dashboard-panel-title"><strong>آخرین مطالعات</strong><span>۵ مورد اخیر</span></div><div id="dashboardRecentStudies" class="dashboard-recent-list"></div></section>
        <section class="dashboard-panel"><div class="dashboard-panel-title"><strong>آخرین تصاویر</strong><span>۵ مورد اخیر</span></div><div id="dashboardRecentImages" class="dashboard-recent-list"></div></section>
      </div>
      <section class="dashboard-system-panel"><div><strong>وضعیت سامانه</strong><span id="dashboardGeneratedAt">-</span></div><div class="dashboard-system-items"><span id="dashboardDatabaseStatus">پایگاه‌داده: در حال بررسی</span><span id="dashboardStorageStatus">فضای تصاویر: در حال بررسی</span></div></section>`;
    main.appendChild(dashboard);

    const settings = document.createElement("section");
    settings.id = "settingsSection";
    settings.className = "card hidden shell-page";
    settings.innerHTML = `
      <div class="section-header"><div><h2>تنظیمات و مدیریت سیستم</h2><p>تعاریف پایه و دسترسی‌های مدیریتی DentalRay</p></div></div>
      <div id="settingsAdminActions" class="settings-admin-actions"></div>`;
    main.appendChild(settings);

    const placeholders = {
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
            const response = await fetch("/api/dashboard", { cache: "no-store" });
            const result = await response.json();
            if (!response.ok || !result.success) throw new Error();
            const overall = result.overall || {}, today = result.today || {};
            const values = {
                dashboardTotalPatients: overall.totalPatients, dashboardActivePatients: overall.activePatients,
                dashboardInactivePatients: overall.inactivePatients, dashboardTotalStudies: overall.totalStudies,
                dashboardTotalImages: overall.totalImages, dashboardPatientsToday: today.patientsToday,
                dashboardStudiesToday: today.studiesToday, dashboardImagesToday: today.imagesToday,
                dashboardNewPatientsToday: today.newPatientsToday
            };
            Object.entries(values).forEach(([id, value]) => document.getElementById(id).textContent = value ?? 0);
            renderRecentStudies(result.recentStudies || []);
            renderRecentImages(result.recentImages || []);
            document.getElementById("dashboardGeneratedAt").textContent = `آخرین به‌روزرسانی: ${formatPersianDateTime(result.generatedAt)}`;
            document.getElementById("dashboardDatabaseStatus").textContent = result.system?.databaseConnected ? "● پایگاه‌داده متصل است" : "● پایگاه‌داده در دسترس نیست";
            document.getElementById("dashboardDatabaseStatus").className = result.system?.databaseConnected ? "system-ok" : "system-error";
            const storage = result.system?.storage || {};
            document.getElementById("dashboardStorageStatus").textContent = storage.available ? `● فضای تصاویر آماده است — ${formatBytes(storage.freeBytes)} آزاد` : `● مسیر ${storage.rootPath || "D:\\RadiologyData"} در دسترس نیست`;
            document.getElementById("dashboardStorageStatus").className = storage.available ? "system-ok" : "system-error";
        } catch {
            dashboard.querySelectorAll(".dashboard-summary-card strong").forEach(x => x.textContent = "-");
            document.getElementById("dashboardRecentStudies").textContent = "دریافت اطلاعات داشبورد ناموفق بود.";
            document.getElementById("dashboardRecentImages").textContent = "دریافت اطلاعات داشبورد ناموفق بود.";
        }
    }

    const formatBytes = value => !Number.isFinite(Number(value)) ? "-" : `${(Number(value) / 1073741824).toLocaleString("fa-IR", { maximumFractionDigits: 1 })} گیگابایت`;
    function renderRecentStudies(items) {
        const root = document.getElementById("dashboardRecentStudies"); root.replaceChildren();
        if (!items.length) { root.textContent = "مطالعه‌ای ثبت نشده است."; return; }
        items.forEach(item => { const row = document.createElement("div"); row.className = "dashboard-recent-row"; row.innerHTML = `<span class="dashboard-recent-icon">▣</span><span><strong></strong><small></small></span><time></time>`; row.querySelector("strong").textContent = item.patientName; row.querySelector("small").textContent = `${item.studyTypeName}${item.bodyPart ? ` — ${item.bodyPart}` : ""}`; row.querySelector("time").textContent = formatPersianDateTime(item.studyDate); root.appendChild(row); });
    }
    function renderRecentImages(items) {
        const root = document.getElementById("dashboardRecentImages"); root.replaceChildren();
        if (!items.length) { root.textContent = "تصویری ثبت نشده است."; return; }
        items.forEach(item => { const row = document.createElement("div"); row.className = "dashboard-recent-row"; const media = item.contentType === "application/pdf" ? document.createElement("span") : document.createElement("img"); media.className = "dashboard-image-thumb"; if (media.tagName === "IMG") { media.src = `/api/radiologyimages/${item.imageID}`; media.alt = ""; media.loading = "lazy"; } else media.textContent = "PDF"; const text = document.createElement("span"), name = document.createElement("strong"), type = document.createElement("small"), time = document.createElement("time"); name.textContent = item.patientName; type.textContent = item.imageTypeName || item.fileName; time.textContent = formatPersianDateTime(item.createdDate); text.append(name, type); row.append(media, text, time); root.appendChild(row); });
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
    navigate("dashboard");
})();
