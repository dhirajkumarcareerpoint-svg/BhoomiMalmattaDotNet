(function () {
  'use strict';
  // Single-choice checkbox UI, with the workflow API as the async path. The
  // submit button remains a Web Forms postback fallback if this script is absent.
  var officerRow = function (el) { return el && el.closest ? el.closest('.officer-row-action') : null; };
  var decisionRow = function (el) { return el && el.closest ? el.closest('tr') : null; };
  var requiresRemark = function (action) { return /JEReturnForCorrection|JEReject|OSRejectPayment|OSReturnForCorrection|OSReject|ACReturn|ACReject/.test(action || ''); };
  // Per-action confirm/success texts rendered by the server (OfficerText) on the row's submit button.
  var rowMessages = function (button, action) {
    var all = {}; try { all = JSON.parse(button.getAttribute('data-messages') || '{}'); } catch (_) {}
    return all[action] || {};
  };
  var updateOfficerButton = function (box) {
    if (!box) return;
    var row = decisionRow(box), checks = row ? row.querySelectorAll('.officer-action-options input[type="checkbox"]') : [], selected = [];
    Array.prototype.forEach.call(checks, function (x) { if (x.checked) selected.push(x); });
    var action = selected.length === 1 ? selected[0].value : '';
    var panel = officerRow(box), button = panel && panel.querySelector('[data-once]'), remark = panel && panel.querySelector('.officer-action-remark'), wrap = panel && panel.querySelector('.officer-remark-wrap');
    if (button && button.getAttribute('data-single-action')) return; // single Approve: always enabled, remark optional and shown via "Add remark"
    var needs = requiresRemark(action);
    if (wrap) wrap.style.display = needs && action ? '' : 'none';
    var validRemark = !needs || !!(remark && remark.value.trim());
    if (button) button.disabled = selected.length !== 1 || !validRemark;
  };
  var showRowError = function (row, message) { var target = row && row.querySelector('.officer-row-error'); if (target) { target.textContent = message || 'The action could not be completed.'; target.style.display = ''; } };
  var setCounts = function (counts) {
    if (!counts) return;
    [['total','total'],['pending','pending'],['processed','processed'],['approved','approved'],['rejected','rejected']].forEach(function (pair) {
      var value = counts[pair[1]]; if (value == null) value = counts[pair[1][0].toUpperCase()+pair[1].slice(1)];
      var card = document.querySelector('.officer-kpi[data-kpi="'+pair[0]+'"] strong'); if (card && value != null) card.textContent = value;
    });
  };
  var displayNotice = function (message) { var n = document.getElementById('officerNotice'); if (n) { n.textContent = message; n.className = 'notice ok'; n.style.display = ''; } };
  var submitOfficerAction = function (button) {
    var row = decisionRow(button), panel = officerRow(button); if (!row || !panel || button.disabled || button.getAttribute('data-submitted') === '1') return false;
    var single = button.getAttribute('data-single-action');
    var checks = row.querySelectorAll('.officer-action-options input[type="checkbox"]'), selected = [];
    Array.prototype.forEach.call(checks, function (x) { if (x.checked) selected.push(x); });
    if (!single && selected.length !== 1) return false;
    var action = single || selected[0].value, remark = panel.querySelector('.officer-action-remark');
    if (requiresRemark(action) && !(remark && remark.value.trim())) { updateOfficerButton(selected[0]); return false; }
    var number = button.getAttribute('data-application-number'), messages = rowMessages(button, action);
    var label = single ? null : document.querySelector('label[for="' + selected[0].id + '"]');
    var prompt = messages.confirm || (button.getAttribute('data-confirm-start') + (label ? label.textContent.trim() : '') + button.getAttribute('data-confirm-end') + number + button.getAttribute('data-confirm-question'));
    if (!window.confirm(prompt)) return false;
    // Without fetch the confirmed click falls back to the Web Forms postback.
    if (typeof window.fetch !== 'function') return true;
    button.setAttribute('data-submitted', '1');
    var controls = row.querySelectorAll('input,textarea,button'), spinner = panel.querySelector('.officer-spinner'), error = panel.querySelector('.officer-row-error');
    Array.prototype.forEach.call(controls, function (x) { x.disabled = true; });
    if (spinner) spinner.style.display = ''; if (error) { error.textContent = ''; error.style.display = 'none'; }
    var payload = { ApplicationId: Number(row.getAttribute('data-application-id')), Actions: [action], Remarks: remark ? remark.value.trim() : '', ExpectedStage: row.getAttribute('data-expected-stage'), ExpectedRowVersion: row.getAttribute('data-row-version') };
    fetch((window.SMC_BASE || '') + '/api/v1/applications/' + payload.ApplicationId + '/actions', { method:'POST', credentials:'same-origin', headers:{'Content-Type':'application/json','X-SMC-Request':'1'}, body:JSON.stringify(payload) })
      .then(function (response) { return response.text().then(function (text) { var body = {}; try { body = text ? JSON.parse(text) : {}; } catch (_) {} if (!response.ok) throw new Error(body.message || body.Message || body.Code || 'The action could not be completed.'); return body; }); })
      .then(function (data) {
        var counts = data.counts || data.Counts; setCounts(counts); row.parentNode.removeChild(row);
        var listCount = document.getElementById('litApplicationCount');
        if (listCount) { var current = parseInt(listCount.textContent, 10); if (!isNaN(current) && current > 0) listCount.textContent = String(current - 1); }
        displayNotice(messages.success || 'Application ' + number + ' updated.');
      })
      .catch(function (err) {
        showRowError(row, err && err.message ? err.message : 'The action could not be completed.');
        if (spinner) spinner.style.display = 'none';
        Array.prototype.forEach.call(controls, function (x) { x.disabled = false; });
        button.removeAttribute('data-submitted');
        Array.prototype.forEach.call(checks, updateOfficerButton);
      });
    return false;
  };
  document.addEventListener('change', function (event) {
    var input = event.target;
    if (input && input.matches && input.matches('.officer-action-options input[type="checkbox"]')) {
      var row = decisionRow(input);
      if (input.checked && row) Array.prototype.forEach.call(row.querySelectorAll('.officer-action-options input[type="checkbox"]'), function (other) { if (other !== input) other.checked = false; });
      updateOfficerButton(input);
    }
  });
  document.addEventListener('input', function (event) { if (event.target.matches && event.target.matches('.officer-action-remark')) updateOfficerButton(event.target); });
  // "Add remark" (single Approve rows): reveal the optional remark box.
  document.addEventListener('click', function (event) {
    var toggle = event.target && event.target.closest ? event.target.closest('.officer-add-remark') : null;
    if (!toggle) return;
    var wrap = officerRow(toggle) && officerRow(toggle).querySelector('.officer-remark-wrap');
    if (wrap) { wrap.style.display = ''; toggle.style.display = 'none'; var box = wrap.querySelector('textarea'); if (box) box.focus(); }
  });
  Array.prototype.forEach.call(document.querySelectorAll('.officer-action-options input[type="checkbox"]'), updateOfficerButton);
  window.SMCOfficerSubmit = submitOfficerAction;

  // Reveal the bound business-type list immediately before the service AutoPostBack begins.
  var serviceChoice = document.querySelector('[data-smc-service-choice="1"]');
  var businessPanel = document.getElementById('pnlBusiness');
  var updateBusinessPanel = function () {
    if (!serviceChoice || !businessPanel) return;
    businessPanel.style.display = serviceChoice.value === 'VariousBusinessSpace' ? '' : 'none';
  };
  if (serviceChoice && businessPanel) {
    updateBusinessPanel();
    document.addEventListener('change', function (event) {
      if (event.target === serviceChoice) updateBusinessPanel();
    }, true);
  }

  // double-submit guard: disable the clicked submit button after validation passes
  document.addEventListener('submit', function (e) {
    var f = e.target; if (!f || f.nodeName !== 'FORM') return;
    if (typeof Page_IsValid !== 'undefined' && Page_IsValid === false) return;
    var b = f.querySelector('[data-once]'); if (b) { setTimeout(function () { b.disabled = true; }, 0); }
  });

  // live pricing preview (the server recomputes everything again on submit; this is display only)
  var box = document.getElementById('smcPreview'), annual = document.getElementById('pnlAnnualSummary');
  if (box || annual) {
    var q = function (k) { return document.querySelector('[data-smc="' + k + '"]'); };
    var timer = null;
    var requestVersion = 0;
    var fmt = function (n) { return n == null ? '—' : Number(n).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); };
    var dimension = function (k) {
      var value = (q(k) || {}).value || '', raw = value.trim();
      return /^[+-]?(?:\d+(?:\.\d*)?|\.\d+)$/.test(raw) ? Number(raw) : null;
    };
    var fields = ['एकूण दिवस', 'क्षेत्रफळ (चौ.फूट)', 'दर (प्रतिदिन)', 'एकूण रक्कम'];
    var render = function (values, note) {
      box.textContent = '';
      fields.forEach(function (label, i) {
        var card = document.createElement('div'); card.className = 'price-box' + (i === 3 ? ' price-total' : '');
        var title = document.createElement('span'); title.textContent = label;
        var value = document.createElement('strong'); value.textContent = values[i] == null ? '—' : values[i];
        card.appendChild(title); card.appendChild(value); box.appendChild(card);
      });
      if (note) { var msg = document.createElement('div'); msg.className = 'price-note'; msg.textContent = note; box.appendChild(msg); }
    };
    var requestBody = function () {
      var s = q('service'), a = q('start'), b = q('end');
      if (s && s.value !== 'VariousBusinessSpace' && a && b && a.value) {
        var parts = a.value.split('-').map(Number), year = parts[0] + 1, monthNumber = parts[1];
        var lastDay = new Date(Date.UTC(year, monthNumber, 0)).getUTCDate(), dayNumber = Math.min(parts[2], lastDay);
        var month = String(monthNumber).padStart(2, '0'), day = String(dayNumber).padStart(2, '0');
        b.value = year + '-' + month + '-' + day;
      }
      return { serviceType: s ? s.value : '', startDate: a ? a.value : '', endDate: b ? b.value : '', lengthFt: dimension('len'), widthFt: dimension('wid') };
    };
    var formatDate = function (value) {
      if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return '—';
      var p = value.split('-'); return p[2] + '/' + p[1] + '/' + p[0];
    };
    var setAnnualDates = function (start, end) {
      var a = document.getElementById('annualStart'), b = document.getElementById('annualEnd');
      if (a) a.textContent = formatDate(start);
      if (b) b.textContent = formatDate(end);
    };
    var isAreaService = function () { var s = q('service'); return !!s && s.value === 'VariousBusinessSpace'; };
    var keyFor = function (d) { return JSON.stringify(d); };
    var run = function () {
      var body = requestBody(), key = keyFor(body), version = ++requestVersion;
      if (!body.serviceType || !body.startDate || !body.endDate) {
        var emptyArea = q('area'), emptyRate = q('rate'); if (emptyArea) emptyArea.value = ''; if (emptyRate) emptyRate.value = '';
        setAnnualDates(body.startDate, body.endDate);
        if (box) render([], 'सेवा व दिनांक निवडा.'); return;
      }
      if (box) render([], 'गणना सुरू आहे…');
      fetch((window.SMC_BASE || '') + '/api/v1/pricing/preview', { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json', 'X-SMC-Request': '1' }, body: JSON.stringify(body) })
        .then(function (r) { return r.json(); })
        .then(function (d) {
          if (version !== requestVersion || key !== keyFor(requestBody())) return;
          setAnnualDates(body.startDate, d.effectiveEndDate || body.endDate);
          var endControl = q('end'); if (endControl && !isAreaService()) endControl.value = d.effectiveEndDate || body.endDate;
          if (annual) {
            var amountControl = document.getElementById('annualAmount');
            if (amountControl) amountControl.textContent = d.finalAmount == null ? '—' : '₹ ' + fmt(d.finalAmount);
          }
          if (!box) return;
          var areaField = q('area'), rateField = q('rate');
          if (d.error) { if (areaField) areaField.value = ''; if (rateField) rateField.value = ''; render([], d.error); return; }
          if (areaField) areaField.value = fmt(d.areaSqFt);
          if (rateField) rateField.value = d.rate == null ? '' : fmt(d.rate);
          render([String(d.totalDays), fmt(d.areaSqFt), d.rate == null ? 'कार्यालयाकडून निश्चित केला जाईल' : '₹ ' + fmt(d.rate) + (d.unit ? ' (' + d.unit + ')' : ''), d.finalAmount == null ? '—' : '₹ ' + fmt(d.finalAmount)]);
        }).catch(function () { if (version === requestVersion && key === keyFor(requestBody()) && box) render([], 'गणना सध्या उपलब्ध नाही.'); });
    };
    var sched = function () {
      requestVersion++; var area = q('area'), rate = q('rate'); if (area) area.value = ''; if (rate) rate.value = '';
      if (box) render([], 'गणना सुरू आहे…'); clearTimeout(timer); timer = setTimeout(run, 180);
    };
    ['service', 'start', 'end', 'len', 'wid'].forEach(function (k) { var el = q(k); if (el) { el.addEventListener('change', sched); el.addEventListener('input', sched); } });
    sched();
  }

  // Auto-download after submission in an iframe so a PDF error leaves the
  // saved-application confirmation and its manual retry link available.
  var auto = document.getElementById('autoDownload');
  if (auto && auto.getAttribute('data-href')) {
    var appId = auto.getAttribute('data-application-id');
    if (appId) {
      var guardKey = 'smc:application-pdf:auto:v1:' + appId, alreadyDownloaded = false;
      try { alreadyDownloaded = window.localStorage.getItem(guardKey) === '1'; } catch (_) { alreadyDownloaded = true; }
      if (!alreadyDownloaded) {
        try { window.localStorage.setItem(guardKey, '1'); } catch (_) { alreadyDownloaded = true; }
        if (!alreadyDownloaded) window.setTimeout(function () { var frame = document.createElement('iframe'); frame.setAttribute('aria-hidden', 'true'); frame.tabIndex = -1; frame.style.display = 'none'; frame.src = auto.getAttribute('data-href'); document.body.appendChild(frame); }, 600);
      }
    }
  }
})();
