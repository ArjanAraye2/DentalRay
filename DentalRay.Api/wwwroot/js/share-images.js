// DentalRay - send a Study's images as a link by SMS.
//
// The link is issued by the server and opened without a login, so the message
// itself can be reworded freely: only the URL is what makes it work. This file
// adds the toolbar button to the Study images screen and runs the dialog.
(function () {
  "use strict";

  function $(id) { return document.getElementById(id); }

  let shareID = null;

  function buildDialog() {
    if ($("shareImagesDialog")) return;
    const wrap = document.createElement("div");
    wrap.id = "shareImagesDialog";
    wrap.className = "confirm-overlay hidden";
    wrap.innerHTML = `
      <div class="confirm-dialog share-dialog">
        <h3>ارسال لینک تصاویر</h3>
        <p class="share-dialog-hint">بیمار یا پزشک با باز کردن این لینک، تصاویر همین Study را بدون ورود به سیستم می‌بیند.</p>

        <div class="form-field">
          <label for="shareLinkInput">لینک</label>
          <div class="share-link-row">
            <input id="shareLinkInput" type="text" readonly />
            <button id="shareCopyButton" type="button" class="secondary-button">کپی</button>
          </div>
        </div>

        <div class="form-field">
          <label for="shareRecipient">گیرنده</label>
          <select id="shareRecipient">
            <option value="patient">بیمار</option>
            <option value="other">شماره دیگر (مثلاً پزشک معالج)</option>
          </select>
        </div>

        <div id="shareOtherFields" class="form-field hidden">
          <label for="shareOtherMobile">شماره و نام گیرنده</label>
          <input id="shareOtherMobile" type="tel" inputmode="numeric" placeholder="09…" />
          <input id="shareOtherName" type="text" placeholder="نام و خانواده (مثلاً دکتر …)" />
        </div>

        <div class="form-field">
          <label for="shareMessage">متن پیامک</label>
          <textarea id="shareMessage" rows="4"></textarea>
          <small class="field-hint">نام و لینک داخل متن پر شده‌اند؛ هر قالبی بنویسید همان ارسال می‌شود.</small>
        </div>

        <div id="shareStatus" class="status-message"></div>

        <div class="form-actions">
          <button id="shareSendButton" type="button">ارسال پیامک</button>
          <button id="shareCloseButton" type="button" class="secondary-button">انصراف</button>
        </div>
      </div>`;
    document.body.appendChild(wrap);

    wrap.addEventListener("click", e => { if (e.target === wrap) close(); });
    $("shareCloseButton").onclick = close;
    $("shareRecipient").onchange = () => {
      $("shareOtherFields").classList.toggle("hidden", $("shareRecipient").value !== "other");
    };
    $("shareCopyButton").onclick = async () => {
      const value = $("shareLinkInput").value;
      try {
        await navigator.clipboard.writeText(value);
        $("shareCopyButton").textContent = "کپی شد";
        setTimeout(() => { $("shareCopyButton").textContent = "کپی"; }, 1500);
      } catch {
        window.prompt("لینک را کپی کنید:", value);
      }
    };
    $("shareSendButton").onclick = send;
    document.addEventListener("keydown", e => {
      if (e.key === "Escape" && !$("shareImagesDialog").classList.contains("hidden")) close();
    });
  }

  function close() {
    const dialog = $("shareImagesDialog");
    if (dialog) dialog.classList.add("hidden");
    shareID = null;
  }

  function status(text, isError) {
    const el = $("shareStatus");
    if (!el) return;
    el.textContent = text || "";
    el.classList.toggle("error", !!isError);
  }

  async function open() {
    const study = window.selectedStudy;
    if (!study) {
      window.showToast?.("ابتدا Study مورد نظر را باز کنید.", "error");
      return;
    }
    buildDialog();

    const patient = window.selectedPatient;
    const patientLabel = patient?.mobile
      ? `بیمار — ${patient.mobile}`
      : "بیمار — شماره‌ای ثبت نشده";
    const select = $("shareRecipient");
    select.options[0].value = "patient";
    select.options[0].textContent = patientLabel;
    select.value = patient?.mobile ? "patient" : "other";
    $("shareOtherFields").classList.toggle("hidden", select.value !== "other");
    $("shareOtherMobile").value = "";
    $("shareOtherName").value = "";
    $("shareLinkInput").value = "";
    $("shareMessage").value = "";
    status("در حال ساخت لینک…", false);
    $("shareImagesDialog").classList.remove("hidden");

    try {
      const res = await fetch(`/api/sharelinks/study/${study.studyID}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({})
      });
      const data = await res.json();
      if (!res.ok || !data.success) throw new Error(data.message || "ساخت لینک انجام نشد.");
      shareID = data.shareID;
      $("shareLinkInput").value = data.url;
      $("shareMessage").value = data.message || "";
      status("", false);
    } catch (e) {
      status(e.message || "ساخت لینک انجام نشد.", true);
    }
  }

  async function send() {
    if (!shareID) { status("لینکی ساخته نشده است.", true); return; }

    const usePatient = $("shareRecipient").value === "patient";
    const mobile = usePatient ? (window.selectedPatient?.mobile || "") : $("shareOtherMobile").value.trim();
    const name = usePatient
      ? `${window.selectedPatient?.firstName || ""} ${window.selectedPatient?.lastName || ""}`.trim()
      : $("shareOtherName").value.trim();
    const message = $("shareMessage").value;

    if (!mobile) {
      status(usePatient
        ? "برای بیمار شماره موبایلی ثبت نشده است؛ گیرنده را «شماره دیگر» انتخاب کنید."
        : "شماره موبایل گیرنده را وارد کنید.", true);
      return;
    }
    if (!message.trim()) { status("متن پیامک خالی است.", true); return; }

    const button = $("shareSendButton");
    button.disabled = true;
    status("در حال ارسال…", false);
    try {
      const res = await fetch("/api/sharelinks/send", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ shareID, mobile, recipientName: name, message })
      });
      const data = await res.json();
      if (!res.ok || !data.success) throw new Error(data.message || "ارسال پیامک انجام نشد.");
      window.showToast?.("پیامک لینک تصاویر ارسال شد.");
      close();
    } catch (e) {
      status(e.message || "ارسال پیامک انجام نشد.", true);
    } finally {
      button.disabled = false;
    }
  }

  function addShareButton() {
    const toolbar = document.querySelector("#studyImagesSection .details-toolbar");
    if (!toolbar || $("shareStudyImagesButton")) return;
    const btn = document.createElement("button");
    btn.type = "button";
    btn.id = "shareStudyImagesButton";
    btn.className = "secondary-button";
    btn.textContent = "ارسال لینک تصاویر";
    btn.onclick = open;
    toolbar.appendChild(btn);
  }

  const observer = new MutationObserver(addShareButton);
  const host = document.getElementById("studyImagesSection");
  if (host) observer.observe(host, { childList: true, subtree: true, attributes: true, attributeFilter: ["class"] });
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", addShareButton);
  else addShareButton();
})();
