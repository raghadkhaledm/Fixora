/* Fixora — shared behaviour (load with `defer` on every page). Opt-in through data attributes. */
(function () {
  'use strict';
  var $ = function (s, r) { return (r || document).querySelector(s); };
  var $$ = function (s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };
  var csrf = ($('meta[name=csrf-token]') || {}).content;
  function esc(s) { return String(s).replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); }
  function mmss(s) { s = Math.max(0, s); return ('0' + Math.floor(s / 60)).slice(-2) + ':' + ('0' + s % 60).slice(-2); }

  /* ---------- burger drawer: <button class="fx-burger" data-fx-nav-toggle> + <div class="fx-backdrop" data-fx-nav-close> ---------- */
  $$('[data-fx-nav-toggle]').forEach(function (b) {
    b.addEventListener('click', function () { var o = document.body.classList.toggle('fx-nav-open'); b.setAttribute('aria-expanded', o); });
  });
  $$('[data-fx-nav-close], .appnav .links a, .nav-links a').forEach(function (n) { n.addEventListener('click', function () { document.body.classList.remove('fx-nav-open'); }); });
  document.addEventListener('keydown', function (e) {
    if (e.key !== 'Escape') return;
    document.body.classList.remove('fx-nav-open');
    $$('[data-fx-dialog]').forEach(function (d) { d.hidden = true; });
    $$('[data-fx-menu-box]').forEach(function (m) { m.hidden = true; });
  });

  /* ---------- auto-submit (filter chips, sort, availability toggle): <form data-fx-autosubmit> ---------- */
  $$('form[data-fx-autosubmit]').forEach(function (f) { f.addEventListener('change', function () { f.submit(); }); });

  /* ---------- menus: [data-fx-menu="#sort-menu"] toggles <div id="sort-menu" class="dropdown fx-menu" data-fx-menu-box hidden> ---------- */
  $$('[data-fx-menu]').forEach(function (b) {
    var m = $(b.dataset.fxMenu);
    b.addEventListener('click', function (e) { e.preventDefault(); e.stopPropagation(); m.hidden = !m.hidden; });
    document.addEventListener('click', function (e) { if (!m.hidden && !m.contains(e.target)) m.hidden = true; });
  });

  /* ---------- dialogs: [data-fx-open="#id"] → <div class="fx-dialog" id="id" data-fx-dialog hidden><div class="modal-f">… ---------- */
  $$('[data-fx-open]').forEach(function (b) {
    b.addEventListener('click', function (e) { e.preventDefault(); var d = $(b.dataset.fxOpen); if (d) d.hidden = false; });
  });
  $$('[data-fx-dialog]').forEach(function (d) {
    d.addEventListener('click', function (e) { if (e.target === d || e.target.closest('[data-fx-close]')) { e.preventDefault(); d.hidden = true; } });
  });

  /* ---------- toasts: <div class="toast fx-toast" data-fx-toast> ---------- */
  $$('[data-fx-toast]').forEach(function (t) {
    var x = $('.x, [data-fx-close]', t); if (x) x.addEventListener('click', function () { t.remove(); });
    setTimeout(function () { t.classList.add('hide'); }, 5000); setTimeout(function () { t.remove(); }, 5500);
  });

  /* ---------- busy buttons (UC1-13 "Sending request…", UC2-06 "Processing…", login): <button data-fx-busy="Sending request…"> ---------- */
  $$('form').forEach(function (f) {
    f.addEventListener('submit', function (e) {
      var b = e.submitter && e.submitter.dataset.fxBusy ? e.submitter : $('button[type=submit][data-fx-busy]', f);
      if (!b || !f.checkValidity()) return;
      setTimeout(function () { b.disabled = true; b.innerHTML = '<i class="bi bi-arrow-repeat spin"></i> ' + esc(b.dataset.fxBusy); }, 0);
      var o = f.dataset.fxOverlay && $(f.dataset.fxOverlay); if (o) o.hidden = false;
    });
  });

  /* ---------- quantity: <div class="qty" data-fx-qty data-min="1" data-max="10"> ---------- */
  $$('[data-fx-qty]').forEach(function (q) {
    var i = $('input', q), min = +(q.dataset.min || 1), max = +(q.dataset.max || 10);
    function sync() { $$('button[data-step]', q).forEach(function (b) { b.disabled = (+b.dataset.step < 0 && +i.value <= min) || (+b.dataset.step > 0 && +i.value >= max); }); }
    $$('button[data-step]', q).forEach(function (b) {
      b.addEventListener('click', function () {
        var v = Math.min(max, Math.max(min, (+i.value || min) + (+b.dataset.step)));
        if (v !== +i.value) { i.value = v; i.dispatchEvent(new Event('change', { bubbles: true })); }
        sync();
      });
    });
    sync();
  });

  /* ---------- live price on the service step: inputs with data-fx-price + data-fx-total target ---------- */
  $$('[data-fx-estimate]').forEach(function (box) {
    var form = box.closest('form') || document;
    function money(n) { return n.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }
    function calc() {
      var r = $('input[name=service_id]:checked', form), q = $('input[name=quantity]', form);
      if (!r) return;
      var price = +r.dataset.price, qty = q ? +q.value : 1, fee = +(box.dataset.fee || 15), exp = +(box.dataset.express || 0);
      $$('[data-fx-line]', box).forEach(function (n) { n.textContent = r.dataset.name + ' × ' + qty; });
      $$('[data-fx-line-amount]', box).forEach(function (n) { n.textContent = 'SAR ' + money(price * qty); });
      $$('[data-fx-total]', box).forEach(function (n) { n.textContent = money(price * qty + fee + exp); });
    }
    form.addEventListener('change', calc); calc();
  });

  /* ---------- Saudi mobile after the fixed +966: 9 digits shown "55 214 7730" ---------- */
  $$('[data-fx-phone]').forEach(function (i) {
    function fmt() {
      var d = i.value.replace(/\D/g, '');
      if (d.indexOf('966') === 0) d = d.slice(3);
      if (d.indexOf('0') === 0) d = d.slice(1);
      d = d.slice(0, 9);
      i.value = [d.slice(0, 2), d.slice(2, 5), d.slice(5, 9)].filter(Boolean).join(' ');
    }
    i.addEventListener('input', fmt); fmt();
  });

  /* ---------- password: eye + "Strong password" hint (SH-04) ---------- */
  $$('[data-fx-eye]').forEach(function (b) {
    b.addEventListener('click', function () {
      var i = $('input', b.closest('.input')), show = i.type === 'password';
      i.type = show ? 'text' : 'password';
      var ic = $('i', b); ic.classList.toggle('bi-eye-slash', !show); ic.classList.toggle('bi-eye', show);
    });
  });
  $$('[data-fx-strength-for]').forEach(function (h) {
    var i = document.getElementById(h.dataset.fxStrengthFor); if (!i) return;
    function draw() {
      var v = i.value, ok = v.length >= 8 && /\d/.test(v) && /[^A-Za-z0-9]/.test(v);
      h.hidden = !v;
      h.className = 'help' + (ok ? '' : ' err');
      h.innerHTML = ok ? '<i class="bi bi-check-circle" style="color:var(--success)"></i> Strong password'
                       : '<i class="bi bi-x-circle"></i> Too weak. Use at least 8 characters with a number and a symbol.';
    }
    i.addEventListener('input', draw); draw();
  });

  /* ---------- card form: number groups, MM / YY, CVV ---------- */
  $$('[data-fx-card]').forEach(function (i) { i.addEventListener('input', function () { i.value = i.value.replace(/\D/g, '').slice(0, 16).replace(/(\d{4})(?=\d)/g, '$1 '); }); });
  $$('[data-fx-expiry]').forEach(function (i) {
    i.addEventListener('input', function (e) {
      var d = i.value.replace(/\D/g, '').slice(0, 4);
      i.value = d.length > 2 ? d.slice(0, 2) + ' / ' + d.slice(2) : (d.length === 2 && e.inputType !== 'deleteContentBackward' ? d + ' / ' : d);
    });
  });
  $$('[data-fx-cvv]').forEach(function (i) { i.addEventListener('input', function () { i.value = i.value.replace(/\D/g, '').slice(0, 3); }); });
  $$('[data-fx-upper]').forEach(function (i) { i.addEventListener('input', function () { i.value = i.value.toUpperCase(); }); });

  /* ---------- character counter: <textarea data-fx-count="#cnt" maxlength="500"> ---------- */
  $$('[data-fx-count]').forEach(function (t) {
    var c = $(t.dataset.fxCount), max = t.maxLength;
    function d() { c.textContent = t.value.length + ' / ' + max; }
    t.addEventListener('input', d); d();
  });

  /* ---------- OTP boxes: <div class="otp-d" data-fx-otp="code"> six inputs → hidden input name=code ---------- */
  $$('[data-fx-otp]').forEach(function (box) {
    var ins = $$('input', box), hidden = $('input[name="' + box.dataset.fxOtp + '"]', box.closest('form') || document);
    ins.forEach(function (i, k) {
      i.setAttribute('inputmode', 'numeric'); i.setAttribute('placeholder', ' '); i.setAttribute('autocomplete', k ? 'off' : 'one-time-code');
      i.addEventListener('input', function () {
        var v = i.value.replace(/\D/g, '');
        if (v.length > 1) {                       // pasted the whole code
          v.split('').slice(0, ins.length - k).forEach(function (ch, j) { ins[k + j].value = ch; });
          (ins[Math.min(ins.length - 1, k + v.length)] || i).focus();
        } else { i.value = v; if (v && ins[k + 1]) ins[k + 1].focus(); }
        box.classList.remove('err');
        if (hidden) hidden.value = ins.map(function (x) { return x.value; }).join('');
        if (hidden && hidden.value.length === ins.length && box.hasAttribute('data-fx-autosubmit')) box.closest('form').submit();
      });
      i.addEventListener('keydown', function (e) { if (e.key === 'Backspace' && !i.value && ins[k - 1]) ins[k - 1].focus(); });
    });
  });

  /* ---------- countdowns: <span data-fx-countdown="582" data-fx-expired="Code expired"> mm:ss ---------- */
  $$('[data-fx-countdown]').forEach(function (n) {
    var s = +n.dataset.fxCountdown;
    var t = setInterval(function () {
      s--; n.textContent = mmss(s);
      if (s <= 0) { clearInterval(t); if (n.dataset.fxExpired) n.textContent = n.dataset.fxExpired; var b = $(n.dataset.fxEnable || '_'); if (b) { b.classList.remove('dis'); b.removeAttribute('disabled'); } }
    }, 1000);
    n.textContent = mmss(s);
  });

  /* ---------- wait for the technician to confirm cash (UC2-09): <div data-fx-poll="/invoices/1873/status" data-fx-done="/invoices/1873/receipt"> ---------- */
  $$('[data-fx-poll]').forEach(function (n) {
    var t = setInterval(function () {
      fetch(n.dataset.fxPoll, { headers: { Accept: 'application/json' } }).then(function (r) { return r.json(); }).then(function (j) {
        if (j.paid) { clearInterval(t); window.location = n.dataset.fxDone; }
      }).catch(function () {});
    }, 4000);
  });

  /* ---------- photos of the problem: <label class="drop" data-fx-photos="#thumbs" data-max="3"> + <div id="thumbs" class="thumbs"> ---------- */
  $$('[data-fx-photos]').forEach(function (z) {
    var input = $('input[type=file]', z), list = $(z.dataset.fxPhotos), max = +(z.dataset.max || 3), err = $(z.dataset.fxError || '_');
    var kept = new DataTransfer();
    function render(bad) {
      var html = '';
      Array.prototype.forEach.call(kept.files, function (f, k) {
        html += '<div class="thumb"><img src="' + URL.createObjectURL(f) + '" alt=""><button type="button" class="x" data-k="' + k + '"><i class="bi bi-x"></i></button></div>';
      });
      if (bad) html += '<div class="thumb bad"><i class="bi bi-file-earmark-x"></i></div>';
      if (kept.files.length < max) html += '<div class="thumb add" data-fx-add><i class="bi bi-plus-lg"></i></div>';
      var size = Array.prototype.reduce.call(kept.files, function (s, f) { return s + f.size; }, 0);
      if (kept.files.length) html += '<div class="align-self-center mini-l" style="margin-left:6px">' + kept.files.length + ' photo' + (kept.files.length > 1 ? 's' : '') + ' attached · ' + (size / 1048576).toFixed(1) + ' MB</div>';
      list.innerHTML = html; input.files = kept.files;
      z.hidden = kept.files.length > 0 || !!bad; list.hidden = !kept.files.length && !bad;
      if (err) { err.hidden = !bad; if (bad) err.innerHTML = '<i class="bi bi-x-circle"></i> "' + esc(bad) + '" can\'t be uploaded. Use a JPG or PNG photo under 10 MB.'; }
    }
    function add(files) {
      var bad = null;
      Array.prototype.forEach.call(files, function (f) {
        if (!/^image\/(jpeg|png)$/.test(f.type) || f.size > 10485760) bad = f.name;
        else if (kept.files.length < max) kept.items.add(f);
      });
      render(bad);
    }
    input.addEventListener('change', function () { add(input.files); });
    ['dragenter', 'dragover'].forEach(function (t) { z.addEventListener(t, function (e) { e.preventDefault(); z.classList.add('drag'); }); });
    ['dragleave', 'drop'].forEach(function (t) { z.addEventListener(t, function (e) { e.preventDefault(); z.classList.remove('drag'); }); });
    z.addEventListener('drop', function (e) { add(e.dataTransfer.files); });
    list.addEventListener('click', function (e) {
      if (e.target.closest('[data-fx-add]')) { input.click(); return; }
      var x = e.target.closest('.x[data-k]'); if (!x) return;
      var n = new DataTransfer(); Array.prototype.forEach.call(kept.files, function (f, k) { if (k !== +x.dataset.k) n.items.add(f); });
      kept = n; render();
    });
  });

  /* ---------- scheduled date + free slots (UC1-05): [data-fx-cal] draws the prototype .cal; slots come from the server ---------- */
  $$('[data-fx-cal]').forEach(function (w) {
    var input = $('input[name=date]', w.closest('form')), slotsBox = $(w.dataset.fxSlots), slotsUrl = w.dataset.fxSlotsUrl;
    var min = new Date(w.dataset.min), max = new Date(w.dataset.max), full = (w.dataset.full || '').split(',');
    var MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    var view = input.value ? new Date(input.value) : new Date(min);
    function iso(d) { return d.getFullYear() + '-' + ('0' + (d.getMonth() + 1)).slice(-2) + '-' + ('0' + d.getDate()).slice(-2); }
    function draw() {
      var first = new Date(view.getFullYear(), view.getMonth(), 1), start = new Date(first); start.setDate(1 - first.getDay());
      var html = '<div class="mh"><i class="bi bi-chevron-left" data-m="-1" role="button"></i>' + MONTHS[view.getMonth()] + ' ' + view.getFullYear() +
                 '<i class="bi bi-chevron-right" data-m="1" role="button"></i></div><div class="g">' +
                 ['SU', 'MO', 'TU', 'WE', 'TH', 'FR', 'SA'].map(function (d) { return '<span class="w">' + d + '</span>'; }).join('');
      for (var k = 0; k < 42; k++) {
        var d = new Date(start); d.setDate(start.getDate() + k);
        if (k >= 35 && d.getMonth() !== view.getMonth()) break;
        var c = 'd', s = iso(d);
        if (d < min || d > max || d.getMonth() !== view.getMonth()) c += ' x';
        else if (full.indexOf(s) > -1) c += ' full';
        if (s === input.value) c += ' on';
        if (s === iso(new Date())) c += ' today';
        html += '<span class="' + c + '" data-d="' + s + '">' + d.getDate() + '</span>';
      }
      w.innerHTML = html + '</div>';
    }
    function loadSlots() {
      if (!slotsBox || !input.value) return;
      fetch(slotsUrl + '?date=' + input.value, { headers: { Accept: 'application/json' } }).then(function (r) { return r.json(); }).then(function (j) {
        var cur = (($('input[name=slot]:checked', slotsBox) || {}).value) || slotsBox.dataset.selected;
        slotsBox.innerHTML = j.slots.map(function (s) {
          return s.free ? '<label class="slot"><input type="radio" class="fx-hide" name="slot" value="' + s.start + '"' + (s.start === cur ? ' checked' : '') + '>' + esc(s.label) + '</label>'
                        : '<span class="slot off">' + esc(s.label) + '</span>';
        }).join('');
        var t = $(slotsBox.dataset.fxTitle || '_'); if (t) t.textContent = j.title;
      });
    }
    w.addEventListener('click', function (e) {
      var m = e.target.closest('[data-m]'); if (m) { view.setMonth(view.getMonth() + (+m.dataset.m)); return draw(); }
      var d = e.target.closest('[data-d]'); if (!d || /\b(x|full)\b/.test(d.className)) return;
      input.value = d.dataset.d; draw(); loadSlots(); input.dispatchEvent(new Event('change', { bubbles: true }));
    });
    draw(); loadSlots();
  });

  /* ---------- confirm simple destructive forms: <form data-fx-confirm="…"> ---------- */
  $$('form[data-fx-confirm]').forEach(function (f) { f.addEventListener('submit', function (e) { if (!window.confirm(f.dataset.fxConfirm)) e.preventDefault(); }); });

  /* ---------- live search box on the category step: filters .cat cards by text ---------- */
  $$('[data-fx-filter]').forEach(function (i) {
    var items = $$(i.dataset.fxFilter);
    i.addEventListener('input', function () { var q = i.value.trim().toLowerCase(); items.forEach(function (n) { n.hidden = q && n.textContent.toLowerCase().indexOf(q) < 0; }); });
  });
})();
