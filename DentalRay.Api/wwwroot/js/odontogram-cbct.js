// Dentix CBCT view - the "CBCT" view.
//
// A CBCT study is read on a dark screen, so this view is deliberately dark like the
// imaging software a dentist already knows rather than the light clinical UI.
//
// Three schemes, chosen with a small toolbar:
//   pano     a full panoramic radiograph (OPG): the mandible as one silhouette with
//            condyle, sigmoid notch and coronoid process, the canal ending at the
//            mental foramen, both maxillary sinuses, the nasal cavity with its
//            septum, the hard palate crossing the upper roots, and the hyoid under
//            the mandible in front of the cervical spine ghost
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

  // Layout-only fallback: the fields a view needs to place a tooth, not its artwork.
  const FALLBACK = { crownWidth: 20, width: 20, height: 40, crownCenterY: 14 };

  const esc = v => String(v ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

  // ---------------------------------------------------------------- panoramic
  //
  // A full panoramic radiograph (OPG), drawn the way X-ray film reads: dense
  // tissue light, air dark. The mandible is one silhouette - body, angle, ramus,
  // coronoid process, sigmoid notch, condyle - with the mandibular canal running
  // forward to the mental foramen beside the premolar apices. Above it the
  // maxilla carries both sinuses over the molar roots, the hard palate crossing
  // the upper roots, and the nasal cavity with its septum. The hyoid floats
  // under the mandible in front of the cervical spine ghost. The teeth of both
  // jaws ride one occlusal curve, nearly in occlusion, their roots buried in
  // the brighter alveolar bone.
  function renderPano(container, chosen) {
    const lib = window.DentalRayToothArch;
    const shapes = window.DentalRayToothShapes;
    if (!lib || !shapes) return;

    const CENTER = 590, FILM_W = 1180, FILM_H = 800;

    // The occlusal plane: a wide arc that dips at the front, the way the bite
    // reads on a flattened panoramic projection.
    const CURVE_Y = t => 400 + 44 * Math.cos(t * Math.PI / 2);
    const CURVE_X = t => CENTER + 460 * t;
    const curveYAt = x => CURVE_Y((x - CENTER) / 460);

    // The two rows sit almost in occlusion around the plane, the way a patient
    // bites on the positioning rod while the machine sweeps around the head.
    const ROW_GAP = 32;
    // The teeth own about three quarters of the arch; behind them the retromolar
    // gap runs into the rising ramus.
    const TOOTH_SCALE = 1.9;

    function place(teeth, jaw) {
      const k = TOOTH_SCALE;
      const info = teeth.map(n => shapes.shapeOf(n) || FALLBACK);
      const upper = jaw === "upper";
      // Where the row sits relative to the curve, and which way its crowns point.
      const rowDir = upper ? -1 : 1;
      const crownTurn = upper ? 180 : 0;

      // Teeth are distributed by ARC LENGTH along the curve, not by a fraction of
      // the width. The panoramic curve flattens at its ends, so a width fraction
      // bunched the molars together and they overlapped. Walking the arc keeps the
      // spacing even, which is how a real panoramic film looks.
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
        // rather than leaning with it.
        return { n: n, shape: info[i], x: here.x, y: here.y + rowDir * ROW_GAP, rot: crownTurn, k: k, w: w };
      });
    }

    const upper = place(UPPER, "upper");
    const lower = place(LOWER, "lower");

    // Where bone sits relative to the curve. The alveolar crest starts at the
    // cervical line of the teeth, the roots are buried in the brighter alveolar
    // band, and the basal bone fills the body of the mandible below it.
    const CREST = 64, APEX = 124, UCREST = -64, UAPEX = -126;
    // How far the tooth-bearing part of the arch reaches, in curve parameter: the
    // crest ends where the retromolar gap begins, and the rami rise behind it.
    const T_END = 0.86;

    // A bone band following the occlusal curve between two offsets: the alveolar
    // process of either jaw.
    function band(yNear, yFar, tA, tB) {
      const N = 40, top = [], bot = [];
      for (let i = 0; i <= N; i++) {
        const t = tA + (tB - tA) * i / N;
        top.push((i ? "L" : "M") + CURVE_X(t).toFixed(1) + " " + (CURVE_Y(t) + yNear).toFixed(1));
      }
      for (let i = N; i >= 0; i--) {
        const t = tA + (tB - tA) * i / N;
        bot.push("L" + CURVE_X(t).toFixed(1) + " " + (CURVE_Y(t) + yFar).toFixed(1));
      }
      return top.join(" ") + " " + bot.join(" ") + " Z";
    }

    // A cortical line along the curve at one offset, for the alveolar crests.
    function crestLine(y, tA, tB) {
      const pts = [];
      for (let i = 0; i <= 40; i++) {
        const t = tA + (tB - tA) * i / 40;
        pts.push((i ? "L" : "M") + CURVE_X(t).toFixed(1) + " " + (CURVE_Y(t) + y).toFixed(1));
      }
      return pts.join(" ");
    }

    // The mandible as one closed silhouette: from the crest behind the last molar,
    // up the anterior ramus border to the pointed coronoid process, down through
    // the sigmoid notch to the rounded condyle, down the posterior border to the
    // angle, then along the lower border to the other side.
    function mandiblePath() {
      const N = 48, crest = [];
      for (let i = 0; i <= N; i++) {
        const t = -T_END + 2 * T_END * i / N;
        crest.push(CURVE_X(t).toFixed(1) + " " + (CURVE_Y(t) + CREST).toFixed(1));
      }
      return "M" + crest.join(" L") +
        // right anterior ramus border, up to the coronoid tip
        " C996 336 1006 196 1010 110" +
        // back of the coronoid, down into the sigmoid notch
        " C1028 128 1041 156 1044 178" +
        // out of the notch onto the neck of the condyle
        " Q1051 164 1055 150" +
        // the condyle, rounded, over the top
        " A26 21 -14 0 1 1097 141" +
        // posterior border of the ramus, down to the angle
        " C1101 262 1097 444 1076 562" +
        // the rounded angle
        " Q1069 586 1056 589" +
        // the lower border, dipping slightly at the symphysis
        " C902 603 762 622 590 626" +
        " C418 622 278 603 124 589" +
        " Q111 586 104 562" +
        // left posterior border, up to the left condyle
        " C83 444 79 262 83 141" +
        " A26 21 14 0 1 125 150" +
        " Q129 164 136 178" +
        // left sigmoid notch, up to the left coronoid tip
        " C139 156 152 128 170 110" +
        // left anterior ramus border, back down to the crest
        " C174 196 184 336 194.4 " + (CURVE_Y(-T_END) + CREST).toFixed(1) +
        " Z";
    }

    // The temporomandibular joint above each condyle: the roof of the fossa with
    // the dark joint space under it, and the articular eminence in front.
    function tmj(side) {
      return side < 0
        ? '<path class="cbct-tmj" d="M84 98 Q100 90 116 96"/>' +
            '<path class="cbct-tmj" d="M118 118 Q134 98 150 122"/>'
        : '<path class="cbct-tmj" d="M1064 96 Q1080 90 1096 98"/>' +
            '<path class="cbct-tmj" d="M1030 122 Q1046 98 1062 118"/>';
    }

    // The mental foramen beside the premolar apices, and the canal that reaches
    // it from under the sigmoid notch of the same side.
    const foramenAt = (ia, ib) => {
      const x = (lower[ia].x + lower[ib].x) / 2;
      return { x: x, y: curveYAt(x) + 100 };
    };
    const fL = foramenAt(3, 4), fR = foramenAt(12, 11);
    const canal = (x0, dir, f) =>
      "M" + x0 + " 184" +
      " C" + (x0 + dir * 15) + " 302 " + (x0 + dir * 34) + " 420 " + (x0 + dir * 72) + " 498" +
      " C" + (x0 + dir * 110) + " 546 " + (f.x - dir * 42) + " " + (f.y + 34) + " " + (f.x - dir * 4) + " " + f.y;

    // Each maxillary sinus: a dark chamber from the premolar to behind the third
    // molar, floored just under the root tips so the molar roots read through it,
    // walled with a thin dense line.
    function sinus(frontX, backX, tilt) {
      const cx = (frontX + backX) / 2, cy = curveYAt(cx) - 168;
      const rx = Math.max(72, Math.abs(frontX - backX) / 2);
      return '<ellipse class="cbct-sinus" cx="' + cx.toFixed(1) + '" cy="' + cy.toFixed(1) +
        '" rx="' + rx.toFixed(1) + '" ry="63" transform="rotate(' + tilt + " " + cx.toFixed(1) + " " + cy.toFixed(1) + ')"/>';
    }
    const sinusL = sinus(upper[4].x + 10, upper[0].x - 45, 7);
    const sinusR = sinus(upper[11].x - 10, upper[15].x + 45, -7);

    // The maxillary tuberosity, the rounded bump of bone behind the last molar.
    function tuber(x) {
      return '<ellipse class="cbct-tuber" cx="' + x.toFixed(1) + '" cy="' + (curveYAt(x) - 92).toFixed(1) + '" rx="30" ry="24"/>';
    }
    const tuberL = tuber(upper[0].x - 44), tuberR = tuber(upper[15].x + 44);

    function toothMarkup(t, chosen) {
      const sel = chosen.has(t.n) ? " selected" : "";
      return '<g class="cbct-tooth' + sel + '" data-tooth="' + t.n + '" role="button" tabindex="0"' +
        ' aria-label="دندان ' + t.n + '" aria-pressed="' + (chosen.has(t.n) ? "true" : "false") + '">' +
        '<g transform="translate(' + t.x.toFixed(1) + " " + t.y.toFixed(1) + ") rotate(" + t.rot.toFixed(1) + ") scale(" + t.k.toFixed(3) + ')">' +
        (window.DentalRayToothShapes ? window.DentalRayToothShapes.markup(t.n, "cbct") : "") +
        "</g></g>";
    }

    // The dense lower border of the mandible, the brightest cortical line on the
    // film, retraced as its own stroke.
    const lowerBorder = "M1056 589 C902 603 762 622 590 626 C418 622 278 603 124 589";

    container.innerHTML =
      '<div class="cbct-view cbct-pano">' +
      '<svg viewBox="0 0 ' + FILM_W + " " + FILM_H + '" role="group" aria-label="نمای پانورامیک سی‌بی‌سی‌تی">' +
      "<defs>" +
      '<linearGradient id="cbctFilm" x1="0" y1="0" x2="0" y2="1">' +
      '<stop offset="0" stop-color="#0b0e12"/><stop offset=".5" stop-color="#171c23"/><stop offset="1" stop-color="#0b0e12"/>' +
      "</linearGradient>" +
      '<radialGradient id="cbctBeam" cx="50%" cy="42%" r="62%">' +
      '<stop offset="0" stop-color="#3b4653" stop-opacity=".55"/><stop offset="1" stop-color="#000" stop-opacity="0"/>' +
      "</radialGradient>" +
      "</defs>" +
      '<rect class="cbct-film" x="0" y="0" width="' + FILM_W + '" height="' + FILM_H + '"/>' +
      '<rect class="cbct-beam" x="0" y="0" width="' + FILM_W + '" height="' + FILM_H + '"/>' +
      // Anatomy stays under the teeth and never takes the pointer, so only the
      // teeth are clickable.
      '<g class="cbct-anat">' +
      // cervical spine ghost, behind everything at the bottom of the film
      [648, 694, 740].map(y =>
        '<rect class="cbct-vert" x="505" y="' + y + '" width="170" height="48" rx="17"/>').join("") +
      // hyoid, floating under the mandible
      '<path class="cbct-hyoid" d="M398 668 Q590 704 782 668"/>' +
      tmj(-1) + tmj(1) +
      // the mandible, then its interior detail
      '<path class="cbct-mandible" d="' + mandiblePath() + '"/>' +
      '<path class="cbct-alv" d="' + band(CREST + 2, APEX, -T_END, T_END) + '"/>' +
      '<path class="cbct-crest" d="' + crestLine(CREST + 1, -T_END, T_END) + '"/>' +
      '<path class="cbct-cortical" d="' + lowerBorder + '"/>' +
      '<path class="cbct-canal" d="' + canal(136, 1, fL) + '"/>' +
      '<path class="cbct-canal" d="' + canal(1044, -1, fR) + '"/>' +
      '<ellipse class="cbct-foramen" cx="' + fL.x.toFixed(1) + '" cy="' + fL.y.toFixed(1) + '" rx="8" ry="6"/>' +
      '<ellipse class="cbct-foramen" cx="' + fR.x.toFixed(1) + '" cy="' + fR.y.toFixed(1) + '" rx="8" ry="6"/>' +
      // the maxilla with its tuberosities and sinuses
      '<path class="cbct-maxilla" d="' + band(UCREST, UAPEX, -T_END, T_END) + '"/>' +
      tuberL + tuberR +
      sinusL + sinusR +
      // nasal cavity, its floor above the incisor roots
      '<ellipse class="cbct-nose" cx="590" cy="232" rx="104" ry="56"/>' +
      // the tongue, a faint soft shadow under the occlusal plane
      '<ellipse class="cbct-tongue" cx="590" cy="468" rx="232" ry="48"/>' +
      "</g>" +
      lower.map(t => toothMarkup(t, chosen)).join("") +
      upper.map(t => toothMarkup(t, chosen)).join("") +
      // Layers that read OVER the teeth: the hard palate is superimposed on the
      // upper roots exactly as it is on a real film.
      '<g class="cbct-anat-over">' +
      '<path class="cbct-palate" d="' + band(-93, -107, -0.8, 0.8) + '"/>' +
      '<path class="cbct-septum" d="M588 180 L588 284"/>' +
      '<path class="cbct-nose-floor" d="M486 287 Q590 281 694 287"/>' +
      '<path class="cbct-nose-tip" d="M534 98 Q590 60 646 98"/>' +
      "</g>" +
      '<text class="cbct-label" x="' + CENTER + '" y="793">CBCT · نمای پانورامیک</text>' +
      "</svg></div>";

    wire(container);
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
      const pitch = (PANEL - 120) / side.length;
      const k = Math.min(1.15, pitch / 34);
      const total = pitch * side.length;
      const startX = (PANEL - total) / 2 + pitch / 2;
      let row = "";
      side.forEach((n, i) => {
        const x = startX + i * pitch;
        const sel = chosen.has(n) ? " selected" : "";
        row += '<g class="cbct-side' + sel + '" data-tooth="' + n + '" role="button" tabindex="0">' +
          '<g transform="translate(' + x.toFixed(1) + " " + oy + ") scale(" + k.toFixed(3) + ')">' +
          (window.DentalRayToothShapes ? window.DentalRayToothShapes.markup(n, "cbct") : "") +
          "</g></g>";
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
          (window.DentalRayToothShapes ? window.DentalRayToothShapes.markup(t.n, "cbct") : "") +
          "</g></g>";
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
            (window.DentalRayToothShapes ? window.DentalRayToothShapes.markup(t.n, "cbct-vol") : "") +
            "</g></g>"
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
