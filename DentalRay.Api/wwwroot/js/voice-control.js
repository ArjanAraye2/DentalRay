// DentalRay — کنترل تصویر با صوت (فاز ۱).
//
// دکمهٔ «کنترل صوتی» در هدر مراجعه است تا زمینهٔ کار روشن باشد: هر فرمانی که
// زده می‌شود روی تصاویرِ همان مراجعه اعمال می‌شود. فقط برای دندانپزشک و مدیر
// سیستم دیده می‌شود (منشی آن را نمی‌بیند)؛ منشی مراجعه را باز می‌کند و دکتر
// دست نزده کار می‌کند.
//
// شرط مرورگر: Web Speech API (کروم/اِج). اگر نبود، پیام فارسی می‌دهیم.
(function () {
  "use strict";

  const $ = (id) => document.getElementById(id);
  const SILENCE_STOP_MS = 30000;      // بعد از این همه سکوت، گوش دادن خودکار قطع می‌شود

  let recognition = null;
  let listening = false;
  let lastSpeechAt = 0;
  let silenceTimer = null;
  let lastVisit = null;              // آخرین مراجعهٔ شناخته‌شده (اگر انتخابش پاک شد)
  let finalSeen = 0;                 // چند نتیجهٔ قطعی قبلاً اجرا شده (جلوگیری از تکرار)
  let lastHeard = "";
  let lastHeardAt = 0;

  // بعد از رفرش صفحه، مراجعهٔ باز از یاد نرود.
  const VISIT_KEY = "dentix-last-visit";
  function rememberVisit(v) {
    try { if (v && v.studyID) sessionStorage.setItem(VISIT_KEY, JSON.stringify(v)); } catch (e) { /* بی‌اثر */ }
  }
  function recallVisit() {
    try { const s = sessionStorage.getItem(VISIT_KEY); return s ? JSON.parse(s) : null; } catch (e) { return null; }
  }
  lastVisit = recallVisit();

  // یک عبارت قطعی در چند صدا (یا دو بار رسیدن) نباید دو بار اجرا شود.
  function handle(text) {
    if (!text) return;
    if (text === lastHeard && Date.now() - lastHeardAt < 2500) return;
    lastHeard = text; lastHeardAt = Date.now();
    resetSilence();
    const done = runCommand(text);
    if (!done) setStatus(`شنیده شد: ${text}`, null);
  }

  /* ---------------- نقش کاربر: فقط دندانپزشک یا مدیر ---------------- */
  function truthyFlag(v) { return v === true || v === "true" || v === 1 || v === "1"; }

  function allowedUser() {
    const u = window.dentalRayCurrentUser;
    if (!u) return false;                                   // ورود نشده
    if (truthyFlag(u.isSuperAdmin) || truthyFlag(u.IsSuperAdmin)) return true;
    if (Number(u.staffType) === 2) return true;             // 2 = دندانپزشک
    // اگر نقش کاملاً معلوم نیست (بعضی مسیرهای ورود فیلد را برنمی‌گردانند) ولی
    // کاربر وارد شده، دکمه را نشان می‌دهیم؛ پنهان ماندن دکمه برای مدیر از هر
    // اشتباهی بدتر است. فقط وقتی صریحاً غیردندانپزشک باشد مخفی می‌ماند.
    const hasRole = u.staffType !== undefined && u.staffType !== null && u.staffType !== "";
    if (hasRole) return false;                              // کارمند/منشی
    return true;
  }

  function supportedBrowser() {
    return !!(window.SpeechRecognition || window.webkitSpeechRecognition);
  }

  /* ---------------- وضعیت روی صفحه ---------------- */
  // هم در هدر مراجعه و هم داخل بینندهٔ تصویر، چون بیننده تمام‌صفحه است و
  // هدر را می‌پوشاند؛ بدون این، کاربر فکر می‌کند فرمان اصلاً اجرا نشده است.
  function setStatus(text, state) {
    ["voiceStatus", "voiceStatusViewer"].forEach((id) => {
      const el = $(id);
      if (!el) return;
      el.classList.remove("hidden", "is-listening", "is-error");
      if (state) el.classList.add(state);
      el.textContent = text || "";
      if (!text) el.classList.add("hidden");
    });
  }

  function currentVisit() {
    const v = (typeof selectedStudy === "object" && selectedStudy) ? selectedStudy : (window.selectedStudy || null);
    if (v) { lastVisit = v; rememberVisit(v); return v; }
    // انتخاب مراجعه ممکن است وسط کار پاک شود (مثلاً بعد از رفرش پرونده) در حالی
    // که کاربر هنوز داخل همان مراجعه است؛ به آخرین مراجعهٔ شناخته‌شده برمی‌گردیم.
    return lastVisit;
  }

  /* ---------------- فهرست تصاویر همین مراجعه ---------------- */
  // اول از شبکهٔ تصاویرِ روی صفحه خوانده می‌شود (سریع). اگر خالی بود — مثلاً
  // وقتی تصویر از پنجرهٔ «الصاق تصویر» باز شده — از سرور گرفته می‌شود تا
  // پیام غلط «تصویری در این مراجعه نیست» ندهد.
  function scanGallery() {
    const grid = $("studyDetailsImagesGrid");
    if (!grid) return [];
    const list = [];
    grid.querySelectorAll(".image-card").forEach((card) => {
      const img = card.querySelector("img[src*='/api/radiologyimages/']");
      const pdf = card.querySelector(".pdf-thumbnail");
      const m = img ? (img.getAttribute("src").match(/radiologyimages\/(\d+)/) || []) : [];
      if (!m[1] && !pdf) return;
      const title = card.querySelector(".image-card-title");
      list.push({
        imageID: Number(m[1] || 0),
        contentType: pdf ? "application/pdf" : "image/jpeg",
        fileName: title ? title.textContent : "تصویر",
        imageTypeName: null
      });
    });
    return list;
  }

  // خروجی: آرایه = فهرست | null = مراجعه‌ای شناخته نشد | undefined = دریافت ناموفق
  async function visitGallery() {
    const dom = scanGallery();
    if (dom.length) return dom;
    let visit = currentVisit();
    if (!visit) visit = await visitFromImage();      // از خودِ تصویرِ باز، مراجعه را پیدا می‌کنیم
    if (!visit) return null;
    try {
      const r = await fetch(`/api/radiologyimages/study/${visit.studyID}`, { cache: "no-store" });
      if (!r.ok) return undefined;
      const x = await r.json();
      return (x.images || []).map((i) => ({
        imageID: i.imageID,
        contentType: i.contentType || "image/jpeg",
        fileName: i.fileName,
        imageTypeName: i.imageTypeName || null
      }));
    } catch (e) { return undefined; }
  }

  // آخرین راه: از تصویرِ باز شده بپرسیم به کدام مراجعه‌ها وصل است.
  async function visitFromImage() {
    const el = $("largeImage");
    const m = el && el.src ? (el.src.match(/radiologyimages\/(\d+)/) || []) : [];
    if (!m[1]) return null;
    try {
      const r = await fetch(`/api/radiologyimages/${m[1]}/studies`, { cache: "no-store" });
      if (!r.ok) return null;
      const x = await r.json();
      const list = x.studies || [];
      if (!list.length) return null;
      const chosen = (lastVisit && list.find((s) => s.studyID === lastVisit.studyID)) || list[0];
      lastVisit = chosen;
      rememberVisit(chosen);
      return chosen;
    } catch (e) { return null; }
  }

  function currentIndex(list) {
    const el = $("largeImage");
    const m = el && el.src ? (el.src.match(/radiologyimages\/(\d+)/) || []) : [];
    if (!m[1]) return -1;
    return list.findIndex((x) => x.imageID === Number(m[1]));
  }

  function viewerOpen() {
    const m = $("imageModal");
    return !!(m && !m.classList.contains("hidden"));
  }

  function openAt(list, index) {
    const i = Math.max(0, Math.min(list.length - 1, index));
    if (typeof window.openLargeImage === "function") window.openLargeImage(list[i]);
    setStatus(`تصویر ${i + 1} از ${list.length}`, "is-listening");
  }

  function galleryError(list) {
    if (list === null) setStatus("ابتدا یک مراجعه را باز کنید.", "is-error");
    else if (list === undefined) setStatus("فهرست تصاویر دریافت نشد.", "is-error");
    else setStatus("تصویری در این مراجعه نیست.", "is-error");
  }

  async function goNext() {
    const list = await visitGallery();
    if (!Array.isArray(list) || !list.length) { galleryError(list); return; }
    const i = currentIndex(list);
    if (i < 0) { openAt(list, 0); return; }
    if (i >= list.length - 1) { setStatus("آخرین تصویر است.", "is-listening"); return; }
    openAt(list, i + 1);
  }

  async function goPrev() {
    const list = await visitGallery();
    if (!Array.isArray(list) || !list.length) { galleryError(list); return; }
    const i = currentIndex(list);
    if (i < 0) { openAt(list, list.length - 1); return; }
    if (i <= 0) { setStatus("اولین تصویر است.", "is-listening"); return; }
    openAt(list, i - 1);
  }

  /* ---------------- عملیات بیننده (با کلیک روی دکمه‌های خودش) ---------------- */
  function clickViewer(id) { const b = $(id); if (b) { b.click(); return true; } return false; }

  function pan(dx, dy) {
    // imageViewX/Y و applyImageView در سطح سراسریِ app.js تعریف شده‌اند.
    try {
      if (typeof imageViewX === "undefined" || typeof applyImageView !== "function") return false;
      imageViewX += dx; imageViewY += dy;
      applyImageView();
      return true;
    } catch (e) { return false; }
  }

  async function ensureViewer() {
    if (viewerOpen()) return true;
    const list = await visitGallery();
    if (!Array.isArray(list) || !list.length) { galleryError(list); return false; }
    openAt(list, 0);
    return true;
  }

  /* ---------------- تشخیص فرمان از متن شنیده‌شده ---------------- */
  function normalize(s) {
    return String(s || "")
      .replace(/[يئ]/g, "ی")
      .replace(/ك/g, "ک")
      .replace(/[ًٌٍَُِْـ]/g, "")
      .replace(/\u200c/g, " ")
      .replace(/\s+/g, " ")
      .trim()
      .toLowerCase();
  }

  const zoom = () => ensureViewer().then((ok) => ok && clickViewer("zoomInImageButton"));
  const zoomOut = () => ensureViewer().then((ok) => ok && clickViewer("zoomOutImageButton"));
  const reset = () => ensureViewer().then((ok) => ok && clickViewer("resetImageViewButton"));
  const rotate = () => ensureViewer().then((ok) => ok && clickViewer("rotateLeftImageButton"));
  const rotateRight = () => ensureViewer().then((ok) => ok && clickViewer("rotateRightImageButton"));
  const move = (dx, dy) => ensureViewer().then((ok) => ok && pan(dx, dy));

  // ترتیب مهم است: هر فرمان پیش از همسایه‌هایش خوانده می‌شود.
  // selfStatus = خودِ تابع پیام را روی صفحه می‌گذارد.
  const RULES = [
    { re: /(ببند|بستن|ببندش|\bclose\b)/, label: "بستن تصویر", act: () => { if (typeof window.closeLargeImage === "function") window.closeLargeImage(); } },
    { re: /(صد\s*در\s*صد|صددرصد|اندازه اصلی|اصلي|اصلی|\breset\b)/, label: "اندازهٔ اصلی", act: reset },
    { re: /(بزرگ\s*تر|بزرگ\s*نمایی|زوم\s*(in|این)|zoom\s*in)/, label: "بزرگ‌نمایی", act: zoom },
    { re: /(کوچک\s*تر|کوچک\s*نمایی|زوم\s*(out|اوت)|zoom\s*out)/, label: "کوچک‌نمایی", act: zoomOut },
    { re: /(چرخش به راست|بچرخان به راست|راست بچرخان|rotate right)/, label: "چرخش به راست", act: rotateRight },
    { re: /(چرخش به چپ|بچرخان به چپ|چپ بچرخان|چرخش|بچرخان|\brotate left\b)/, label: "چرخش به چپ", act: rotate },
    { re: /(تصویر بعدی|بعدی|عکس بعدی|\bnext\b)/, label: "تصویر بعدی", act: goNext, selfStatus: true },
    { re: /(تصویر قبلی|قبلی|عکس قبلی|\bprevious\b|\bprev\b)/, label: "تصویر قبلی", act: goPrev, selfStatus: true },
    { re: /(^|\s)بالا(تر)?(\s|$| کن)/, label: "بالا", act: () => move(0, -70) },
    { re: /(^|\s)پایین(تر)?(\s|$| کن)/, label: "پایین", act: () => move(0, 70) },
    { re: /(^|\s)چپ(تر)?(\s|$| کن)/, label: "چپ", act: () => move(-70, 0) },
    { re: /(^|\s)راست(تر)?(\s|$| کن)/, label: "راست", act: () => move(70, 0) }
  ];

  function runCommand(raw) {
    const text = normalize(raw);
    if (!text) return false;
    for (const rule of RULES) {
      if (!rule.re.test(text)) continue;
      let out;
      try { out = rule.act(); } catch (e) { setStatus("انجام نشد: " + rule.label, "is-error"); return true; }
      if (out && typeof out.then === "function") {
        if (!rule.selfStatus) {
          out.then(() => { if (!rule.selfStatus) setStatus(`انجام شد: ${rule.label}`, "is-listening"); })
             .catch(() => setStatus("انجام نشد: " + rule.label, "is-error"));
        }
      } else if (!rule.selfStatus) {
        setStatus(`انجام شد: ${rule.label}`, "is-listening");
      }
      return true;
    }
    return false;
  }

  /* ---------------- چرخهٔ گوش دادن ---------------- */
  function resetSilence() {
    lastSpeechAt = Date.now();
    if (silenceTimer) clearTimeout(silenceTimer);
    silenceTimer = setTimeout(() => {
      if (!listening) return;
      // تا وقتی بینندهٔ تصویر باز است، بی‌صدا هم گوش می‌دهیم؛ دکتر ممکن است
      // ثانیه‌ها به تصویر نگاه کند بعد بگوید «بعدی». قطع فقط با دکمه/خروج است.
      if (viewerOpen()) { resetSilence(); return; }
      stop(true);
    }, SILENCE_STOP_MS);
  }

  function start() {
    if (!supportedBrowser()) {
      setStatus("این مرورگر تشخیص گفتار ندارد؛ از کروم یا اِج استفاده کنید.", "is-error");
      return;
    }
    const visit = currentVisit();
    const label = visit ? `مراجعهٔ شماره ${visit.studyID}` : "این مراجعه";

    if (!recognition) {
      const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
      recognition = new SR();
      recognition.lang = "fa-IR";
      recognition.continuous = true;
      recognition.interimResults = true;

      recognition.onresult = (event) => {
        let interim = "";
        for (let i = event.resultIndex; i < event.results.length; i++) {
          const res = event.results[i];
          const t = res && res[0] ? (res[0].transcript || "").trim() : "";
          if (!t) continue;
          if (res.isFinal) {
            // فقط نتیجهٔ قطعی اجرا می‌شود؛ اجرای هم‌زمان روی متن موقت باعث
            // می‌شد هر فرمان دو بار انجام شود.
            if (i < finalSeen) continue;
            finalSeen = i + 1;
            handle(t);
          } else {
            interim += t;
          }
        }
        // متن موقت فقط نمایش داده می‌شود تا بدانید چه شنیده شده است.
        if (interim) setStatus(`شنیده شد: ${interim}`, null);
      };

      recognition.onerror = (e) => {
        if (e.error === "no-speech" || e.error === "aborted") return;   // عادی است
        if (e.error === "not-allowed" || e.error === "service-not-allowed") {
          stop(false);
          setStatus("دسترسی به میکروفن داده نشده؛ از نوار آدرس مرورگر اجازه بدهید.", "is-error");
          return;
        }
        if (e.error === "network") {
          stop(false);
          setStatus("تشخیص گفتار به اینترنت نیاز دارد؛ اتصال را بررسی کنید.", "is-error");
          return;
        }
        setStatus("خطا: " + e.error, "is-error");
      };

      // کروم بعد از سکوت خودش قطع می‌کند؛ اگر کاربر نخواسته، دوباره وصل می‌کنیم.
      recognition.onend = () => {
        if (listening) {
          try { recognition.start(); } catch (e) { /* هم‌زمان اجرا شده */ }
        }
      };
    }

    try { recognition.start(); } catch (e) { /* قبلاً شروع شده */ }
    finalSeen = 0;
    lastHeard = "";
    listening = true;
    resetSilence();
    setStatus(`در حال گوش دادن — ${label}`, "is-listening");
  }

  function stop(bySilence) {
    listening = false;
    if (silenceTimer) { clearTimeout(silenceTimer); silenceTimer = null; }
    try { recognition && recognition.stop(); } catch (e) { /* بی‌اثر */ }
    setStatus("", null);
    if (bySilence) setStatus("به دلیل سکوت، گوش دادن قطع شد.", null);
  }

  function toggle() { if (listening) stop(false); else start(); }

  /* ---------------- دکمه‌های قبلی/بعدی در نوار بیننده ---------------- */
  function wireViewerNav() {
    const prev = $("prevImageButton"), next = $("nextImageButton");
    if (prev && !prev.dataset.wired) { prev.dataset.wired = "1"; prev.onclick = goPrev; }
    if (next && !next.dataset.wired) { next.dataset.wired = "1"; next.onclick = goNext; }
  }

  // صوت فقط برای دندانپزشک/مدیر فعال می‌شود؛ اگر نقش مجاز نباشد، هیچ‌وقت
  // گوش دادن خودکار شروع نمی‌شود.
  function refreshVisibility() {
    const ok = allowedUser();
    if (!ok && listening) stop(false);
    if (!ok) setStatus("", null);
    console.log("[Dentix صدا]", { allowed: ok, listening: listening, user: window.dentalRayCurrentUser || null });
  }

  // اگر کاربر از هر دو بخش مراجعه خارج شد، گوش دادن قطع شود تا فرمانی روی
  // مراجعهٔ دیگر اشتباه اعمال نشود. «تصاویر این مراجعه» صفحهٔ جداگانه‌ای است و
  // هدر مراجعه را مخفی می‌کند؛ پس نباید به تنهایی باعث قطع شدن شود.
  function anyVisitVisible() {
    const details = $("studyDetailsSection");
    const images = $("studyImagesSection");
    return (details && !details.classList.contains("hidden")) ||
           (images && !images.classList.contains("hidden"));
  }

  function watchStudySection() {
    const host = $("studyDetailsSection");
    if (!host || host.dataset.voiceWatched) return;
    host.dataset.voiceWatched = "1";
    const obs = new MutationObserver(() => {
      if (!anyVisitVisible() && listening) stop(false);
    });
    [host, $("studyImagesSection")].filter(Boolean).forEach((el) => {
      obs.observe(el, { attributes: true, attributeFilter: ["class"] });
    });
  }

  // ورود به بیننده = شروع خودکار گوش دادن (بدون هیچ دکمه‌ای)؛ خروج = توقف.
  // فقط برای دندانپزشک/مدیر.
  function watchImageViewer() {
    const modal = $("imageModal");
    if (!modal || modal.dataset.voiceWatched) return;
    modal.dataset.voiceWatched = "1";
    const obs = new MutationObserver(async () => {
      if (modal.classList.contains("hidden")) {
        if (listening) stop(false);
        setStatus("", null);
        return;
      }
      if (allowedUser() && !listening) start();
      const list = await visitGallery();
      if (Array.isArray(list) && list.length) {
        const i = currentIndex(list);
        if (i >= 0) setStatus(`تصویر ${i + 1} از ${list.length}`, "is-listening");
      }
    });
    obs.observe(modal, { attributes: true, attributeFilter: ["class"] });
  }

  function init() {
    refreshVisibility();
    wireViewerNav();
    watchStudySection();
    watchImageViewer();
    // هر لحظه که برنامه مراجعه‌ای را انتخاب کرد به‌خاطر می‌سپاریم؛ چون انتخاب
    // ممکن است وسط کار پاک شود (رفرش پرونده) و بدون این، «بعدی» با پیام غلط
    // «مراجعه را انتخاب کن» مواجه می‌شود.
    setInterval(() => {
      const v = (typeof selectedStudy === "object" && selectedStudy) ? selectedStudy : (window.selectedStudy || null);
      if (v && v.studyID && (!lastVisit || lastVisit.studyID !== v.studyID)) {
        lastVisit = v;
        rememberVisit(v);
      }
    }, 1000);
    // ورود ممکن است بعد از بارگذاری صفحه تمام شود؛ چند بار دیگر هم چک می‌کنیم.
    [400, 1500, 4000].forEach((ms) => setTimeout(refreshVisibility, ms));
  }

  window.addEventListener("dentalray-auth-changed", refreshVisibility);

  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
  else init();

  // پنجرهٔ تصویر ممکن است بعداً ساخته شود؛ دکمه‌های قبلی/بعدی را هر چند لحظه وصل می‌کنیم.
  setInterval(wireViewerNav, 1500);

  window.DentalRayVoice = {
    start, stop, isListening: () => listening, goNext, goPrev,
    runCommand, visitGallery, handle
  };
})();
