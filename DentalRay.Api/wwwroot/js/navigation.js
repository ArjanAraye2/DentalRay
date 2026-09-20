// DentalRay shell navigation.
// Keeps global navigation separate from Patient and Study actions and moves
// administrative maintenance commands out of the application header.
(() => {
    const main = document.querySelector(".page-container");
    const sidebarLinks = [...document.querySelectorAll(".sidebar-link[data-nav]")];
    let latestNetwork = {};
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
        <div class="dashboard-summary-card metric-blue"><span class="dashboard-metric-icon" aria-hidden="true"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><circle cx="9" cy="8" r="3.4"/><path d="M2.8 20c0-3.4 2.8-5.6 6.2-5.6s6.2 2.2 6.2 5.6"/><path d="M16.2 5.2a3.4 3.4 0 0 1 0 6.5"/><path d="M17.6 14.7c2.2.6 3.6 2.3 3.6 4.5"/></svg></span><strong id="dashboardTotalPatients">-</strong><span>کل بیماران</span></div>
        <div class="dashboard-summary-card metric-green"><span class="dashboard-metric-icon" aria-hidden="true"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="9"/><path d="m8 12.4 2.6 2.6L16 9.6"/></svg></span><strong id="dashboardActivePatients">-</strong><span>بیماران فعال</span></div>
        <div class="dashboard-summary-card metric-gray"><span class="dashboard-metric-icon" aria-hidden="true"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round"><circle cx="12" cy="12" r="9"/><path d="M8 12h8"/></svg></span><strong id="dashboardInactivePatients">-</strong><span>بیماران غیرفعال</span></div>
        <div class="dashboard-summary-card metric-purple"><span class="dashboard-metric-icon" aria-hidden="true"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M4 5.5a1.5 1.5 0 0 1 1.5-1.5h13A1.5 1.5 0 0 1 20 5.5v13a1.5 1.5 0 0 1-1.5 1.5h-13A1.5 1.5 0 0 1 4 18.5Z"/><path d="M8.5 3v18"/><path d="M12 8.5h5"/><path d="M12 12h5"/><path d="M12 15.5h3"/></svg></span><strong id="dashboardTotalStudies">-</strong><span>کل مطالعات</span></div>
        <div class="dashboard-summary-card metric-cyan"><span class="dashboard-metric-icon" aria-hidden="true"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="4.5" width="18" height="15" rx="2"/><path d="m3.5 16 4.6-4.3a1.6 1.6 0 0 1 2.2 0l3.2 3"/><path d="m13.8 14 1.6-1.5a1.6 1.6 0 0 1 2.2 0l3.1 2.9"/><circle cx="9" cy="9.4" r="1.3"/></svg></span><strong id="dashboardTotalImages">-</strong><span>کل تصاویر</span></div>
      </div>
      <h3 class="dashboard-group-title">امروز</h3>
      <div class="dashboard-summary-grid dashboard-today-grid">
        <div class="dashboard-summary-card today-card today-patients"><strong id="dashboardPatientsToday">-</strong><span>بیماران امروز</span></div>
        <div class="dashboard-summary-card today-card today-studies"><strong id="dashboardStudiesToday">-</strong><span>مطالعات امروز</span></div>
        <div class="dashboard-summary-card today-card today-images"><strong id="dashboardImagesToday">-</strong><span>تصاویر امروز</span></div>
        <div class="dashboard-summary-card today-card today-new"><strong id="dashboardNewPatientsToday">-</strong><span>بیماران جدید امروز</span></div>
      </div>
      <div class="dashboard-detail-grid">
        <section class="dashboard-panel"><div class="dashboard-panel-title"><strong>آخرین مطالعات</strong><span>۵ مورد اخیر</span></div><div id="dashboardRecentStudies" class="dashboard-recent-list"></div></section>
        <section class="dashboard-panel"><div class="dashboard-panel-title"><strong>آخرین تصاویر</strong><span>۵ مورد اخیر</span></div><div id="dashboardRecentImages" class="dashboard-recent-list"></div></section>
      </div>
      <section class="dashboard-network-panel">
        <div class="dashboard-panel-title"><strong>دسترسی شبکه</strong><button type="button" class="secondary-button dashboard-network-settings-button" data-open-nav="settings" data-settings-focus="network">⚙ تنظیمات دسترسی شبکه</button></div>
        <div class="dashboard-network-grid">
          <div><span>نام کامپیوتر سرور</span><strong id="dashboardServerName">-</strong></div>
          <div><span>IP محلی</span><strong id="dashboardLocalIp">-</strong></div>
          <div><span>IP عمومی / استاتیک</span><strong id="dashboardPublicIp">-</strong></div>
        </div>
        <div class="dashboard-access-links"><strong>لینک اجرای برنامه در دستگاه‌های دیگر</strong><div id="dashboardLanLinks"></div><div id="dashboardPublicLink"></div></div>
        <p id="dashboardNetworkNote" class="dashboard-network-note"></p>
      </section>
      <section class="dashboard-system-panel"><div><strong>وضعیت سامانه</strong><span id="dashboardGeneratedAt">-</span></div><div class="dashboard-system-items"><span id="dashboardDatabaseStatus">پایگاه‌داده: در حال بررسی</span><span id="dashboardStorageStatus">فضای تصاویر: در حال بررسی</span></div></section>`;
    main.appendChild(dashboard);

    const settings = document.createElement("section");
    settings.id = "settingsSection";
    settings.className = "card hidden shell-page";
    settings.innerHTML = `
      <div class="section-header"><div><h2>تنظیمات و مدیریت سیستم</h2><p>تعاریف پایه و دسترسی‌های مدیریتی DentalRay</p></div></div>
      <section id="networkAccessSettings" class="network-settings-card">
        <div class="dashboard-panel-title"><strong>تنظیمات دسترسی شبکه</strong><span>اجرای DentalRay در کامپیوتر و موبایل</span></div>
        <div class="network-settings-steps">
          <div><strong>۱. آدرس برنامه</strong><span>برای دستگاه‌های شبکه از لینک نام سرور یا IP محلی استفاده کنید.</span></div>
          <div><strong>۲. Windows Firewall</strong><span>پورت TCP شماره 5202 باید برای شبکه Private باز باشد.</span><code>netsh advfirewall firewall add rule name="DentalRay Port 5202" dir=in action=allow protocol=TCP localport=5202 profile=private</code></div>
          <div><strong>۳. IP استاتیک اینترنت</strong><span>PublicHost را در فایل DentalRay.config.json تنظیم و Port Forwarding روتر را به سرور هدایت کنید.</span></div>
        </div>
        <div id="settingsNetworkLinks" class="dashboard-access-links"></div>
      </section>
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

    // The dashboard refreshes itself while it is open. The timer is paused when
    // the tab is hidden so background tabs never poll needlessly.
    const DASHBOARD_REFRESH_MS = 30000;
    let dashboardTimer = null;
    let dashboardVisible = false;

    function startDashboardAutoRefresh() {
        stopDashboardAutoRefresh();
        dashboardTimer = setInterval(() => {
            if (dashboardVisible && document.visibilityState === "visible" && !dashboard.classList.contains("hidden")) {
                openDashboard({ silent: true });
            }
        }, DASHBOARD_REFRESH_MS);
    }
    function stopDashboardAutoRefresh() {
        if (dashboardTimer) { clearInterval(dashboardTimer); dashboardTimer = null; }
    }
    document.addEventListener("visibilitychange", () => {
        if (document.visibilityState === "visible" && dashboardVisible && !dashboard.classList.contains("hidden")) {
            openDashboard({ silent: true });
        }
    });

    async function openDashboard(options = {}) {
        const silent = options.silent === true;
        if (!silent) {
            hidePages(); dashboard.classList.remove("hidden"); setActive("dashboard");
            dashboardVisible = true;
            startDashboardAutoRefresh();
        }
        try {
            const response = await fetch("/api/dashboard", { cache: "no-store" });
            const result = await response.json();
            if (!response.ok || !result.success) throw new Error();
            const overall = result.overall || {}, today = result.today || {};
            const connected = result.system?.databaseConnected !== false;
            const values = {
                dashboardTotalPatients: overall.totalPatients, dashboardActivePatients: overall.activePatients,
                dashboardInactivePatients: overall.inactivePatients, dashboardTotalStudies: overall.totalStudies,
                dashboardTotalImages: overall.totalImages, dashboardPatientsToday: today.patientsToday,
                dashboardStudiesToday: today.studiesToday, dashboardImagesToday: today.imagesToday,
                dashboardNewPatientsToday: today.newPatientsToday
            };
            // Show a dash instead of a misleading 0 when the database is unreachable.
            Object.entries(values).forEach(([id, value]) => {
                const el = document.getElementById(id);
                if (!el) return;
                el.textContent = connected ? (value ?? 0) : "—";
                // Cards that track "today" turn green once they have activity.
                const card = el.closest(".dashboard-summary-card");
                if (card?.classList.contains("today-card")) {
                    card.classList.toggle("has-value", connected && Number(value) > 0);
                }
            });
            renderRecentStudies(connected ? (result.recentStudies || []) : []);
            renderRecentImages(connected ? (result.recentImages || []) : []);
            document.getElementById("dashboardGeneratedAt").textContent = `آخرین به‌روزرسانی: ${formatPersianDateTime(result.generatedAt)}`;
            document.getElementById("dashboardDatabaseStatus").textContent = connected ? "● پایگاه‌داده متصل است" : "● پایگاه‌داده در دسترس نیست";
            document.getElementById("dashboardDatabaseStatus").className = connected ? "system-ok" : "system-error";
            const storage = result.system?.storage || {};
            document.getElementById("dashboardStorageStatus").textContent = storage.available
                ? `● فضای تصاویر آماده است — ${formatBytes(storage.freeBytes)} آزاد`
                : `● مسیر ${storage.rootPath || "تنظیم‌نشده"} در دسترس نیست`;
            document.getElementById("dashboardStorageStatus").className = storage.available ? "system-ok" : "system-error";
            renderNetworkAccess(result.system?.network || {});
            renderSettingsNetworkLinks(result.system?.network || {});
        } catch {
            if (silent) return; // keep the last good snapshot on a background refresh
            dashboard.querySelectorAll(".dashboard-summary-card strong").forEach(x => x.textContent = "-");
            document.getElementById("dashboardRecentStudies").textContent = "دریافت اطلاعات داشبورد ناموفق بود.";
            document.getElementById("dashboardRecentImages").textContent = "دریافت اطلاعات داشبورد ناموفق بود.";
        }
    }

    const formatBytes = value => !Number.isFinite(Number(value)) ? "-" : `${(Number(value) / 1073741824).toLocaleString("fa-IR", { maximumFractionDigits: 1 })} گیگابایت`;
    function renderNetworkAccess(network) {
        latestNetwork = network;
        document.getElementById("dashboardServerName").textContent = network.hostName || "-";
        document.getElementById("dashboardLocalIp").textContent = (network.localIps || []).join(" ، ") || "شناسایی نشد";
        const publicIp = document.getElementById("dashboardPublicIp");
        publicIp.textContent = network.publicConfigured ? network.publicHost : "تنظیم نشده";
        publicIp.className = network.publicConfigured ? "network-configured" : "network-not-configured";
        const lanRoot = document.getElementById("dashboardLanLinks"); lanRoot.replaceChildren();
        if (network.serverNameUrl) lanRoot.appendChild(createAccessLink(network.serverNameUrl, "نام سرور"));
        (network.localUrls || []).forEach(url => lanRoot.appendChild(createAccessLink(url, "شبکه محلی")));
        if (!network.serverNameUrl && !(network.localUrls || []).length) lanRoot.textContent = "لینک شبکه محلی شناسایی نشد.";
        const publicRoot = document.getElementById("dashboardPublicLink"); publicRoot.replaceChildren();
        if (network.publicUrl) publicRoot.appendChild(createAccessLink(network.publicUrl, "اینترنت / IP استاتیک"));
        else publicRoot.textContent = "برای لینک اینترنتی، PublicHost را در DentalRay.config.json تنظیم کنید.";
        document.getElementById("dashboardNetworkNote").textContent = network.note || "";
    }
    function renderSettingsNetworkLinks(network) {
        const root = document.getElementById("settingsNetworkLinks"); if (!root) return; root.replaceChildren();
        const heading = document.createElement("strong"); heading.textContent = "لینک‌های آماده استفاده"; root.appendChild(heading);
        if (network.serverNameUrl) root.appendChild(createAccessLink(network.serverNameUrl, "نام سرور"));
        (network.localUrls || []).forEach(url => root.appendChild(createAccessLink(url, "IP محلی")));
        if (network.publicUrl) root.appendChild(createAccessLink(network.publicUrl, "IP استاتیک"));
    }
    function createAccessLink(url, label) {
        const row = document.createElement("div"); row.className = "dashboard-access-row";
        const title = document.createElement("span"); title.textContent = label;
        const link = document.createElement("a"); link.href = url; link.target = "_blank"; link.rel = "noopener"; link.textContent = url;
        const copy = document.createElement("button"); copy.type = "button"; copy.className = "secondary-button"; copy.textContent = "کپی";
        copy.onclick = async () => { try { await navigator.clipboard.writeText(url); copy.textContent = "کپی شد"; setTimeout(() => copy.textContent = "کپی", 1500); } catch { window.prompt("لینک را کپی کنید:", url); } };
        row.append(title, link, copy); return row;
    }
    function renderRecentStudies(items) {
        const root = document.getElementById("dashboardRecentStudies"); root.replaceChildren();
        if (!items.length) { root.textContent = "مطالعه‌ای ثبت نشده است."; return; }
        items.forEach(item => {
            const row = document.createElement("button");
            row.type = "button";
            row.className = "dashboard-recent-row dashboard-recent-clickable";
            row.title = "باز کردن پرونده بیمار و همین مطالعه";

            // Prefer a real thumbnail; fall back to the study icon when the study
            // has no image (or only PDFs).
            const media = item.thumbnailImageID
                ? document.createElement("img")
                : document.createElement("span");
            media.className = "dashboard-recent-icon";
            if (item.thumbnailImageID) {
                media.src = `/api/radiologyimages/${item.thumbnailImageID}`;
                media.alt = ""; media.loading = "lazy";
                media.onerror = () => { media.removeAttribute("src"); media.textContent = "▣"; };
            } else media.textContent = "▣";

            const text = document.createElement("span");
            const name = document.createElement("strong");
            name.textContent = item.patientName;
            const meta = document.createElement("small");
            meta.textContent = `${item.studyTypeName}${item.bodyPart ? ` — ${item.bodyPart}` : ""}`;
            text.append(name, meta);

            const time = document.createElement("time");
            time.textContent = formatPersianDateTime(item.studyDate);

            row.append(media, text, time);
            row.addEventListener("click", () => openPatientStudy(item));
            root.appendChild(row);
        });
    }
    function renderRecentImages(items) {
        const root = document.getElementById("dashboardRecentImages"); root.replaceChildren();
        if (!items.length) { root.textContent = "تصویری ثبت نشده است."; return; }
        items.forEach(item => {
            const row = document.createElement("button");
            row.type = "button";
            row.className = "dashboard-recent-row dashboard-recent-clickable";
            row.title = item.contentType === "application/pdf" ? "باز کردن PDF در تب جدید" : "نمایش تصویر بزرگ";

            const isPdf = item.contentType === "application/pdf";
            const media = isPdf ? document.createElement("span") : document.createElement("img");
            media.className = "dashboard-image-thumb";
            if (isPdf) media.textContent = "PDF";
            else { media.src = `/api/radiologyimages/${item.imageID}`; media.alt = ""; media.loading = "lazy"; }

            const text = document.createElement("span"), name = document.createElement("strong"),
                  type = document.createElement("small"), time = document.createElement("time");
            name.textContent = item.patientName;
            type.textContent = item.imageTypeName || item.fileName;
            time.textContent = formatPersianDateTime(item.createdDate);
            text.append(name, type);
            row.append(media, text, time);
            row.addEventListener("click", () => openDashboardImage(item, isPdf));
            root.appendChild(row);
        });
    }

    // Opens the patient record and scrolls to the clicked study card. The
    // patients workspace is reused so behaviour matches the patient list.
    async function openPatientStudy(item) {
        navigate("patients");
        try {
            if (typeof window.openPatientInline === "function") await window.openPatientInline(item.patientID);
            // Studies render asynchronously; wait for the card to exist.
            const card = await waitForStudyCard(item.studyID);
            card?.scrollIntoView({ behavior: "smooth", block: "start" });
        } catch { /* patient lookup failed; the workspace already shows its own error */ }
    }

    function waitForStudyCard(studyID, timeoutMs = 4000) {
        return new Promise(resolve => {
            const started = Date.now();
            const tick = () => {
                const card = document.querySelector(`.study-scroll-card[data-study-id="${studyID}"]`);
                if (card) return resolve(card);
                if (Date.now() - started > timeoutMs) return resolve(null);
                setTimeout(tick, 120);
            };
            tick();
        });
    }

    function openDashboardImage(item, isPdf) {
        const url = `/api/radiologyimages/${item.imageID}`;
        if (isPdf) { window.open(url, "_blank", "noopener"); return; }
        // Reuse the application's own image viewer when it is available.
        if (typeof window.openLargeImage === "function") {
            window.openLargeImage({ imageID: item.imageID, fileName: item.fileName, contentType: item.contentType, imageTypeName: item.imageTypeName });
        } else {
            window.open(url, "_blank", "noopener");
        }
    }

    function openPatients() {
        hidePages();
        document.getElementById("patientsSection")?.classList.remove("hidden");
        setActive("patients");
        if (typeof window.loadPatients === "function") window.loadPatients();
        window.scrollTo(0, 0);
    }

    function openSettings(focus) {
        hidePages(); settings.classList.remove("hidden"); setActive("settings");
        renderSettingsNetworkLinks(latestNetwork);
        if (focus === "network") document.getElementById("networkAccessSettings")?.scrollIntoView({ behavior: "smooth", block: "start" });
        else window.scrollTo(0, 0);
    }

    function openPlaceholder(name) {
        hidePages(); placeholderSections[name]?.classList.remove("hidden"); setActive(name); window.scrollTo(0, 0);
    }

    // The header search previously did nothing. It now performs a real
    // patient lookup and opens the selected record in the Patients workspace.
    function setupGlobalSearch() {
        const input = document.getElementById("globalSearchInput");
        const results = document.getElementById("globalSearchResults");
        if (!input || !results) return;
        let timer = null, controller = null;

        const close = () => { results.classList.add("hidden"); results.replaceChildren(); };

        const render = patients => {
            results.replaceChildren();
            if (!patients.length) {
                const empty = document.createElement("div");
                empty.className = "global-search-empty";
                empty.textContent = "بیماری با این مشخصات یافت نشد.";
                results.appendChild(empty);
            } else {
                patients.slice(0, 8).forEach(p => {
                    const item = document.createElement("button");
                    item.type = "button";
                    item.className = "global-search-item";
                    item.setAttribute("role", "option");
                    const avatar = document.createElement("span");
                    avatar.className = "global-search-avatar";
                    const img = document.createElement("img");
                    img.alt = ""; img.loading = "lazy";
                    img.src = `/api/patients/${p.patientID}/photo`;
                    img.onerror = () => { img.remove(); avatar.textContent = "👤"; };
                    avatar.appendChild(img);
                    const text = document.createElement("span");
                    text.className = "global-search-text";
                    const name = document.createElement("strong");
                    name.textContent = `${p.firstName || ""} ${p.lastName || ""}`.trim() || "-";
                    const meta = document.createElement("small");
                    meta.textContent = `${p.nationalCode || "-"}${p.mobile ? ` — ${p.mobile}` : ""}`;
                    text.append(name, meta);
                    item.append(avatar, text);
                    item.addEventListener("click", () => { close(); input.value = ""; openSearchResult(p); });
                    results.appendChild(item);
                });
            }
            results.classList.remove("hidden");
        };

        const search = async term => {
            controller?.abort(); controller = new AbortController();
            try {
                const q = new URLSearchParams();
                q.set("search", term);
                q.set("includeInactive", "true");
                const r = await fetch(`/api/patients?${q}`, { cache: "no-store", signal: controller.signal });
                const x = await r.json();
                if (!r.ok) throw new Error();
                render(x.patients || x || []);
            } catch (e) { if (e?.name !== "AbortError") close(); }
        };

        input.addEventListener("input", () => {
            const term = input.value.trim();
            clearTimeout(timer);
            if (term.length < 2) { close(); return; }
            timer = setTimeout(() => search(term), 220);
        });
        input.addEventListener("keydown", e => {
            if (e.key === "Escape") { close(); input.blur(); return; }
            if (e.key === "Enter") { e.preventDefault(); results.querySelector(".global-search-item")?.click(); }
        });
        document.addEventListener("click", e => { if (!e.target.closest("#globalSearchBox")) close(); });
    }

    async function openSearchResult(patient) {
        navigate("patients");
        const search = document.getElementById("patientSearch");
        if (search) search.value = patient.nationalCode || `${patient.firstName || ""} ${patient.lastName || ""}`.trim();
        if (typeof window.loadPatients === "function") await window.loadPatients(search?.value || "");
        if (typeof window.openPatientInline === "function") await window.openPatientInline(patient.patientID);
    }

    function navigate(name) {
        if (name === "dashboard") return openDashboard();
        if (name === "patients") { dashboardVisible = false; stopDashboardAutoRefresh(); return openPatients(); }
        if (name === "settings") { dashboardVisible = false; stopDashboardAutoRefresh(); return openSettings(); }
        if (placeholderSections[name]) { dashboardVisible = false; stopDashboardAutoRefresh(); return openPlaceholder(name); }
    }

    sidebarLinks.forEach(link => link.addEventListener("click", event => {
        event.preventDefault();
        navigate(link.dataset.nav);
    }));
    dashboard.addEventListener("click", event => {
        const button = event.target.closest("[data-open-nav]");
        if (!button) return;
        if (button.dataset.openNav === "settings") return openSettings(button.dataset.settingsFocus);
        navigate(button.dataset.openNav);
    });

    // Admin scripts run before this file and create their own working buttons.
    // Moving those same nodes preserves every original click handler.
    moveAdministrativeButtons();
    setupGlobalSearch();
    window.DentalRayNavigation = { navigate, moveAdministrativeButtons };
    navigate("dashboard");
})();
