// ReSiRai — تور اولین ورود (سطح آموزشی ۳).
//
// چهار گام «کار امروز» را یک‌بار نشان می‌دهد: جست‌وجوی بیمار ← ثبت بیمار ←
// ثبت مراجعه ← افزودن تصویر. متن‌ها از help-content.js می‌آیند (منبع واحد).
//
// رفتار:
//   • «شروع کن» (پایان تور) → دیگر هرگز نمایش داده نمی‌شود.
//   • «بعداً» یا Escape یا کلیک بیرون → بسته می‌شود ولی در بازدید بعدی دوباره می‌آید،
//     چون نیروی تازه هنوز چیزی یاد نگرفته است.
// فقط دسکتاپ: کاربر موبایل همان نیروی باتجربه است.
(function () {
  "use strict";

  var DONE_KEY = "resirai-tour-done";

  function isDone() {
    try { return localStorage.getItem(DONE_KEY) === "1"; } catch (e) { return false; }
  }

  function markDone() {
    try { localStorage.setItem(DONE_KEY, "1"); } catch (e) { /* بی‌اثر */ }
  }

  var modal = null, body = null, titleEl = null, progressEl = null;
  var prevBtn = null, nextBtn = null, skipBtn = null, dotsEl = null;
  var index = 0, tour = [];

  function steps() {
    var help = window.ReSiRaiHelp || {};
    return (help.tour || []).map(function (s) {
      if (s.form && help[s.form]) {
        var h = help[s.form];
        return {
          // «راهنمای …» مال زیر فرم است؛ در تور همان عنوان کوتاه کافی است.
          title: String(h.title || "").replace(/^راهنمای\s+/, ""),
          parts: [["قبل از شروع", h.before], ["هنگام پر کردن", h.during], ["بعد از ذخیره", h.after]].filter(function (p) { return p[1]; })
        };
      }
      return { title: s.title, parts: [["نکته", s.text]].filter(function (p) { return p[1]; }) };
    }).filter(function (s) { return s.title; });
  }

  function ensureModal() {
    if (modal) return modal;
    modal = document.createElement("div");
    modal.id = "firstRunTour";
    modal.className = "tour-overlay hidden";
    modal.innerHTML =
      '<div class="tour-dialog" role="dialog" aria-modal="true" aria-label="تور اولین ورود">' +
      '<div class="tour-head">' +
      '<div><div class="tour-kicker">شروع کار در ReSiRai</div><div id="tourStepTitle" class="tour-title"></div></div>' +
      '<div id="tourProgress" class="tour-progress"></div>' +
      "</div>" +
      '<div id="tourDots" class="tour-dots"></div>' +
      '<div id="tourBody" class="tour-body"></div>' +
      '<div class="tour-actions">' +
      '<button id="tourPrev" type="button" class="secondary-button">قبلی</button>' +
      '<button id="tourNext" type="button">بعدی</button>' +
      '<button id="tourSkip" type="button" class="secondary-button">بعداً</button>' +
      "</div>" +
      '<div class="tour-note">این تور فقط یک‌بار نمایش داده می‌شود و بعداً از «راهنما» در منو قابل دیدن است.</div>' +
      "</div>";
    document.body.appendChild(modal);

    body = modal.querySelector("#tourBody");
    titleEl = modal.querySelector("#tourStepTitle");
    progressEl = modal.querySelector("#tourProgress");
    dotsEl = modal.querySelector("#tourDots");
    prevBtn = modal.querySelector("#tourPrev");
    nextBtn = modal.querySelector("#tourNext");
    skipBtn = modal.querySelector("#tourSkip");

    prevBtn.onclick = function () { if (index > 0) { index--; render(); } };
    nextBtn.onclick = function () {
      if (index >= tour.length - 1) { markDone(); close(); window.showToast?.("تور پایان یافت؛ هر وقت لازم شد از «راهنما» ببین.", "success"); return; }
      index++; render();
    };
    skipBtn.onclick = close;
    modal.addEventListener("click", function (e) { if (e.target === modal) close(); });
    document.addEventListener("keydown", function (e) {
      if (e.key === "Escape" && modal && !modal.classList.contains("hidden")) close();
    });
    return modal;
  }

  function render() {
    var step = tour[index] || { title: "", parts: [] };
    titleEl.textContent = step.title;
    progressEl.textContent = "گام " + (index + 1).toLocaleString("fa-IR") + " از " + tour.length.toLocaleString("fa-IR");

    dotsEl.replaceChildren();
    tour.forEach(function (_, i) {
      var d = document.createElement("span");
      d.className = "tour-dot" + (i === index ? " is-active" : "");
      dotsEl.appendChild(d);
    });

    body.replaceChildren();
    step.parts.forEach(function (p) {
      var el = document.createElement("p");
      el.className = "tour-part";
      var b = document.createElement("b");
      b.textContent = p[0];
      el.append(b, document.createTextNode(" — " + p[1]));
      body.appendChild(el);
    });

    prevBtn.classList.toggle("hidden", index === 0);
    nextBtn.textContent = index >= tour.length - 1 ? "شروع کن" : "بعدی";
  }

  function open() {
    tour = steps();
    if (!tour.length) return;
    index = 0;
    ensureModal();
    render();
    modal.classList.remove("hidden");
  }

  function close() { if (modal) modal.classList.add("hidden"); }

  function maybeShow(user) {
    if (!user || isDone()) return;
    if (window.matchMedia && !window.matchMedia("(min-width: 860px)").matches) return;
    if (!document.getElementById("reSiRaiLoginScreen")) open();
  }

  window.addEventListener("resirai-auth-changed", function (e) { maybeShow(e.detail); });

  // اگر نشست قبلاً برقرار شده بود (بازگشت به تب، رفرش سریع)
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", function () { maybeShow(window.reSiRaiCurrentUser); });
  } else {
    maybeShow(window.reSiRaiCurrentUser);
  }

  window.ReSiRaiTour = { open: open, close: close, isDone: isDone };
})();
