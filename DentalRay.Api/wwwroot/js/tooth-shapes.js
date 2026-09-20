// Dentix tooth shapes.
//
// A dental chart needs teeth that look like teeth. The previous version used three
// shapes for eight tooth types, so a wisdom tooth was drawn the same as a premolar,
// every tooth had the same width and every tooth had two roots.
//
// This module defines a real shape per FDI type - crown, roots and occlusal detail.
// Both drawn views (the anatomical rows and the arch) read from here, so they can
// never drift apart.
//
// Proportions follow real teeth rather than exaggerating them: a first molar crown is
// 28 units wide against 20 for a central incisor, which is about the real 10.5mm and
// 8.5mm. The difference is visible without a molar looking huge.
//
// FDI second digit is the type:
//   1,2 incisors   3 canine   4,5 premolars   6,7,8 molars
// FDI first digit is the quadrant:
//   1 upper right  2 upper left  3 lower left  4 lower right
(function () {
  "use strict";

  // Local space: the crown at the top, the root below, which is how the chart has
  // always drawn them. crownWidth is the real width of the crown art, so the rows can
  // size a tooth without guessing.
  const TYPES = {
    1: { // central incisor: the widest of the front teeth, straight incisal edge
      crown: "M10 10 Q20 6 30 10 L29 25 Q20 29 11 25 Z",
      root: "M13 25 L16.5 38 Q20 40 23.5 38 L27 25 Z",
      detail: "M13 15 Q20 13 27 15",
      crownWidth: 20
    },
    2: { // lateral incisor: narrower and a little shorter than the central
      crown: "M11 11 Q20 7 29 11 L28 25 Q20 28 12 25 Z",
      root: "M14 25 L17 37 Q20 39 23 37 L26 25 Z",
      detail: "M14 16 Q20 14 26 16",
      crownWidth: 18
    },
    3: { // canine: one pointed cusp, the longest single root
      crown: "M10 12 L20 6 L30 12 L28 25 Q20 30 12 25 Z",
      root: "M13 25 L16.5 39 Q20 41 23.5 39 L27 25 Z",
      detail: "M13 17 L20 10 L27 17",
      crownWidth: 20
    },
    4: { // first premolar: two cusps, two roots
      crown: "M8 12 Q14 7 20 12 Q26 7 32 12 L29 25 Q20 30 11 25 Z",
      root: "M9 25 L12 37 L15 25 Z M25 25 L28 37 L31 25 Z",
      detail: "M11 17 Q20 12 29 17 M20 15 L20 23",
      crownWidth: 24
    },
    5: { // second premolar: similar width, cusps a little lower
      crown: "M8 13 Q14 8 20 13 Q26 8 32 13 L29 26 Q20 31 11 26 Z",
      root: "M9 26 L12 38 L15 26 Z M25 26 L28 38 L31 26 Z",
      detail: "M11 18 Q20 13 29 18 M20 16 L20 24",
      crownWidth: 24
    },
    6: { // first molar: the widest crown, the most occlusal detail
      crown: "M6 13 Q10 7 15 12 Q20 7 25 12 Q30 7 34 13 L30 26 Q20 32 10 26 Z",
      root: "M8 26 L12 39 L16 26 Z M24 26 L28 39 L32 26 Z",
      detail: "M8 18 Q20 12 32 18 M20 15 L20 25 M12 22 L28 22",
      crownWidth: 28
    },
    7: { // second molar: a little smaller than the first
      crown: "M7 13 Q11 7 15.5 12 Q20 7.5 24.5 12 Q29 7 33 13 L29 26 Q20 31.5 11 26 Z",
      root: "M9 26 L12.5 39 L16 26 Z M24 26 L27.5 39 L31 26 Z",
      detail: "M9 18 Q20 12.5 31 18 M20 15 L20 25 M13 22 L27 22",
      crownWidth: 26
    },
    8: { // third molar: the smallest crown of the molars
      crown: "M9 14 Q13 9 16.5 13 Q20 9 23.5 13 Q27 9 31 14 L28 26 Q20 31 12 26 Z",
      root: "M11 26 L14 38 L17 26 Z M23 26 L26 38 L29 26 Z",
      detail: "M12 19 Q20 14 28 19 M20 16 L20 25",
      crownWidth: 22
    }
  };

  // The root layout is what the old chart got wrong for every tooth: an upper molar
  // has three roots, a lower molar two, the front teeth a single one in both jaws.
  const MOLAR_ROOTS_UPPER = "M7 26 L9 37 L13 26 Z M17 26 L19.5 39 L23 26 Z M27 26 L31 37 L33 26 Z";
  const MOLAR_ROOTS_LOWER = "M8 26 L12 39 L16 26 Z M24 26 L28 39 L32 26 Z";

  const quadrantOf = n => Math.floor(Number(n) / 10);
  const typeOf = n => Number(n) % 10;
  const isUpper = n => quadrantOf(n) === 1 || quadrantOf(n) === 2;

  // The tight bounds of a set of paths, so the standalone SVG can be cropped to the
  // tooth instead of leaving a 40-unit box around a 20-unit tooth. Without this a
  // narrow incisor and a wide molar would render at the same visual width.
  function boundsOf(paths) {
    let minX = 1e9, minY = 1e9, maxX = -1e9, maxY = -1e9;
    const re = /(-?\d+(?:\.\d+)?)\s+(-?\d+(?:\.\d+)?)/g;
    paths.forEach(d => {
      let m;
      while ((m = re.exec(d))) {
        const x = parseFloat(m[1]), y = parseFloat(m[2]);
        if (x < minX) minX = x;
        if (x > maxX) maxX = x;
        if (y < minY) minY = y;
        if (y > maxY) maxY = y;
      }
    });
    return { x: minX, y: minY, w: maxX - minX, h: maxY - minY };
  }

  /** Crown, root and detail for one tooth, in the 40x42 local space. */
  function shapeOf(toothNumber) {
    const type = typeOf(toothNumber);
    const base = TYPES[type] || TYPES[6];
    let root = base.root;
    // Upper molars carry a third root; lower molars keep two.
    if (type >= 6) root = isUpper(toothNumber) ? MOLAR_ROOTS_UPPER : MOLAR_ROOTS_LOWER;
    return {
      crown: base.crown,
      root: root,
      detail: base.detail,
      crownWidth: base.crownWidth,
      type: type,
      upper: isUpper(toothNumber),
      bounds: boundsOf([base.crown, root, base.detail])
    };
  }

  /** The quadrant class used for the subtle colour grouping. */
  function quadrantClass(toothNumber) {
    return "quadrant-" + quadrantOf(toothNumber);
  }

  /** Standalone SVG for one tooth, used by the anatomical and linear views. */
  function svg(toothNumber) {
    const s = shapeOf(toothNumber);
    const b = s.bounds;
    // preserveAspectRatio="none" plus a cropped viewBox means the caller controls the
    // width and height independently, so a molar really is wider than an incisor.
    return '<svg viewBox="' + b.x.toFixed(1) + " " + b.y.toFixed(1) + " " + b.w.toFixed(1) + " " + b.h.toFixed(1) +
      '" preserveAspectRatio="none" aria-hidden="true">' +
      '<path class="tooth-root" d="' + s.root + '"/>' +
      '<path class="tooth-crown" d="' + s.crown + '"/>' +
      '<path class="tooth-detail" d="' + s.detail + '"/>' +
      "</svg>";
  }

  window.DentalRayToothShapes = {
    shapeOf, svg, quadrantClass, typeOf, quadrantOf, isUpper, TYPES
  };
})();
