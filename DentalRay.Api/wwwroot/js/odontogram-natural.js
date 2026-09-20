// Dentix natural jaw - the "فک طبیعی" view.
//
// The wheel view was too abstract: a circle of teeth does not look like a mouth. This
// one draws a real dental arch - a horseshoe that is deeper than it is wide, with the
// incisors at the front and the molars curving back - and the teeth sit on it at their
// real relative widths, so an incisor is narrow and a molar is wide.
//
// The two jaws are drawn the way an open mouth looks from the front: the upper arch
// above, the lower arch below, the incisors facing each other and the crowns pointing
// into the mouth. The whole figure can be spun in its own plane and tilted in 3D.
//
// Geometry is in a local space centred on the middle of the mouth, then moved into the
// viewBox by one group transform, so a full turn never clips.
(function () {
  "use strict";

  const UPPER = [18, 17, 16, 15, 14, 13, 12, 11, 21, 22, 23, 24, 25, 26, 27, 28];
  const LOWER = [48, 47, 46, 45, 44, 43, 42, 41, 31, 32, 33, 34, 35, 36, 37, 38];

  // Local space: origin at the middle of the mouth, y grows downward.
  const CENTER = 530;          // where the local origin lands in the viewBox
  const VIEW = 1060;
  const HALF_WIDTH = 286;      // incisor to the last molar, sideways
  const DEPTH = 320;           // incisor to the last molar, front to back
  const FRONT = { upper: -44, lower: 46 };  // y of the incisors; they face each other
  const FRONT_FLAT = 1.3;      // real arches are flatter at the front than an ellipse
  const FILL = 0.88;           // share of the arch the teeth take; the rest is gaps
  const FIT = 0.98;            // keeps a full turn inside the viewBox

  function archPoint(t, jaw) {
    // t: -1 at the left molar, 0 at the incisors, +1 at the right molar.
    const th = t * Math.PI / 2;
    const u = Math.sin(th);
    const v = Math.pow(1 - Math.cos(th), FRONT_FLAT);
    const y = jaw === "upper" ? FRONT.upper - DEPTH * v : FRONT.lower + DEPTH * v;
    return { x: HALF_WIDTH * u, y: y };
  }

  // The interior each jaw's teeth face away from: the palate above, the tongue below.
  const FOCUS = {
    upper: { x: 0, y: FRONT.upper - DEPTH * 0.55 },
    lower: { x: 0, y: FRONT.lower + DEPTH * 0.55 }
  };

  function buildArch(jaw) {
    const N = 220, pts = [];
    let s = 0, prev = null;
    for (let i = 0; i <= N; i++) {
      const p = archPoint(-1 + 2 * i / N, jaw);
      if (prev) s += Math.hypot(p.x - prev.x, p.y - prev.y);
      pts.push({ x: p.x, y: p.y, s: s });
      prev = p;
    }
    return pts;
  }

  function pointAt(pts, s) {
    const last = pts.length - 1;
    if (s <= 0) return pts[0];
    if (s >= pts[last].s) return pts[last];
    let lo = 0, hi = last;
    while (lo + 1 < hi) {
      const mid = (lo + hi) >> 1;
      if (pts[mid].s <= s) lo = mid; else hi = mid;
    }
    const a = pts[lo], b = pts[hi], span = b.s - a.s;
    const f = span ? (s - a.s) / span : 0;
    return { x: a.x + (b.x - a.x) * f, y: a.y + (b.y - a.y) * f };
  }

  const FALLBACK = { crown: "M10 10 Q20 6 30 10 L29 25 Q20 29 11 25 Z", root: "M13 25 L16.5 38 Q20 40 23.5 38 L27 25 Z", detail: "M13 15 Q20 13 27 15" };

  function layout(jaw) {
    const shapes = window.DentalRayToothShapes;
    const teeth = jaw === "upper" ? UPPER : LOWER;
    const pts = buildArch(jaw);
    const total = pts[pts.length - 1].s;
    const info = teeth.map(n => (shapes ? shapes.shapeOf(n) : FALLBACK));
    const sumW = info.reduce((a, s) => a + (s.crownWidth || 20), 0);
    // One scale for the whole jaw: the teeth keep their real width ratios, so a molar
    // is wide and an incisor is narrow, and together they fill the arch.
    const k = (total * FILL) / sumW;
    let acc = (total - sumW * k) / 2;
    const focus = FOCUS[jaw];

    return teeth.map((n, i) => {
      const shape = info[i];
      const w = (shape.crownWidth || 20) * k;
      const at = acc + w / 2;
      acc += w;
      const p = pointAt(pts, at);
      // Tangent by finite difference, then the normal, then make sure it points away
      // from the inside of the mouth so the crown faces the mouth opening.
      const a = pointAt(pts, at - 3), b = pointAt(pts, at + 3);
      let tx = b.x - a.x, ty = b.y - a.y;
      const tl = Math.hypot(tx, ty) || 1; tx /= tl; ty /= tl;
      let nx = -ty, ny = tx;
      if ((p.x - focus.x) * nx + (p.y - focus.y) * ny < 0) { nx = -nx; ny = -ny; }
      const rot = Math.atan2(nx, -ny) * 180 / Math.PI;
      return { n: n, shape: shape, x: p.x, y: p.y, rot: rot, nx: nx, ny: ny, k: k, w: w };
    });
  }

  function toothMarkup(t, chosen) {
    const s = t.shape;
    const cx = 20, cy = 21;  // the tooth library draws around this point
    const selected = chosen.has(t.n) ? " selected" : "";
    const labelGap = (21 * t.k) + 13;
    const lx = t.x + t.nx * labelGap, ly = t.y + t.ny * labelGap;
    return '<g class="jaw-tooth' + selected + '" data-tooth="' + t.n + '"' +
      ' role="button" tabindex="0" aria-label="دندان ' + t.n + '"' +
      ' aria-pressed="' + (chosen.has(t.n) ? "true" : "false") + '">' +
      '<g transform="translate(' + t.x.toFixed(1) + " " + t.y.toFixed(1) + ") rotate(" + t.rot.toFixed(1) + ") scale(" + t.k.toFixed(3) + ')">' +
      '<g transform="translate(' + (-cx) + " " + (-cy) + ')">' +
      '<path class="jaw-root" d="' + s.root + '"/>' +
      '<path class="jaw-crown" d="' + s.crown + '"/>' +
      '<path class="jaw-detail" d="' + s.detail + '"/>' +
      "</g></g>" +
      // Counter-rotate so the number stays upright: the text sits outside the tooth's
      // rotated group, so it only has to cancel the jaw's spin (apply() recomputes it).
      '<text class="jaw-tooth-number" data-base="0" data-x="' + lx.toFixed(1) + '" data-y="' + ly.toFixed(1) + '" transform="translate(' + lx.toFixed(1) + " " + ly.toFixed(1) + ') rotate(0)" text-anchor="middle">' + t.n + "</text>" +
      "</g>";
  }

  const pathOf = pts => pts.map((p, i) => (i ? "L" : "M") + p.x.toFixed(1) + " " + p.y.toFixed(1)).join(" ");

  function render(container, selected) {
    if (!container) return;
    const chosen = new Set((selected || []).map(Number));
    const shapes = window.DentalRayToothShapes;
    if (!shapes) return;

    const upperPts = buildArch("upper"), lowerPts = buildArch("lower");
    const upper = layout("upper"), lower = layout("lower");

    container.innerHTML =
      '<div class="natural-odontogram">' +
      '<div class="natural-stage">' +
      '<svg viewBox="0 0 ' + VIEW + " " + VIEW + '" role="group" aria-label="شکل طبیعی فک و دندان‌ها">' +
      "<defs>" +
      '<radialGradient id="jawGum" cx="50%" cy="40%" r="70%">' +
      '<stop offset="0" stop-color="#f6bdb2"/><stop offset="1" stop-color="#d3766d"/>' +
      "</radialGradient>" +
      '<linearGradient id="jawPalate" x1="0" y1="0" x2="0" y2="1">' +
      '<stop offset="0" stop-color="#ffd9cf"/><stop offset="1" stop-color="#f3b6a8"/>' +
      "</linearGradient>" +
      "</defs>" +
      '<g class="jaw-rotor" transform="translate(' + CENTER + " " + CENTER + ") scale(" + FIT + ')">' +
      // Palate (inside the upper arch) and the floor of the mouth (inside the lower).
      '<path class="jaw-inside" id="jawPalate" d="' + pathOf(upperPts) + ' Z"/>' +
      '<path class="jaw-inside lower" id="jawFloor" d="' + pathOf(lowerPts) + ' Z"/>' +
      // The gum band follows the arch.
      '<path class="jaw-gum" d="' + pathOf(upperPts) + '"/>' +
      '<path class="jaw-gum" d="' + pathOf(lowerPts) + '"/>' +
      upper.map(t => toothMarkup(t, chosen)).join("") +
      lower.map(t => toothMarkup(t, chosen)).join("") +
      "</g></svg></div>" +
      '<div class="natural-controls">' +
      '<label class="natural-control"><span>چرخش</span><input type="range" min="0" max="360" step="1" value="0" data-ctrl="spin" /></label>' +
      '<label class="natural-control"><span>شیب</span><input type="range" min="-75" max="75" step="1" value="0" data-ctrl="tiltX" /></label>' +
      '<label class="natural-control"><span>گردش</span><input type="range" min="-75" max="75" step="1" value="0" data-ctrl="tiltY" /></label>' +
      '<button type="button" class="secondary-button natural-reset">بازگشت به حالت اول</button>' +
      '<span class="natural-hint">برای چرخاندن، فک را با ماوس یا انگشت بکشید</span>' +
      "</div></div>";

    const rotor = container.querySelector(".jaw-rotor");
    const stage = container.querySelector(".natural-stage");
    const state = { spin: 0, tiltX: 0, tiltY: 0 };

    // Rotation only moves a transform, so dragging never rebuilds the teeth.
    const apply = () => {
      rotor.setAttribute("transform", "translate(" + CENTER + " " + CENTER + ") scale(" + FIT + ") rotate(" + state.spin.toFixed(1) + ")");
      // A number's own rotation is fixed; only the spin the whole jaw gained has to be
      // cancelled, so the label stays upright as the jaw turns.
      container.querySelectorAll(".jaw-tooth-number").forEach(txt => {
        const base = parseFloat(txt.dataset.base || "0");
        txt.setAttribute("transform", "translate(" + txt.dataset.x + " " + txt.dataset.y + ") rotate(" + (base - state.spin).toFixed(1) + ")");
      });
      stage.style.transform = "perspective(1500px) rotateX(" + state.tiltX.toFixed(1) + "deg) rotateY(" + state.tiltY.toFixed(1) + "deg)";
    };

    const sliders = {};
    container.querySelectorAll("[data-ctrl]").forEach(input => { sliders[input.dataset.ctrl] = input; });
    const syncSliders = () => {
      if (sliders.spin) sliders.spin.value = String(state.spin);
      if (sliders.tiltX) sliders.tiltX.value = String(state.tiltX);
      if (sliders.tiltY) sliders.tiltY.value = String(state.tiltY);
    };
    Object.keys(sliders).forEach(key => sliders[key].addEventListener("input", () => {
      state[key] = Number(sliders[key].value);
      apply();
    }));

    container.querySelector(".natural-reset").addEventListener("click", () => {
      state.spin = 0; state.tiltX = 0; state.tiltY = 0;
      syncSliders(); apply();
    });

    // Drag to turn: sideways spins the jaw in its plane, up and down tilts it.
    let dragging = false, lastX = 0, lastY = 0;
    const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));
    stage.addEventListener("pointerdown", e => {
      dragging = true; lastX = e.clientX; lastY = e.clientY;
      stage.classList.add("is-dragging");
      try { stage.setPointerCapture(e.pointerId); } catch (_) { }
    });
    stage.addEventListener("pointermove", e => {
      if (!dragging) return;
      const dx = e.clientX - lastX, dy = e.clientY - lastY;
      lastX = e.clientX; lastY = e.clientY;
      state.spin = (state.spin + dx * 0.7 + 360) % 360;
      state.tiltX = clamp(state.tiltX + dy * 0.5, -75, 75);
      syncSliders(); apply();
    });
    const stop = () => { dragging = false; stage.classList.remove("is-dragging"); };
    stage.addEventListener("pointerup", stop);
    stage.addEventListener("pointercancel", stop);
    stage.addEventListener("lostpointercapture", stop);

    // Selection mirrors onto the hidden buttons of the linear view, which stay the
    // source of truth for the saved tooth numbers.
    container.querySelectorAll(".jaw-tooth").forEach(g => {
      const toggle = () => {
        g.classList.toggle("selected");
        const on = g.classList.contains("selected");
        g.setAttribute("aria-pressed", on ? "true" : "false");
        const n = Number(g.dataset.tooth);
        const root = container.closest(".dental-chart");
        const original = root && root.querySelector('.tooth-button[data-tooth="' + n + '"]');
        if (original) {
          original.classList.toggle("selected", on);
          original.setAttribute("aria-pressed", on ? "true" : "false");
          original.dispatchEvent(new CustomEvent("odontogram-sync"));
        }
      };
      g.addEventListener("click", toggle);
      g.addEventListener("keydown", e => {
        if (e.key === "Enter" || e.key === " ") { e.preventDefault(); toggle(); }
      });
    });

    apply();
  }

  window.DentalRayNaturalOdontogram = { render, FOCUS: FOCUS, archPoint: archPoint };
})();
