// Dentix circular dental wheel - the "نمای جدید" view.
//
// A different way of showing the same FDI numbers: the teeth sit on a circle with
// their crowns facing the mouth in the middle, the way a mouth looks from the front,
// and each quadrant gets its own soft colour band. The arch view draws a
// perspective horseshoe; this one is deliberately flat, round and colourful.
//
// The tooth artwork still comes from tooth-shapes.js, so a molar is wide and has the
// right roots here too.
(function () {
  "use strict";

  const upper = [18, 17, 16, 15, 14, 13, 12, 11, 21, 22, 23, 24, 25, 26, 27, 28];
  const lower = [48, 47, 46, 45, 44, 43, 42, 41, 31, 32, 33, 34, 35, 36, 37, 38];

  const CX = 400, CY = 400;
  const RADIUS = 288;
  const SCALE = 1.15;
  // Radii from the outside in: the gum ring, the mouth disc the teeth face into, the
  // ring the numbers sit on, a thin decorative ring, then the coloured quadrant bands
  // at the centre. Keeping them apart stops anything landing on anything else.
  const MOUTH_RADIUS = RADIUS - 48;
  const NUMBER_RADIUS = RADIUS - 93;
  const INNER_RING = RADIUS - 118;
  const BAND_OUTER = RADIUS - 138;
  const BAND_INNER = RADIUS - 183;

  // The four bands behind the teeth. Colours are deliberately desaturated so the
  // teeth stay the subject, and each quadrant is still easy to tell apart.
  const QUADRANT_FILL = {
    1: "rgba(120, 170, 235, .26)",
    2: "rgba(120, 210, 200, .26)",
    3: "rgba(150, 205, 130, .26)",
    4: "rgba(235, 180, 120, .26)"
  };

  // Angle on the wheel, in radians, with screen coordinates (y grows downward).
  // The upper jaw spans the top half, the lower jaw the bottom half, and the two
  // midline teeth leave a small gap so they do not collide.
  function angleFor(index, isUpper) {
    const step = Math.PI / 16;
    const a = Math.PI + (index + 0.5) * step;
    return isUpper ? a : a + Math.PI;
  }

  // A tooth drawn with its crown up must be turned so "up" points at the middle.
  function rotationFor(a) {
    return Math.atan2(-Math.cos(a), Math.sin(a)) * (180 / Math.PI);
  }

  const FALLBACK = {
    crown: "M10 10 Q20 6 30 10 L29 25 Q20 29 11 25 Z",
    root: "M13 25 L16.5 38 Q20 40 23.5 38 L27 25 Z",
    detail: "M13 15 Q20 13 27 15",
    bounds: { x: 10, y: 6, w: 20, h: 34 }
  };

  function toothMarkup(number, index, isUpper, chosen) {
    const a = angleFor(index, isUpper);
    const x = CX + RADIUS * Math.cos(a);
    const y = CY + RADIUS * Math.sin(a);
    const rot = rotationFor(a);
    const shapes = window.DentalRayToothShapes;
    const s = shapes ? shapes.shapeOf(number) : FALLBACK;
    const b = s.bounds;
    const cx = b.x + b.w / 2, cy = b.y + b.h / 2;
    const k = SCALE;
    // The crown faces the middle, so the number sits inside the mouth ring on the
    // same radial line and is kept upright.
    const nx = CX + NUMBER_RADIUS * Math.cos(a);
    const ny = CY + NUMBER_RADIUS * Math.sin(a);
    const quadrant = shapes ? shapes.quadrantClass(number) : "";
    const selected = chosen.has(number) ? " selected" : "";

    return '<g class="fan-tooth' + (quadrant ? " " + quadrant : "") + selected +
      '" data-tooth="' + number + '"' +
      ' role="button" tabindex="0" aria-label="دندان ' + number + '"' +
      ' aria-pressed="' + (chosen.has(number) ? "true" : "false") + '">' +
      '<g transform="translate(' + x.toFixed(1) + " " + y.toFixed(1) + ") rotate(" + rot.toFixed(1) + ") scale(" + k + ')">' +
      '<g transform="translate(' + (-cx).toFixed(1) + " " + (-cy).toFixed(1) + ')">' +
      '<path class="fan-root" d="' + s.root + '"/>' +
      '<path class="fan-crown" d="' + s.crown + '"/>' +
      '<path class="fan-detail" d="' + s.detail + '"/>' +
      "</g></g>" +
      '<text class="fan-tooth-number" x="' + nx.toFixed(1) + '" y="' + ny.toFixed(1) + '">' + number + "</text>" +
      "</g>";
  }

  // One coloured band per quadrant, drawn as a ring segment so it follows the wheel.
  function bandMarkup(quadrant, fromDeg, toDeg) {
    // The bands sit inboard of the numbers, so the numbers stay on the plain mouth
    // surface and remain readable. They read as coloured spokes pointing at each
    // quadrant of teeth.
    const r1 = BAND_INNER, r2 = BAND_OUTER;
    const p = (r, deg) => {
      const a = deg * Math.PI / 180;
      return (CX + r * Math.cos(a)).toFixed(1) + " " + (CY + r * Math.sin(a)).toFixed(1);
    };
    const large = Math.abs(toDeg - fromDeg) > 180 ? 1 : 0;
    const d = "M" + p(r1, fromDeg) + " A" + r1 + " " + r1 + " 0 " + large + " 1 " + p(r1, toDeg) +
      " L" + p(r2, toDeg) + " A" + r2 + " " + r2 + " 0 " + large + " 0 " + p(r2, fromDeg) + " Z";
    return '<path class="fan-band quadrant-' + quadrant + '" d="' + d + '" fill="' + QUADRANT_FILL[quadrant] + '"/>';
  }

  function render(container, selected) {
    if (!container) return;
    const chosen = new Set((selected || []).map(Number));

    container.innerHTML =
      '<div class="fan-odontogram">' +
      '<svg viewBox="0 0 800 800" role="group" aria-label="شمای گرافیکی دایره‌ای دندان‌ها">' +
      "<defs>" +
      '<radialGradient id="fanGum" cx="50%" cy="50%" r="62%">' +
      '<stop offset="0" stop-color="#f7c3b9"/><stop offset="1" stop-color="#d3807a"/>' +
      "</radialGradient>" +
      '<radialGradient id="fanMouth" cx="50%" cy="50%" r="60%">' +
      '<stop offset="0" stop-color="#fff6f2"/><stop offset=".7" stop-color="#f8e2da"/><stop offset="1" stop-color="#eebfb4"/>' +
      "</radialGradient>" +
      "</defs>" +
      // Quadrant bands, then the gum ring, then the mouth, then the teeth.
      bandMarkup(1, 180, 270) +
      bandMarkup(2, 270, 360) +
      bandMarkup(3, 0, 90) +
      bandMarkup(4, 90, 180) +
      '<circle class="fan-gum" cx="' + CX + '" cy="' + CY + '" r="' + (RADIUS + 30) + '"/>' +
      '<circle class="fan-mouth" cx="' + CX + '" cy="' + CY + '" r="' + MOUTH_RADIUS + '"/>' +
      '<circle class="fan-inner" cx="' + CX + '" cy="' + CY + '" r="' + INNER_RING + '"/>' +
      '<text class="fan-label" x="' + CX + '" y="' + (CY - RADIUS + 6) + '">فک بالا</text>' +
      '<text class="fan-label" x="' + CX + '" y="' + (CY + RADIUS + 14) + '">فک پایین</text>' +
      upper.map((n, i) => toothMarkup(n, i, true, chosen)).join("") +
      lower.map((n, i) => toothMarkup(n, i, false, chosen)).join("") +
      "</svg></div>";

    container.querySelectorAll(".fan-tooth").forEach(g => {
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
          if (on) original.focus();
        }
      };
      g.addEventListener("click", toggle);
      g.addEventListener("keydown", e => {
        if (e.key === "Enter" || e.key === " ") { e.preventDefault(); toggle(); }
      });
    });
  }

  window.DentalRayFanOdontogram = { render };
})();
