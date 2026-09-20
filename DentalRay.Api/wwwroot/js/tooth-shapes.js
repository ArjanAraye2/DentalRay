// Dentix tooth shapes.
//
// The tooth outline, the crown shading and the ridge lines come from the open source
// react-odontogram project (MIT licence, github.com/biomathcode/react-odontogram).
// Those paths were drawn by an illustrator to look like real teeth, which is the part
// hand-written geometry cannot fake: a molar needs its cusps, a premolar its two
// ridges, and an incisor its chisel edge.
//
// Only the artwork is borrowed. The positioning, scaling and selection stay in this
// project, and the paths are normalised here into one coordinate system so every view
// can place a tooth without knowing where the original artwork happened to sit.
//
// FDI second digit is the type:
//   1,2 incisors   3 canine   4,5 premolars   6,7,8 molars
// FDI first digit is the quadrant:
//   1 upper right  2 upper left  3 lower left  4 lower right
(function () {
  "use strict";

  // --- the borrowed artwork -------------------------------------------------
  // Each entry is { outline, shadow, highlight } in the source's own coordinates.
  // "shadow" is the inner face that gets filled; "highlight" is the enamel ridge
  // lines drawn on top.
  const ART = [
    {
      type: "Central Incisor",
      outline: "M397.992 51.339C396.126 51.1841 393.495 50.5617 393.495 50.5617C393.495 50.5617 388.804 49.6453 386.903 48.5677C384.873 47.4162 382.213 45.7195 381.838 44.0514C381.487 42.4836 381.216 40.0226 381.478 38.243L381.503 38.0727C381.82 35.9287 382.29 32.7452 382.642 29.3191C382.817 27.6207 382.583 26.5905 383.332 24.0911C384.118 21.4652 384.555 20.2179 385.09 19.2766C385.648 18.2937 386.375 17.5159 386.841 16.5072C387.593 14.8802 388.91 12.8822 390.18 11.3573C390.902 10.4905 391.602 9.74461 392.207 9.11454C393.08 8.20669 394.629 6.4959 395.556 5.46719C396.866 4.01329 398.196 2.73364 399.344 2.32547C400.382 1.95648 401.652 1.80289 402.575 1.56192C403.741 1.25751 404.7 1.04041 406.481 1.59988C407.789 2.01078 408.79 2.15093 410.175 3.6958C411.909 5.63147 412.563 6.70795 413.119 7.75747C414.007 9.43534 415.224 11.7273 416.073 13.2435C416.584 14.1554 417.935 15.8859 419.471 20.7919C420.739 24.8422 421.417 27.4048 421.716 28.4564C422.057 29.6562 422.602 31.5093 422.815 33.7313C423.154 37.2832 423.699 40.0978 423.625 43.1077C423.562 45.6958 423.671 47.3905 422.667 48.4567C421.852 49.3227 420.962 50.1403 419.837 50.5815C418.166 51.2367 416.619 51.7043 415.323 51.8901C414.174 52.0548 409.33 52.1292 405.791 51.8445C404.461 51.7375 402.725 51.7319 397.992 51.339Z",
      shadow: "M410.562 10.2393C426.588 45.9698 419.344 47.3813 414.383 48.2436C409.421 49.1058 390.229 45.8566 389.156 45.6013C388.783 45.4122 381.586 45.5268 385.27 28.9613C388.307 15.307 399.25 6.5008 400.149 5.94125C402.84 4.2654 407.678 3.81025 410.562 10.2393Z",
      highlight: ["M386.65 40.1568C386.979 40.5013 390.29 43.0038 394.2 44.879C396.819 46.1347 400.79 46.3275 403.707 46.4582C411.145 46.8422 412.214 45.7688 414.456 45.2944C414.934 45.1809 415.23 45.0675 415.965 44.8037"]
    },
    {
      type: "Lateral Incisor",
      outline: "M339.362 5.90227C342.255 3.71416 346.038 1.25774 347.807 1.15688C351.997 0.918049 355.515 3.10622 357.625 6.73623C361.668 13.6941 364.043 21.5609 365.951 29.3396C366.793 32.7727 371.064 45.061 366.866 47.897C360.805 55.8472 336.444 55.8684 329.833 49.9979C323.057 43.9805 325.09 29.7675 327.77 22.2921C329.981 16.1281 334.178 9.82403 339.362 5.90227Z",
      shadow: "M340.341 10.7533C342.637 9.01674 345.639 7.06721 347.044 6.98717C350.369 6.79762 353.161 8.53425 354.835 11.4152C358.044 16.9373 359.929 23.1807 361.443 29.3542C362.112 32.0788 365.502 41.8314 362.169 44.0822C357.359 50.3918 338.025 50.4086 332.779 45.7496C327.401 40.9738 329.014 29.6938 331.142 23.761C332.896 18.869 336.227 13.8658 340.341 10.7533Z",
      highlight: ["M334.964 39.2131C335.743 39.9998 343.128 45.3167 349.494 44.3903C352.66 43.9297 359.564 41.7577 360.67 41.2767"]
    },
    {
      type: "Canine",
      outline: "M275.285 36.7016C275.461 34.8357 275.849 18.5473 286.018 5.57867C288.971 1.81325 299.434 0.829071 303.316 3.82989C310.408 9.31139 313.426 28.9506 310.396 37.4059C308.951 41.4393 297.215 55.4587 291.42 52.8366C285.975 50.3727 274.6 43.9742 275.285 36.7016Z",
      shadow: "M301.598 7.93945C307.291 12.3406 309.864 28.5513 307.396 35.4392C306.212 38.7427 296.601 50.225 291.854 48.0774C287.984 46.3265 281.376 42.5321 279.505 38.5086C275.156 29.1535 286.937 -3.39247 301.598 7.93945Z",
      highlight: ["M281.255 35.7132C281.766 36.2349 283.933 38.6225 286.307 40.4563C289.837 43.1816 290.269 43.5706 291.974 44.5673C293.243 45.3091 296.192 46.2724 304.782 36.2803", "M287.379 19.4012C287.632 19.2016 291.839 21.1066 296.933 17.499"]
    },
    {
      type: "First Premolar",
      outline: "M236.98 4.87854C231.34 8.91989 227.474 33.8121 227.41 33.9971C227.423 35.3948 224.346 44.1157 245.059 53.0117C246.025 53.4266 247.125 52.888 247.86 52.4695C248.67 52.0073 250.354 50.8376 253.096 48.11C254.772 46.4429 261.519 39.4872 261.637 34.8665C261.691 32.7406 261.314 28.4868 260.916 24.2565C260.517 20.0262 259.172 12.5851 258.255 9.7502C257.37 7.00905 252.569 -3.57975 236.98 4.87854Z",
      shadow: "M238.114 7.66346C233.204 11.182 229.838 32.8544 229.782 33.0155C229.793 34.2324 227.114 41.8252 245.148 49.5706C245.989 49.9317 246.947 49.4629 247.586 49.0985C248.293 48.6961 249.758 47.6777 252.146 45.3029C253.605 43.8514 259.479 37.7955 259.582 33.7724C259.629 31.9215 259.301 28.218 258.954 24.5348C258.607 20.8517 257.435 14.3731 256.638 11.905C255.866 9.51838 251.687 0.299255 238.114 7.66346Z",
      highlight: ["M232.699 39.8277C232.885 39.9138 233.796 40.4028 238.753 42.4719C241.555 43.6416 243.526 43.4623 245.192 43.1391C246.68 42.8502 249.007 41.9121 250.856 40.686C251.932 39.8864 252.954 38.9499 254.698 37.0231C255.147 36.4782 255.449 36.0189 256.052 35.3458", "M239.545 12.8442C241.885 10.1252 250.366 10.5793 250.366 10.5793"]
    },
    {
      type: "Second Premolar",
      outline: "M186.485 45.8905C188.39 47.4915 194.878 52.668 197.762 53.1457C200.384 53.5801 211.318 44.6344 213.743 37.254C214.322 35.4928 215.038 15.1384 208.58 4.13152C202.248 -2.02701 189.273 -0.756658 185.395 11.0477C183.218 17.6734 180.668 30.608 180.423 32.2205C180.177 33.8383 180.188 36.0117 180.547 38.1716C180.751 39.3933 180.853 40.0649 181.221 40.6135C181.726 41.3662 183.233 43.1574 186.485 45.8905Z",
      shadow: "M188.107 43.3403C189.761 44.7301 195.393 49.2236 197.896 49.6383C200.172 50.0154 209.664 42.2499 211.769 35.8432C212.271 34.3143 212.893 16.6453 207.287 7.09053C201.791 1.74449 190.527 2.84724 187.16 13.0943C185.271 18.8458 183.057 30.074 182.845 31.4738C182.631 32.8782 182.641 34.7648 182.953 36.6398C183.129 37.7003 183.218 38.2832 183.537 38.7594C183.976 39.4129 185.284 40.9678 188.107 43.3403Z",
      highlight: ["M185.566 38.954C200.902 47.5796 204.772 37.6999 208.388 34.1061", "M191.566 11.1268C196.702 7.12464 201.745 8.78762 202.549 8.92105"]
    },
    {
      type: "First Molar",
      outline: "M128.904 45.8941C130.676 47.3001 145.884 61.9755 162.145 45.1423C163.249 43.9986 172.589 28.7255 163.065 9.88792C162.022 7.82514 161.32 5.92758 160.392 4.9355C159.008 3.45593 158.296 2.7052 156.622 2.60279C155.155 2.51311 153.656 3.01459 150.242 3.84879C148.517 4.2702 147.372 4.53309 144.251 3.3168C143.121 2.87675 141.495 2.08665 139.43 1.98738C137.768 1.90751 136.636 1.9765 135.262 2.89208C133.14 4.30717 131.556 6.06007 130.301 8.67838C129.938 9.43672 129.094 10.8117 127.581 13.8497C126.625 15.77 125.381 18.4495 124.394 20.9063C123.406 23.3631 122.657 25.5204 122.24 27.0814C121.585 29.5351 120.898 32.0017 121.468 34.2957L121.471 34.3102C121.817 35.7 122.322 37.7345 123.709 39.8068C125.153 41.9637 126.819 44.2397 128.904 45.8941Z",
      shadow: "M131.01 43.4559C132.509 44.7414 145.836 57.4979 160.034 42.7994C160.999 41.8008 169.154 28.4646 160.838 12.016C159.927 10.2149 159.315 8.55796 158.504 7.6917C157.296 6.39976 156.674 5.74424 155.212 5.65482C153.932 5.57651 152.623 6.01439 149.641 6.7428C148.135 7.11076 147.135 7.34032 144.41 6.27827C143.424 5.89404 142.004 5.20414 140.2 5.11746C129.959 4.62519 125.555 25.666 125.191 27.029L125.189 27.0338C124.616 29.1822 122.705 36.3359 131.01 43.4559Z",
      highlight: ["M153.854 27.4742C149.502 26.6409 144.049 27.8519 142.802 30.5372C143.302 25.2189 138.067 23.5889 133.932 23.5051", "M153.854 27.4742C156.709 28.0209 159.09 29.4476 159.498 31.9153", "M153.854 27.4742C155.556 27.6746 159.3 27.5062 160.657 25.2286", "M129.139 24.6538C129.507 24.1665 131.326 23.4523 133.932 23.5051", "M133.932 23.5051C132.471 22.6048 129.672 20.364 130.173 18.6034"]
    },
    {
      type: "Second Molar",
      outline: "M60.6793 25.3418C59.9161 27.6449 57.3636 48.9037 84.9129 53.0846C99.7889 54.3626 106.055 39.2037 106.365 36.7909C108.564 30.6549 106.469 17.6337 105.276 14.6833C103.739 10.8816 102.7 8.48706 101.946 7.37378C100.442 5.15401 99.23 3.29064 98.43 2.92655C97.2983 2.41145 95.3919 1.13034 91.2714 2.68435C88.4359 3.75373 86.0838 3.4998 83.6256 2.76604C80.7363 1.9036 76.9082 0.690209 75.4367 0.962055C74.4606 1.14236 73.4249 1.15689 72.6333 1.98215C71.4867 3.17764 70.1618 4.75807 67.8021 9.5839C66.634 11.9729 64.7932 15.7196 63.8949 17.7849C62.387 21.2519 61.3298 23.3785 60.6793 25.3418Z",
      shadow: "M63.8099 25.4532C63.1456 27.4579 60.9238 45.9624 84.9037 49.6015C97.8523 50.7139 103.307 37.5191 103.576 35.419C106.215 28.0569 104.202 16.4153 99.73 9.81324C98.4211 7.88108 97.3658 6.25914 96.6695 5.94222C95.6844 5.49387 93.796 4.7905 90.2093 6.14316C87.7412 7.07399 85.9229 6.4412 83.7832 5.80251C81.4234 5.09812 76.4488 2.79134 74.2151 5.12018C73.2171 6.16077 72.0638 7.53645 70.0098 11.737C67.83 16.195 65.3744 20.7318 63.8099 25.4532Z",
      highlight: ["M92.3375 21.9418C89.3377 22.2849 84.8906 23.6339 83.7253 27.9829C84.7838 23.138 77.3962 22.8185 73.5022 23.7247", "M92.3375 21.9418C96.1666 21.5038 99.585 23.1101 100.215 24.165", "M92.3375 21.9418C94.4127 21.402 98.5317 19.898 99.5584 17.8597", "M68.7524 26.3173C69.2513 25.49 70.8242 24.3479 73.5022 23.7247", "M73.5022 23.7247C71.8339 23.4169 68.3955 22.3238 67.9878 20.414"]
    },
    {
      type: "Third Molar",
      outline: "M22.8836 52.8066C23.9272 53.0915 25.7986 52.8756 26.2096 52.9116C26.501 52.9372 29.1459 52.7852 30.9534 52.2805C33.3552 51.4149 34.9901 50.7777 35.8495 50.1498C37.4391 48.9886 39.8965 46.9387 41.9506 44.3052C43.4092 42.4352 44.6381 40.2207 45.6008 37.6721C46.2927 35.8405 46.8536 34.1203 46.9928 31.3837C47.0827 29.6169 47.0888 27.1207 46.8396 24.256C46.5904 21.3913 46.1008 18.2531 45.5737 15.8629C45.0466 13.4727 44.4986 11.8788 43.9103 10.4861C43.3219 9.09334 42.7016 7.94145 42.3718 7.27535C41.7672 6.05446 41.3188 4.92976 40.5172 4.18705C38.9052 2.69353 37.6504 2.08716 36.39 1.85884C35.3031 1.66195 33.6407 1.67752 32.5207 2.07341C30.9374 2.63304 30.0912 3.2864 27.6613 3.30729C24.3667 3.3356 22.5667 1.96049 20.5098 1.81402C17.0827 1.56998 15.2376 1.60216 14.1182 2.20976C13.2193 2.69768 12.0927 3.47083 9.80979 6.74515C9.14188 7.70312 8.43252 8.93509 6.93122 12.162C5.88407 14.4128 4.32452 17.8482 3.48639 19.6641C2.55627 21.6793 1.61739 23.4684 0.935372 26.3698C0.449847 28.4352 0.305589 30.9466 0.839682 33.6862C1.20408 35.5554 1.8879 38.1004 4.17249 41.0505C6.62542 44.218 8.40151 46.3149 9.38601 47.091C10.2686 47.7868 11.1762 48.4668 12.4397 49.0231C14.0734 49.7423 15.5681 50.8399 17.5147 51.4152C19.7705 52.0818 21.1395 52.3305 22.8836 52.8066Z",
      shadow: "M22.9904 49.6908C23.9066 49.9409 25.5494 49.7513 25.9103 49.783C26.166 49.8054 28.488 49.6719 30.0748 49.2289C32.1834 48.469 33.6186 47.9096 34.3731 47.3584C35.7686 46.3389 37.9259 44.5394 39.7292 42.2274C41.0097 40.5858 42.0885 38.6416 42.9337 36.4042C43.5411 34.7963 44.0335 33.2862 44.1557 30.8837C44.2346 29.3326 44.24 27.1413 44.0212 24.6263C43.8025 22.1114 43.3726 19.3564 42.9099 17.2581C42.4472 15.1598 41.9661 13.7604 41.4496 12.5378C40.9331 11.3151 40.3885 10.3039 40.099 9.71911C39.5682 8.64729 39.1745 7.65993 38.4708 7.00789C37.0556 5.69674 35.9541 5.16442 34.8476 4.96398C33.8934 4.79113 32.434 4.8048 31.4507 5.15234C30.0608 5.64364 29.3179 6.21722 27.1847 6.23556C24.2924 6.26042 22.7122 5.05321 20.9064 4.92463C17.8978 4.71038 16.2779 4.73864 15.2953 5.27204C14.5061 5.70039 13.5171 6.37914 11.5129 9.25365C10.9266 10.0946 10.3038 11.1762 8.98585 14.0091C8.06656 15.9851 6.69744 19.001 5.96165 20.5951C5.1451 22.3642 4.32086 23.9349 3.72212 26.482C3.29588 28.2953 3.16924 30.4999 3.63812 32.9051C3.95802 34.546 4.55834 36.7803 6.56397 39.3702C8.71739 42.1509 10.2766 43.9917 11.1409 44.6731C11.9158 45.2839 12.7125 45.8809 13.8217 46.3692C15.256 47.0006 16.5682 47.9643 18.2771 48.4693C20.2574 49.0544 21.4592 49.2728 22.9904 49.6908Z",
      highlight: ["M7.13742 27.9101C7.89327 26.717 10.2562 25.7846 12.8627 25.483", "M38.0458 27.6444C37.0983 25.9368 34.2284 24.8447 31.1328 24.7739", "M37.58 21.3626C36.685 22.4687 34.1426 24.6994 31.1328 24.7739", "M31.1328 24.7739C27.1226 24.6823 22.7336 26.3048 21.6544 30.5239C21.9221 26.1555 17.0744 24.9957 12.8627 25.483", "M6.646 21.8371C7.23084 23.1942 9.29296 25.8232 12.8627 25.483"]
    }
  ];

  // FDI type digit -> index into ART.
  const TYPE_TO_ART = { 1: 0, 2: 1, 3: 2, 4: 3, 5: 4, 6: 5, 7: 6, 8: 7 };

  // --- normalisation ---------------------------------------------------------
  // The artwork is spread across the source document, so each tooth is measured and
  // mapped into its own box. After that every tooth is the same height, which is what
  // a view needs to place it on an arch.


  const NUM = /-?\d+(?:\.\d+)?/g;
  function numbersOf(paths) {
    const out = [];
    paths.forEach(d => {
      const m = String(d).match(NUM);
      if (m) m.forEach(x => out.push(parseFloat(x)));
    });
    return out;
  }

  // Tight box of a path, in the same coordinate space the path is written in.
  function bounds(paths) {
    const nums = numbersOf(paths);
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (let i = 0; i + 1 < nums.length; i += 2) {
      if (nums[i] < minX) minX = nums[i];
      if (nums[i] > maxX) maxX = nums[i];
      if (nums[i + 1] < minY) minY = nums[i + 1];
      if (nums[i + 1] > maxY) maxY = nums[i + 1];
    }
    return { x: minX, y: minY, w: maxX - minX, h: maxY - minY };
  }

  // A single scale for every type. Normalising each tooth to the same height was wrong:
  // the illustrations include the root, and root length varies far more than crown size,
  // so equalising the height flattened a molar down to an incisor's width. One shared
  // scale keeps the artist's own proportions, and a first molar does come out wider than
  // a central incisor, which is what the mouth looks like.
  const TARGET_WIDEST = 34;    // the widest tooth, in local units

  // Map the source coordinates into a local box: the top of the crown at y 0, the box
  // horizontally centred on 0. A view then only has to place the tooth at translate(x y).
  //
  // The transform is applied to the PATH STRING. A path's `d` is text, so wrapping the
  // element in a <g transform> would write markup into the attribute and corrupt it.
  function normalise(art, index, scale) {
    const paths = [art.outline, art.shadow].concat(art.highlight);
    const b = bounds(paths);
    const k = scale;
    const w = b.w * k, h = b.h * k;
    const mapX = x => (x - b.x) * k - w / 2;
    const mapY = y => (y - b.y) * k;
    const conv = d => transformPath(d, k, mapX, mapY);
    return {
      outline: conv(art.outline),
      shadow: conv(art.shadow),
      highlight: art.highlight.map(conv),
      width: w,
      crownWidth: w,
      height: h,
      // Local box: x from -w/2 to +w/2, y from 0 to h.
      box: { x: -w / 2, y: 0, w: w, h: h },
      crownCenterY: h * 0.34
    };
  }

  // Rewrite every coordinate in a path. Commands keep their kind, so a relative command
  // stays relative and only its offsets are scaled.
  function transformPath(d, k, mapX, mapY) {
    const tokens = String(d).match(/[MmLlHhVvCcSsQqTtAaZz]|-?\d*\.?\d+(?:e[-+]?\d+)?/gi) || [];
    const out = [];
    const arity = { M: 2, L: 2, H: 1, V: 1, C: 6, S: 4, Q: 4, T: 2, A: 7, Z: 0 };
    let cmd = "", i = 0;
    while (i < tokens.length) {
      if (/[A-Za-z]/.test(tokens[i])) { cmd = tokens[i]; out.push(cmd); i++; continue; }
      const upper = cmd.toUpperCase();
      const n = arity[upper] || 0;
      if (!n) { i++; continue; }
      const group = [];
      for (let j = 0; j < n && i < tokens.length && !/[A-Za-z]/.test(tokens[i]); j++) group.push(parseFloat(tokens[i++]));
      if (group.length < n) break;
      const rel = cmd !== upper;
      if (upper === "A") {
        // rx ry rotation large-arc sweep x y - the radii scale, the flags do not.
        out.push((group[0] * k).toFixed(3), (group[1] * k).toFixed(3), group[2].toFixed(3), group[3], group[4]);
        out.push((rel ? group[5] * k : mapX(group[5])).toFixed(3));
        out.push((rel ? group[6] * k : mapY(group[6])).toFixed(3));
        continue;
      }
      if (upper === "H") {
        out.push((rel ? group[0] * k : mapX(group[0])).toFixed(3));
        continue;
      }
      if (upper === "V") {
        out.push((rel ? group[0] * k : mapY(group[0])).toFixed(3));
        continue;
      }
      for (let j = 0; j + 1 < n; j += 2) {
        const x = group[j], y = group[j + 1];
        out.push((rel ? x * k : mapX(x)).toFixed(3), (rel ? y * k : mapY(y)).toFixed(3));
      }
    }
    return out.join(" ");
  }

  // Measure first, then pick the one scale that makes the widest tooth TARGET_WIDEST.
  const WIDEST = ART.reduce((max, art) => {
    const b = bounds([art.outline, art.shadow].concat(art.highlight));
    return Math.max(max, b.w);
  }, 0);
  const GLOBAL_SCALE = TARGET_WIDEST / WIDEST;
  const SHAPES = ART.map((art, i) => normalise(art, i, GLOBAL_SCALE));

  const quadrantOf = n => Math.floor(Number(n) / 10);
  const typeOf = n => Number(n) % 10;
  const isUpper = n => quadrantOf(n) === 1 || quadrantOf(n) === 2;

  const fallback = SHAPES[5];

  /** The artwork and box for one tooth, in a centred local space. */
  function shapeOf(toothNumber) {
    const type = typeOf(toothNumber);
    const idx = TYPE_TO_ART[type];
    const s = (idx === undefined ? fallback : SHAPES[idx]);
    const upper = isUpper(toothNumber);
    return {
      type: type,
      upper: upper,
      // Local space: crown at y 0, root tip at y height, horizontally centred on 0.
      width: s.width,
      crownWidth: s.crownWidth,
      height: s.height,
      crownCenterY: s.crownCenterY,
      box: s.box,
      outline: s.outline,
      shadow: s.shadow,
      highlight: s.highlight,
      // A short description, useful for a title.
      name: ART[idx === undefined ? 5 : idx].type
    };
  }

  /** The quadrant class used for the subtle colour grouping. */
  function quadrantClass(toothNumber) {
    return "quadrant-" + quadrantOf(toothNumber);
  }

  /**
   * Standalone SVG for one tooth, for the views that place teeth in a row. The paths
   * are already in the local space, so the viewBox is just the tooth's box.
   */
  function svg(toothNumber) {
    const s = shapeOf(toothNumber);
    const vb = s.box.x.toFixed(2) + " 0 " + s.box.w.toFixed(2) + " " + s.box.h.toFixed(2);
    const parts = [
      '<path class="tooth-outline" d="' + s.outline + '"/>',
      '<path class="tooth-shadow" d="' + s.shadow + '"/>'
    ].concat(s.highlight.map(d => '<path class="tooth-highlight" d="' + d + '"/>'));
    return '<svg viewBox="' + vb + '" preserveAspectRatio="xMidYMid meet" aria-hidden="true">' + parts.join("") + "</svg>";
  }

  /**
   * The three layers of one tooth as SVG, already in the local space: crown at y 0,
   * root tip at y height, horizontally centred on 0. Every view uses this, so a
   * change to the artwork shows up everywhere at once.
   *
   * classPrefix is applied to each layer (outline / shadow / highlight), which is how
   * a view gives the layers its own colours.
   */
  function markup(toothNumber, classPrefix) {
    const s = shapeOf(toothNumber);
    const p = classPrefix || "tooth";
    const parts = [
      '<path class="' + p + '-outline" d="' + s.outline + '"/>',
      '<path class="' + p + '-shadow" d="' + s.shadow + '"/>'
    ].concat(s.highlight.map(d => '<path class="' + p + '-highlight" d="' + d + '"/>'));
    return parts.join("");
  }

  window.DentalRayToothShapes = {
    shapeOf, svg, quadrantClass, typeOf, quadrantOf, isUpper,
    TARGET_WIDEST, GLOBAL_SCALE, SHAPES, markup,
    // Provenance, kept in the code so the licence stays with the artwork.
    credit: "Tooth artwork from react-odontogram (MIT), github.com/biomathcode/react-odontogram"
  };
})();
