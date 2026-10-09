/* Department/Properties.aspx - list, filters, pager and the new / edit / view dialog over api/v1/properties.
   Every dropdown option (category, status, ward, zone) comes from GET api/v1/properties/options; this file holds no option values. */
(function () {
  'use strict';
  var page = document.getElementById('propPage');
  if (!page || !window.fetch) return;

  var API = (window.SMC_BASE || '') + '/api/v1/properties';
  var NEW_VALUE = '__new__';
  var FIELDS = ['Category', 'PropertyCode', 'Name', 'Ward', 'Zone', 'Address', 'AreaSqFt', 'MonthlyRent', 'AnnualDemand', 'SurveyNumber', 'TpNumber', 'Status', 'CurrentOccupant', 'Shera'];
  var MSG = {
    loading: 'माहिती लोड होत आहे…',
    empty: 'कोणतीही नोंद आढळली नाही.',
    loadError: 'माहिती लोड करता आली नाही. कृपया पुन्हा प्रयत्न करा.',
    saved: 'नोंद जतन केली.',
    deleted: 'नोंद हटवली.',
    dirty: 'बदल जतन केलेले नाहीत. बंद करायचे?',
    saveError: 'नोंद जतन करता आली नाही.',
    deleteError: 'नोंद हटवता आली नाही.',
    newValue: '+ नवीन मूल्य टाका',
    choose: '-- निवडा --',
    none: '-- नाही --',
    titleNew: 'नवीन नोंद', titleEdit: 'नोंद संपादन', titleView: 'नोंद तपशील',
    view: 'पाहा', edit: 'संपादन', del: 'हटवा',
    related: 'संबंधित नोंदी', docs: 'कागदपत्रे', noDocs: 'कागदपत्रे जोडलेली नाहीत.', manageDocs: 'कागदपत्रे जोडा / पाहा',
    pageInfo: function (p, t, n) { return 'पृष्ठ ' + p + ' / ' + t + ' (एकूण ' + n + ')'; }
  };

  var $ = function (id) { return document.getElementById(id); };
  var state = { page: 1, pageSize: 10, totalPages: 0, search: '', category: '', status: '', options: null, seq: 0 };
  var dlg = { mode: null, id: 0, version: '', opener: null, dirty: false, codeTouched: false, busy: false };

  function request(method, url, body) {
    var init = { method: method, credentials: 'same-origin', headers: { 'Accept': 'application/json' } };
    if (method !== 'GET') { init.headers['X-SMC-Request'] = '1'; init.headers['Content-Type'] = 'application/json'; }
    if (body !== undefined) init.body = JSON.stringify(body);
    return fetch(url, init).then(function (r) {
      return r.text().then(function (t) {
        var data = null; try { data = t ? JSON.parse(t) : null; } catch (e) { data = null; }
        return { ok: r.ok, status: r.status, data: data };
      });
    });
  }

  function text(el, s) { el.textContent = s == null ? '' : String(s); }
  function el(tag, cls, s) { var e = document.createElement(tag); if (cls) e.className = cls; if (s != null) text(e, s); return e; }
  function show(e, on) { if (on) e.removeAttribute('hidden'); else e.setAttribute('hidden', ''); }
  function msgOf(res, fallback) { return res && res.data && res.data.message ? res.data.message : fallback; }

  function notice(s) {
    var n = $('propNotice'); text(n, s); show(n, !!s);
    if (s) { clearTimeout(notice.t); notice.t = setTimeout(function () { show(n, false); }, 5000); }
  }

  /* ---------- options ---------- */
  function fill(select, list, first, keepValue) {
    var current = keepValue === undefined ? select.value : keepValue;
    while (select.options.length) select.remove(0);
    if (first != null) select.add(new Option(first, ''));
    (list || []).forEach(function (o) { select.add(new Option(o.label, o.code)); });
    if (current) setSelect(select, current);
  }

  /* Selects the value; a stored value missing from the list (legacy) is added so it is shown and kept. */
  function setSelect(select, value) {
    value = value == null ? '' : String(value);
    if (value && !Array.prototype.some.call(select.options, function (o) { return o.value === value; })) {
      var opt = new Option(value, value), last = select.options[select.options.length - 1];
      if (last && last.value === NEW_VALUE) select.add(opt, last); else select.add(opt);
    }
    select.value = value;
  }

  function loadOptions() {
    return request('GET', API + '/options').then(function (res) {
      if (!res.ok || !res.data) throw new Error('options');
      var o = res.data; state.options = o;
      fill($('propFilterCategory'), o.categories, $('propFilterCategory').options[0].text);
      fill($('propFilterStatus'), o.statuses, $('propFilterStatus').options[0].text);
      var w = $('propWarn');
      if (o.warnings && o.warnings.length) { text(w, o.warnings.join(' ')); show(w, true); } else show(w, false);
    }).catch(function () {
      var w = $('propWarn'); text(w, MSG.loadError); show(w, true);
    });
  }

  function fillDialogSelects() {
    var o = state.options || { categories: [], statuses: [], wards: [], zones: [] };
    fill($('pf_Category'), o.categories, MSG.choose, '');
    fill($('pf_Status'), o.statuses, MSG.choose, '');
    ['Ward', 'Zone'].forEach(function (f) {
      var s = $('pf_' + f);
      fill(s, f === 'Ward' ? o.wards : o.zones, MSG.none, '');
      s.add(new Option(MSG.newValue, NEW_VALUE));
      var free = $(s.getAttribute('data-free')); free.value = ''; show(free, false);
    });
  }

  /* ---------- list ---------- */
  function setState(s, cls) { var st = $('propState'); text(st, s); st.className = 'prop-state' + (cls ? ' ' + cls : ''); show(st, !!s); }

  function loadList() {
    var seq = ++state.seq, rows = $('propRows');
    rows.setAttribute('aria-busy', 'true'); setState(MSG.loading);
    var q = '?page=' + state.page + '&pageSize=' + state.pageSize +
      '&search=' + encodeURIComponent(state.search) + '&category=' + encodeURIComponent(state.category) + '&status=' + encodeURIComponent(state.status);
    return request('GET', API + q).then(function (res) {
      if (seq !== state.seq) return;
      rows.setAttribute('aria-busy', 'false');
      if (!res.ok || !res.data) { rows.innerHTML = ''; setState(msgOf(res, MSG.loadError), 'prop-state-error'); show($('propPager'), false); return; }
      var d = res.data;
      if (d.items.length === 0 && d.page > 1 && d.total > 0) { state.page = Math.max(1, Math.ceil(d.total / d.pageSize)); loadList(); return; }
      render(d);
    }).catch(function () {
      if (seq !== state.seq) return;
      rows.setAttribute('aria-busy', 'false'); rows.innerHTML = '';
      setState(MSG.loadError, 'prop-state-error'); show($('propPager'), false);
    });
  }

  function actionButton(label, cls, act, item) {
    var b = el('button', 'prop-btn prop-btn-sm ' + cls, label);
    b.type = 'button'; b.setAttribute('data-act', act); b.setAttribute('data-id', item.id);
    b.setAttribute('aria-label', label + ' ' + item.propertyCode);
    return b;
  }

  function render(d) {
    var rows = $('propRows'); rows.innerHTML = '';
    d.items.forEach(function (it) {
      var tr = el('tr');
      tr.appendChild(el('td', 'prop-code', it.propertyCode));
      tr.appendChild(el('td', null, it.name));
      tr.appendChild(el('td', null, it.categoryLabel));
      tr.appendChild(el('td', null, it.ward || '-'));
      tr.appendChild(el('td', 'prop-amount', it.annualDemandText));
      var st = el('td'), pill = el('span', 'prop-pill', it.statusLabel);
      pill.setAttribute('data-status', it.statusCode || ''); st.appendChild(pill); tr.appendChild(st);
      var act = el('td', 'prop-actions');
      act.appendChild(actionButton(MSG.view, 'prop-btn-o', 'view', it));
      act.appendChild(actionButton(MSG.edit, 'prop-btn-o', 'edit', it));
      act.appendChild(actionButton(MSG.del, 'prop-btn-danger', 'delete', it));
      act.lastChild.setAttribute('data-label', it.propertyCode + ' — ' + it.name);
      tr.appendChild(act);
      rows.appendChild(tr);
    });
    state.totalPages = d.totalPages; state.page = d.page;
    setState(d.items.length ? '' : MSG.empty);
    var many = d.totalPages > 1;
    show($('propPager'), many);
    if (many) {
      text($('propPageInfo'), MSG.pageInfo(d.page, d.totalPages, d.total));
      $('propPrev').disabled = d.page <= 1; $('propNext').disabled = d.page >= d.totalPages;
    }
  }

  var debounce;
  $('propSearch').addEventListener('input', function () {
    clearTimeout(debounce);
    debounce = setTimeout(function () { state.search = $('propSearch').value.trim(); state.page = 1; loadList(); }, 300);
  });
  $('propFilterCategory').addEventListener('change', function () { state.category = this.value; state.page = 1; loadList(); });
  $('propFilterStatus').addEventListener('change', function () { state.status = this.value; state.page = 1; loadList(); });
  $('propPrev').addEventListener('click', function () { if (state.page > 1) { state.page--; loadList(); } });
  $('propNext').addEventListener('click', function () { if (state.page < state.totalPages) { state.page++; loadList(); } });
  $('propRows').addEventListener('click', function (e) {
    var b = e.target.closest ? e.target.closest('button[data-act]') : null;
    if (!b) return;
    var id = parseInt(b.getAttribute('data-id'), 10), act = b.getAttribute('data-act');
    if (act === 'delete') askDelete(id, b.getAttribute('data-label'), b); else openDialog(act, id, b);
  });
  $('propNew').addEventListener('click', function () { openDialog('new', 0, this); });

  /* Enter inside the page must not submit the Web Forms form. */
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Enter' && e.target && e.target.tagName !== 'TEXTAREA' && e.target.tagName !== 'BUTTON' && (page.contains(e.target) || e.target.closest && e.target.closest('.prop-overlay'))) e.preventDefault();
  });

  /* ---------- dialog ---------- */
  function clearErrors() {
    FIELDS.forEach(function (f) { var e = $('pe_' + f); if (e) text(e, ''); var i = $('pf_' + f); if (i) i.removeAttribute('aria-invalid'); });
    show($('propFormError'), false);
  }

  function fieldError(field, message) {
    var e = $('pe_' + field), i = $('pf_' + field);
    if (!e) return false;
    text(e, message);
    if (i) { i.setAttribute('aria-invalid', 'true'); i.setAttribute('aria-describedby', 'pe_' + field); }
    return true;
  }

  function formError(s) { var e = $('propFormError'); text(e, s); show(e, !!s); }

  function setReadOnly(ro) {
    FIELDS.forEach(function (f) {
      var i = $('pf_' + f);
      if (i.tagName === 'SELECT') i.disabled = ro; else i.readOnly = ro;
    });
    ['pf_WardNew', 'pf_ZoneNew'].forEach(function (id) { $(id).readOnly = ro; });
  }

  function focusables() {
    return Array.prototype.filter.call($('propDialog').querySelectorAll('a[href],button,input,select,textarea,[tabindex]:not([tabindex="-1"])'),
      function (x) { return !x.disabled && !x.hasAttribute('hidden') && x.offsetParent !== null; });
  }

  function openOverlay(overlay) {
    show(overlay, true); document.body.classList.add('prop-modal-open');
  }
  function closeOverlay(overlay) {
    show(overlay, false);
    if ($('propOverlay').hasAttribute('hidden') && $('propConfirm').hasAttribute('hidden')) document.body.classList.remove('prop-modal-open');
  }

  function openDialog(mode, id, opener) {
    dlg = { mode: mode, id: id, version: '', opener: opener, dirty: false, codeTouched: false, busy: false };
    fillDialogSelects(); clearErrors();
    FIELDS.forEach(function (f) { var i = $('pf_' + f); if (i.tagName !== 'SELECT') i.value = ''; });
    var extras = $('propExtras'); extras.innerHTML = ''; show(extras, false);
    text($('propDialogTitle'), mode === 'new' ? MSG.titleNew : mode === 'edit' ? MSG.titleEdit : MSG.titleView);
    show($('propSave'), mode !== 'view');
    setReadOnly(mode === 'view');
    $('pf_Category').disabled = mode !== 'new';
    $('pf_PropertyCode').readOnly = mode !== 'new';
    openOverlay($('propOverlay'));
    if (mode === 'new') {
      $('pf_MonthlyRent').value = '0'; $('pf_AnnualDemand').value = '0';
      if (state.options && state.options.defaultStatus) setSelect($('pf_Status'), state.options.defaultStatus);
      $('pf_Category').focus();
      return;
    }
    $('propDialog').focus(); $('propSave').disabled = true;
    request('GET', API + '/' + id).then(function (res) {
      $('propSave').disabled = false;
      if (!res.ok || !res.data) { formError(msgOf(res, MSG.loadError)); return; }
      var d = res.data; dlg.version = d.version || '';
      FIELDS.forEach(function (f) {
        var i = $('pf_' + f), v = d[f.charAt(0).toLowerCase() + f.slice(1)];
        if (i.tagName === 'SELECT') setSelect(i, v || ''); else i.value = v == null ? '' : v;
      });
      if (mode === 'view') renderExtras(d);
      var first = mode === 'edit' ? $('pf_Name') : $('propClose'); first.focus();
    }).catch(function () { $('propSave').disabled = false; formError(MSG.loadError); });
  }

  function renderExtras(d) {
    var box = $('propExtras'); box.innerHTML = '';
    var rel = el('section', 'prop-related'); rel.appendChild(el('h3', null, MSG.related));
    var tiles = el('div', 'tiles');
    (d.relatedLinks || []).forEach(function (l) {
      var a = el('a', 'tile'); a.href = l.href;
      var ic = el('span', null, l.icon); ic.setAttribute('aria-hidden', 'true'); a.appendChild(ic); a.appendChild(document.createTextNode(l.label));
      tiles.appendChild(a);
    });
    rel.appendChild(tiles); box.appendChild(rel);
    var docs = el('section', 'prop-docs'); docs.appendChild(el('h3', null, MSG.docs));
    if (!d.documents || !d.documents.length) docs.appendChild(el('p', 'muted', MSG.noDocs));
    else {
      var ul = el('ul', 'prop-doc-list');
      d.documents.forEach(function (x) {
        var li = el('li'), a = el('a', null, x.fileName); a.href = x.url; li.appendChild(a);
        li.appendChild(document.createTextNode(' — ' + x.sizeText + ', ' + x.dateText)); ul.appendChild(li);
      });
      docs.appendChild(ul);
    }
    if (d.documentsUrl) { var m = el('a', 'prop-btn prop-btn-o prop-btn-sm', MSG.manageDocs); m.href = d.documentsUrl; docs.appendChild(m); }
    box.appendChild(docs); show(box, true);
  }

  function closeDialog(force) {
    if (!force && dlg.mode !== 'view' && dlg.dirty && !window.confirm(MSG.dirty)) return;
    closeOverlay($('propOverlay'));
    var o = dlg.opener; dlg.mode = null;
    if (o && document.body.contains(o)) o.focus();
  }

  function refreshNextCode() {
    if (dlg.mode !== 'new' || dlg.codeTouched) return;
    var cat = $('pf_Category').value;
    if (!cat) { $('pf_PropertyCode').value = ''; return; }
    request('GET', API + '/next-code?category=' + encodeURIComponent(cat)).then(function (res) {
      if (dlg.mode === 'new' && !dlg.codeTouched && $('pf_Category').value === cat && res.ok && res.data) $('pf_PropertyCode').value = res.data.code || '';
    }).catch(function () { });
  }

  $('pf_Category').addEventListener('change', refreshNextCode);
  $('pf_PropertyCode').addEventListener('input', function () { dlg.codeTouched = true; });
  ['pf_Ward', 'pf_Zone'].forEach(function (id) {
    $(id).addEventListener('change', function () {
      var free = $(this.getAttribute('data-free')), on = this.value === NEW_VALUE;
      show(free, on); if (on) free.focus(); else free.value = '';
    });
  });
  $('propForm').addEventListener('input', function () { dlg.dirty = true; });
  $('propForm').addEventListener('change', function () { dlg.dirty = true; });

  function valueOf(f) {
    var i = $('pf_' + f);
    if ((f === 'Ward' || f === 'Zone') && i.value === NEW_VALUE) return $(i.getAttribute('data-free')).value.trim().slice(0, 50);
    return i.value.trim();
  }

  function save() {
    if (dlg.busy || dlg.mode === 'view') return;
    clearErrors();
    var body = {}; FIELDS.forEach(function (f) { body[f] = valueOf(f); });
    body.Version = dlg.version;
    dlg.busy = true; $('propSave').disabled = true; $('propSave').setAttribute('aria-busy', 'true');
    var isNew = dlg.mode === 'new';
    request(isNew ? 'POST' : 'PUT', isNew ? API : API + '/' + dlg.id, body).then(function (res) {
      if (res.ok) {
        closeDialog(true); notice(MSG.saved);
        if (isNew) state.page = 1;
        loadOptions(); loadList();
        return;
      }
      if (res.status === 422 && res.data && res.data.errors) {
        var firstBad = null, rest = [];
        res.data.errors.forEach(function (x) {
          if (fieldError(x.field, x.message)) { if (!firstBad) firstBad = $('pf_' + x.field); } else rest.push(x.message);
        });
        formError(rest.length ? rest.join(' ') : res.data.message);
        if (firstBad) firstBad.focus();
        return;
      }
      formError(msgOf(res, MSG.saveError));
    }).catch(function () { formError(MSG.saveError); }).then(function () {
      dlg.busy = false; $('propSave').disabled = false; $('propSave').removeAttribute('aria-busy');
    });
  }

  $('propSave').addEventListener('click', save);
  $('propCancel').addEventListener('click', function () { closeDialog(false); });
  $('propClose').addEventListener('click', function () { closeDialog(false); });
  $('propOverlay').addEventListener('mousedown', function (e) { if (e.target === this) closeDialog(false); });

  /* ---------- delete ---------- */
  var del = { id: 0, opener: null, busy: false };
  function askDelete(id, label, opener) {
    del = { id: id, opener: opener, busy: false };
    text($('propConfirmText'), label); show($('propConfirmError'), false);
    $('propConfirmYes').disabled = false;
    openOverlay($('propConfirm')); $('propConfirmNo').focus();
  }
  function closeConfirm() {
    closeOverlay($('propConfirm'));
    if (del.opener && document.body.contains(del.opener)) del.opener.focus();
  }
  $('propConfirmNo').addEventListener('click', closeConfirm);
  $('propConfirm').addEventListener('mousedown', function (e) { if (e.target === this) closeConfirm(); });
  $('propConfirmYes').addEventListener('click', function () {
    if (del.busy) return;
    del.busy = true; this.disabled = true;
    request('DELETE', API + '/' + del.id).then(function (res) {
      if (res.ok) { del.opener = $('propNew'); closeConfirm(); notice(MSG.deleted); loadList(); return; }
      var e = $('propConfirmError'); text(e, msgOf(res, MSG.deleteError)); show(e, true);
    }).catch(function () { var e = $('propConfirmError'); text(e, MSG.deleteError); show(e, true); })
      .then(function () { del.busy = false; $('propConfirmYes').disabled = false; });
  });

  /* ---------- keyboard: Esc closes, Tab stays inside the open dialog ---------- */
  document.addEventListener('keydown', function (e) {
    var confirmOpen = !$('propConfirm').hasAttribute('hidden'), dialogOpen = !$('propOverlay').hasAttribute('hidden');
    if (!confirmOpen && !dialogOpen) return;
    if (e.key === 'Escape' || e.key === 'Esc') { e.preventDefault(); if (confirmOpen) closeConfirm(); else closeDialog(false); return; }
    if (e.key !== 'Tab') return;
    var root = confirmOpen ? $('propConfirm') : $('propDialog');
    var items = confirmOpen
      ? Array.prototype.filter.call(root.querySelectorAll('button'), function (x) { return !x.disabled; })
      : focusables();
    if (!items.length) return;
    var first = items[0], last = items[items.length - 1];
    if (!root.contains(document.activeElement)) { e.preventDefault(); first.focus(); }
    else if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
    else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
  });

  loadOptions().then(loadList);
})();
