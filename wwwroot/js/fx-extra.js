/* Fixora — page behaviour that fx-ui.js does not cover (load with `defer`, after fx-ui.js). Opt-in through data attributes. */
(function () {
  'use strict';
  var $ = function (s, r) { return (r || document).querySelector(s); };
  var $$ = function (s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };
  var csrf = ($('meta[name=csrf-token]') || {}).content;
  function money(n) { return n.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }

  /* ---------- bell: opening it marks the feed as read ---------- */
  $$('[data-fx-seen]').forEach(function (b) {
    b.addEventListener('click', function () {
      var d = $('.d', b); if (!d) return;
      fetch(b.dataset.fxSeen, { method: 'POST', headers: { 'X-CSRF-TOKEN': csrf, Accept: 'application/json' } }).then(function () { d.remove(); });
    });
  });

  /* ---------- keyboard: role=button spans open their menu / dialog with Enter / Space ---------- */
  $$('[role=button][tabindex]').forEach(function (n) {
    n.addEventListener('keydown', function (e) { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); n.click(); } });
  });

  /* ---------- password meter: <div class="pw-meter" data-fx-meter-for="password"> 4 bars ---------- */
  $$('[data-fx-meter-for]').forEach(function (m) {
    var i = document.getElementById(m.dataset.fxMeterFor); if (!i) return;
    var bars = $$('i', m);
    function draw() {
      var v = i.value, s = 0;
      if (v.length >= 8) s++; if (/\d/.test(v)) s++; if (/[^A-Za-z0-9]/.test(v)) s++; if (v.length >= 12 && /[A-Z]/.test(v) && /[a-z]/.test(v)) s++;
      var ok = v.length >= 8 && /\d/.test(v) && /[^A-Za-z0-9]/.test(v);
      bars.forEach(function (b, k) { b.className = k < s ? 'on' : ''; b.style.background = (k < s && !ok) ? 'var(--danger)' : ''; });
    }
    i.addEventListener('input', draw); if (i.value) draw();
  });

  /* ---------- account type cards (SH-04): button label follows the choice ---------- */
  $$('[data-fx-role-label]').forEach(function (btn) {
    var form = btn.closest('form');
    function sync() { var r = $('input[name=type]:checked', form); if (r) { $('span', btn).textContent = r.dataset.label; } $$('.role', form).forEach(function (c) { var on = !!$('input:checked', c); c.classList.toggle('on', on); var rd = $('.radio', c); if (rd) rd.classList.toggle('on', on); }); }
    form.addEventListener('change', sync); sync();
  });

  /* ---------- district <select> filtered by the city <select>: data-fx-city="#city" ---------- */
  $$('select[data-fx-city]').forEach(function (d) {
    var c = $(d.dataset.fxCity); if (!c) return;
    function sync() {
      $$('option[data-city]', d).forEach(function (o) { o.hidden = o.dataset.city !== c.value; o.disabled = o.hidden; });
      var cur = d.selectedOptions[0];
      if (cur && cur.dataset.city && cur.dataset.city !== c.value) d.value = '';
    }
    c.addEventListener('change', sync); sync();
  });

  /* ---------- zone chips filtered by the city: <div data-fx-zones-city="#city"> labels with data-city ---------- */
  $$('[data-fx-zones-city]').forEach(function (box) {
    var c = $(box.dataset.fxZonesCity); if (!c) return;
    function sync() {
      $$('[data-city]', box).forEach(function (l) { var on = l.dataset.city === c.value; l.hidden = !on; if (!on) { var i = $('input', l); if (i) i.checked = false; } });
    }
    c.addEventListener('change', sync); sync();
  });

  /* ---------- OTP submit button looks disabled (btn-f disabled, SH-07) until the 6 digits are in ---------- */
  $$('[data-fx-otp-submit]').forEach(function (b) {
    var f = b.closest('form'), h = $('input[name=code]', f), on = b.className, off = on.replace(/\bbtn-(key|blue|danger)\b/, 'disabled');
    function sync() { var ok = h && h.value.length === 6; b.className = ok ? on : off; b.disabled = !ok; }
    f.addEventListener('input', sync); sync();
  });

  /* ---------- file input → .doc row label (technician documents) ---------- */
  $$('[data-fx-doc]').forEach(function (inp) {
    var row = inp.closest('.doc'), out = $('[data-fx-doc-name]', row), tag = $('[data-fx-doc-tag]', row), empty = $('[data-fx-doc-tag-empty]', row);
    inp.addEventListener('change', function () {
      var f = inp.files[0]; if (!f) return;
      if (out) out.textContent = f.name + ' · ' + (f.size > 1048576 ? (f.size / 1048576).toFixed(1) + ' MB' : Math.round(f.size / 1024) + ' KB') + ' · ready to upload';
      row.classList.add('ok'); row.classList.remove('err'); if (tag) tag.hidden = false; if (empty) empty.hidden = true;
    });
  });

  /* ---------- invoice materials rows (technician create invoice) ---------- */
  $$('[data-fx-materials]').forEach(function (box) {
    var tpl = $('template', box), list = $('[data-fx-rows]', box), form = box.closest('form');
    var base = +(box.dataset.base || 0), max = +(box.dataset.max || 15);
    function renum() {
      $$('[data-fx-row]', list).forEach(function (r, k) {
        $$('input', r).forEach(function (i) { i.name = i.name.replace(/materials\[\d*\]/, 'materials[' + k + ']'); });
      });
      var add = $('[data-fx-add-row]', box); if (add) add.hidden = $$('[data-fx-row]', list).length >= max;
    }
    function total() {
      var m = 0;
      $$('[data-fx-row]', list).forEach(function (r) {
        var q = +(($('[data-q]', r) || {}).value || 0), a = +(($('[data-a]', r) || {}).value || 0);
        m += q * a; var t = $('[data-line]', r); if (t) t.textContent = 'SAR ' + money(q * a);
      });
      $$('[data-fx-mat-total]', form).forEach(function (n) { n.textContent = money(m); });
      $$('[data-fx-grand]', form).forEach(function (n) { n.textContent = money(base + m); });
    }
    box.addEventListener('click', function (e) {
      if (e.target.closest('[data-fx-add-row]')) { e.preventDefault(); list.appendChild(tpl.content.cloneNode(true)); renum(); total(); var last = $$('[data-fx-row]', list).pop(); if (last) $('input', last).focus(); }
      var x = e.target.closest('[data-fx-del-row]'); if (x) { e.preventDefault(); x.closest('[data-fx-row]').remove(); renum(); total(); }
    });
    box.addEventListener('input', total); renum(); total();
  });

  /* ---------- technician results (UC1-07 → UC1-08): the page shows the loading state while the list loads ---------- */
  $$('[data-fx-results]').forEach(function (box) {
    var url = box.dataset.fxResults, loading = $(box.dataset.fxLoading || '_');
    function load(u, push) {
      if (loading) loading.hidden = false;
      fetch(u, { headers: { Accept: 'text/html', 'X-Requested-With': 'XMLHttpRequest' } }).then(function (r) { return r.text(); }).then(function (h) {
        box.innerHTML = h; if (loading) loading.hidden = true; box.hidden = false;
        if (push) history.replaceState(null, '', u.replace('/book/technicians/list', '/book/technicians'));
        wire();
      });
    }
    function wire() {
      // filters / sort inside the partial reload the list without leaving the page
      $$('form[data-fx-filter-form]', box).forEach(function (f) {
        f.addEventListener('change', function () { var q = new URLSearchParams(new FormData(f)).toString(); load(url.split('?')[0] + '?' + q, true); });
      });
      $$('a[data-fx-reload]', box).forEach(function (a) { a.addEventListener('click', function (e) { e.preventDefault(); load(a.href.replace(/\/book\/technicians(\?|$)/, '/book/technicians/list$1'), true); }); });
      $$('[data-fx-menu]', box).forEach(function (b) {
        var m = $(b.dataset.fxMenu, box); if (!m) return;
        function mark(open) { b.classList.toggle('open', open); var i = $('i.bi-chevron-down, i.bi-chevron-up', b); if (i) { i.classList.toggle('bi-chevron-up', open); i.classList.toggle('bi-chevron-down', !open); } }
        b.addEventListener('click', function (e) { e.preventDefault(); e.stopPropagation(); $$('.pop', box).forEach(function (p) { if (p !== m) p.hidden = true; }); m.hidden = !m.hidden; mark(!m.hidden); });
        document.addEventListener('click', function (e) { if (!m.hidden && !m.contains(e.target)) { m.hidden = true; mark(false); } });
      });
      $$('[role=button][tabindex]', box).forEach(function (n) { n.addEventListener('keydown', function (e) { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); n.click(); } }); });
      $$('[data-fx-profile]', box).forEach(function (a) { a.addEventListener('click', function (e) { e.preventDefault(); openProfile(a.dataset.fxProfile); }); });
    }
    load(url, false);
  });

  /* ---------- technician profile pop-up (UC1-11): partial loaded into #fx-profile ---------- */
  function openProfile(u) {
    var d = $('#fx-profile'); if (!d) return;
    var body = $('[data-fx-profile-body]', d);
    fetch(u, { headers: { Accept: 'text/html', 'X-Requested-With': 'XMLHttpRequest' } }).then(function (r) { return r.text(); }).then(function (h) {
      body.innerHTML = h; d.hidden = false;
    });
  }
  $$('[data-fx-profile]').forEach(function (a) { if (!a.closest('[data-fx-results]')) a.addEventListener('click', function (e) { e.preventDefault(); openProfile(a.dataset.fxProfile); }); });

  /* ---------- report a problem: problem chips (radio labels styled .fchip) keep the selected look ---------- */

  /* ---------- service list (UC1-02): only the chosen service's stepper is named "quantity"; the summary row follows ---------- */
  $$('[data-fx-svc-list]').forEach(function (f) {
    function sync() {
      $$('.svc-opt', f).forEach(function (o) {
        var r = $('input[type=radio]', o), q = $('.qty input', o); if (!q) return;
        q.name = r.checked ? 'quantity' : 'q_' + r.value;
      });
      var r = $('input[name=service_id]:checked', f), q = $('input[name=quantity]', f);
      if (r) $$('.srow [data-fx-line]').forEach(function (n) { n.textContent = r.dataset.name + ' × ' + (q ? q.value : 1); });
    }
    f.addEventListener('change', sync, true); sync();
  });

  /* ---------- details step (UC1-04/05): calendar pop-up, labels, typed address picks "other" ---------- */
  $$('[data-fx-pop]').forEach(function (b) {
    var pop = $(b.dataset.fxPop); if (!pop) return;
    function chev(open) { $$('[data-fx-pop] i.tr').forEach(function (i) { if (i.classList.contains('bi-chevron-down') || i.classList.contains('bi-chevron-up')) { i.classList.toggle('bi-chevron-up', open && i.closest('[data-fx-pop]') === b); i.classList.toggle('bi-chevron-down', !(open && i.closest('[data-fx-pop]') === b)); } }); }
    b.addEventListener('click', function (e) { e.stopPropagation(); pop.hidden = !pop.hidden; b.classList.toggle('focus', !pop.hidden); chev(!pop.hidden); });
    document.addEventListener('click', function (e) { if (!pop.hidden && !pop.contains(e.target) && !e.target.closest('[data-fx-pop]')) { pop.hidden = true; b.classList.remove('focus'); } });
  });
  $$('#cal-pop').forEach(function (pop) {
    var form = pop.closest('form'), date = $('input[name=date]', form), dl = $('[data-fx-date-label]', form), sl = $('[data-fx-slot-label]', form);
    var MON = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'], DAY = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
    function labels() {
      if (date.value) { var p = date.value.split('-'), d = new Date(+p[0], +p[1] - 1, +p[2]); dl.textContent = DAY[d.getDay()] + ', ' + d.getDate() + ' ' + MON[d.getMonth()] + ' ' + d.getFullYear(); dl.classList.remove('ph'); }
      else { dl.textContent = 'Choose a date'; dl.classList.add('ph'); }
      var s = $('input[name=slot]:checked', form);
      if (s) { sl.textContent = s.parentNode.textContent; sl.classList.remove('ph'); } else { sl.textContent = 'Choose a slot'; sl.classList.add('ph'); }
    }
    form.addEventListener('change', function (e) { if (e.target === date || e.target.name === 'slot') labels(); });
    $('[data-fx-cal-apply]', pop).addEventListener('click', function () { labels(); pop.hidden = true; $$('[data-fx-pop]', form).forEach(function (b) { b.classList.remove('focus'); }); });
    $('[data-fx-cal-clear]', pop).addEventListener('click', function () { var s = $('input[name=slot]:checked', form); if (s) s.checked = false; labels(); });
  });
  $$('[data-fx-other-addr]').forEach(function (i) {
    var form = i.closest('form');
    function pick() { var o = $('input[name=address_choice][value=other]', form); if (o && !o.checked) { o.checked = true; o.dispatchEvent(new Event('change', { bubbles: true })); } }
    i.addEventListener('focus', pick); i.addEventListener('input', pick);
  });
  $$('[data-fx-autoupload]').forEach(function (i) {
    i.addEventListener('change', function () {
      var n = i.files.length, row = i.previousElementSibling, txt = row && $('.mini-l', row);
      if (txt && n) txt.textContent = txt.textContent.replace(/ · \d+ new.*$/, '') + ' · ' + n + ' new to upload';
    });
  });

  /* ---------- UC1-13 sending overlay: the booking exists; the remaining steps tick, then the booking page opens ---------- */
  $$('[data-fx-sending]').forEach(function (o) {
    var steps = $$('[data-fx-step]', o), k = 0;
    var t = setInterval(function () {
      var s = steps[k]; if (!s) { clearInterval(t); window.location = o.dataset.fxSending; return; }
      s.className = 'lstep done'; $('.c', s).innerHTML = '<i class="bi bi-check-lg"></i>';
      k++; var n = steps[k]; if (n) { n.className = 'lstep now'; $('.c', n).innerHTML = '<span class="spin o"></span>'; }
    }, 900);
  });

  /* ---------- payment method (UC2-03…07): button label, bank overlay only for cards ---------- */
  $$('form[data-fx-pay]').forEach(function (f) {
    var btn = $('[data-fx-pay-btn]', f), with_ = $('[data-fx-pay-with]'), overlay = f.dataset.fxOverlay;
    function sync() {
      var r = $('input[name=method]:checked', f); if (!r) return;
      $('span', btn).textContent = r.dataset.label; btn.disabled = false; btn.className = 'btn-f btn-key lg';
      if (r.value === 'saved' || r.value === 'card') { f.dataset.fxOverlay = overlay; if (with_) with_.textContent = r.dataset.with; }
      else delete f.dataset.fxOverlay;
      btn.dataset.fxBusy = r.value === 'cash' ? 'Creating your code…' : (r.value === 'stc' ? 'Opening STC Pay…' : 'Processing…');
    }
    f.addEventListener('change', sync); sync();
  });

  /* ---------- single file into a drawn .input (UC2-15 "1 photo added · 3.1 MB") ---------- */
  $$('[data-fx-file]').forEach(function (i) {
    var box = i.closest('.input'), lbl = $('[data-fx-file-label]', box), size = $('[data-fx-file-size]', box);
    i.addEventListener('change', function () {
      var f = i.files[0]; if (!f) return;
      var ok = /^image\/(jpeg|png)$/.test(f.type) && f.size <= 10485760;
      lbl.textContent = ok ? '1 photo added' : '"' + f.name + '" can\'t be uploaded. Use a JPG or PNG photo under 10 MB.';
      box.classList.toggle('error', !ok);
      if (size) { size.hidden = !ok; $('span:last-child', size).textContent = (f.size / 1048576).toFixed(1) + ' MB'; }
      if (!ok) i.value = '';
    });
  });

  /* ---------- details step: "Find technicians" looks disabled until a booking type is chosen (UC1-03) ---------- */
  $$('[data-fx-details-submit]').forEach(function (b) {
    var f = b.closest('form'), on = b.className;
    function sync() { var ok = !!$('input[name=booking_type]:checked', f); b.className = ok ? on : 'btn-f disabled'; b.disabled = !ok; }
    f.addEventListener('change', sync); sync();
  });

  /* ---------- coverage zones (SH-08): chosen districts show as chips; "+ Add district" checks one ---------- */
  $$('[data-fx-zone-add]').forEach(function (s) {
    var box = s.closest('.input');
    function sync() { $$('option', s).forEach(function (o) { if (!o.value) return; var i = $('input[value="' + o.value + '"]', box); o.hidden = o.disabled = !!(i && i.checked); }); }
    s.addEventListener('change', function () {
      var i = $('input[value="' + s.value + '"]', box);
      if (i && $$('input:checked', box).length < 6) { i.checked = true; i.dispatchEvent(new Event('change', { bubbles: true })); }
      s.value = ''; sync();
    });
    box.addEventListener('change', sync); sync();
  });

  /* ---------- print buttons ---------- */
  $$('[data-fx-print]').forEach(function (b) { b.addEventListener('click', function (e) { e.preventDefault(); window.print(); }); });

  /* ---------- show / hide blocks from a radio or checkbox: data-fx-show="#id" on the input, value matched by data-fx-when ---------- */
  $$('[data-fx-toggle-by]').forEach(function (blk) {
    var name = blk.dataset.fxToggleBy, when = blk.dataset.fxWhen.split(','), form = blk.closest('form') || document;
    function sync() {
      var c = $('input[name="' + name + '"]:checked', form) || $('select[name="' + name + '"]', form);
      var on = when.indexOf(c ? c.value : '') > -1;
      blk.hidden = !on;
      $$('input,select,textarea', blk).forEach(function (i) { if (i.dataset.fxReq !== undefined) i.required = on; });
    }
    form.addEventListener('change', sync); sync();
  });
})();
