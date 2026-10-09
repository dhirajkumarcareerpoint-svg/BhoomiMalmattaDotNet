<%@ Page Title="अधिकारी मागणी अर्ज" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Applications.aspx.cs" Inherits="SMC.BhoomiMalmatta.Web.Department.ApplicationsPage" %>
<%@ Register Src="~/Department/Controls/OfficerKpiCards.ascx" TagPrefix="ui" TagName="OfficerKpiCards" %><%@ Register Src="~/Department/Controls/OfficerFilterBar.ascx" TagPrefix="ui" TagName="OfficerFilterBar" %><%@ Register Src="~/Department/Controls/OfficerPager.ascx" TagPrefix="ui" TagName="OfficerPager" %><%@ Register Src="~/Department/Controls/OfficerPendingGrid.ascx" TagPrefix="ui" TagName="OfficerPendingGrid" %>
<asp:Content ID="Content1" ContentPlaceHolderID="cphMain" runat="server"><div class="officer-page"><div id="officerNotice" class="notice" role="status" aria-live="polite" style="display:none"></div><div class="officer-page-head"><div><div class="muted">मागणी अर्ज</div><h2><asp:Literal ID="litOfficerTitle" runat="server" /></h2><p>प्राप्त अर्जांची पडताळणी, स्थिती आणि कार्यवाही</p></div><div class="officer-user-actions"><div class="officer-user-chip"><span class="role-badge"><asp:Literal ID="litRoleBadge" runat="server" /></span><strong><asp:Literal ID="litOfficerName" runat="server" /></strong></div><asp:Button ID="btnRefresh" runat="server" CssClass="btn o" Text="Refresh" OnClick="btnRefresh_Click" /><asp:Button ID="btnLogout" runat="server" CssClass="btn" Text="लॉगआउट" CausesValidation="false" OnClick="btnLogout_Click" /></div></div><div class="officer-kpis"><ui:OfficerKpiCards ID="officerKpis" runat="server" /></div><ui:OfficerFilterBar ID="officerFilters" runat="server" /><div class="card mt"><h3>अर्ज <asp:Literal ID="litApplicationCount" runat="server" ClientIDMode="Static" /> अर्ज</h3><ui:OfficerPendingGrid ID="pendingGrid" runat="server" /><ui:OfficerPager ID="pendingPager" runat="server" /></div></div>
<div id="officerDetailModal" class="officer-detail-backdrop" hidden role="dialog" aria-modal="true" aria-labelledby="officerDetailTitle" tabindex="-1">
  <section class="officer-detail-dialog">
    <header class="officer-detail-header"><h2 id="officerDetailTitle">अर्ज तपशील</h2><button type="button" class="officer-modal-close" aria-label="बंद करा">×</button></header>
    <div id="officerDetailBody" class="officer-detail-body" tabindex="0"><p class="officer-detail-loading">तपशील लोड होत आहेत…</p></div>
    <footer class="officer-detail-footer"><button type="button" class="btn officer-modal-ok">ठीक आहे</button></footer>
  </section>
</div>
<script>
(function () {
  'use strict';
  var modal = document.getElementById('officerDetailModal');
  if (!modal) return;
  var body = document.getElementById('officerDetailBody'), title = document.getElementById('officerDetailTitle');
  var previousFocus = null, busy = false;
  var failText = 'तपशील मिळाला नाही. कृपया पुन्हा प्रयत्न करा.';
  function node(tag, cls, text) { var n = document.createElement(tag); if (cls) n.className = cls; if (text != null) n.textContent = text; return n; }
  function field(parent, label, value, cls) { var item = node('div', 'officer-detail-field' + (cls ? ' ' + cls : '')); item.appendChild(node('span', 'officer-detail-label', label)); item.appendChild(node('strong', 'officer-detail-value', value == null || value === '' ? '-' : String(value))); parent.appendChild(item); }
  function section(parent, heading) { var s = node('section', 'officer-detail-section'); s.appendChild(node('h3', '', heading)); parent.appendChild(s); return s; }
  function link(parent, href, label) { if (!href) return; var a = node('a', 'btn o officer-doc-link', label); a.href = href; a.target = '_blank'; a.rel = 'noopener'; parent.appendChild(a); }
  function render(data) {
    title.textContent = 'अर्ज तपशील — ' + (data.applicationNumber || '-'); body.textContent = '';
    var head = node('div', 'officer-detail-summary');
    field(head, 'अर्जदार', data.applicantName); field(head, 'मोबाईल', data.mobile); field(head, 'ई-मेल', data.email);
    var stage = field(head, 'स्थिती', data.stageLabel); stage.querySelector('.officer-detail-value').classList.add('officer-detail-pill');
    field(head, 'देयक स्थिती', data.paymentStatusLabel); field(head, 'शुल्क', data.amountLabel);
    body.appendChild(head);
    var details = section(body, 'जागेच्या मागणीचा तपशील'), grid = node('div', 'officer-detail-grid');
    (data.fields || []).forEach(function (x) { field(grid, x.label, x.value, x.label === 'पत्ता' ? 'span-wide' : ''); }); details.appendChild(grid);
    var photo = section(body, 'प्रत्यक्ष जागेचा फोटो');
    if (data.sitePhotoUrl) { var img = node('img', 'officer-site-photo'); img.src = data.sitePhotoUrl; img.alt = 'प्रत्यक्ष जागेचा फोटो'; photo.appendChild(img); }
    else photo.appendChild(node('p', 'muted', data.sitePhotoMessage || 'प्रत्यक्ष जागेचा फोटो उपलब्ध नाही.'));
    if (data.canUploadDocuments) photo.appendChild(node('p', 'muted', 'दस्तऐवज अपलोड सुविधा उपलब्ध आहे.'));
    var docs = section(body, 'कागदपत्रे'), docList = node('div', 'officer-detail-documents');
    (data.documents || []).forEach(function (d) {
      var card = node('article', 'officer-document-card'); card.appendChild(node('strong', '', d.type || 'कागदपत्र')); card.appendChild(node('div', 'officer-document-name', d.fileName || '-'));
      field(card, 'पडताळणी स्थिती', d.verificationStatus || 'Unchecked'); field(card, 'विनंती शेरा / स्थिती', d.requestRemark || '- / ' + (d.verificationStatus || 'Unchecked'));
      var actions = node('div', 'officer-document-actions'); link(actions, d.previewUrl, 'Preview'); link(actions, d.downloadUrl, 'Download'); card.appendChild(actions);
      if (data.canVerifyDocuments) card.appendChild(node('div', 'muted', 'पडताळणी अधिकार उपलब्ध'));
      docList.appendChild(card);
    });
    if (!docList.children.length) docList.appendChild(node('p', 'muted', 'कागदपत्रे उपलब्ध नाहीत.'));
    docs.appendChild(docList);
    if (data.showPayment) {
      var payment = section(body, 'पेमेंट'), paymentGrid = node('div', 'officer-detail-grid');
      field(paymentGrid, 'रक्कम', data.amountLabel); field(paymentGrid, 'स्थिती', data.paymentStatusLabel); field(paymentGrid, 'UTR / संदर्भ', data.payment && data.payment.reference);
      field(paymentGrid, 'भरणा दिनांक', data.payment && data.payment.date); field(paymentGrid, 'पुरावा', data.payment && data.payment.proofFileName); field(paymentGrid, 'OS पडताळणी', data.payment && data.payment.verificationState);
      field(paymentGrid, 'पडताळणी अधिकारी', data.payment && data.payment.verifier);
      payment.appendChild(paymentGrid);
      var payActions = node('div', 'officer-document-actions'); link(payActions, data.payment && data.payment.proofPreviewUrl, (data.payment && data.payment.proofPreviewLabel) || 'Preview'); link(payActions, data.payment && data.payment.proofDownloadUrl, 'Download'); payment.appendChild(payActions);
    }
    if (data.certificateUrl) { var cert = section(body, data.certificateLabel || '-'), certActions = node('div', 'officer-document-actions'); link(certActions, data.certificateUrl, data.certificateLabel); cert.appendChild(certActions); }
    var history = section(body, 'कार्यवाही इतिहास'), timeline = node('ol', 'officer-detail-timeline');
    (data.history || []).forEach(function (h) {
      var li = node('li', 'officer-history-item'), row = node('div', 'officer-history-title'); row.appendChild(node('strong', '', h.action || '-'));
      row.appendChild(node('time', '', h.atUtc ? h.dateLabel || '-' : '-')); li.appendChild(row);
      li.appendChild(node('div', 'officer-history-by', (h.role || '-') + ' — ' + (h.officerName || '-')));
      if (h.remark) li.appendChild(node('p', 'officer-history-remark', h.remark)); timeline.appendChild(li);
    });
    if (!timeline.children.length) timeline.appendChild(node('li', 'muted', 'इतिहास उपलब्ध नाही.'));
    history.appendChild(timeline);
    if (data.allowedActionLabels && data.allowedActionLabels.length) { var actions = section(body, 'सध्याच्या उपलब्ध कृती'); var list = node('ul', 'officer-readonly-actions'); data.allowedActionLabels.forEach(function (x) { list.appendChild(node('li', '', x)); }); actions.appendChild(list); }
  }
  function close() { if (modal.hidden) return; modal.hidden = true; document.body.classList.remove('officer-modal-open'); busy = false; if (previousFocus) previousFocus.focus(); }
  function open(linkEl) {
    if (busy) return; busy = true; previousFocus = linkEl; modal.hidden = false; document.body.classList.add('officer-modal-open');
    body.textContent = ''; body.appendChild(node('p', 'officer-detail-loading', 'तपशील लोड होत आहेत…')); title.textContent = 'अर्ज तपशील';
    var id = linkEl.getAttribute('data-officer-detail-id'); modal.focus();
    fetch((window.SMC_BASE || '') + '/api/v1/applications/' + encodeURIComponent(id) + '/officer-view', { method:'GET', credentials:'same-origin', headers:{'Accept':'application/json'} })
      .then(function (r) { if (!r.ok) throw new Error(failText); return r.json(); })
      .then(render).catch(function () { body.textContent = ''; body.appendChild(node('p', 'officer-detail-error', failText)); })
      .then(function () { busy = false; var c = modal.querySelector('.officer-modal-close'); if (c) c.focus(); });
  }
  document.addEventListener('click', function (e) { var a = e.target.closest && e.target.closest('[data-officer-detail-id]'); if (a) { e.preventDefault(); open(a); } });
  modal.addEventListener('click', function (e) { if (e.target === modal || e.target.closest('.officer-modal-close') || e.target.closest('.officer-modal-ok')) close(); });
  document.addEventListener('keydown', function (e) {
    if (modal.hidden) return;
    if (e.key === 'Escape') { e.preventDefault(); close(); return; }
    if (e.key !== 'Tab') return;
    var items = Array.prototype.slice.call(modal.querySelectorAll('button,a[href],input,textarea,select,[tabindex]:not([tabindex="-1"])')).filter(function (x) { return !x.disabled && x.offsetParent !== null; });
    if (!items.length) { e.preventDefault(); modal.focus(); return; }
    var first = items[0], last = items[items.length - 1];
    if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); } else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
  });
}());
</script>
</asp:Content>

