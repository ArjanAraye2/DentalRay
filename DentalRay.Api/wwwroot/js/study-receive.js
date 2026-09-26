// DentalRay - receiving images for one specific Study.
//
// Two ways in, both entered from the Study itself, and neither installs
// anything on the patient's phone:
//   1) a QR the patient scans with the plain camera and pastes the SMS into
//   2) the patient forwards the SMS to the clinic phone, and the secretary
//      pulls it here - pictures land in this Study only.
(() => {
  "use strict";

  const $ = id => document.getElementById(id);

  function buildDialog() {
    if ($("studyReceiveDialog")) return;
    const wrap = document.createElement("div");
    wrap.id = "studyReceiveDialog";
    wrap.className = "confirm-overlay hidden";
    wrap.innerHTML = `
      <div class="confirm-dialog">
        <h3>دریافت تصویر برای این Study</h3>
        <p class="share-dialog-hint" id="studyReceiveInfo"></p>

        <div class="inbox-card">
          <strong>روش ۱ — کیوآرکد برای بیمار</strong>
          <p class="share-dialog-hint">
            از بیمار بخواهید این کد را با <strong>دوربین خودِ گوشی</strong> اسکن کند
            (بدون نصب هیچ برنامه‌ای) و سپس متن پیامک رادیولوژی را در صفحه‌ای که باز
            می‌شود بچپاند. تصاویر مستقیماً به همین Study می‌آید.
          </p>
          <div class="pair-qr" id="studyReceiveQr"></div>
          <div class="pair-payload" id="studyReceiveUrl"></div>
          <div class="form-actions">
            <button id="studyReceiveCopy" type="button" class="secondary-button">کپی لینک</button>
          </div>
        </div>

        <div class="inbox-card">
          <strong>روش ۲ — فوروارد به گوشی مطب</strong>
          <p class="share-dialog-hint">
            شمارهٔ مطب: <strong id="studyClinicMobile">—</strong><br />
            به بیمار بگویید پیامک رادیولوژیست را به این شماره <strong>فوروارد</strong> کند،
            بعد این دکمه را بزنید تا از گوشی مطب گرفته شود.
          </p>
          <div class="form-actions">
            <button id="studyPullInbox" type="button">دریافت پیامک‌های مطب برای همین Study</button>
          </div>
          <div id="studyPullStatus" class="status-message"></div>
        </div>

        <div class="form-actions">
          <button id="studyReceiveClose" type="button" class="secondary-button">بستن</button>
        </div>
      </div>`;
    document.body.appendChild(wrap);

    wrap.addEventListener("click", e => { if (e.target === wrap) closeDialog(); });
    $("studyReceiveClose").onclick = closeDialog;
    $("studyReceiveCopy").onclick = async () => {
      const url = $("studyReceiveUrl").textContent;
      if (!url) return;
      try {
        await navigator.clipboard.writeText(url);
        $("studyReceiveCopy").textContent = "کپی شد";
        setTimeout(() => { $("studyReceiveCopy").textContent = "کپی لینک"; }, 1500);
      } catch { window.prompt("لینک را کپی کنید:", url); }
    };
    $("studyPullInbox").onclick = pullFromClinicPhone;
  }

  function closeDialog() {
    $("studyReceiveDialog")?.classList.add("hidden");
    $("studyReceiveQr")?.replaceChildren();
  }

  function selectedStudy() {
    return window.selectedStudy || null;
  }

  async function openDialog() {
    const study = selectedStudy();
    if (!study) { window.showToast?.("ابتدا Study را باز کنید.", "error"); return; }
    buildDialog();

    const patientID = (window.selectedPatient && window.selectedPatient.patientID) || study.patientID;
    $("studyReceiveInfo").textContent =
      `بیمار: ${window.selectedPatient ? `${window.selectedPatient.firstName || ""} ${window.selectedPatient.lastName || ""}`.trim() : "-"} — Study شمارهٔ ${study.studyID}`;
    $("studyReceiveUrl").textContent = "";
    $("studyReceiveQr").replaceChildren();
    $("studyPullStatus").textContent = "";
    $("studyReceiveDialog").classList.remove("hidden");

    loadClinicMobile();
    await issueToken(patientID, study.studyID);
  }

  async function issueToken(patientID, studyID) {
    try {
      const res = await fetch(`/api/patients/${patientID}/receive-link`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ expiresInDays: 1, studyID })
      });
      const data = await res.json();
      if (!res.ok || !data.success) throw new Error(data.message || "ساخت کیوآرکد انجام نشد.");

      $("studyReceiveUrl").textContent = data.url;
      if (typeof qrcode === "function") {
        const code = qrcode(0, "M");
        code.addData(data.url);
        code.make();
        $("studyReceiveQr").innerHTML = code.createSvgTag({
          cellSize: 4, margin: 16, scalable: true, alt: "کیوآرکد دریافت تصویر"
        });
      }
    } catch (e) {
      $("studyReceiveUrl").textContent = e.message || "ساخت کیوآرکد انجام نشد.";
    }
  }

  async function loadClinicMobile() {
    try {
      // فقط شمارهٔ مطب برمی‌گردد؛ تنظیمات کامل پیامک SuperAdmin-only است.
      const res = await fetch("/api/communications/clinic-mobile");
      if (!res.ok) return;
      const data = await res.json();
      $("studyClinicMobile").textContent = (data.mobile && data.mobile.trim()) || "در تنظیمات پیامک ثبت نشده";
    } catch {
      $("studyClinicMobile").textContent = "در تنظیمات پیامک ثبت نشده";
    }
  }

  async function pullFromClinicPhone() {
    const study = selectedStudy();
    if (!study) return;
    const status = $("studyPullStatus");
    const button = $("studyPullInbox");
    button.disabled = true;
    status.classList.remove("error");
    status.textContent = "در حال دریافت از گوشی مطب…";
    try {
      const res = await fetch(`/api/studies/${study.studyID}/pull-inbox`, { method: "POST" });
      const data = await res.json();
      if (!res.ok || !data.success) throw new Error(data.message || "دریافت انجام نشد.");
      status.textContent = data.message;
      if (data.imported > 0 || data.fetched > 0) {
        window.showToast?.(data.message);
        // تصاویر تازه را همان‌جا نشان بده
        if (typeof window.openStudyImages === "function") window.openStudyImages(study);
      }
    } catch (e) {
      status.textContent = e.message || "دریافت انجام نشد.";
      status.classList.add("error");
    } finally {
      button.disabled = false;
    }
  }

  function addButton() {
    const toolbar = document.querySelector("#studyDetailsSection .details-toolbar");
    if (!toolbar || $("studyReceiveImagesButton")) return;
    const btn = document.createElement("button");
    btn.type = "button";
    btn.id = "studyReceiveImagesButton";
    btn.className = "secondary-button";
    btn.textContent = "دریافت تصویر از گوشی بیمار";
    btn.onclick = openDialog;
    toolbar.appendChild(btn);
  }

  // دکمه در index.html هست؛ اگر جایی حذف شد دوباره ساخته می‌شود.
  function wire() {
    const direct = $("studyReceiveImagesButton");
    if (direct) direct.onclick = openDialog;
    else addButton();
  }

  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", wire);
  else wire();

  const observer = new MutationObserver(wire);
  const host = document.getElementById("studyDetailsSection");
  if (host) observer.observe(host, { childList: true, subtree: true, attributes: true, attributeFilter: ["class"] });
})();
