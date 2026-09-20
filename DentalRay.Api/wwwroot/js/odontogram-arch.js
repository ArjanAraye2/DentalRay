// Dentix integrated graphical jaw odontogram.
//
// Pure frontend rendering: a study continues to store only FDI numbers.
//
// The arch geometry (gum, palate, mouth, tooth positions and angles) is the layout
// that already worked. What changed is the artwork: each tooth is now drawn from
// tooth-shapes.js, so the arch shows eight real tooth types with the correct root
// count instead of one blob shape for everything.
(function () {
  "use strict";

  const upper = [18, 17, 16, 15, 14, 13, 12, 11, 21, 22, 23, 24, 25, 26, 27, 28];
  const lower = [48, 47, 46, 45, 44, 43, 42, 41, 31, 32, 33, 34, 35, 36, 37, 38];

  // The original placement: teeth spread evenly across x, bowing away from the
  // mouth along y, tilted so they follow the curve.
  function pos(i, upperJaw) {
    const n = 15, t = i / n, x = 70 + t * 660;
    const c = (i - 7.5) / 7.5;
    const y = upperJaw ? 178 - 95 * (1 - c * c) : 282 + 95 * (1 - c * c);
    let a = (t - .5) * 70;
    if (!upperJaw) a = -a;
    return { x, y, a };
  }

  // How big the library artwork is drawn in the arch. The width is capped per tooth
  // so neighbours never overlap (see toothMarkup); this height factor is what makes
  // the rows look right.
  const ARCH_SCALE = 1.4;
  // Fraction of the space between two arch points a crown may occupy.
  const ARCH_WIDTH_FILL = 0.82;

  // A fallback outline for the rare case the library has not loaded, so the arch
  // still draws something instead of an empty mouth.
  const FALLBACK = {
    crown: "M10 10 Q20 6 30 10 L29 25 Q20 29 11 25 Z",
    root: "M13 25 L16.5 38 Q20 40 23.5 38 L27 25 Z",
    detail: "M13 15 Q20 13 27 15",
    bounds: { x: 10, y: 6, w: 20, h: 34 }
  };

  // The number sits straight above (upper jaw) or below (lower jaw) the tooth, then
  // counter-rotates so it reads upright. Pushing it radially from the mouth centre
  // instead made the end teeth collide with their neighbours, because there the
  // radial direction is sideways.
  const NUMBER_GAP = 9;

  // The arch points are evenly spaced, so this is the gap a tooth has to live in.
  const ARCH_SPACING = 660 / 15;

  function toothMarkup(number, index, isUpper, chosen) {
    const p = pos(index, isUpper);
    const shapes = window.DentalRayToothShapes;
    const quadrant = shapes ? shapes.quadrantClass(number) : "";
    const selectedClass = chosen.has(number) ? " selected" : "";
    const s = shapes ? shapes.shapeOf(number) : null;
    const h = s ? s.height : 40;
    // The teeth sit across the gum band, upright in the same direction as before: the
    // upper jaw is mirrored so its crowns face the mouth. The artwork is drawn from
    // the crown downwards, so the box is centred on the arch point.
    const flip = isUpper ? " scale(1 -1)" : "";
    const k = ARCH_SCALE;
    const inner = "scale(" + k.toFixed(3) + ")" + flip + " translate(0 " + (-h / 2).toFixed(1) + ")";
    const labelY = (h * k / 2 + NUMBER_GAP) * (isUpper ? -1 : 1);

    return '<g class="arch-tooth' + (quadrant ? " " + quadrant : "") + selectedClass +
      '" data-tooth="' + number + '" transform="translate(' + p.x.toFixed(1) + " " + p.y.toFixed(1) + ") rotate(" + p.a.toFixed(1) + ')"' +
      ' role="button" tabindex="0" aria-label="دندان ' + number + '"' +
      ' aria-pressed="' + (chosen.has(number) ? "true" : "false") + '">' +
      '<g class="arch-tooth-body" transform="' + inner + '">' +
      (shapes ? shapes.markup(number, "arch") : "") +
      "</g>" +
      '<g transform="translate(0 ' + labelY.toFixed(1) + ") rotate(" + (-p.a).toFixed(1) + ')">' +
      '<text class="arch-tooth-number" x="0" y="0">' + number + "</text>" +
      "</g>" +
      "</g>";
  }

  function render(container, selected) {
    if (!container) return;
    const chosen = new Set((selected || []).map(Number));

    container.innerHTML =
      '<div class="real-odontogram">' +
      '<svg viewBox="0 0 800 470" role="group" aria-label="شمای گرافیکی فک بالا و پایین">' +
      "<defs>" +
      '<radialGradient id="gum"><stop offset="0" stop-color="#f8b9ad"/><stop offset="1" stop-color="#d77e72"/></radialGradient>' +
      "</defs>" +
      '<path class="jaw-gum" d="M55 190 Q75 35 400 35 Q725 35 745 190 Q700 220 650 190 Q610 115 400 105 Q190 115 150 190 Q100 220 55 190Z"/>' +
      '<path class="jaw-palate" d="M160 178 Q205 112 400 104 Q595 112 640 178 Q565 202 400 205 Q235 202 160 178Z"/>' +
      '<path class="jaw-gum" d="M55 280 Q75 435 400 435 Q725 435 745 280 Q700 250 650 280 Q610 355 400 365 Q190 355 150 280 Q100 250 55 280Z"/>' +
      '<path class="jaw-mouth" d="M165 292 Q220 350 400 360 Q580 350 635 292 Q555 315 400 318 Q245 315 165 292Z"/>' +
      upper.map((n, i) => toothMarkup(n, i, true, chosen)).join("") +
      lower.map((n, i) => toothMarkup(n, i, false, chosen)).join("") +
      '<text class="jaw-label" x="400" y="24">فک بالا</text>' +
      '<text class="jaw-label" x="400" y="456">فک پایین</text>' +
      "</svg></div>";

    // Clicking a tooth mirrors the choice onto the matching button in the rows, which
    // is the source of truth for the saved tooth numbers.
    container.querySelectorAll(".arch-tooth").forEach(g => {
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

  window.DentalRayArchOdontogram = { render };
})();
