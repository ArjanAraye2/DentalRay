// ============================================================
// ReSiRai - صفحهٔ گزارش‌ها (مالی، مراجعات، تصویر)
// ============================================================
// دسترسی: مدیرِ سیستم، یا هر کاربری که پرچمِ «دسترسی به گزارش‌ها» را دارد.
// همهٔ اعداد سمتِ سرور در یک فراخوانی محاسبه می‌شود؛ اینجا فقط نمایش و چاپ.
(() => {
  const main = document.querySelector(".page-container");
  const link = document.querySelector('.sidebar-link[data-nav="reports"]');
  if (!main || !link) return;

  const canView = () =>
    window.reSiRaiCurrentUser?.isSuperAdmin === true ||
    window.reSiRaiCurrentUser?.viewReports === true;

  const esc = value => { const d = document.createElement("div"); d.textContent = value ?? ""; return d.innerHTML; };
  const num = value => Number(value || 0).toLocaleString("fa-IR");
  const money = value => `${num(value)} تومان`;
  const dayText = value => {
    if (!value) return "—";
    try { return new Date(value).toLocaleDateString("fa-IR"); } catch { return String(value).slice(0, 10); }
  };

  const section = document.createElement("section");
  section.id = "reportsSection";
  section.className = "card hidden shell-page";
  section.innerHTML = `
    <div class="section-header">
      <div><h2>گزارش‌ها</h2><p>مالی، مراجعات و تصویر در یک بازهٔ تاریخی</p></div>
      <div class="reports-actions"><button type="button" id="reportsPrint" class="secondary-button">چاپ</button></div>
    </div>
    <div class="reports-filters">
      <div class="form-field"><label for="reportsFrom">از تاریخ</label><input id="reportsFrom" type="text" data-jalali-date inputmode="numeric" placeholder="1405/01/01"></div>
      <div class="form-field"><label for="reportsTo">تا تاریخ</label><input id="reportsTo" type="text" data-jalali-date inputmode="numeric" placeholder="1405/12/30"></div>
      <div class="reports-presets">
        <button type="button" class="secondary-button" data-range="today">امروز</button>
        <button type="button" class="secondary-button" data-range="week">این هفته</button>
        <button type="button" class="secondary-button" data-range="month">این ماه</button>
        <button type="button" class="secondary-button" data-range="year">امسال</button>
      </div>
      <button type="button" id="reportsLoad">نمایش گزارش</button>
    </div>
    <div id="reportsStatus" class="status-message"></div>
    <div class="reports-print-head" id="reportsPrintHead"></div>
    <div id="reportsSummary" class="reports-cards"></div>
    <div id="reportsBody"></div>`;
  main.appendChild(section);

  const $ = id => document.getElementById(id);

  // ---- بازهٔ زمانی (تاریخِ شمسی در فرم، میلادی برای سرور) -------------------
  const jalaliParts = date => {
    const parts = new Intl.DateTimeFormat("en-US-u-ca-persian", { year: "numeric", month: "2-digit", day: "2-digit" }).formatToParts(date);
    const get = type => Number(parts.find(x => x.type === type)?.value || 0);
    return { y: get("year"), m: get("month"), d: get("day") };
  };
  const jalaliText = (y, m, d) =>
    `${String(y).padStart(4, "0")}/${String(m).padStart(2, "0")}/${String(d).padStart(2, "0")}`;

  function applyRange(range) {
    const today = new Date();
    const now = jalaliParts(today);
    if (range === "today") { $("reportsFrom").value = jalaliText(now.y, now.m, now.d); $("reportsTo").value = $("reportsFrom").value; }
    else if (range === "week") {
      // هفتهٔ کاری از شنبه شروع می‌شود.
      const sinceSaturday = (today.getDay() + 1) % 7;
      const start = new Date(today);
      start.setDate(today.getDate() - sinceSaturday);
      const s = jalaliParts(start);
      $("reportsFrom").value = jalaliText(s.y, s.m, s.d);
      $("reportsTo").value = jalaliText(now.y, now.m, now.d);
    }
    else if (range === "month") { $("reportsFrom").value = jalaliText(now.y, now.m, 1); $("reportsTo").value = jalaliText(now.y, now.m, now.d); }
    else if (range === "year") { $("reportsFrom").value = jalaliText(now.y, 1, 1); $("reportsTo").value = jalaliText(now.y, now.m, now.d); }
    load();
  }

  function block(title, hint, inner) {
    return `<section class="reports-block">
      <div class="reports-block-head"><strong>${esc(title)}</strong>${hint ? `<span>${esc(hint)}</span>` : ""}</div>
      ${inner}
    </section>`;
  }

  function table(headers, rows, emptyText) {
    if (!rows.length) return `<p class="field-hint">${esc(emptyText || "موردی در این بازه نیست.")}</p>`;
    return `<div class="table-container"><table class="patient-table"><thead><tr>${headers.map(h => `<th>${esc(h)}</th>`).join("")}</tr></thead>
      <tbody>${rows.map(r => `<tr>${r.map(c => `<td>${c}</td>`).join("")}</tr>`).join("")}</tbody></table></div>`;
  }

  function render(d) {
    const f = d.finance || {}, v = d.visits || {}, im = d.images || {};
    const debtorTotal = (d.debtors || []).reduce((sum, x) => sum + Number(x.balance || 0), 0);

    $("reportsPrintHead").textContent = `گزارش ReSiRai — از ${dayText(d.from)} تا ${dayText(d.to)} · صادرشده: ${dayText(d.generatedAt)}`;

    $("reportsSummary").innerHTML = [
      ["دریافتی", money(f.received), "metric-green"],
      ["بازپرداخت", money(f.refunded), "metric-red"],
      ["تخفیف", money(f.discounted), "metric-purple"],
      ["کارهای ثبت‌شده", money(f.performed), "metric-blue"],
      ["مراجعات", num(v.count), "metric-cyan"],
      ["بیماران جدید", num(v.newPatients), "metric-gray"]
    ].map(([label, value, cls]) => `<div class="dashboard-summary-card ${cls}"><strong>${value}</strong><span>${label}</span></div>`).join("");

    const dailyRows = (f.daily || []).map(x => [
      dayText(x.date), num(x.count), money(x.received), x.refunded ? money(x.refunded) : "—"
    ]);
    const methodRows = (f.byMethod || []).map(x => [esc(x.label), num(x.count), money(x.amount)]);
    const typeRows = (v.byStudyType || []).map(x => [esc(x.name), num(x.count)]);
    const doctorRows = (v.byDoctor || []).map(x => [esc(x.name), num(x.count)]);
    const imageRows = (im.byType || []).map(x => [esc(x.name), num(x.count)]);
    const debtorRows = (d.debtors || []).map(x => [esc(x.name), esc(x.mobile || "—"), money(x.balance)]);

    $("reportsBody").innerHTML =
      block("دریافتی به تفکیکِ روز", `${num(f.paymentCount)} پرداخت · ${num(f.refundCount)} بازپرداخت`,
        table(["تاریخ", "تعداد", "دریافتی", "بازپرداخت"], dailyRows)) +
      block("دریافتی به تفکیکِ روش", "",
        table(["روش", "تعداد", "مبلغ"], methodRows)) +
      block("مراجعات", `${num(v.patientCount)} بیمار در این بازه`,
        table(["نوع مراجعه", "تعداد"], typeRows) + table(["دندانپزشک", "تعداد"], doctorRows)) +
      block("تصاویر", `${num(im.total)} تصویر`,
        table(["نوع تصویر", "تعداد"], imageRows)) +
      block("بدهکاران", (d.debtors || []).length ? `مجموعِ نمایش‌داده‌شده: ${money(debtorTotal)}` : "",
        table(["نام بیمار", "موبایل", "مانده"], debtorRows, "بیمارِ بدهکاری نیست."));

    const empty = !dailyRows.length && !typeRows.length && !imageRows.length && !debtorRows.length;
    if (empty) $("reportsBody").insertAdjacentHTML("afterbegin", '<p class="field-hint">در این بازه داده‌ای ثبت نشده است.</p>');
  }

  async function load() {
    if (!canView()) return;
    const status = $("reportsStatus");
    status.classList.remove("error");
    status.textContent = "در حال محاسبهٔ گزارش...";
    try {
      let from, to;
      try {
        from = window.parsePersianDateForBackend?.($("reportsFrom").value, false) || null;
        to = window.parsePersianDateForBackend?.($("reportsTo").value, false) || null;
      } catch (e) { throw new Error(e.message || "تاریخ واردشده معتبر نیست."); }
      if (!from || !to) throw new Error("هر دو تاریخ را وارد کنید.");

      const r = await fetch(`/api/reports/summary?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`, { cache: "no-store" });
      const x = await r.json().catch(() => ({}));
      if (!r.ok || x.success === false) throw new Error(x.message || "گزارش دریافت نشد.");
      render(x);
      status.textContent = "";
    } catch (e) {
      status.textContent = e.message || "گزارش دریافت نشد.";
      status.classList.add("error");
    }
  }

  function open() {
    if (!canView()) return;
    document.querySelectorAll(".page-container > section").forEach(x => x.classList.add("hidden"));
    section.classList.remove("hidden");
    document.querySelectorAll(".sidebar-link").forEach(x => x.classList.toggle("active", x === link));
    window.scrollTo(0, 0);
    if (!$("reportsFrom").value) applyRange("month");
    else load();
  }

  $("reportsLoad").addEventListener("click", load);
  $("reportsPrint").addEventListener("click", () => window.print());
  section.querySelectorAll("[data-range]").forEach(button =>
    button.addEventListener("click", () => applyRange(button.dataset.range)));

  // آیتمِ منو فقط برای کسانی که اجازهٔ دیدن دارند؛ اگر دسترسی وسطِ کار
  // بازپس گرفته شود، خودِ صفحه هم باید بسته شود.
  function sync() {
    const allowed = canView();
    link.classList.toggle("hidden", !allowed);
    if (!allowed) section.classList.add("hidden");
  }
  window.addEventListener("resirai-auth-changed", sync);
  setTimeout(sync, 0);
  window.ReSiRaiJalali?.enhanceAll?.(section);

  window.ReSiRaiReports = { open, refresh: load };
})();
