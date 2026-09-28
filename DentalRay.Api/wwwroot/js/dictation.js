// دیکتهٔ «شرح اقدام» — بدونِ واژهٔ بیداری، فقط نوشتن.
//
// با یک کلیک روی 🎤 شروع می‌شود و هر جملهٔ قطعی همان لحظه به انتهای فیلد
// اضافه می‌شود؛ توقف با کلیک دوباره یا Esc. چیزی خودکار ثبت نمی‌شود — متن
// می‌ماند و کاربر بعد از بازبینی، خودش «+ ثبت» را می‌زند.
(() => {
 "use strict";

 let recognition = null;
 let listening = false;
 let currentInput = null;

 function status(msg, isError) {
  const el = document.querySelector(".study-actions-status");
  if (!el) return;
  el.textContent = msg || "";
  el.classList.toggle("error", !!isError);
 }

 function button() { return document.getElementById("actionDictateBtn"); }

 function setUi(on) {
  const b = button();
  if (!b) return;
  b.classList.toggle("is-listening", on);
  b.setAttribute("aria-pressed", on ? "true" : "false");
  b.textContent = on ? "●" : "🎤";
  b.title = on ? "در حال گوش دادن — کلیک یا Esc برای توقف" : "دیکتهٔ شرح اقدام (کلیک برای شروع)";
 }

 // دکمه داخلِ خودِ فیلدِ «شرح اقدام» قرار می‌گیرد تا چیدمانِ فرم به‌هم نریزد.
 function mount() {
  const form = document.querySelector(".study-actions-form");
  if (!form || document.getElementById("actionDictateBtn")) return;
  const input = form.querySelector('input[placeholder="شرح اقدام"]');
  if (!input) return;

  const wrap = document.createElement("span");
  wrap.className = "dictate-wrap";
  input.parentNode.insertBefore(wrap, input);
  wrap.appendChild(input);

  const btn = document.createElement("button");
  btn.type = "button";
  btn.id = "actionDictateBtn";
  btn.className = "dictate-btn";
  btn.textContent = "🎤";
  btn.setAttribute("aria-pressed", "false");
  btn.title = "دیکتهٔ شرح اقدام (کلیک برای شروع)";
  wrap.appendChild(btn);

  currentInput = input;
  btn.addEventListener("click", () => (listening ? stop() : start(input)));
 }

 function append(text) {
  if (!currentInput || !text) return;
  const t = String(text).trim();
  if (!t) return;
  const cur = currentInput.value;
  // بینِ تکه‌های پشت‌سرِهم فاصله می‌افتد تا جمله خوانا بماند.
  currentInput.value = cur ? cur.replace(/\s+$/, "") + " " + t : t;
  currentInput.dispatchEvent(new Event("input", { bubbles: true }));
 }

 function start(input) {
  const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
  if (!SR) {
   status("دیکتهٔ صوتی در این مرورگر نیست — از Chrome یا Edge استفاده کنید.", true);
   return;
  }
  currentInput = input;
  const rec = new SR();
  recognition = rec;
  rec.lang = "fa-IR";
  rec.continuous = true;
  rec.interimResults = true;

  rec.onstart = () => { listening = true; setUi(true); status("در حال گوش دادن… شرح را روان بگویید."); };

  rec.onresult = (event) => {
   let interim = "";
   for (let i = event.resultIndex; i < event.results.length; i++) {
    const res = event.results[i];
    if (res.isFinal) append(res[0].transcript);
    else interim += res[0].transcript;
   }
   if (interim) status("می‌شنوم: " + interim.trim());
  };

  rec.onerror = (e) => {
   if (e.error === "not-allowed" || e.error === "service-not-allowed") {
    stop();
    status("دسترسی به میکروفن داده نشد؛ از نوارِ آدرسِ مرورگر مجوز بدهید.", true);
   } else if (e.error === "no-speech") {
    status("صدایی شنیده نشد… ادامه دهید.");
   } else if (e.error !== "aborted") {
    status("خطای دیکته: " + e.error, true);
   }
  };

  // مرورگر بعد از چند ثانیهِ سکوت، جلسه را خودش می‌بندد؛ اگر کاربر هنوز دارد
  // حرف می‌زند دوباره وصل می‌شویم تا وسطِ جمله قطع نشود.
  rec.onend = () => {
   if (!listening) { setUi(false); return; }
   try { rec.start(); } catch { listening = false; setUi(false); }
  };

  try { rec.start(); }
  catch { listening = false; setUi(false); status("گوش دادن شروع نشد؛ دوباره تلاش کنید.", true); }
 }

 function stop() {
  listening = false;
  const rec = recognition;
  recognition = null;
  if (rec) { try { rec.stop(); } catch { /* بی‌اثر */ } }
  setUi(false);
  status("دیکته متوقف شد. متن را بازبینی و «+ ثبت» را بزنید.");
 }

 // توقف با Esc
 document.addEventListener("keydown", (e) => { if (e.key === "Escape" && listening) stop(); });

 // اگر پنلِ اقدامات بسته شد، گوش دادن هم قطع شود + دکمه ساخته شود.
 const observer = new MutationObserver(() => {
  mount();
  if (listening && !document.querySelector(".study-actions-form")) stop();
 });
 observer.observe(document.body, { childList: true, subtree: true });
 mount();
})();
