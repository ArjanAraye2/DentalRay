// Dentix - interactive graphical FDI Dental Chart
//
// Three views: a plain numbered row (linear), anatomical rows, and an arch. The
// tooth artwork comes from tooth-shapes.js so both drawn views share one source of
// truth for shape, root layout and width.
(function () {
  "use strict";

  const permanentRows = [
    [18, 17, 16, 15, 14, 13, 12, 11, 21, 22, 23, 24, 25, 26, 27, 28],
    [48, 47, 46, 45, 44, 43, 42, 41, 31, 32, 33, 34, 35, 36, 37, 38]
  ];
  const primaryRows = [
    [55, 54, 53, 52, 51, 61, 62, 63, 64, 65],
    [85, 84, 83, 82, 81, 71, 72, 73, 74, 75]
  ];
  const storageKey = "dentalray.dentalChartView";

  // Falls back to a plain outline if the shapes module did not load, so the chart
  // stays usable rather than throwing.
  function toothSvg(n) {
    if (window.DentalRayToothShapes) return window.DentalRayToothShapes.svg(n);
    return '<svg viewBox="10 6 20 34" preserveAspectRatio="none" aria-hidden="true">' +
      '<path class="tooth-root" d="M13 25 L16.5 38 Q20 40 23.5 38 L27 25 Z"/>' +
      '<path class="tooth-crown" d="M10 10 Q20 6 30 10 L29 25 Q20 29 11 25 Z"/>' +
      '<path class="tooth-detail" d="M13 15 Q20 13 27 15"/></svg>';
  }

  function toothButton(n, s) {
    const b = document.createElement("button");
    b.type = "button";
    // A molar is visibly wider than an incisor, and the quadrant tints the tooth
    // so the four quarters are easy to tell apart.
    const shapes = window.DentalRayToothShapes;
    b.className = "tooth-button" + (s.has(n) ? " selected" : "") + (shapes ? " " + shapes.quadrantClass(n) : "");
    b.dataset.tooth = n;
    b.title = "دندان " + n;
    if (shapes) {
      // The SVG is cropped to the tooth and stretched to this box, so the width has
      // to keep the crown's real proportions. The .tooth-shape box is 46px tall and
      // a tooth is about 34 local units tall, which is the scale used here.
      const shape = shapes.shapeOf(n);
      b.style.setProperty("--tooth-width", Math.round(shape.crownWidth * 1.35) + "px");
    }
    b.setAttribute("aria-pressed", s.has(n) ? "true" : "false");
    b.innerHTML = '<span class="tooth-shape">' + toothSvg(n) + '</span><span class="tooth-number">' + n + "</span>";
    b.onclick = () => {
      b.classList.toggle("selected");
      b.setAttribute("aria-pressed", b.classList.contains("selected") ? "true" : "false");
      syncArch(b.closest(".dental-chart"));
      summary(b.closest(".dental-chart"));
    };
    return b;
  }

  function addSection(root, title, rows, s) {
    const g = document.createElement("div");
    g.className = "dental-chart-group";
    const h = document.createElement("div");
    h.className = "dental-chart-group-title";
    h.textContent = title;
    g.appendChild(h);
    rows.forEach((a, i) => {
      const r = document.createElement("div");
      r.className = "dental-chart-row dental-chart-row-" + (i ? "lower" : "upper");
      a.forEach(n => r.appendChild(toothButton(n, s)));
      g.appendChild(r);
    });
    root.appendChild(g);
  }

  function selected(root) {
    return Array.from(root.querySelectorAll(".tooth-button.selected")).map(x => Number(x.dataset.tooth));
  }

  function summary(root) {
    const t = root.querySelector(".dental-chart-selected"), v = selected(root);
    if (!t) return;
    t.textContent = v.length
      ? "دندان‌های انتخاب‌شده: " + v.sort((a, b) => a - b).join("، ")
      : "هنوز دندانی انتخاب نشده است.";
  }

  function syncArch(root) {
    const box = root.querySelector(".dental-integrated-arch");
    if (!box) return;
    if (window.DentalRayArchOdontogram) return window.DentalRayArchOdontogram.render(box, selected(root));
    load("script", "odontogramArchJs", "/js/odontogram-arch.js");
    const s = document.getElementById("odontogramArchJs");
    s?.addEventListener("load", () => window.DentalRayArchOdontogram?.render(box, selected(root)), { once: true });
  }

  // The natural jaw is a third self-contained renderer. Each drawn view loads its own
  // file and waits only for that one, so a missing renderer never blocks the others.
  function syncNatural(root) {
    const box = root.querySelector(".dental-integrated-natural");
    if (!box) return;
    if (window.DentalRayNaturalOdontogram) return window.DentalRayNaturalOdontogram.render(box, selected(root));
    load("script", "odontogramNaturalJs", "/js/odontogram-natural.js");
    const s = document.getElementById("odontogramNaturalJs");
    s?.addEventListener("load", () => window.DentalRayNaturalOdontogram?.render(box, selected(root)), { once: true });
  }

  const VIEWS = ["linear", "anatomical", "arch", "natural"];

  function setMode(root, m) {
    VIEWS.forEach(x => root.classList.remove("dental-view-" + x));
    root.classList.add("dental-view-" + m);
    root.querySelectorAll(".dental-view-button").forEach(b => b.classList.toggle("active", b.dataset.view === m));
    if (m === "arch") syncArch(root);
    if (m === "natural") syncNatural(root);
    try { localStorage.setItem(storageKey, m); } catch (_) { }
  }

  function toolbar() {
    const b = document.createElement("div");
    b.className = "dental-chart-toolbar";
    b.innerHTML = '<strong>شمای گرافیکی دندان‌ها</strong>' +
      '<span class="dental-chart-view-label">نوع نمایش:</span>' +
      '<button type="button" class="dental-view-button" data-view="linear">خطی</button>' +
      '<button type="button" class="dental-view-button" data-view="anatomical">آناتومیک</button>' +
      '<button type="button" class="dental-view-button" data-view="arch">قوسی</button>' +
      '<button type="button" class="dental-view-button" data-view="natural">فک طبیعی</button>';
    return b;
  }

  function ensureAssets(done) {
    load("link", "odontogramArchCss", "/css/odontogram-arch.css");
    load("script", "odontogramArchJs", "/js/odontogram-arch.js");
    load("link", "odontogramNaturalCss", "/css/odontogram-natural.css");
    load("script", "odontogramNaturalJs", "/js/odontogram-natural.js");
    // The tooth library feeds both drawn views, so make sure it is present.
    if (!window.DentalRayToothShapes) {
      const lib = document.getElementById("dentalrayToothShapes");
      if (!lib) {
        const s = document.createElement("script");
        s.id = "dentalrayToothShapes";
        s.src = "/js/tooth-shapes.js";
        s.addEventListener("load", () => ensureAssets(done), { once: true });
        document.body.appendChild(s);
        return;
      }
    }
    // Do not wait for the renderers here. Each view loads its own renderer in syncArch
    // and syncFan, and one of them being slow or absent must never hold back the
    // other - a chart that waits on an unrelated file is worse than a brief blank.
    done();
  }

  function load(tag, id, url) {
    if (document.getElementById(id)) return;
    const e = document.createElement(tag);
    e.id = id;
    if (tag === "link") { e.rel = "stylesheet"; e.href = url; document.head.appendChild(e); }
    else { e.src = url; document.body.appendChild(e); }
  }

  window.DentalRayDentalChart = {
    render(container, teeth) {
      if (!container) return;
      const s = new Set((teeth || []).map(Number));
      container.innerHTML = "";
      // Add the marker class rather than replacing className. Study cards pass in
      // "study-card-dental-chart study-chart-readonly", and overwriting it wiped the
      // rules that hide the view buttons and make the chart read-only.
      container.classList.add("dental-chart");
      const bar = toolbar();
      container.appendChild(bar);
      const arch = document.createElement("div");
      arch.className = "dental-integrated-arch";
      container.appendChild(arch);
      const natural = document.createElement("div");
      natural.className = "dental-integrated-natural";
      container.appendChild(natural);
      const body = document.createElement("div");
      body.className = "dental-chart-body";
      addSection(body, "دندان‌های دائمی", permanentRows, s);
      addSection(body, "دندان‌های شیری", primaryRows, s);
      container.appendChild(body);
      const sum = document.createElement("div");
      sum.className = "dental-chart-selected";
      container.appendChild(sum);
      bar.querySelectorAll(".dental-view-button").forEach(b => b.onclick = () => setMode(container, b.dataset.view));
      let mode = "arch";
      try { mode = localStorage.getItem(storageKey) || mode; } catch (_) { }
      if (!["linear", "anatomical", "arch", "natural"].includes(mode)) mode = "arch";
      ensureAssets(() => setMode(container, mode));
      summary(container);
    },
    getSelected(container) { return container ? selected(container).sort((a, b) => a - b) : []; }
  };

  // index.html loads the tooth library directly; ensureAssets() lazily injects it on
  // any page that only includes this file, so there is no unconditional load here
  // (which would add a second tag when the library is already present).
  load("link", "dentalGraphicStyles", "/css/dental-graphic.css");
  load("script", "dentalrayTerminology", "/js/frontend-terminology.js");})();
