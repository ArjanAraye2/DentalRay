// Dentix dental arch geometry, shared by the views that draw a real jaw.
//
// The arch is a horseshoe that is deeper than it is wide: the incisors sit at the
// front, the molars curve away at the sides. The curve is flattened at the front
// (FRONT_FLAT) so it reads as a dental arch rather than an ellipse.
//
// Everything is in a local space centred on the middle of the mouth, y growing
// downward, so a view can place it anywhere and rotate it without clipping.
(function () {
  "use strict";

  const FRONT = { upper: -44, lower: 46 };  // y of the incisors; they face each other
  const DEPTH = 320;                        // incisor to last molar, front to back
  const HALF_WIDTH = 286;                   // incisor to last molar, sideways
  const FRONT_FLAT = 1.3;
  const FILL = 0.88;                        // share of the arch the teeth take

  const FOCUS = {
    upper: { x: 0, y: FRONT.upper - DEPTH * 0.55 },
    lower: { x: 0, y: FRONT.lower + DEPTH * 0.55 }
  };

  // t: -1 at the left molar, 0 at the incisors, +1 at the right molar.
  function archPoint(t, jaw) {
    const th = t * Math.PI / 2;
    const u = Math.sin(th);
    const v = Math.pow(1 - Math.cos(th), FRONT_FLAT);
    const y = jaw === "upper" ? FRONT.upper - DEPTH * v : FRONT.lower + DEPTH * v;
    return { x: HALF_WIDTH * u, y: y };
  }

  // The arch sampled by arc length, so teeth can be placed by their real width.
  function buildPath(jaw) {
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

  const FALLBACK = { crownWidth: 20, crown: "M10 10 Q20 6 30 10 L29 25 Q20 29 11 25 Z", root: "M13 25 L16.5 38 Q20 40 23.5 38 L27 25 Z", detail: "M13 15 Q20 13 27 15" };

  /**
   * Place a jaw's teeth on the arch. One scale is used for the whole jaw so the
   * teeth keep their real width ratios: a molar is wide, an incisor is narrow.
   * Each tooth gets its position, the outward normal, its rotation and its scale.
   */
  function layout(jaw, teeth) {
    const shapes = window.DentalRayToothShapes;
    const pts = buildPath(jaw);
    const total = pts[pts.length - 1].s;
    const info = teeth.map(n => (shapes ? shapes.shapeOf(n) : FALLBACK));
    const sumW = info.reduce((a, s) => a + (s.crownWidth || 20), 0);
    const k = (total * FILL) / sumW;
    let acc = (total - sumW * k) / 2;
    const focus = FOCUS[jaw];

    return teeth.map((n, i) => {
      const shape = info[i];
      const w = (shape.crownWidth || 20) * k;
      const at = acc + w / 2;
      acc += w;
      const p = pointAt(pts, at);
      // Tangent by finite difference, then the normal, flipped to point away from the
      // inside of the mouth so the crown always faces the mouth opening.
      const a = pointAt(pts, at - 3), b = pointAt(pts, at + 3);
      let tx = b.x - a.x, ty = b.y - a.y;
      const tl = Math.hypot(tx, ty) || 1; tx /= tl; ty /= tl;
      let nx = -ty, ny = tx;
      if ((p.x - focus.x) * nx + (p.y - focus.y) * ny < 0) { nx = -nx; ny = -ny; }
      return {
        n: n, shape: shape, x: p.x, y: p.y, k: k, w: w,
        nx: nx, ny: ny,
        rot: Math.atan2(nx, -ny) * 180 / Math.PI
      };
    });
  }

  window.DentalRayToothArch = {
    FRONT, DEPTH, HALF_WIDTH, FRONT_FLAT, FILL, FOCUS,
    archPoint, buildPath, pointAt, layout
  };
})();
