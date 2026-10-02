
/* نگهبانِ ورود: تا وضعیتِ ورود (پروبِ /api/auth/me) روشن نشده، درخواست‌های
   /api نگه داشته می‌شوند؛ اگر مهمان باشد همان‌جا با پاسخِ 401ِ ساختگی آزاد
   می‌شوند — بدونِ رفت‌وآمدِ شبکه، بدونِ پیامِ «Failed to load resource». */
(function(){
  var orig = window.fetch.bind(window), state = 'unknown', queue = [];
  function release(guest){
    state = guest ? 'guest' : 'authed';
    queue.splice(0).forEach(function(fn){ fn(guest); });
  }
  window.__resiraiAuthGate = { authed: function(){ release(false); }, guest: function(){ release(true); } };
  window.fetch = function(url, opts){
    var u = String(url);
    var isAuth = u.indexOf('/api/auth/') >= 0;
    if (u.indexOf('/api/') < 0 || isAuth) return orig(url, opts);
    if (state === 'guest') return Promise.resolve(new Response('', { status: 401 }));
    if (state === 'authed') return orig(url, opts);
    return new Promise(function(res, rej){
      queue.push(function(guest){
        if (guest) { res(new Response('', { status: 401 })); return; }
        orig(url, opts).then(res, rej);
      });
    });
  };
  setTimeout(function(){ if (state === 'unknown') release(true); }, 5000);
})();
