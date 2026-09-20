// Dentix patient messaging.
//
// Adds an "ارسال پیامک" action to the patient record with ready templates and a
// history of what was already sent. Reception staff use this all day, so the flow
// is: open the dialog, pick a template, review the text, send.
(() => {
  "use strict";
  const $ = id => document.getElementById(id);
  const api = async (url, options) => {
    const r = await fetch(url, options);
    let x = {};
    try { x = await r.json(); } catch { }
    if (!r.ok || x.success === false) throw new Error(x.message || `خطای پیامک (HTTP ${r.status})`);
    return x;
  };
  const formatDate = v => { try { return new Intl.DateTimeFormat("fa-IR", { dateStyle: "short", timeStyle: "short" }).format(new Date(v)); } catch { return v; } };
  const escapeHtml = v => String(v ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

  let templates = [];

  async function loadTemplates() {
    if (templates.length) return templates;
    try { templates = (await api("/api/messages/templates")).templates || []; } catch { templates = []; }
    return templates;
  }

  function buildSection() {
    if ($("patientMessagesCard")) return;
    const card = document.createElement("section");
    card.id = "patientMessagesCard";
    card.className = "confirm-overlay hidden";
    card.innerHTML = `
      <div class="confirm-dialog message-dialog" role="dialog" aria-modal="true" aria-label="ارسال پیامک به بیمار">
        <h3>ارسال پیامک</h3>
        <p id="msgRecipient" class="message-recipient"></p>

        <div class="form-field">
          <label for="msgTemplate">قالب پیام</label>
          <select id="msgTemplate"></select>
        </div>

        <div class="form-field">
          <label for="msgBody">متن پیام</label>
          <textarea id="msgBody" rows="4" maxlength="2000"></textarea>
          <small class="field-hint"><span id="msgLength">0</span> / ۲۰۰۰</small>
        </div>

        <div class="form-field message-mobile-field">
          <label for="msgMobile">شماره موبایل</label>
          <input id="msgMobile" type="text" inputmode="tel" maxlength="30" />
        </div>

        <div id="msgStatus" class="status-message"></div>

        <div class="confirm-actions message-actions">
          <button type="button" id="msgSendButton">ارسال پیامک</button>
          <button type="button" id="msgCancelButton" class="secondary-button">انصراف</button>
        </div>

        <details class="message-history">
          <summary>تاریخچه پیام‌های این بیمار</summary>
          <div id="msgHistory" class="message-history-list"></div>
        </details>
      </div>`;
    document.body.appendChild(card);

    $("msgCancelButton").onclick = close;
    $("msgSendButton").onclick = send;
    $("msgTemplate").onchange = applyTemplate;
    $("msgBody").addEventListener("input", updateLength);
    card.addEventListener("click", e => { if (e.target === card) close(); });
  }

  function updateLength() {
    const n = $("msgBody").value.length;
    $("msgLength").textContent = String(n);
    $("msgLength").classList.toggle("over", n > 1900);
  }

  function applyTemplate() {
    const key = $("msgTemplate").value;
    const t = templates.find(x => x.key === key);
    if (!t) return;
    // The body keeps its placeholders so the server fills in the real date.
    $("msgBody").value = t.body || "";
    if (t.containsAmount) {
      setStatus("این قالب شامل مبلغ است؛ لطفاً پیش از ارسال متن را بررسی کنید.", false);
    } else setStatus("", false);
    updateLength();
  }

  function setStatus(message, error) {
    const s = $("msgStatus");
    s.textContent = message || "";
    s.classList.toggle("error", !!error);
  }

  async function open() {
    const patient = window.selectedPatient;
    if (!patient) { window.showToast?.("ابتدا یک بیمار را انتخاب کنید.", "error"); return; }
    buildSection();
    $("patientMessagesCard").classList.remove("hidden");
    $("msgRecipient").textContent = `${patient.firstName || ""} ${patient.lastName || ""}`.trim() || "-";
    $("msgMobile").value = patient.mobile || "";
    setStatus("", false);

    const list = await loadTemplates();
    $("msgTemplate").innerHTML = list.map(t =>
      `<option value="${escapeHtml(t.key)}">${escapeHtml(t.title)}${t.containsAmount ? " (شامل مبلغ)" : ""}</option>`).join("");

    const preferred = list.find(t => t.key === "appointment-reminder") || list[0];
    if (preferred) { $("msgTemplate").value = preferred.key; }
    applyTemplate();
    await loadHistory(patient.patientID);
  }

  async function loadHistory(patientID) {
    const box = $("msgHistory");
    box.textContent = "در حال دریافت...";
    try {
      const x = await api(`/api/patients/${patientID}/messages`);
      const items = x.messages || [];
      if (!items.length) { box.textContent = "پیامی برای این بیمار ثبت نشده است."; return; }
      box.replaceChildren();
      items.forEach(m => {
        const row = document.createElement("div");
        row.className = "message-history-row " + (Number(m.status) === 1 ? "ok" : "err");
        const head = document.createElement("div");
        head.className = "message-history-head";
        head.innerHTML = `<strong>${Number(m.status) === 1 ? "ارسال شد" : "ناموفق"}</strong>
          <span>${escapeHtml(formatDate(m.sentAt || m.createdDate))}</span>`;
        const body = document.createElement("p");
        body.textContent = m.body;
        row.append(head, body);
        if (m.errorMessage) {
          const err = document.createElement("small");
          err.className = "message-history-error";
          err.textContent = m.errorMessage;
          row.appendChild(err);
        }
        box.appendChild(row);
      });
    } catch (e) { box.textContent = e.message; }
  }

  async function send() {
    const patient = window.selectedPatient;
    if (!patient) return;
    const body = $("msgBody").value.trim();
    if (!body) { setStatus("متن پیام را وارد کنید.", true); return; }

    $("msgSendButton").disabled = true;
    setStatus("در حال ارسال...", false);
    try {
      const x = await api(`/api/patients/${patient.patientID}/messages`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          templateKey: $("msgTemplate").value || null,
          body,
          mobile: $("msgMobile").value.trim() || null
        })
      });
      setStatus(x.message || "پیامک ارسال شد.", false);
      window.showToast?.("پیامک با موفقیت ارسال شد.");
      await loadHistory(patient.patientID);
    } catch (e) { setStatus(e.message, true); }
    finally { $("msgSendButton").disabled = false; }
  }

  function close() { $("patientMessagesCard")?.classList.add("hidden"); }

  // Inject the toolbar button once the patient record exists in the page.
  function addToolbarButton() {
    const toolbar = document.querySelector("#patientDetailsSection .details-toolbar");
    if (!toolbar || $("sendPatientSmsButton")) return;
    const btn = document.createElement("button");
    btn.type = "button";
    btn.id = "sendPatientSmsButton";
    btn.className = "secondary-button";
    btn.textContent = "📱 ارسال پیامک";
    btn.onclick = open;
    toolbar.appendChild(btn);
  }

  const observer = new MutationObserver(() => {
    if (!$("patientMessagesCard")) buildSection();
    addToolbarButton();
  });
  const host = document.getElementById("patientDetailsSection");
  if (host) observer.observe(host, { childList: true, subtree: true, attributes: true, attributeFilter: ["class"] });
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", () => { buildSection(); addToolbarButton(); });
  else { buildSection(); addToolbarButton(); }

  window.DentalRayPatientMessages = { open };
})();
