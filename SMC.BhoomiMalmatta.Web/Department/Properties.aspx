<%@ Page Title="मालमत्ता" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Properties.aspx.cs" Inherits="SMC.BhoomiMalmatta.Web.Department.PropertiesPage" EnableViewState="false" %>
<asp:Content ID="Content1" ContentPlaceHolderID="cphMain" runat="server">
<asp:PlaceHolder ID="phList" runat="server">
  <div class="prop-page" id="propPage">
    <h1 class="prop-title">मालमत्ता</h1>
    <p class="prop-sub">Major/Mini गाळे, Land Fee, समाज मंदिर, अभ्यासिका, 256 गाळे, TP-3/23, अधिकृत खोके, इतर मनपा मालमत्ता</p>

    <div class="prop-warn" id="propWarn" role="alert" hidden></div>
    <div class="prop-notice" id="propNotice" role="status" aria-live="polite" hidden></div>

    <div class="prop-card prop-filters" role="search">
      <label class="prop-sr" for="propSearch">शोधा</label>
      <input type="search" id="propSearch" class="prop-in prop-search" placeholder="शोधा (नाव, क्रमांक, इ.)" maxlength="100" autocomplete="off" />
      <label class="prop-sr" for="propFilterCategory">मालमत्ता प्रकार</label>
      <select id="propFilterCategory" class="prop-in"><option value="">मालमत्ता प्रकार - सर्व</option></select>
      <label class="prop-sr" for="propFilterStatus">स्थिती</label>
      <select id="propFilterStatus" class="prop-in"><option value="">स्थिती - सर्व</option></select>
      <button type="button" id="propNew" class="prop-btn prop-btn-primary">+ नवीन नोंद</button>
    </div>

    <div class="prop-card prop-table-card">
      <div class="prop-tscroll">
        <table class="prop-table" id="propTable" aria-describedby="propState">
          <thead>
            <tr>
              <th scope="col">मालमत्ता क्र.</th>
              <th scope="col">नाव</th>
              <th scope="col">मालमत्ता प्रकार</th>
              <th scope="col">प्रभाग</th>
              <th scope="col">वार्षिक मागणी</th>
              <th scope="col">स्थिती</th>
              <th scope="col">क्रिया</th>
            </tr>
          </thead>
          <tbody id="propRows" aria-live="polite" aria-busy="false"></tbody>
        </table>
      </div>
      <p class="prop-state" id="propState" role="status">माहिती लोड होत आहे…</p>
      <nav class="prop-pager" id="propPager" aria-label="पृष्ठे" hidden>
        <button type="button" class="prop-btn prop-btn-o" id="propPrev">← मागील</button>
        <span id="propPageInfo"></span>
        <button type="button" class="prop-btn prop-btn-o" id="propNext">पुढील →</button>
      </nav>
    </div>
  </div>

  <div class="prop-overlay" id="propOverlay" hidden>
    <div class="prop-dialog" id="propDialog" role="dialog" aria-modal="true" aria-labelledby="propDialogTitle" tabindex="-1">
      <div class="prop-dialog-head">
        <h2 id="propDialogTitle">नवीन नोंद</h2>
        <button type="button" class="prop-close" id="propClose" aria-label="बंद करा">&times;</button>
      </div>
      <div class="prop-tabs" role="tablist"><span class="prop-tab" role="tab" aria-selected="true" id="propTabInfo">माहिती</span></div>
      <div class="prop-form" id="propForm" role="tabpanel" aria-labelledby="propTabInfo">
        <div class="prop-form-error" id="propFormError" role="alert" hidden></div>
        <div class="prop-grid">
          <div class="prop-field"><label for="pf_Category">मालमत्ता प्रकार <span class="prop-req" aria-hidden="true">*</span></label><select id="pf_Category" name="Category" class="prop-in" aria-required="true"></select><span class="prop-err" id="pe_Category"></span></div>
          <div class="prop-field"><label for="pf_PropertyCode">मालमत्ता क्रमांक <span class="prop-req" aria-hidden="true">*</span></label><input type="text" id="pf_PropertyCode" name="PropertyCode" class="prop-in" maxlength="50" aria-required="true" autocomplete="off" /><span class="prop-err" id="pe_PropertyCode"></span></div>
          <div class="prop-field"><label for="pf_Name">मालमत्तेचे नाव <span class="prop-req" aria-hidden="true">*</span></label><input type="text" id="pf_Name" name="Name" class="prop-in" maxlength="250" aria-required="true" /><span class="prop-err" id="pe_Name"></span></div>
          <div class="prop-field"><label for="pf_Ward">प्रभाग</label><select id="pf_Ward" name="Ward" class="prop-in" data-free="pf_WardNew"></select><input type="text" id="pf_WardNew" class="prop-in prop-free" maxlength="50" aria-label="नवीन प्रभाग" hidden /><span class="prop-err" id="pe_Ward"></span></div>
          <div class="prop-field"><label for="pf_Zone">झोन</label><select id="pf_Zone" name="Zone" class="prop-in" data-free="pf_ZoneNew"></select><input type="text" id="pf_ZoneNew" class="prop-in prop-free" maxlength="50" aria-label="नवीन झोन" hidden /><span class="prop-err" id="pe_Zone"></span></div>
          <div class="prop-field prop-empty" aria-hidden="true"></div>
          <div class="prop-field prop-full"><label for="pf_Address">पत्ता</label><input type="text" id="pf_Address" name="Address" class="prop-in" maxlength="500" /><span class="prop-err" id="pe_Address"></span></div>
          <div class="prop-field"><label for="pf_AreaSqFt">क्षेत्रफळ (चौ.फूट)</label><input type="text" id="pf_AreaSqFt" name="AreaSqFt" class="prop-in" inputmode="decimal" /><span class="prop-err" id="pe_AreaSqFt"></span></div>
          <div class="prop-field"><label for="pf_MonthlyRent">मासिक भाडे (₹)</label><input type="text" id="pf_MonthlyRent" name="MonthlyRent" class="prop-in" inputmode="decimal" /><span class="prop-err" id="pe_MonthlyRent"></span></div>
          <div class="prop-field"><label for="pf_AnnualDemand">वार्षिक मागणी (₹)</label><input type="text" id="pf_AnnualDemand" name="AnnualDemand" class="prop-in" inputmode="decimal" /><span class="prop-err" id="pe_AnnualDemand"></span></div>
          <div class="prop-field"><label for="pf_SurveyNumber">सर्वे/गट क्रमांक</label><input type="text" id="pf_SurveyNumber" name="SurveyNumber" class="prop-in" maxlength="50" /><span class="prop-err" id="pe_SurveyNumber"></span></div>
          <div class="prop-field"><label for="pf_TpNumber">TP क्रमांक</label><input type="text" id="pf_TpNumber" name="TpNumber" class="prop-in" maxlength="50" /><span class="prop-err" id="pe_TpNumber"></span></div>
          <div class="prop-field"><label for="pf_Status">स्थिती <span class="prop-req" aria-hidden="true">*</span></label><select id="pf_Status" name="Status" class="prop-in" aria-required="true"></select><span class="prop-err" id="pe_Status"></span></div>
          <div class="prop-field prop-full"><label for="pf_CurrentOccupant">सध्याचा धारक/भाडेकरू</label><input type="text" id="pf_CurrentOccupant" name="CurrentOccupant" class="prop-in" maxlength="200" /><span class="prop-err" id="pe_CurrentOccupant"></span></div>
          <div class="prop-field prop-full"><label for="pf_Shera">शेरा</label><textarea id="pf_Shera" name="Shera" class="prop-in" rows="4" maxlength="2000"></textarea><span class="prop-err" id="pe_Shera"></span></div>
        </div>
        <div class="prop-extras" id="propExtras" hidden></div>
      </div>
      <div class="prop-dialog-foot">
        <button type="button" class="prop-btn prop-btn-o" id="propCancel">बंद करा</button>
        <button type="button" class="prop-btn prop-btn-primary" id="propSave">जतन करा (Save)</button>
      </div>
    </div>
  </div>

  <div class="prop-overlay" id="propConfirm" hidden>
    <div class="prop-dialog prop-dialog-sm" role="alertdialog" aria-modal="true" aria-labelledby="propConfirmTitle" aria-describedby="propConfirmText" tabindex="-1">
      <div class="prop-dialog-head"><h2 id="propConfirmTitle">ही मालमत्ता हटवायची का?</h2></div>
      <div class="prop-form"><p id="propConfirmText"></p><div class="prop-form-error" id="propConfirmError" role="alert" hidden></div></div>
      <div class="prop-dialog-foot">
        <button type="button" class="prop-btn prop-btn-o" id="propConfirmNo">नाही</button>
        <button type="button" class="prop-btn prop-btn-danger" id="propConfirmYes">हटवा</button>
      </div>
    </div>
  </div>
  <script src="<%= ResolveUrl("~/Scripts/properties.js") %>"></script>
</asp:PlaceHolder>
<asp:PlaceHolder ID="phDocuments" runat="server" Visible="false">
  <div class="prop-page">
    <p><a href="<%= ResolveUrl("~/Department/Properties.aspx") %>">← मालमत्ता यादी</a></p>
    <h1 class="prop-title"><asp:Literal ID="litDocTitle" runat="server" /></h1>
    <asp:PlaceHolder ID="phDocHost" runat="server" />
  </div>
</asp:PlaceHolder>
</asp:Content>
