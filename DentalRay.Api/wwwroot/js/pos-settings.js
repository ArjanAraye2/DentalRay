// Dentix POS (card reader) settings.
//
// Terminals differ by vendor, so the connection details and the exact text sent
// to the device are configuration. A new model can usually be supported by
// filling this form instead of changing code.
(() => {
  "use strict";
  const $ = id => document.getElementById(id);
  const api = async (url, options) => {
    const r = await fetch(url, options);
    let x = {};
    try { x = await r.json(); } catch { }
    if (!r.ok || x.success === false) throw new Error(x.message || `خطای پوز (HTTP ${r.status})`);
    return x;
  };
  const money = v => Number(v || 0).toLocaleString("fa-IR");

  const CONNECTION_TYPES = [
    { value: 1, label: "شبکه‌ای (TCP/IP)" },
    { value: 2, label: "سریال (COM)" },
    { value: 3, label: "USB / PC-POS" },
    { value: 4, label: "سرویس ابری" }
  ];
  const typeLabel = v => (CONNECTION_TYPES.find(t => t.value === Number(v)) || {}).label || "-";

  let editing = null;
  let cache = [];

  function buildSection() {
    const main = document.querySelector(".page-container");
    if (!main || $("posSettingsCard")) return;

    const card = document.createElement("section");
    card.id = "posSettingsCard";
    card.className = "network-settings-card hidden";
    card.innerHTML = `
      <div class="dashboard-panel-title">
        <strong>دستگاه پوز (کارتخوان)</strong>
        <span>ارسال مبلغ دریافت به پوز</span>
      </div>

      <div class="pos-note">
        تنظیمات این بخش فقط توسط مدیر سیستم قابل تغییر است. برای پوزهای شبکه‌ای،
        آدرس IP و پورت دستگاه را وارد کنید. <strong>دکمه «تست اتصال»</strong> مشخص می‌کند
        پوز در شبکه در دسترس است یا نه.
      </div>

      <div id="posList" class="pos-list"></div>

      <div class="pos-actions">
        <button type="button" id="posNewButton">+ افزودن پوز</button>
        <button type="button" id="posRefreshButton" class="secondary-button">به‌روزرسانی</button>
      </div>

      <form id="posForm" class="hidden">
        <h6 id="posFormTitle">افزودن پوز</h6>
        <div class="pos-form-grid">
          <div class="form-field">
            <label for="posName">نام دستگاه</label>
            <input id="posName" type="text" maxlength="100" placeholder="مثال: پوز پذیرش" required />
          </div>
          <div class="form-field">
            <label for="posConnectionType">نوع اتصال</label>
            <select id="posConnectionType"></select>
          </div>
          <div class="form-field" data-when="tcp">
            <label for="posHost">آدرس IP</label>
            <input id="posHost" type="text" maxlength="100" placeholder="192.168.1.50" />
          </div>
          <div class="form-field" data-when="tcp">
            <label for="posPort">پورت</label>
            <input id="posPort" type="number" min="1" max="65535" placeholder="9100" />
          </div>
          <div class="form-field" data-when="serial">
            <label for="posComPort">پورت COM</label>
            <input id="posComPort" type="text" maxlength="20" placeholder="COM3" />
          </div>
          <div class="form-field" data-when="serial">
            <label for="posBaudRate">نرخ باود</label>
            <input id="posBaudRate" type="number" min="1200" max="115200" placeholder="9600" />
          </div>
          <div class="form-field">
            <label for="posProtocol">پروتکل</label>
            <select id="posProtocol"></select>
            <small class="field-hint" id="posProtocolHint"></small>
          </div>
          <div class="form-field">
            <label for="posTimeout">مهلت انتظار (ثانیه)</label>
            <input id="posTimeout" type="number" min="1" max="60" value="5" />
          </div>
          <div class="form-field full-width">
            <label for="posRequestPattern">الگوی درخواست</label>
            <input id="posRequestPattern" type="text" maxlength="500" dir="ltr" placeholder="SALE|{amount}|{invoice}{newline}" />
            <small class="field-hint">
              جایگزین‌ها: <code>{amount}</code> مبلغ با دو رقم اعشار · <code>{amountRaw}</code> مبلغ بدون اعشار ·
              <code>{invoice}</code> شماره پرداخت · <code>{newline}</code> خط جدید
            </small>
          </div>
          <div class="form-field full-width">
            <label for="posSuccessPattern">متن تأیید در پاسخ پوز</label>
            <input id="posSuccessPattern" type="text" maxlength="200" dir="ltr" placeholder="APPROVED" />
            <small class="field-hint">اگر خالی باشد، هر پاسخی موفق در نظر گرفته می‌شود.</small>
          </div>
          <div class="form-field">
            <label for="posEncoding">کدگذاری</label>
            <select id="posEncoding"><option value="UTF8">UTF8</option><option value="ASCII">ASCII</option></select>
          </div>
          <div class="form-field pos-toggles">
            <label class="checkbox-row"><input id="posIsActive" type="checkbox" checked /><span>فعال</span></label>
            <label class="checkbox-row"><input id="posIsDefault" type="checkbox" /><span>پوز پیش‌فرض</span></label>
          </div>
        </div>
        <div id="posFormStatus" class="status-message"></div>
        <div class="form-actions">
          <button type="submit">ذخیره</button>
          <button type="button" id="posCancelButton" class="secondary-button">انصراف</button>
        </div>
      </form>

      <div id="posStatus" class="status-message"></div>
    `;
    main.appendChild(card);
    return card;
  }

  function fillSelects() {
    const conn = $("posConnectionType");
    conn.innerHTML = CONNECTION_TYPES.map(t => `<option value="${t.value}">${t.label}</option>`).join("");
    conn.onchange = () => applyConnectionVisibility();
  }

  function applyConnectionVisibility() {
    const type = Number($("posConnectionType").value || 1);
    $("posForm").querySelectorAll("[data-when]").forEach(el => {
      const when = el.dataset.when;
      const show = (when === "tcp" && type === 1) || (when === "serial" && type === 2);
      el.classList.toggle("hidden", !show);
    });
  }

  function setStatus(message, error) {
    const s = $("posStatus");
    if (!s) return;
    s.textContent = message || "";
    s.classList.toggle("error", !!error);
  }
  function setFormStatus(message, error) {
    const s = $("posFormStatus");
    if (!s) return;
    s.textContent = message || "";
    s.classList.toggle("error", !!error);
  }

  function renderList() {
    const list = $("posList");
    list.replaceChildren();
    if (!cache.length) {
      list.innerHTML = '<div class="pos-empty">هنوز پوزی ثبت نشده است.</div>';
      return;
    }
    cache.forEach(row => {
      const item = document.createElement("div");
      item.className = "pos-item" + (row.isDefault ? " is-default" : "");

      const head = document.createElement("div");
      head.className = "pos-item-head";
      const title = document.createElement("strong");
      title.textContent = row.name;
      head.appendChild(title);

      const badges = document.createElement("span");
      badges.className = "pos-badges";
      if (row.isDefault) badges.innerHTML += '<span class="pos-badge default">پیش‌فرض</span>';
      if (!row.isActive) badges.innerHTML += '<span class="pos-badge off">غیرفعال</span>';
      head.appendChild(badges);
      item.appendChild(head);

      const meta = document.createElement("div");
      meta.className = "pos-item-meta";
      const target = Number(row.connectionType) === 1
        ? `${row.host || "-"}:${row.port || "-"}`
        : typeLabel(row.connectionType);
      meta.innerHTML = `
        <span>اتصال: ${typeLabel(row.connectionType)}</span>
        <span>مقصد: <bdi dir="ltr">${escapeHtml(target)}</bdi></span>
        <span>پروتکل: ${escapeHtml(row.protocol || "Generic")}</span>
      `;
      if (row.lastTestAt) {
        meta.innerHTML += `<span class="pos-test-result ${row.lastTestMessage && /نشد|خطا|ناموفق/.test(row.lastTestMessage) ? "err" : "ok"}">
          آخرین تست: ${escapeHtml(formatDate(row.lastTestAt))}
        </span>`;
      }
      item.appendChild(meta);

      const actions = document.createElement("div");
      actions.className = "pos-item-actions";
      const test = document.createElement("button");
      test.type = "button"; test.className = "secondary-button"; test.textContent = "تست اتصال";
      test.onclick = () => runTest(row, test);
      const edit = document.createElement("button");
      edit.type = "button"; edit.className = "secondary-button"; edit.textContent = "ویرایش";
      edit.onclick = () => openForm(row);
      const del = document.createElement("button");
      del.type = "button"; del.className = "danger-button"; del.textContent = "حذف";
      del.onclick = () => remove(row);
      actions.append(test, edit, del);
      item.appendChild(actions);

      list.appendChild(item);
    });
  }

  const escapeHtml = value => String(value ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
  const formatDate = value => { try { return new Intl.DateTimeFormat("fa-IR", { dateStyle: "short", timeStyle: "short" }).format(new Date(value)); } catch { return value; } };

  async function load() {
    try {
      setStatus("در حال دریافت تنظیمات...", false);
      const x = await api("/api/pos/settings");
      cache = x.settings || [];
      renderList();
      setStatus("", false);
    } catch (e) { setStatus(e.message, true); }
  }

  async function loadProtocols() {
    try {
      const x = await api("/api/pos/protocols");
      const sel = $("posProtocol");
      sel.innerHTML = (x.protocols || []).map(p => `<option value="${escapeHtml(p.name)}">${escapeHtml(p.name)} — ${escapeHtml(p.description)}</option>`).join("");
      const update = () => {
        const found = (x.protocols || []).find(p => p.name === sel.value);
        $("posProtocolHint").textContent = found ? found.description : "";
      };
      sel.onchange = update; update();
    } catch { /* the list still works without the protocol descriptions */ }
  }

  function openForm(row) {
    editing = row || null;
    $("posFormTitle").textContent = row ? "ویرایش پوز" : "افزودن پوز";
    $("posName").value = row?.name || "";
    $("posConnectionType").value = String(row?.connectionType || 1);
    $("posHost").value = row?.host || "";
    $("posPort").value = row?.port ?? "";
    $("posComPort").value = row?.comPort || "";
    $("posBaudRate").value = row?.baudRate ?? "";
    if (row?.protocol) $("posProtocol").value = row.protocol;
    $("posTimeout").value = row?.timeoutSeconds ?? 5;
    $("posRequestPattern").value = row?.requestPattern || "";
    $("posSuccessPattern").value = row?.successPattern || "";
    $("posEncoding").value = row?.encoding || "UTF8";
    $("posIsActive").checked = row ? !!row.isActive : true;
    $("posIsDefault").checked = row ? !!row.isDefault : false;
    setFormStatus("", false);
    applyConnectionVisibility();
    $("posForm").classList.remove("hidden");
    $("posName").focus();
  }

  function closeForm() {
    editing = null;
    $("posForm").classList.add("hidden");
    setFormStatus("", false);
  }

  function payload() {
    const type = Number($("posConnectionType").value || 1);
    return {
      name: $("posName").value.trim(),
      connectionType: type,
      host: type === 1 ? ($("posHost").value.trim() || null) : null,
      port: type === 1 ? (Number($("posPort").value) || null) : null,
      comPort: type === 2 ? ($("posComPort").value.trim() || null) : null,
      baudRate: type === 2 ? (Number($("posBaudRate").value) || null) : null,
      protocol: $("posProtocol").value || "Generic",
      requestPattern: $("posRequestPattern").value.trim() || null,
      successPattern: $("posSuccessPattern").value.trim() || null,
      encoding: $("posEncoding").value,
      timeoutSeconds: Number($("posTimeout").value) || 5,
      isActive: $("posIsActive").checked,
      isDefault: $("posIsDefault").checked
    };
  }

  async function save(event) {
    event.preventDefault();
    try {
      const body = payload();
      if (!body.name) throw new Error("نام دستگاه را وارد کنید.");
      setFormStatus("در حال ذخیره...", false);
      await api(editing ? `/api/pos/settings/${editing.posSettingID}` : "/api/pos/settings",
        { method: editing ? "PUT" : "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
      closeForm();
      await load();
      setStatus("تنظیمات ذخیره شد.", false);
    } catch (e) { setFormStatus(e.message, true); }
  }

  async function runTest(row, button) {
    const original = button.textContent;
    button.disabled = true; button.textContent = "در حال تست...";
    setStatus("", false);
    try {
      const x = await api(`/api/pos/settings/${row.posSettingID}/test`, { method: "POST" });
      setStatus(`${row.name}: ${x.message}${x.raw ? " — پاسخ پوز: " + x.raw : ""}`, !x.ok);
      await load();
    } catch (e) { setStatus(`${row.name}: ${e.message}`, true); }
    finally { button.disabled = false; button.textContent = original; }
  }

  async function remove(row) {
    if (!confirm(`پوز «${row.name}» حذف شود؟`)) return;
    try {
      await api(`/api/pos/settings/${row.posSettingID}`, { method: "DELETE" });
      await load();
      setStatus("پوز حذف شد.", false);
    } catch (e) { setStatus(e.message, true); }
  }

  function init() {
    buildSection();
    if (!$("posSettingsCard")) return;
    fillSelects();
    loadProtocols();
    $("posNewButton").onclick = () => openForm(null);
    $("posRefreshButton").onclick = () => load();
    $("posCancelButton").onclick = closeForm;
    $("posForm").onsubmit = save;
    load();
  }

  window.DentalRayPosSettings = { show() { $("posSettingsCard")?.classList.remove("hidden"); load(); } };
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init); else init();
})();
