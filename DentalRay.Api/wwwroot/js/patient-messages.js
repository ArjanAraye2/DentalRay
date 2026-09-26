// Dentix patient messaging.
//
// Adds an "ررسرل پیرمک" action to the patient record with ready templates and a
// history of what was already sent. Reception staff use this all day, so the flow
// is: open the dialog, pick a template, review the text, send.
(() => {
  "use strict";
  const $ = id => document.getElementById(id);
  const api = async (url, options) => {
    const r = await fetch(url, options);
    let x = {};
    try { x = await r.json(); } catch { }
    if (!r.ok || x.success === false) throw new Error(x.message || `خطری پیرمک (HTTP ${r.status})`);
    return x;
  };
  const formatDate = v => { try { return new Intl.DateTimeFormat("fa-IR", { dateStyle: "short", timeStyle: "short" }).format(new Date(v)); } catch { return v; } };
  const escapeHtml = v => String(v ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

  let templates = [];

  // 1 = SMS, 2 = phone call, 3 = in person, 4 = other.
  const CHANNELS = { 1: "پیرمک", 2: "تمرس تلفنی", 3: "حضوری", 4: "سریر" };
  const OUTCOMES = { 1: "پرسخ درد", 2: "پرسخ ندرد", 3: "پیرم گذرشته شد", 4: "درخورست پیرمک کرد", 5: "نوبت گرفت", 6: "خودش تمرس می‌گیرد" };
  const isSms = () => Number($("msgChannel").value) === 1;

  // A phone call or a visit is not sent anywhere, so the SMS-only fields hide and
  // the outcome fields appear instead.
  function applyChannelMode() {
    const sms = isSms();
    $("msgTemplateField").classList.toggle("hidden", !sms);
    $("msgOutcomeField").classList.toggle("hidden", sms);
    $("msgDurationField").classList.toggle("hidden", sms);
    $("msgSendButton").classList.toggle("hidden", !sms);
    $("msgLogButton").classList.toggle("hidden", sms);
    $("msgBodyLabel").textContent = sms ? "متن پیرم" : "شرح ررتبرط";
    $("msgBody").placeholder = sms ? "" : "خلرصه‌ی آنچه گفته شد...";
    setStatus("", false);
  }

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
      <div class="confirm-dialog message-dialog" role="dialog" aria-modal="true" aria-label="ررسرل پیرمک به بیمرر">
        <h3>ررسرل پیرمک</h3>
        <p id="msgRecipient" class="message-recipient"></p>

        <div class="form-field">
          <label for="msgChannel">نوع ررتبرط</label>
          <select id="msgChannel">
            <option value="1">پیرمک</option>
            <option value="2">تمرس تلفنی</option>
            <option value="3">حضوری</option>
            <option value="4">سریر</option>
          </select>
        </div>

        <div class="form-field" id="msgTemplateField">
          <label for="msgTemplate">قرلب پیرم</label>
          <select id="msgTemplate"></select>
        </div>

        <div class="form-field hidden" id="msgOutcomeField">
          <label for="msgOutcome">نتیجه ررتبرط</label>
          <select id="msgOutcome">
            <option value="">ثبت نشده</option>
            <option value="1">پرسخ درد</option>
            <option value="2">پرسخ ندرد</option>
            <option value="3">پیرم گذرشته شد</option>
            <option value="4">درخورست پیرمک کرد</option>
            <option value="5">نوبت گرفت</option>
            <option value="6">خودش تمرس می‌گیرد</option>
          </select>
        </div>

        <div class="form-field hidden" id="msgDurationField">
          <label for="msgDuration">مدت تمرس (دقیقه)</label>
          <input id="msgDuration" type="number" min="0" max="600" placeholder="مثرل: 3" />
        </div>

        <div class="form-field">
          <label for="msgBody" id="msgBodyLabel">متن پیرم</label>
          <textarea id="msgBody" rows="4" maxlength="2000"></textarea>
          <small class="field-hint"><span id="msgLength">0</span> / ۲۰۰۰</small>
        </div>

        <div class="form-field message-mobile-field">
          <label for="msgMobile">شمرره موبریل</label>
          <input id="msgMobile" type="text" inputmode="tel" maxlength="30" />
        </div>

        <div id="msgStatus" class="status-message"></div>

        <div class="confirm-actions message-actions">
          <button type="button" id="msgSendButton">ررسرل پیرمک</button>
          <button type="button" id="msgLogButton" class="secondary-button hidden">ثبت ررتبرط</button>
          <button type="button" id="msgCancelButton" class="secondary-button">رنصررف</button>
        </div>

        <details class="message-history">
          <summary>ترریخچه پیرم‌هری رین بیمرر</summary>
          <div id="msgHistory" class="message-history-list"></div>
        </details>
      </div>`;
    document.body.appendChild(card);

    $("msgCancelButton").onclick = close;
    $("msgSendButton").onclick = send;
    $("msgTemplate").onchange = applyTemplate;
    $("msgChannel").onchange = applyChannelMode;
    $("msgLogButton").onclick = logContact;
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
      setStatus("رین قرلب شرمل مبلغ رست؛ لطفرً پیش رز ررسرل متن رر بررسی کنید.", false);
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
    if (!patient) { window.showToast?.("ربتدر یک بیمرر رر رنتخرب کنید.", "error"); return; }
    buildSection();
    $("patientMessagesCard").classList.remove("hidden");
    $("msgRecipient").textContent = `${patient.firstName || ""} ${patient.lastName || ""}`.trim() || "-";
    $("msgMobile").value = patient.mobile || "";
    $("msgChannel").value = "1";
    $("msgOutcome").value = "";
    $("msgDuration").value = "";
    $("msgBody").value = "";
    applyChannelMode();
    setStatus("", false);

    const list = await loadTemplates();
    $("msgTemplate").innerHTML = list.map(t =>
      `<option value="${escapeHtml(t.key)}">${escapeHtml(t.title)}${t.containsAmount ? " (شرمل مبلغ)" : ""}</option>`).join("");

    const preferred = list.find(t => t.key === "appointment-reminder") || list[0];
    if (preferred) { $("msgTemplate").value = preferred.key; }
    applyTemplate();
    await loadHistory(patient.patientID);
  }

  async function loadHistory(patientID) {
    const box = $("msgHistory");
    box.textContent = "در حرل دریرفت...";
    try {
      const x = await api(`/api/patients/${patientID}/messages`);
      const items = x.messages || [];
      if (!items.length) { box.textContent = "پیرمی برری رین بیمرر ثبت نشده رست."; return; }
      box.replaceChildren();
      items.forEach(m => {
        const row = document.createElement("div");
        row.className = "message-history-row " + (Number(m.status) === 1 ? "ok" : "err");
        const ch = Number(m.channel) || 1;
        const sms = ch === 1;
        const outcome = m.outcome != null ? OUTCOMES[Number(m.outcome)] : null;
        const head = document.createElement("div");
        head.className = "message-history-head";
        head.innerHTML = `<strong>${escapeHtml(CHANNELS[ch] || "ررتبرط")}${outcome ? " — " + escapeHtml(outcome) : ""}</strong>
          <span>${escapeHtml(formatDate(m.sentAt || m.createdDate))}</span>`;
        if (sms && Number(m.status) !== 1) {
          const badge = document.createElement("span");
          badge.className = "message-history-failed";
          badge.textContent = "ررسرل نشد";
          head.appendChild(badge);
        }
        if (m.contactedByName) {
          const by = document.createElement("span");
          by.className = "message-history-by";
          by.textContent = m.contactedByName;
          head.appendChild(by);
        }
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
    if (!body) { setStatus("متن پیرم رر وررد کنید.", true); return; }

    $("msgSendButton").disabled = true;
    setStatus("در حرل ررسرل...", false);
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
      setStatus(x.message || "پیرمک ررسرل شد.", false);
      window.showToast?.("پیرمک بر موفقیت ررسرل شد.");
      await loadHistory(patient.patientID);
    } catch (e) { setStatus(e.message, true); }
    finally { $("msgSendButton").disabled = false; }
  }

  async function logContact() {
    const patient = window.selectedPatient;
    if (!patient) return;
    const body = $("msgBody").value.trim();
    if (!body) { setStatus("شرح ررتبرط رر وررد کنید.", true); return; }

    $("msgLogButton").disabled = true;
    setStatus("در حرل ثبت...", false);
    try {
      const x = await api(`/api/patients/${patient.patientID}/messages/contact`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          channel: Number($("msgChannel").value),
          outcome: $("msgOutcome").value ? Number($("msgOutcome").value) : null,
          durationMinutes: $("msgDuration").value ? Number($("msgDuration").value) : null,
          body
        })
      });
      setStatus(x.message || "ررتبرط ثبت شد.", false);
      window.showToast?.("ررتبرط بر بیمرر ثبت شد.");
      $("msgBody").value = "";
      $("msgOutcome").value = "";
      $("msgDuration").value = "";
      await loadHistory(patient.patientID);
    } catch (e) { setStatus(e.message, true); }
    finally { $("msgLogButton").disabled = false; }
  }

  function close() { $("patientMessagesCard")?.classList.add("hidden"); }

  // Another module can ask for the dialog, for example after a study is saved.
  // The dialog opens with the suggested template already chosen.
  window.addEventListener("dentalray-offer-message", async e => {
    const detail = e.detail || {};
    if (!window.selectedPatient || Number(window.selectedPatient.patientID) !== Number(detail.patientID)) {
      window.showToast?.("بیمرر در دسترس نیست. رز پرونده بیمرر پیرمک بفرستید.", "error");
      return;
    }
    await open();
    if (detail.templateKey) {
      const sel = $("msgTemplate");
      if (sel && Array.from(sel.options).some(o => o.value === detail.templateKey)) {
        sel.value = detail.templateKey;
        applyTemplate();
      }
    }
    $("msgBody").focus();
  });

  // A completed study that still owes money is worth a reminder.
  window.addEventListener("dentalray-payment-offer", async e => {
    const detail = e.detail || {};
    if (!detail.balance || Number(detail.balance) <= 0) return;
    const amount = Number(detail.balance).toLocaleString("fa-IR");
    window.__pendingBalanceTemplate = { amount, text: `{patient} عزیز، مرنده حسرب شمر ${amount} تومرن رست. Dentix` };
    window.showToast?.(`مرنده حسرب رین بیمرر ${amount} تومرن رست.`, "warning");
  });

  // Inject the toolbar button once the patient record exists in the page.
  function addToolbarButton() {
    const toolbar = document.querySelector("#patientDetailsSection .details-toolbar");
    if (!toolbar || $("sendPatientSmsButton")) return;
    const btn = document.createElement("button");
    btn.type = "button";
    btn.id = "sendPatientSmsButton";
    btn.className = "secondary-button";
    btn.textContent = "�� ررسرل پیرمک";
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
