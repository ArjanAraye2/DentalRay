// DentalRay - patient record toolbar.
//
// نوار پروندهٔ بیمار نُه دکمه داشت و در نمای موبایل به هم می‌ریخت. کارهایی که
// هر روز استفاده می‌شوند سر جایشان می‌مانند و بقیه زیر یک کلید «بیشتر» جمع
// می‌شوند. دکمه‌هایی که بعداً توسط بخش‌های دیگر ساخته می‌شوند هم خودکار به
// همانجا می‌روند، پس هیچ دکمه‌ای گم نمی‌شود.
(() => {
  "use strict";

  const PRIMARY = ["backToPatientsButton", "editPatientButton", "newStudyButton"];
  const STYLE_ID = "toolbarMoreStyles";
  let moreButton = null;
  let moreBox = null;

  function addStyles() {
    if (document.getElementById(STYLE_ID)) return;
    const style = document.createElement("style");
    style.id = STYLE_ID;
    style.textContent = `
      .toolbar-more-box{display:flex;flex-wrap:wrap;gap:8px;width:100%;margin-top:8px;padding-top:10px;border-top:1px dashed #cfdfe6}
      .toolbar-more-box.hidden{display:none}
      @media (max-width:700px){.toolbar-more-box{flex-direction:column}.toolbar-more-box button{width:100%}}
    `;
    document.head.appendChild(style);
  }

  function ensure() {
    const toolbar = document.querySelector("#patientDetailsSection .details-toolbar");
    if (!toolbar) return null;
    if (moreButton && moreButton.isConnected) return toolbar;

    moreBox = document.createElement("div");
    moreBox.id = "toolbarMoreBox";
    moreBox.className = "toolbar-more-box hidden";

    moreButton = document.createElement("button");
    moreButton.type = "button";
    moreButton.id = "toolbarMoreButton";
    moreButton.className = "secondary-button";
    moreButton.textContent = "بیشتر ▾";
    moreButton.onclick = () => {
      const hidden = moreBox.classList.toggle("hidden");
      moreButton.textContent = hidden ? "بیشتر ▾" : "بستن ▴";
    };

    toolbar.appendChild(moreButton);
    toolbar.appendChild(moreBox);
    return toolbar;
  }

  function organize() {
    const toolbar = ensure();
    if (!toolbar) return;

    let moved = false;
    [...toolbar.children].forEach(child => {
      if (child === moreButton || child === moreBox) return;
      if (child.tagName !== "BUTTON") return;
      if (PRIMARY.includes(child.id)) return;
      moreBox.appendChild(child);
      moved = true;
    });

    const count = moreBox.children.length;
    const hidden = moreBox.classList.contains("hidden");
    const label = count ? (hidden ? `بیشتر (${count}) ▾` : "بستن ▴") : "";
    // فقط وقتی متن عوض می‌شود نوشته می‌شود؛ وگرنه همین نوشتن باعث
    // تحریک دوبارهٔ مشاهده‌گر و حلقهٔ بی‌نهایت می‌شود.
    if (moreButton.textContent !== label) moreButton.textContent = label;
    const display = count ? "" : "none";
    if (moreButton.style.display !== display) moreButton.style.display = display;
    return moved;
  }

  const observer = new MutationObserver(records => {
    // تغییراتی که خودِ همین ماژول ایجاد می‌کند دلیل بر اضافه شدن دکمه نیستند.
    const ours = records.every(r => {
      if (r.target === moreButton || (moreBox && moreBox.contains(r.target))) return true;
      if (r.type !== "childList") return false;
      return [...r.addedNodes, ...r.removedNodes].every(n =>
        n === moreButton || (moreBox && (n === moreBox || moreBox.contains(n))));
    });
    if (ours) return;
    organize();
  });

  const host = document.getElementById("patientDetailsSection");
  if (host) observer.observe(host, { childList: true, subtree: true, attributes: true, attributeFilter: ["class", "style"] });

  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", organize);
  else organize();
})();
