// ============================================================
// DentalRay - گفتگو با AI پس از تحلیل
// ============================================================
// سرویسِ AI بینِ درخواست‌ها حافظه ندارد؛ پس سرور گزارشِ ذخیره‌شده را به‌عنوانِ
// زمینه می‌فرستد و پرسش‌های قبلی هم همراهِ پرسشِ تازه می‌روند.
//
// دو راه‌انداز:
//  - پرسشِ متنی: فقط گزارش + پرسش ⇒ چند ثانیه، بدونِ ارسالِ تصویر
//  - پرسش با تصویر: تصویر از دیسکِ خودِ مطب دوباره خوانده می‌شود؛ کاربر نیازی
//    به انتخابِ دوباره ندارد، فقط هزینهٔ آن نوبت را می‌پردازد.
(() => {
  "use strict";
  const esc = value => { const d = document.createElement("div"); d.textContent = value ?? ""; return d.innerHTML; };

  function bubble(who, text, isError) {
    const el = document.createElement("div");
    el.className = "ai-chat-bubble " + (who === "user" ? "is-user" : "is-ai") + (isError ? " is-error" : "");
    el.textContent = text;
    return el;
  }

  async function openChat(imageIDs, report) {
    const ids = (Array.isArray(imageIDs) ? imageIDs : [])
      .map(Number)
      .filter(n => Number.isFinite(n) && n > 0)
      .slice(0, 6);
    if (!ids.length) return false;

    // همیشه فقط یک گفتگو باز باشد.
    document.getElementById("cardExtractionModal")?.remove();

    const history = [];
    const modal = document.createElement("div");
    modal.id = "cardExtractionModal";
    modal.className = "card-extraction-overlay";
    modal.innerHTML = `<div class="card-extraction-dialog" role="dialog" aria-modal="true">
      <header><div><h3>گفتگو با AI</h3><span>پرسش دربارهٔ همین تحلیل</span></div><button type="button" class="card-extraction-close">×</button></header>
      <div class="card-extraction-body">
        <details class="ai-chat-report"><summary>گزارشِ تحلیل</summary><pre>${esc(typeof report === "string" ? report : JSON.stringify(report || {}, null, 2))}</pre></details>
        <div class="ai-chat-log"></div>
        <textarea class="ai-chat-input" rows="2" placeholder="پرسش خود را بنویسید… (Enter برای ارسال)"></textarea>
        <div class="ai-chat-actions">
          <button type="button" class="secondary-button" data-ask="0">پرسشِ متنی</button>
          <button type="button" class="secondary-button" data-ask="1">پرسش با تصویر</button>
          <span class="field-hint">پرسشِ متنی چند ثانیه طول می‌کشد؛ با تصویر ممکن است ۱ تا ۴ دقیقه.</span>
        </div>
      </div>
      <footer><button type="button" class="card-extraction-done">بستن</button></footer>
    </div>`;
    document.body.appendChild(modal);

    const onKey = e => { if (e.key === "Escape") done(); };
    function done() { modal.remove(); document.removeEventListener("keydown", onKey); }
    modal.querySelector(".card-extraction-close").onclick = done;
    modal.querySelector(".card-extraction-done").onclick = done;
    modal.addEventListener("click", e => { if (e.target === modal) done(); });
    document.addEventListener("keydown", onKey);

    const log = modal.querySelector(".ai-chat-log");
    const input = modal.querySelector(".ai-chat-input");
    const buttons = Array.from(modal.querySelectorAll("[data-ask]"));
    const add = (who, text, isError) => {
      const el = bubble(who, text, isError);
      log.appendChild(el);
      log.scrollTop = log.scrollHeight;
      return el;
    };

    const post = async (question, withImages, consented) => {
      const params = [];
      if (withImages && consented) params.push("consent=1");
      const r = await fetch(`/api/ai/images/ask${params.length ? "?" + params.join("&") : ""}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ imageIDs: ids, question, withImages, history: history.slice(-6) })
      });
      let x = {};
      try { x = await r.json(); } catch {}
      return { r, x };
    };

    const send = async withImages => {
      const question = input.value.trim();
      if (!question) { input.focus(); return; }
      input.value = "";
      add("user", question);
      const waiting = add("ai", withImages
        ? "در حال بررسیِ تصاویر و گزارش… ممکن است ۱ تا ۴ دقیقه طول بکشد"
        : "در حال پاسخ…");
      buttons.forEach(b => { b.disabled = true; });
      try {
        let res = await post(question, withImages, false);
        // سرور برای ارسالِ دوبارهٔ تصویر تأییدِ صریح می‌خواهد.
        if (res.r.status === 400 && res.x && res.x.needsConsent) {
          const ok = window.DentalRayAIConsent && await window.DentalRayAIConsent.ask();
          if (!ok) { waiting.replaceWith(bubble("ai", "ارسال لغو شد.", true)); return; }
          res = await post(question, withImages, true);
        }
        if (!res.r.ok || res.x.success === false)
          throw new Error(res.x.message || "پاسخی دریافت نشد.");
        const answer = String(res.x.answer || "").trim();
        if (!answer) throw new Error("پاسخ خالی بود؛ دوباره تلاش کنید.");
        waiting.replaceWith(bubble("ai", answer));
        history.push({ question, answer });
        log.scrollTop = log.scrollHeight;
      } catch (e) {
        waiting.replaceWith(bubble("ai", e.message || "پاسخی دریافت نشد.", true));
      } finally {
        buttons.forEach(b => { b.disabled = false; });
        input.focus();
      }
    };

    buttons.forEach(b => b.addEventListener("click", () => send(b.dataset.ask === "1")));
    input.addEventListener("keydown", e => {
      if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); send(false); }
    });

    add("ai", "پرسش‌تان را بپرسید. برای «دوباره دقیق‌تر نگاه کن» باید «پرسش با تصویر» را بزنید تا تصویرها دوباره بررسی شوند.");
    input.focus();
    return true;
  }

  // این فایل باید بعد از card-extraction.js بیاید تا شیء آماده باشد.
  if (window.DentalRayImageAI) window.DentalRayImageAI.openChat = openChat;
})();
