<%@ Page Title="मागणी अर्ज" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Apply.aspx.cs" Inherits="SMC.BhoomiMalmatta.Web.ApplyPage" %>
<asp:Content ID="Content1" ContentPlaceHolderID="cphMain" runat="server">
<asp:Panel ID="pnlForm" runat="server">
  <div class="card">
    <h2>मागणी अर्ज</h2>
    <div class="bar" aria-hidden="true"><i id="barFill" runat="server" style="width:33.33%"></i></div>
    <div class="steps stepbar">
      <div class="st on" id="st1" runat="server"><span>1</span>सेवेची माहिती</div>
      <div class="st" id="st2" runat="server"><span>2</span>पत्ता व जागा</div>
      <div class="st" id="st3" runat="server"><span>3</span>कागदपत्रे व सादरीकरण</div>
    </div>
    <asp:Label ID="lblErrors" runat="server" CssClass="notice bad" Visible="false" role="alert" />

    <asp:MultiView ID="mv" runat="server" ActiveViewIndex="0">
      <asp:View ID="v1" runat="server">
        <h3>१. सेवेची माहिती</h3>
        <asp:Label ID="lblServiceCaption" runat="server" AssociatedControlID="ddlService" CssClass="lb" Text="सेवा *" />
        <asp:DropDownList ID="ddlService" runat="server" CssClass="in" data-smc="service" data-smc-service-choice="1" AutoPostBack="true" CausesValidation="false" OnSelectedIndexChanged="ddlService_SelectedIndexChanged" />
        <asp:RequiredFieldValidator ID="rfvService" runat="server" ControlToValidate="ddlService" InitialValue="" ValidationGroup="g1" CssClass="valmsg" Display="Dynamic" ErrorMessage="कृपया सेवा निवडा." />
        <asp:Panel ID="pnlBusiness" runat="server" ClientIDMode="Static">
          <asp:Label ID="lblBusinessCaption" runat="server" AssociatedControlID="ddlBusiness" CssClass="lb" Text="व्यवसायाचा प्रकार *" />
          <asp:DropDownList ID="ddlBusiness" runat="server" CssClass="in" AutoPostBack="true" CausesValidation="false" OnSelectedIndexChanged="ddlBusiness_SelectedIndexChanged" />
          <asp:RequiredFieldValidator ID="rfvBusiness" runat="server" ControlToValidate="ddlBusiness" InitialValue="" ValidationGroup="g1" CssClass="valmsg" Display="Dynamic" ErrorMessage="कृपया व्यवसायाचा प्रकार निवडा." />
          <asp:Panel ID="pnlOtherBusiness" runat="server" Visible="false">
            <asp:Label ID="lblOtherCaption" runat="server" AssociatedControlID="txtOtherBusiness" CssClass="lb" Text="इतर व्यवसायाचा तपशील *" />
            <asp:TextBox ID="txtOtherBusiness" runat="server" CssClass="in" MaxLength="200" />
          </asp:Panel>
        </asp:Panel>
      </asp:View>

      <asp:View ID="v2" runat="server">
        <h3>२. पत्ता व जागा</h3>
        <%-- Apply.CollectApplicantDetails: rendered (and validated) only when the setting is true; otherwise step 2 starts with "कायमचा पत्ता". --%>
        <asp:PlaceHolder ID="phApplicant" runat="server" Visible="false">
        <fieldset class="applicant-block">
          <legend>अर्जदार</legend>
          <div class="two">
            <div>
              <asp:Label ID="lblNameCaption" runat="server" AssociatedControlID="txtName" CssClass="lb" Text="अर्जदाराचे पूर्ण नाव *" />
              <asp:TextBox ID="txtName" runat="server" CssClass="in" MaxLength="200" />
              <asp:RequiredFieldValidator ID="rfvName" runat="server" ControlToValidate="txtName" ValidationGroup="g2" CssClass="valmsg" Display="Dynamic" ErrorMessage="अर्जदाराचे नाव आवश्यक आहे." />
            </div>
            <div>
              <asp:Label ID="lblMobileCaption" runat="server" AssociatedControlID="txtMobile" CssClass="lb" Text="मोबाईल क्रमांक *" />
              <asp:TextBox ID="txtMobile" runat="server" CssClass="in" MaxLength="10" TextMode="Phone" />
              <asp:RegularExpressionValidator ID="revMobile" runat="server" ControlToValidate="txtMobile" ValidationExpression="^\s*[6-9][0-9]{9}\s*$" ValidationGroup="g2" CssClass="valmsg" Display="Dynamic" ErrorMessage="मोबाइल क्रमांक १० अंकी आणि ६, ७, ८ किंवा ९ ने सुरू असावा." />
              <asp:RequiredFieldValidator ID="rfvMobile" runat="server" ControlToValidate="txtMobile" ValidationGroup="g2" CssClass="valmsg" Display="Dynamic" ErrorMessage="मोबाईल क्रमांक आवश्यक आहे." />
            </div>
            <div>
              <asp:Label ID="lblEmailCaption" runat="server" AssociatedControlID="txtEmail" CssClass="lb" Text="ई-मेल आयडी" />
              <asp:TextBox ID="txtEmail" runat="server" CssClass="in" MaxLength="200" TextMode="Email" />
              <asp:RegularExpressionValidator ID="revEmail" runat="server" ControlToValidate="txtEmail" ValidationExpression="^[^\s@]+@[^\s@]+\.[^\s@]+$" ValidationGroup="g2" CssClass="valmsg" Display="Dynamic" ErrorMessage="कृपया वैध ई-मेल पत्ता भरा." />
            </div>
          </div>
        </fieldset>
        </asp:PlaceHolder>
        <asp:Label ID="lblPermCaption" runat="server" AssociatedControlID="txtPermAddr" CssClass="lb" Text="कायमचा पत्ता *" />
        <asp:TextBox ID="txtPermAddr" runat="server" CssClass="in" MaxLength="1000" />
        <asp:RequiredFieldValidator ID="rfvPerm" runat="server" ControlToValidate="txtPermAddr" ValidationGroup="g2" CssClass="valmsg" Display="Dynamic" ErrorMessage="कायमचा पत्ता आवश्यक आहे." />
        <label class="chk"><asp:CheckBox ID="chkSame" runat="server" Checked="true" CausesValidation="false" /> कायमचा व पत्रव्यवहाराचा पत्ता समान</label>
        <asp:Label ID="lblCorrCaption" runat="server" AssociatedControlID="txtCorrAddr" CssClass="lb" Text="पत्रव्यवहाराचा पत्ता *" />
        <asp:TextBox ID="txtCorrAddr" runat="server" CssClass="in" MaxLength="1000" />
        <asp:RequiredFieldValidator ID="rfvCorr" runat="server" ControlToValidate="txtCorrAddr" ValidationGroup="g2" CssClass="valmsg" Display="Dynamic" ErrorMessage="पत्रव्यवहाराचा पत्ता आवश्यक आहे." />
        <script>
          (function () {
            function wire() {
              var chk = document.querySelector('input[id$="chkSame"]');
              var perm = document.querySelector('input[id$="txtPermAddr"]');
              var corr = document.querySelector('input[id$="txtCorrAddr"]');
              if (!chk || !perm || !corr) return;
              function sync() { if (chk.checked) { corr.value = perm.value; corr.setAttribute('readonly', 'readonly'); } else { corr.removeAttribute('readonly'); } }
              chk.addEventListener('change', sync);
              perm.addEventListener('input', function () { if (chk.checked) corr.value = perm.value; });
              sync();
            }
            if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', wire); else wire();
          })();
        </script>
        <div class="two">
          <div><asp:Label ID="lblStateCaption" runat="server" AssociatedControlID="txtState" CssClass="lb" Text="राज्य *" /><asp:TextBox ID="txtState" runat="server" CssClass="in" ReadOnly="true" Text="महाराष्ट्र" /></div>
          <div><asp:Label ID="lblDistrictCaption" runat="server" AssociatedControlID="txtDistrict" CssClass="lb" Text="जिल्हा *" /><asp:TextBox ID="txtDistrict" runat="server" CssClass="in" ReadOnly="true" Text="सोलापूर" /></div>
          <div><asp:Label ID="lblCityCaption" runat="server" AssociatedControlID="txtCity" CssClass="lb" Text="शहर *" /><asp:TextBox ID="txtCity" runat="server" CssClass="in" MaxLength="100" ReadOnly="true" Text="सोलापूर" /></div>
          <div>
            <asp:Label ID="lblTalukaCaption" runat="server" AssociatedControlID="ddlTaluka" CssClass="lb" Text="तालुका *" />
            <asp:DropDownList ID="ddlTaluka" runat="server" CssClass="in" />
            <asp:RequiredFieldValidator ID="rfvTaluka" runat="server" ControlToValidate="ddlTaluka" InitialValue="" ValidationGroup="g2" CssClass="valmsg" Display="Dynamic" ErrorMessage="कृपया तालुका निवडा." />
          </div>
          <div>
            <asp:Label ID="lblPinCaption" runat="server" AssociatedControlID="txtPin" CssClass="lb" Text="पिनकोड *" />
            <asp:TextBox ID="txtPin" runat="server" CssClass="in" MaxLength="6" />
            <asp:RegularExpressionValidator ID="revPin" runat="server" ControlToValidate="txtPin" ValidationExpression="^\s*\d{6}\s*$" ValidationGroup="g2" CssClass="valmsg" Display="Dynamic" ErrorMessage="कृपया वैध ६ अंकी पिनकोड भरा." />
          </div>
          <div>
            <asp:Label ID="lblZoneCaption" runat="server" AssociatedControlID="ddlZone" CssClass="lb" Text="झोन *" />
            <asp:DropDownList ID="ddlZone" runat="server" CssClass="in" />
            <asp:RequiredFieldValidator ID="rfvZone" runat="server" ControlToValidate="ddlZone" InitialValue="" ValidationGroup="g2" CssClass="valmsg" Display="Dynamic" ErrorMessage="कृपया झोन निवडा (१ ते ८)." />
          </div>
          <div>
            <asp:Label ID="lblLocationCaption" runat="server" AssociatedControlID="txtLocation" CssClass="lb" Text="जागेचे ठिकाण *" />
            <asp:TextBox ID="txtLocation" runat="server" CssClass="in" TextMode="MultiLine" Rows="2" MaxLength="500" />
            <asp:RequiredFieldValidator ID="rfvLocation" runat="server" ControlToValidate="txtLocation" ValidationGroup="g2" CssClass="valmsg" Display="Dynamic" ErrorMessage="जागेचे ठिकाण आवश्यक आहे." />
          </div>
          <asp:Panel ID="pnlAreaPricing" runat="server" CssClass="two area-pricing-fields">
            <div><asp:Label ID="lblLenCaption" runat="server" AssociatedControlID="txtLength" CssClass="lb" Text="Length (ft) *" /><asp:TextBox ID="txtLength" runat="server" CssClass="in" data-smc="len" inputmode="decimal" /></div>
            <div><asp:Label ID="lblWidCaption" runat="server" AssociatedControlID="txtWidth" CssClass="lb" Text="Width (ft) *" /><asp:TextBox ID="txtWidth" runat="server" CssClass="in" data-smc="wid" inputmode="decimal" /></div>
            <div><asp:Label ID="lblAreaCaption" runat="server" AssociatedControlID="txtArea" CssClass="lb" Text="क्षेत्रफळ (sq ft)" /><asp:TextBox ID="txtArea" runat="server" CssClass="in" ReadOnly="true" data-smc="area" /></div>
            <div><asp:Label ID="lblRateCaption" runat="server" AssociatedControlID="txtRate" CssClass="lb" Text="Rate (₹)" /><asp:TextBox ID="txtRate" runat="server" CssClass="in" ReadOnly="true" data-smc="rate" /></div>
          </asp:Panel>
          <div><asp:Label ID="lblStallCaption" runat="server" AssociatedControlID="txtStall" CssClass="lb" Text="स्टॉल/जागेची आवश्यकता (क्षेत्रफळामध्ये) *" /><asp:TextBox ID="txtStall" runat="server" CssClass="in" MaxLength="200" /><asp:RequiredFieldValidator ID="rfvStall" runat="server" ControlToValidate="txtStall" ValidationGroup="g2" CssClass="valmsg" Display="Dynamic" ErrorMessage="स्टॉल/जागेची आवश्यकता भरणे आवश्यक आहे." /></div>
          <div><asp:Label ID="lblStartCaption" runat="server" AssociatedControlID="txtStart" CssClass="lb" Text="प्रारंभ तारीख *" /><asp:TextBox ID="txtStart" runat="server" CssClass="in" TextMode="Date" data-smc="start" /><asp:RequiredFieldValidator ID="rfvStart" runat="server" ControlToValidate="txtStart" ValidationGroup="g2" CssClass="valmsg" Display="Dynamic" ErrorMessage="प्रारंभ तारीख आवश्यक आहे." /></div>
          <div><asp:Label ID="lblEndCaption" runat="server" AssociatedControlID="txtEnd" CssClass="lb" Text="समाप्ती तारीख" /><asp:TextBox ID="txtEnd" runat="server" CssClass="in" TextMode="Date" data-smc="end" /><asp:RequiredFieldValidator ID="rfvEnd" runat="server" ControlToValidate="txtEnd" ValidationGroup="g2" CssClass="valmsg" Display="Dynamic" ErrorMessage="समाप्ती तारीख आवश्यक आहे." /></div>
          <input id="hidPricingService" type="hidden" data-smc="service" runat="server" />
          <input id="hidPricingServiceName" type="hidden" data-smc="serviceName" runat="server" />
        </div>
        <asp:Label ID="lblDescCaption" runat="server" AssociatedControlID="txtDescription" CssClass="lb" Text="इतर आवश्यक माहिती" />
        <asp:TextBox ID="txtDescription" runat="server" CssClass="in" MaxLength="1000" />
        <asp:Panel ID="pnlAnnualSummary" runat="server" ClientIDMode="Static" CssClass="annual-summary" Visible="false">
          <div class="annual-fields">
            <div><span>सेवा</span><strong id="annualService" runat="server" ClientIDMode="Static">—</strong></div>
            <div><span>प्रारंभ तारीख</span><strong id="annualStart" runat="server" ClientIDMode="Static">—</strong></div>
            <div><span>समाप्ती तारीख</span><strong id="annualEnd" runat="server" ClientIDMode="Static">—</strong></div>
            <div><span>कालावधी</span><strong>१ वर्ष</strong></div>
            <div class="annual-total"><span>वार्षिक निश्चित रक्कम</span><strong id="annualAmount" runat="server" ClientIDMode="Static">—</strong></div>
          </div>
        </asp:Panel>
        <asp:Panel ID="pnlPricingPreview" runat="server" Visible="false">
          <div class="preview" role="status" aria-live="polite">
            <div id="smcPreview" class="pricing-grid">
            <div class="price-box"><span>एकूण दिवस</span><strong>—</strong></div>
            <div class="price-box"><span>क्षेत्रफळ (चौ.फूट)</span><strong>—</strong></div>
            <div class="price-box"><span>दर (प्रतिदिन)</span><strong>—</strong></div>
            <div class="price-box price-total"><span>एकूण रक्कम</span><strong>—</strong></div>
            </div>
          </div>
        </asp:Panel>
      </asp:View>
      <asp:View ID="v3" runat="server">
        <h3>३. कागदपत्रे व सादरीकरण</h3>
        <div class="preview"><asp:Literal ID="litReview" runat="server" /></div>
        <div class="attachment-field">
          <asp:Label ID="lblIdCaption" runat="server" AssociatedControlID="fuIdentity" CssClass="lb" Text="ओळखपत्र (PDF / JPG / PNG, कमाल २० MB) *" />
          <asp:FileUpload ID="fuIdentity" runat="server" CssClass="in" accept=".pdf,.jpg,.jpeg,.png" />
          <div class="attachment-actions">
            <asp:Button ID="btnSaveIdentity" runat="server" CssClass="btn o" Text="जोडा / बदला" CausesValidation="false" OnClick="btnSaveIdentity_Click" />
            <asp:Button ID="btnRemoveIdentity" runat="server" CssClass="btn o" Text="काढा" CausesValidation="false" OnClick="btnRemoveIdentity_Click" Visible="false" />
          </div>
          <asp:Label ID="lblIdentityFileName" runat="server" CssClass="attachment-name" />
        </div>
        <div class="attachment-field">
          <asp:Label ID="lblAddrDocCaption" runat="server" AssociatedControlID="fuAddress" CssClass="lb" Text="पत्त्याचा पुरावा (PDF / JPG / PNG, कमाल २० MB) *" />
          <asp:FileUpload ID="fuAddress" runat="server" CssClass="in" accept=".pdf,.jpg,.jpeg,.png" />
          <div class="attachment-actions">
            <asp:Button ID="btnSaveAddress" runat="server" CssClass="btn o" Text="जोडा / बदला" CausesValidation="false" OnClick="btnSaveAddress_Click" />
            <asp:Button ID="btnRemoveAddress" runat="server" CssClass="btn o" Text="काढा" CausesValidation="false" OnClick="btnRemoveAddress_Click" Visible="false" />
          </div>
          <asp:Label ID="lblAddressFileName" runat="server" CssClass="attachment-name" />
        </div>
        <label class="chk"><asp:CheckBox ID="chkDeclaration" runat="server" /> मी घोषित करतो/करते की वरील सर्व माहिती खरी व बरोबर आहे.</label>
        <asp:CustomValidator ID="cvDeclaration" runat="server" ValidationGroup="g3" CssClass="valmsg" Display="Dynamic" ErrorMessage="घोषणापत्र मान्य करणे आवश्यक आहे." OnServerValidate="cvDeclaration_ServerValidate" ClientValidationFunction="smcCheckDeclaration" />
      </asp:View>
    </asp:MultiView>

    <div class="btns">
      <asp:Button ID="btnBack" runat="server" CssClass="btn o" Text="← मागे" CausesValidation="false" OnClick="btnBack_Click" Visible="false" />
      <asp:Button ID="btnNext" runat="server" CssClass="btn" Text="पुढे →" ValidationGroup="g1" OnClick="btnNext_Click" />
      <asp:Button ID="btnSubmit" runat="server" CssClass="btn" Text="अर्ज सादर करा" ValidationGroup="g3" OnClick="btnSubmit_Click" Visible="false" OnClientClick="return confirm('अर्ज सादर करायचा आहे का?');" data-once="1" />
    </div>
  </div>
  <script>function smcCheckDeclaration(s, a) { var c = document.querySelector('input[id$="chkDeclaration"]'); a.IsValid = !!(c && c.checked); }</script>
</asp:Panel>

<asp:Panel ID="pnlDone" runat="server" Visible="false">
  <div class="card">
    <h2>अर्ज यशस्वीरित्या सादर झाला</h2>
    <p class="notice ok">आपला अर्ज प्राप्त झाला आहे. अर्जाची PDF आपोआप डाउनलोड होईल. डाउनलोड अयशस्वी झाल्यास खालील बटण वापरून पुन्हा प्रयत्न करा.</p>
    <p class="notice info">आपला अर्ज क्रमांक जतन करा.</p>
    <dl class="kv">
      <dt>अर्ज क्रमांक</dt><dd class="big"><asp:Literal ID="litNumber" runat="server" /></dd>
      <dt>एकूण रक्कम</dt><dd class="big"><asp:Literal ID="litAmountDone" runat="server" /></dd>
    </dl>
    <div class="btns">
      <a class="btn" id="lnkAck" runat="server" href="~/Download.ashx?type=ack">पोचपावती PDF डाउनलोड करा</a>
      <a class="btn o" href="~/Citizen/Dashboard.aspx" runat="server">माझा अर्ज पहा</a>
    </div>
    <span id="autoDownload" runat="server" ClientIDMode="Static" data-href="" class="sr"></span>
  </div>
</asp:Panel>
</asp:Content>
