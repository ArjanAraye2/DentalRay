// Dentix CBCT view - the "CBCT" view.
//
// A CBCT study is read on a dark screen, so this view is deliberately dark like the
// imaging software a dentist already knows rather than the light clinical UI.
//
// Three schemes, chosen with a small toolbar:
//   pano     a panoramic radiograph: one "smile" curve with the mandible rami rising
//            at the sides, roots inside bone, the way an OPG looks
//   mpr      three orthogonal slices side by side - axial, sagittal, coronal - the
//            way a CBCT viewer lays them out
//   volume   a shaded 3D jaw that can be spun left and right
//
// All three read the same arch geometry (tooth-arch.js) and the same tooth artwork
// (tooth-shapes.js), so switching schemes never changes which teeth are where.
(function () {
  "use strict";

  const UPPER = [18, 17, 16, 15, 14, 13, 12, 11, 21, 22, 23, 24, 25, 26, 27, 28];
  const LOWER = [48, 47, 46, 45, 44, 43, 42, 41, 31, 32, 33, 34, 35, 36, 37, 38];

  const FALLBACK = { crown: "M10 10 Q20 6 30 10 L29 25 Q20 29 11 25 Z", root: "M13 25 L16.5 38 Q20 40 23.5 38 L27 25 Z", detail: "M13 15 Q20 13 27 15", crownWidth: 20 };

  const esc = v => String(v ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

  // ---------------------------------------------------------------- panoramic
  //
  // The classic OPG shape: a wide arc that dips at the front, with the mandible rami
  // swinging up on both sides. Drawn in bone grey on black, with the teeth brighter
  // than the bone and the roots visible inside it.
  function renderPano(container, chosen) {
    const lib = window.DentalRayToothArch;
    const shapes = window.DentalRayToothShapes;
    if (!lib || !shapes) return;

    const CENTER = 500, VIEW = 1150;

    // Panoramic curve: the occlusal plane. x is linear in t, so the teeth are spread
    // evenly across the film the way a flattened panoramic projection does; y dips at
    // the front (t = 0) so the arch reads as a smile rather than a straight line.
    const CURVE_Y = t => 250 + 86 * Math.cos(t * Math.PI / 2);
    const CURVE_X = t => 500 + 470 * t;

    // The two rows sit on opposite sides of that plane. The gap is wide enough that
    // the tilted molars at the ends of the curve never cross the other row.
    const ROW_GAP = 82;
    // The teeth are sized to the space the curve gives them. The curve is 959 units
    // long, so each of the 16 teeth per jaw owns about 30 units; the widest library
    // tooth is 28 units, which leaves a small gap between neighbours. Scaling the
    // artwork by the same factor keeps the real width ratios (molar wide, incisor
    // narrow) without any tooth touching the next.
    const TOOTH_SCALE = 1.0;

    function place(teeth, jaw) {
      const k = TOOTH_SCALE;
      const info = teeth.map(n => shapes.shapeOf(n) || FALLBACK);
      const upper = jaw === "upper";
      // Where the row sits relative to the curve, and which way its crowns point.
      const rowDir = upper ? -1 : 1;
      const crownTurn = upper ? 180 : 0;

      // Teeth are distributed by ARC LENGTH along the curve, not by a fraction of the
      // width. The panoramic curve flattens at its ends, so a width fraction bunched
      // the molars together and they overlapped. Walking the arc keeps the spacing
      // even, which is how a real panoramic film looks.
      const N = 240, samples = [];
      let total = 0, prev = null;
      for (let i = 0; i <= N; i++) {
        const t = -1 + 2 * i / N;
        const x = CURVE_X(t), y = CURVE_Y(t);
        if (prev) total += Math.hypot(x - prev.x, y - prev.y);
        samples.push({ t: t, x: x, y: y, s: total });
        prev = { x: x, y: y };
      }
      const at = s => {
        if (s <= 0) return samples[0];
        if (s >= total) return samples[N];
        let lo = 0, hi = N;
        while (lo + 1 < hi) { const mid = (lo + hi) >> 1; if (samples[mid].s <= s) lo = mid; else hi = mid; }
        const a = samples[lo], b = samples[hi], span = b.s - a.s;
        const f = span ? (s - a.s) / span : 0;
        return { t: a.t + (b.t - a.t) * f, x: a.x + (b.x - a.x) * f, y: a.y + (b.y - a.y) * f };
      };

      const sumW = info.reduce((a, s) => a + (s.crownWidth || 20), 0) * k;
      let acc = (total - sumW) / 2;
      return teeth.map((n, i) => {
        const w = (info[i].crownWidth || 20) * k;
        const here = at(acc + w / 2);
        acc += w;
        // A panoramic film projects the teeth onto the curve, so they stand upright
        // rather than leaning with it. Leaning looked right but a tilted tooth takes up
        // much more width than its crown (a 28-unit molar at 10 degrees needs 33), and
        // the molars at the ends of the curve collided. Upright is both more accurate
        // and gives every tooth room.
        return { n: n, shape: info[i], x: here.x, y: here.y + rowDir * ROW_GAP, rot: crownTurn, k: k, w: w };
      });
    }

    const upper = place(UPPER, "upper");
    const lower = place(LOWER, "lower");

    // The rami: two vertical bands at the ends of the lower curve, like the two
    // uprights of the mandible in a panoramic film.
    const ramus = side => {
      const t = side, x = CURVE_X(t), y = CURVE_Y(t);
      const inner = x - Math.sign(t) * 34, outer = x + Math.sign(t) * 30, top = y - 300;
      return '<path class="cbct-ramus" d="M' + inner + " " + (y + 20) + " L" + inner + " " + top +
        " Q" + ((inner + outer) / 2) + " " + (top - 26) + " " + outer + " " + top +
        " L" + outer + " " + (y + 20) + " Z\"/>";
    };

    function toothMarkup(t, chosen) {
      const s = t.shape, cx = 20, cy = 21;
      const sel = chosen.has(t.n) ? " selected" : "";
      return '<g class="cbct-tooth' + sel + '" data-tooth="' + t.n + '" role="button" tabindex="0"' +
        ' aria-label="دندان ' + t.n + '" aria-pressed="' + (chosen.has(t.n) ? "true" : "false") + '">' +
        '<g transform="translate(' + t.x.toFixed(1) + " " + t.y.toFixed(1) + ") rotate(" + t.rot.toFixed(1) + ") scale(" + t.k.toFixed(3) + ')">' +
        '<g transform="translate(-20 -21)">' +
        '<path class="cbct-root" d="' + s.root + '"/>' +
        '<path class="cbct-crown" d="' + s.crown + '"/>' +
        '<path class="cbct-detail" d="' + s.detail + '"/>' +
        "</g></g></g>";
    }

    container.innerHTML =
      '<div class="cbct-view cbct-pano">' +
      '<svg viewBox="0 0 ' + VIEW + " " + VIEW + '" role="group" aria-label="نمای پانورامیک سی‌بی‌سی‌تی">' +
      "<defs>" +
      '<linearGradient id="cbctFilm" x1="0" y1="0" x2="0" y2="1">' +
      '<stop offset="0" stop-color="#0b0e12"/><stop offset=".5" stop-color="#171c23"/><stop offset="1" stop-color="#0b0e12"/>' +
      "</linearGradient>" +
      '<radialGradient id="cbctBeam" cx="50%" cy="42%" r="62%">' +
      '<stop offset="0" stop-color="#3b4653" stop-opacity=".55"/><stop offset="1" stop-color="#000" stop-opacity="0"/>' +
      "</radialGradient>" +
      "</defs>" +
      '<rect class="cbct-film" x="0" y="0" width="' + VIEW + '" height="' + VIEW + '"/>' +
      '<rect class="cbct-beam" x="0" y="0" width="' + VIEW + '" height="' + VIEW + '"/>' +
      // Bone body of the mandible and the maxilla.
      '<path class="cbct-bone" d="' + bonePath(CURVE_X, CURVE_Y, 1.0, 96) + '"/>' +
      '<path class="cbct-bone upper" d="' + bonePath(CURVE_X, CURVE_Y, 0.86, -150) + '"/>' +
      ramus(1) + ramus(-1) +
      // Mandibular canal, a faint pair of lines inside the lower bone.
      '<path class="cbct-canal" d="' + canalPath(CURVE_X, CURVE_Y) + '"/>' +
      lower.map(t => toothMarkup(t, chosen)).join("") +
      upper.map(t => toothMarkup(t, chosen)).join("") +
      '<text class="cbct-label" x="' + (CENTER - 330) + '" y="1020">CBCT · نمای پانورامیک</text>' +
      "</svg></div>";

    wire(container);
  }

  function bonePath(X, Y, scale, thickness) {
    const pts = [];
    for (let i = 0; i <= 60; i++) {
      const t = -1 + 2 * i / 60;
      pts.push((i ? "L" : "M") + X(t * scale).toFixed(1) + " " + (Y(t * scale) + 34).toFixed(1));
    }
    for (let i = 60; i >= 0; i--) {
      const t = -1 + 2 * i / 60;
      pts.push("L" + X(t * scale).toFixed(1) + " " + (Y(t * scale) + 34 + thickness).toFixed(1));
    }
    return pts.join(" ") + " Z";
  }

  function canalPath(X, Y) {
    const pts = [];
    for (let i = 0; i <= 40; i++) {
      const t = -1 + 2 * i / 40;
      pts.push((i ? "L" : "M") + X(t * 0.82).toFixed(1) + " " + (Y(t * 0.82) + 78).toFixed(1));
    }
    return pts.join(" ");
  }

  // ---------------------------------------------------------------- MPR slices
  //
  // Three orthogonal cuts, laid out the way a CBCT viewer does: axial (looking down),
  // sagittal (from the side) and coronal (from the front). Each is the same arch from
  // a different plane, drawn as grey tissue on black.
  function renderMpr(container, chosen) {
    const lib = window.DentalRayToothArch, shapes = window.DentalRayToothShapes;
    if (!lib || !shapes) return;

    // The viewBox must hold the three panels plus the gaps and padding, with a little
    // slack so the rounded frame strokes are not clipped.
    const PANEL = 420, GAP = 26, PAD = 24;
    const VIEW = PANEL * 3 + GAP * 2 + PAD * 2 + 8;

    function panel(x, y, label, inner) {
      return '<g class="cbct-panel" transform="translate(' + x + " " + y + ')">' +
        '<rect class="cbct-panel-bg" x="0" y="0" width="' + PANEL + '" height="' + PANEL + '" rx="10"/>' +
        "<g>" + inner + "</g>" +
        '<rect class="cbct-panel-frame" x="0" y="0" width="' + PANEL + '" height="' + PANEL + '" rx="10"/>' +
        '<text class="cbct-panel-label" x="14" y="28" text-anchor="start" direction="ltr">' + esc(label) + "</text>" +
        "</g>";
    }

    // Fit scale for a panel: the content is measured and scaled to sit inside the panel
    // with a margin, so nothing overflows its own frame.
    const MARGIN = 34;
    function fitScale(halfW, halfH) {
      return Math.min((PANEL / 2 - MARGIN) / halfW, (PANEL / 2 - MARGIN) / halfH);
    }

    // Layout bounds of one jaw in local units, so the fit is exact rather than guessed.
    function boundsOf(list) {
      let maxX = 0, maxY = 0;
      list.forEach(t => {
        maxX = Math.max(maxX, Math.abs(t.x) + (t.w / 2));
        maxY = Math.max(maxY, Math.abs(t.y) + (t.w / 2));
      });
      return { x: maxX, y: maxY };
    }

    // Axial: the arch seen from above, both jaws, teeth as bright discs.
    function axial() {
      const upper = lib.layout("upper", UPPER), lower = lib.layout("lower", LOWER);
      const all = upper.concat(lower);
      // Fit the arch so the discs stay inside the panel. The layout already knows each
      // tooth's drawn width, so the span including half a tooth is the real extent.
      const halfX = Math.max.apply(null, all.map(t => Math.abs(t.x) + t.w * 0.6));
      const halfY = Math.max.apply(null, all.map(t => Math.abs(t.y) + t.w * 0.6));
      const s = Math.min((PANEL / 2 - 24) / halfX, (PANEL / 2 - 34) / halfY);
      const cx = PANEL / 2, cy = PANEL / 2 + 4;
      const disc = t => {
        const sel = chosen.has(t.n) ? " selected" : "";
        // A slice through a tooth shows its cross-section, so the disc is sized from
        // the tooth's own width. It is slightly smaller than the tooth so neighbouring
        // discs in the slice stay separate, with a minimum to keep the front teeth
        // clickable.
        const r = Math.max(12, (t.w / 2) * s * 0.85);
        return '<g class="cbct-disc' + sel + '" data-tooth="' + t.n + '" role="button" tabindex="0">' +
          '<circle cx="' + (cx + t.x * s).toFixed(1) + '" cy="' + (cy + t.y * s).toFixed(1) + '" r="' + r.toFixed(1) + '"/>' +
          '<text x="' + (cx + t.x * s).toFixed(1) + '" y="' + (cy + t.y * s + 3.6).toFixed(1) + '">' + t.n + "</text></g>";
      };
      return upper.map(disc).join("") + lower.map(disc).join("");
    }

    // Sagittal: a side slice. The teeth of one quadrant seen from the side, with the
    // jaw bone above and below and the condyle at the back.
    function sagittal() {
      const oy = PANEL / 2;
      const side = [18, 17, 16, 15, 14, 13, 12, 11];
      const shape = n => shapes.shapeOf(n) || FALLBACK;
      const pitch = (PANEL - 120) / side.length;
      const k = Math.min(1.15, pitch / 34);
      const total = pitch * side.length;
      const startX = (PANEL - total) / 2 + pitch / 2;
      let row = "";
      side.forEach((n, i) => {
        const sh = shape(n);
        const x = startX + i * pitch;
        const sel = chosen.has(n) ? " selected" : "";
        row += '<g class="cbct-side' + sel + '" data-tooth="' + n + '" role="button" tabindex="0">' +
          '<g transform="translate(' + x.toFixed(1) + " " + oy + ") scale(" + k.toFixed(3) + ')">' +
          '<g transform="translate(-20 -21)">' +
          '<path class="cbct-root" d="' + sh.root + '"/>' +
          '<path class="cbct-crown" d="' + sh.crown + '"/></g></g></g>';
      });
      const edge = 34;
      return '<path class="cbct-sidebone" d="M' + edge + " " + (oy - 54) + " L" + (PANEL - edge) + " " + (oy - 54) +
        " L" + (PANEL - edge) + " " + (oy - 96) + ' Q' + (PANEL - 70) + " " + (oy - 122) + " " + (PANEL - 130) + " " + (oy - 98) +
        " L120 " + (oy - 96) + ' Q70 ' + (oy - 120) + " " + edge + " " + (oy - 96) + ' Z"/>' +
        row +
        '<path class="cbct-sidebone" d="M' + edge + " " + (oy + 52) + " L" + (PANEL - edge) + " " + (oy + 52) +
        " L" + (PANEL - edge) + " " + (oy + 104) + " L" + edge + " " + (oy + 104) + ' Z"/>';
    }

    // Coronal: a front slice. The arch seen head on, the upper row above the lower.
    function coronal() {
      const upper = lib.layout("upper", UPPER), lower = lib.layout("lower", LOWER);
      const b = boundsOf(upper.concat(lower));
      const half = PANEL / 2 - 40;
      const s = Math.min(half / b.x, (PANEL / 2 - 96) / (Math.abs(b.y) * 1.6 + 40));
      const cx = PANEL / 2, cy = PANEL / 2;
      const row = (list, dir) => list.map(t => {
        const sel = chosen.has(t.n) ? " selected" : "";
        const k = Math.min(s * 2.1, 1.5);
        return '<g class="cbct-cor' + sel + '" data-tooth="' + t.n + '" role="button" tabindex="0">' +
          '<g transform="translate(' + (cx + t.x * s).toFixed(1) + " " + (cy + dir * 74).toFixed(1) + ") scale(" + k.toFixed(3) + ')">' +
          '<g transform="translate(-20 -21)">' +
          '<path class="cbct-root" d="' + t.shape.root + '"/>' +
          '<path class="cbct-crown" d="' + t.shape.crown + '"/></g></g></g>';
      }).join("");
      const l = 60, r = PANEL - 60;
      return '<path class="cbct-corbone" d="M' + l + " " + (cy - 118) + " Q" + (PANEL / 2) + " " + (cy - 156) + " " + r + " " + (cy - 118) +
        " L" + r + " " + (cy - 88) + " Q" + (PANEL / 2) + " " + (cy - 122) + " " + l + " " + (cy - 88) + ' Z"/>' +
        row(upper, -1) + row(lower, 1) +
        '<path class="cbct-corbone" d="M' + l + " " + (cy + 116) + " Q" + (PANEL / 2) + " " + (cy + 154) + " " + r + " " + (cy + 116) +
        " L" + r + " " + (cy + 88) + " Q" + (PANEL / 2) + " " + (cy + 122) + " " + l + " " + (cy + 88) + ' Z"/>';
    }

    container.innerHTML =
      '<div class="cbct-view cbct-mpr">' +
      '<svg viewBox="0 0 ' + VIEW + " " + VIEW + '" role="group" aria-label="برش‌های سی‌بی‌سی‌تی">' +
      panel(PAD, PAD, "برش محوری (Axial)", axial()) +
      panel(PAD + PANEL + GAP, PAD, "برش ساژیتال (Sagittal)", sagittal()) +
      panel(PAD + (PANEL + GAP) * 2, PAD, "برش کرونال (Coronal)", coronal()) +
      "</svg></div>";

    wire(container);
  }

  // ---------------------------------------------------------------- 3D volume
  //
  // A shaded jaw that turns. The rotation is a real projection: each point is turned
  // around the vertical axis and scaled by its depth, so the far side gets smaller and
  // darker. No WebGL, just arithmetic on the arch the other views already use.
  function renderVolume(container, chosen) {
    const lib = window.DentalRayToothArch, shapes = window.DentalRayToothShapes;
    if (!lib || !shapes) return;

    const CENTER = 500, VIEW = 1000, SPAN = 340, RISE = 210;

    container.innerHTML =
      '<div class="cbct-view cbct-volume">' +
      '<svg viewBox="0 0 ' + VIEW + " " + VIEW + '" role="group" aria-label="حجم سه‌بعدی فک">' +
      "<defs>" +
      '<linearGradient id="cbctVolBg" x1="0" y1="0" x2="0" y2="1">' +
      '<stop offset="0" stop-color="#11151b"/><stop offset="1" stop-color="#05070a"/>' +
      "</linearGradient>" +
      "</defs>" +
      '<rect x="0" y="0" width="' + VIEW + '" height="' + VIEW + '" fill="url(#cbctVolBg)"/>' +
      '<g class="cbct-vol-scene"></g>' +
      '<text class="cbct-label" x="' + (CENTER - 150) + '" y="966">CBCT · حجم سه‌بعدی</text>' +
      "</svg>" +
      '<div class="cbct-vol-controls">' +
      '<label class="natural-control"><span>چرخش</span><input type="range" min="-180" max="180" step="1" value="-32" data-ctrl="volSpin" /></label>' +
      '<button type="button" class="secondary-button cbct-vol-reset">بازگشت به حالت اول</button>' +
      '<span class="natural-hint">برای چرخاندن، فک را بکشید</span>' +
      "</div></div>";

    const scene = container.querySelector(".cbct-vol-scene");
    const stage = container.querySelector(".cbct-volume svg");
    const spinInput = container.querySelector('[data-ctrl="volSpin"]');
    const upper = lib.layout("upper", UPPER), lower = lib.layout("lower", LOWER);
    let yaw = -32;

    // Turn a local point around the vertical axis and project it. Depth (the z axis)
    // decides the shading, so the far teeth come out darker.
    function project(x, y, z) {
      const a = yaw * Math.PI / 180;
      const rx = x * Math.cos(a) - z * Math.sin(a);
      const rz = x * Math.sin(a) + z * Math.cos(a);
      const persp = 1 - rz / 2600;
      return { x: CENTER + rx * persp, y: 430 + y * persp, z: rz };
    }

    function draw() {
      const pieces = [];
      const tooth = (t, jaw) => {
        const shape = t.shape;
        const k = t.k * 1.5;
        const sel = chosen.has(t.n) ? " selected" : "";
        // The tooth sits on the arch; its depth is how far back along the arch it is.
        const z = jaw === "upper" ? -t.y * 0.55 : t.y * 0.55;
        const p = project(t.x, t.y, z);
        const shade = Math.max(-1, Math.min(1, p.z / SPAN));
        const light = 0.5 + 0.5 * (1 - shade) / 2;
        pieces.push({
          z: p.z, html: '<g class="cbct-vol-tooth' + sel + '" data-tooth="' + t.n + '" role="button" tabindex="0">' +
            '<g transform="translate(' + p.x.toFixed(1) + " " + p.y.toFixed(1) + ") rotate(" + t.rot.toFixed(1) + ") scale(" + k.toFixed(3) + ')" opacity="' + light.toFixed(2) + '">' +
            '<g transform="translate(-20 -21)">' +
            '<path class="cbct-vol-root" d="' + shape.root + '"/>' +
            '<path class="cbct-vol-crown" d="' + shape.crown + '"/></g></g></g>'
        });
      };

      // Two shells, one per jaw: the near half and the far half. The lower mandible is
      // drawn slightly stronger, as it is in a real volume.
      [
        { jaw: "upper", depth: -1, opacity: ".45" },
        { jaw: "lower", depth: 1, opacity: ".6" }
      ].forEach(shell => {
        const depth = shell.depth;
        const pts = [];
        for (let i = 0; i <= 48; i++) {
          const t = -1 + 2 * i / 48;
          const ap = lib.archPoint(t, shell.jaw);
          const z = depth < 0 ? -ap.y * 0.55 : ap.y * 0.55;
          const p = project(ap.x, ap.y + depth * 34, z);
          pts.push({ x: p.x, y: p.y, z: p.z });
        }
        const back = [];
        for (let i = 48; i >= 0; i--) {
          const t = -1 + 2 * i / 48;
          const ap = lib.archPoint(t, shell.jaw);
          const sign = depth < 0 ? -1 : 1;
          const z = depth < 0 ? -ap.y * 0.55 : ap.y * 0.55;
          const p = project(ap.x, ap.y + depth * 34 + sign * RISE, z);
          back.push({ x: p.x, y: p.y, z: p.z });
        }
        const outline = pts.concat(back);
        const zAvg = outline.reduce((a, q) => a + q.z, 0) / outline.length;
        pieces.push({
          z: zAvg, html: '<path class="cbct-vol-bone" opacity="' + shell.opacity + '" d="' +
            outline.map((q, i) => (i ? "L" : "M") + q.x.toFixed(1) + " " + q.y.toFixed(1)).join(" ") + ' Z"/>'
        });
      });

      upper.forEach(t => tooth(t, "upper"));
      lower.forEach(t => tooth(t, "lower"));

      // Painter's algorithm: whatever is nearer the viewer is drawn last.
      pieces.sort((a, b) => b.z - a.z);
      scene.innerHTML = pieces.map(p => p.html).join("");
      wire(container);
    }

    spinInput.addEventListener("input", () => { yaw = Number(spinInput.value); draw(); });

    let dragging = false, lastX = 0;
    stage.addEventListener("pointerdown", e => { dragging = true; lastX = e.clientX; try { stage.setPointerCapture(e.pointerId); } catch (_) { } });
    stage.addEventListener("pointermove", e => {
      if (!dragging) return;
      yaw = Math.max(-180, Math.min(180, yaw + (e.clientX - lastX) * 0.8));
      lastX = e.clientX; spinInput.value = String(Math.round(yaw)); draw();
    });
    const stop = () => { dragging = false; };
    stage.addEventListener("pointerup", stop);
    stage.addEventListener("pointercancel", stop);
    stage.addEventListener("lostpointercapture", stop);

    container.querySelector(".cbct-vol-reset").addEventListener("click", () => {
      yaw = -32; spinInput.value = "-32"; draw();
    });

    draw();
  }

  // Selection mirrors onto the hidden linear buttons, which stay the source of truth
  // for the saved tooth numbers.
  function wire(container) {
    container.querySelectorAll("[data-tooth]").forEach(g => {
      if (g.dataset.cbctWired) return;
      g.dataset.cbctWired = "1";
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

  const SCHEMES = { pano: renderPano, mpr: renderMpr, volume: renderVolume };
  const LABELS = { pano: "پانورامیک", mpr: "برش‌ها", volume: "حجم سه‌بعدی" };

  function render(container, selected, scheme) {
    if (!container) return;
    if (!window.DentalRayToothArch || !window.DentalRayToothShapes) return;
    const chosen = new Set((selected || []).map(Number));
    const current = SCHEMES[scheme] ? scheme : "pano";

    container.innerHTML =
      '<div class="cbct-odontogram">' +
      '<div class="cbct-toolbar">' +
      '<span class="cbct-toolbar-label">حالت نمایش:</span>' +
      Object.keys(SCHEMES).map(k =>
        '<button type="button" class="cbct-scheme-button' + (k === current ? " active" : "") + '" data-scheme="' + k + '">' + LABELS[k] + "</button>"
      ).join("") +
      "</div>" +
      '<div class="cbct-host"></div>' +
      "</div>";

    const host = container.querySelector(".cbct-host");
    SCHEMES[current](host, chosen);

    container.querySelectorAll(".cbct-scheme-button").forEach(btn => {
      btn.addEventListener("click", () => {
        const teeth = currentSelection(container);
        render(container, teeth, btn.dataset.scheme);
        if (container._cbctOnChange) container._cbctOnChange();
      });
    });
    container.dataset.scheme = current;
  }

  // The teeth currently chosen, read from the linear buttons so a scheme switch keeps
  // the selection.
  function currentSelection(container) {
    const root = container.closest(".dental-chart");
    if (!root) return [];
    return Array.from(root.querySelectorAll(".tooth-button.selected")).map(b => Number(b.dataset.tooth));
  }

  window.DentalRayCbctOdontogram = { render, SCHEMES };
})();
