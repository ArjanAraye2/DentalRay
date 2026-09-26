// DentalRay — راهنمای ثابت زیر فرم‌ها.
//
// متن‌ها در help-content.js (منبع واحد) هستند؛ این فایل فقط آن‌ها را زیر هر فرم
// می‌گذارد و مدیریت باز/بسته بودن را می‌کند: راهنما تا اولین بار که کاربر آن را
// می‌بندد باز است، بعد از آن جمع‌شده می‌ماند (مطابق هدف «کم کردن نیاز به آموزش»:
// برای نیروی تازه دیده می‌شود، برای نیروی باتجربه مزاحمت ندارد).
(function () {
  "use strict";

  var STORAGE_PREFIX = "dentix-help-closed-";

  function isClosed(key) {
    try { return localStorage.getItem(STORAGE_PREFIX + key) === "1"; } catch (e) { return false; }
  }

  function setClosed(key, closed) {
    try {
      if (closed) localStorage.setItem(STORAGE_PREFIX + key, "1");
      else localStorage.removeItem(STORAGE_PREFIX + key);
    } catch (e) { /* حالت خصوصی مرورگر؛ بی‌اثر می‌ماند */ }
  }

  function part(label, text) {
    var p = document.createElement("p");
    p.className = "form-guide-part";
    var b = document.createElement("b");
    b.textContent = label;
    p.append(b, document.createTextNode(" — " + text));
    return p;
  }

  function build(key, data, form) {
    var section = form.closest("section") || form.parentElement;
    if (!section) return null;

    var box = document.createElement("div");
    box.className = "form-guide";
    box.setAttribute("data-guide-for", key);

    var head = document.createElement("button");
    head.type = "button";
    head.className = "form-guide-head";
    var title = document.createElement("span");
    title.className = "form-guide-title";
    title.textContent = data.title;
    var caret = document.createElement("span");
    caret.className = "form-guide-caret";
    head.append(title, caret);

    var body = document.createElement("div");
    body.className = "form-guide-body";
    if (data.before) body.appendChild(part("قبل از شروع", data.before));
    if (data.during) body.appendChild(part("هنگام پر کردن", data.during));
    if (data.after) body.appendChild(part("بعد از ذخیره", data.after));

    if (data.buttons && data.buttons.length) {
      var wrap = document.createElement("div");
      wrap.className = "form-guide-buttons";
      var sub = document.createElement("div");
      sub.className = "form-guide-sub";
      sub.textContent = "دکمه‌ها";
      var ul = document.createElement("ul");
      ul.className = "form-guide-steps";
      data.buttons.forEach(function (row) {
        var li = document.createElement("li");
        var b = document.createElement("b");
        b.textContent = row[0];
        li.append(b, document.createTextNode(" " + row[1]));
        ul.appendChild(li);
      });
      wrap.append(sub, ul);
      body.appendChild(wrap);
    }

    function apply() {
      var closed = isClosed(key);
      box.classList.toggle("is-closed", closed);
      caret.textContent = closed ? "+" : "−";
      head.setAttribute("aria-expanded", String(!closed));
    }

    head.onclick = function () {
      setClosed(key, !box.classList.contains("is-closed"));
      apply();
    };
    apply();

    box.append(head, body);
    section.appendChild(box);
    return box;
  }

  function init() {
    var help = window.DentalRayHelp || {};
    Object.keys(help).forEach(function (key) {
      var form = document.getElementById(key);
      if (!form) return;
      if (document.querySelector('[data-guide-for="' + key + '"]')) return;
      build(key, help[key], form);
    });
  }

  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
  else init();
})();
