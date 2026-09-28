// ============================================================
// DentalRay - کارتِ پشتیبان در تنظیمات (فقط مدیرِ سیستم)
// ============================================================
// وضعیتِ پشتیبانِ خودکار را نشان می‌دهد، اجرای دستی می‌دهد و اجازه می‌دهد
// مسیرِ ذخیره و تعدادِ نسخه را همان‌جا عوض کنید. تغییرِ مسیر در فایلِ کانفیگ
// نوشته می‌شود و چون کانفیگ با reloadOnChange بارگذاری شده، بدونِ ریستارت
// اعمال می‌شود.
(() => {
  const settings = document.getElementById("settingsSection");
  if (!settings) return;

  const isSuper = () => window.dentalRayCurrentUser?.isSuperAdmin === true;
  const $ = id => document.getElementById(id);

  const card = document.createElement("section");
  card.id = "backupSettingsCard";
  card.className = "network-settings-card hidden";
  card.innerHTML = `
    <div class="dashboard-panel-title">
      <strong>پشتیبان‌گیری خودکار</strong>
      <span id="backupBadge" class="backup-badge">در حال بررسی...</span>
    </div>
    <div class="backup-status-grid">
      <div><span>آخرین اجرا</span><strong id="backupLastRun">—</strong></div>
      <div><span>نسخه‌های دیتابیس</span><strong id="backupDbCount">—</strong></div>
      <div><span>حجمِ تصاویر در پشتیبان</span><strong id="backupImagesSize">—</strong></div>
      <div><span>زمانِ بعدی</span><strong id="backupDue">—</strong></div>
    </div>
    <p id="backupError" class="field-hint backup-note-error hidden"></p>
    <p id="backupWarning" class="field-hint backup-note-warning hidden"></p>
    <div class="backup-actions">
      <button type="button" id="backupRunNow">الان پشتیبان بگیر</button>
      <button type="button" id="backupRefresh" class="secondary-button">تازه‌سازی</button>
    </div>
    <div class="backup-settings-row">
      <div class="form-field"><label for="backupRootPath">مسیرِ ذخیره</label><input id="backupRootPath" type="text" dir="ltr" placeholder="D:\\DentalRayBackup" /></div>
      <div class="form-field backup-keep"><label for="backupKeep">تعدادِ نسخهٔ دیتابیس</label><input id="backupKeep" type="number" min="1" max="100" /></div>
      <button type="button" id="backupSave" class="secondary-button">ذخیرهٔ تنظیمات</button>
    </div>
    <p id="backupMessage" class="status-message"></p>`;
  settings.appendChild(card);

  const sizeText = value => {
    const n = Number(value);
    if (!Number.isFinite(n)) return "—";
    if (n >= 1073741824) return `${(n / 1073741824).toLocaleString("fa-IR", { maximumFractionDigits: 1 })} گیگابایت`;
    if (n >= 1048576) return `${(n / 1048576).toLocaleString("fa-IR", { maximumFractionDigits: 1 })} مگابایت`;
    return `${Math.round(n / 1024).toLocaleString("fa-IR")} کیلوبایت`;
  };

  let loaded = false;

  async function load(silent = false) {
    if (!isSuper()) return;
    const badge = $("backupBadge");
    if (!silent) badge.textContent = "در حال بررسی...";
    try {
      const r = await fetch("/api/backup/status", { cache: "no-store" });
      const x = await r.json().catch(() => ({}));
      if (!r.ok || x.success === false) throw new Error(x.message || "وضعیتِ پشتیبان دریافت نشد.");
      render(x.status || {});
      loaded = true;
    } catch (e) {
      badge.textContent = "نامشخص";
      setMessage(e.message || "وضعیتِ پشتیبان دریافت نشد.", true);
    }
  }

  function render(s) {
    const badge = $("backupBadge");
    if (s.running) { badge.textContent = "● در حال اجرا..."; badge.className = "backup-badge is-run"; }
    else if (s.lastOk && s.lastRun) { badge.textContent = "● سالم"; badge.className = "backup-badge is-ok"; }
    else if (s.lastRun) { badge.textContent = "● آخرین اجرا ناموفق بود"; badge.className = "backup-badge is-fail"; }
    else { badge.textContent = "● هنوز اجرا نشده"; badge.className = "backup-badge"; }

    $("backupLastRun").textContent = s.lastRun
      ? (typeof formatPersianDateTime === "function" ? formatPersianDateTime(s.lastRun) : String(s.lastRun))
      : "—";
    $("backupDbCount").textContent = Number.isFinite(Number(s.dbBackupCount))
      ? `${Number(s.dbBackupCount).toLocaleString("fa-IR")} نسخه`
      : "—";
    $("backupImagesSize").textContent = sizeText(s.imagesBytes);
    $("backupDue").textContent = s.running ? "همین حالا"
      : s.due ? "الان در صفِ اجراست" : "طبقِ برنامه (هر ساعت بررسی می‌شود)";

    toggle("backupError", s.lastOk === false && s.lastError ? `خطای آخرین اجرا: ${s.lastError}` : "");
    toggle("backupWarning", s.sameDriveWarning
      ? "⚠ مسیرِ پشتیبان همان درایوی است که تصاویر در آن هستند؛ اگر دیسک خراب شود هر دو با هم از بین می‌روند. مسیرِ فلش یا دیسکِ دیگری پیشنهاد می‌شود."
      : "");

    if (document.activeElement !== $("backupRootPath")) $("backupRootPath").value = s.rootPath || "";
    if (document.activeElement !== $("backupKeep")) $("backupKeep").value = s.keepBackups || 7;
    $("backupRunNow").disabled = !!s.running;
  }

  function toggle(id, text) {
    const el = $(id);
    el.textContent = text || "";
    el.classList.toggle("hidden", !text);
  }

  function setMessage(text, isError) {
    const el = $("backupMessage");
    el.textContent = text || "";
    el.classList.toggle("error", !!isError);
  }

  async function call(url, options, busyButton) {
    if (busyButton) busyButton.disabled = true;
    setMessage("در حال انجام...", false);
    try {
      const r = await fetch(url, options);
      const x = await r.json().catch(() => ({}));
      if (!r.ok || x.success === false) throw new Error(x.message || "انجام نشد.");
      if (x.status) render(x.status);
      return x;
    } catch (e) {
      setMessage(e.message || "انجام نشد.", true);
      throw e;
    } finally {
      if (busyButton) busyButton.disabled = false;
    }
  }

  $("backupRunNow").addEventListener("click", async () => {
    const button = $("backupRunNow");
    button.disabled = true;
    try {
      await call("/api/backup/run", { method: "POST" });
      setMessage("پشتیبان انجام شد.", false);
    } catch { /* پیامِ خطا در setMessage ثبت شد */ }
    finally { await load(true); }
  });

  $("backupRefresh").addEventListener("click", () => { setMessage("", false); load(); });

  $("backupSave").addEventListener("click", async () => {
    const rootPath = $("backupRootPath").value.trim();
    const keepBackups = Number($("backupKeep").value || 7);
    if (!rootPath) { setMessage("مسیرِ ذخیره را وارد کنید.", true); return; }
    try {
      await call("/api/backup/settings", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ rootPath, keepBackups })
      });
      setMessage("تنظیمات ذخیره شد؛ از همین حالا اعمال می‌شود.", false);
    } catch { /* پیامِ خطا در setMessage ثبت شد */ }
  });

  // کارت فقط برای مدیرِ سیستم دیده می‌شود و وضعیتش هنگامِ باز شدنِ تنظیمات تازه می‌شود.
  function sync() {
    card.classList.toggle("hidden", !isSuper());
    if (isSuper() && settings && !settings.classList.contains("hidden")) load(!loaded);
  }
  window.addEventListener("dentalray-auth-changed", sync);
  setTimeout(sync, 0);
  if (window.MutationObserver) {
    new MutationObserver(() => {
      if (isSuper() && !settings.classList.contains("hidden") && !card.classList.contains("hidden")) load(true);
    }).observe(settings, { attributes: true, attributeFilter: ["class"] });
  }

  window.DentalRayBackupSettings = { open: sync, refresh: () => load() };
})();
