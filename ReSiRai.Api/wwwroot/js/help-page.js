// ReSiRai — صفحهٔ «راهنما» در منو (سطح ۴: مرجع کامل).
//
// همهٔ متن‌ها از help-content.js می‌آیند؛ پس این صفحه، راهنمای زیر فرم‌ها و تور
// اولین ورود را هم‌زمان نشان می‌دهد و هیچ متنی دوباره نوشته نمی‌شود.
(function () {
  "use strict";

  function part(label, text) {
    var p = document.createElement("p");
    p.className = "form-guide-part";
    var b = document.createElement("b");
    b.textContent = label;
    p.append(b, document.createTextNode(" — " + text));
    return p;
  }

  function guideBlock(title, data) {
    var box = document.createElement("div");
    box.className = "form-guide help-guide";

    var head = document.createElement("div");
    head.className = "form-guide-head";
    var t = document.createElement("span");
    t.className = "form-guide-title";
    t.textContent = title;
    head.appendChild(t);

    var body = document.createElement("div");
    body.className = "form-guide-body";
    if (data.before) body.appendChild(part("قبل از شروع", data.before));
    if (data.during) body.appendChild(part("هنگام پر کردن", data.during));
    if (data.after) body.appendChild(part("بعد از ذخیره", data.after));
    if (data.text) body.appendChild(part("نکته", data.text));

    if (data.buttons && data.buttons.length) {
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
      body.append(sub, ul);
    }

    box.append(head, body);
    return box;
  }

  function render() {
    var host = document.getElementById("helpPageBody");
    if (!host || host.dataset.rendered === "1") return;
    var help = window.ReSiRaiHelp || {};

    // ---- تور اولین ورود ----
    var tourBox = document.createElement("div");
    tourBox.className = "form-guide help-guide";
    var tourHead = document.createElement("div");
    tourHead.className = "form-guide-head";
    var tourTitle = document.createElement("span");
    tourTitle.className = "form-guide-title";
    tourTitle.textContent = "تور اولین ورود";
    tourHead.appendChild(tourTitle);
    var tourBody = document.createElement("div");
    tourBody.className = "form-guide-body";
    tourBody.appendChild(part(
      "چهار گام کار امروز",
      "جست‌وجوی بیمار ← ثبت بیمار ← ثبت مراجعه ← عکس/فایل جدید. برای نیروی تازه همین چهارقدم کل کار روزانه است."
    ));
    var tourRow = document.createElement("p");
    tourRow.className = "tour-actions";
    var replay = document.createElement("button");
    replay.type = "button";
    replay.className = "secondary-button";
    var seen = window.ReSiRaiTour && window.ReSiRaiTour.isDone && window.ReSiRaiTour.isDone();
    replay.textContent = seen ? "دیدن دوبارهٔ تور" : "شروع تور";
    replay.onclick = function () { if (window.ReSiRaiTour) window.ReSiRaiTour.open(); };
    var state = document.createElement("span");
    state.className = "help-tour-state";
    state.textContent = seen ? "تاکنون دیده شده است؛ هر وقت خواستی دوباره ببین." : "هنوز دیده نشده است؛ در بارگذاری بعدی خودکار می‌آید.";
    tourRow.append(replay, state);
    tourBody.appendChild(tourRow);
    tourBox.append(tourHead, tourBody);
    host.appendChild(tourBox);

    // ---- راهنمای فرم‌ها (به ترتیب کار: ثبت بیمار تا ادغام) ----
    var order = ["newPatientForm", "editPatientForm", "newStudyForm", "uploadImageForm", "studyDetailsForm", "studyImagesSection", "mergePatientForm"];
    order.forEach(function (key) {
      if (help[key]) host.appendChild(guideBlock(help[key].title, help[key]));
    });

    host.dataset.rendered = "1";
  }

  window.ReSiRaiHelpPage = { render: render };
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", render);
  else render();
})();
