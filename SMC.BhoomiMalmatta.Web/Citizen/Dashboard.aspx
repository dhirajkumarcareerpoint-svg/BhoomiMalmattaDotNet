<%@ Page Title="माझा अर्ज" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Dashboard.aspx.cs" Inherits="SMC.BhoomiMalmatta.Web.Citizen.DashboardPage" %>
<asp:Content ID="Content1" ContentPlaceHolderID="cphMain" runat="server">
  <div class="card">
    <h2>अर्जाची स्थिती</h2>
    <asp:Literal ID="litSummary" runat="server" />
  </div>
  <asp:Panel ID="pnlPayment" runat="server" Visible="false" CssClass="card mt">
    <h3>शुल्क भरणा</h3>
    <asp:Literal ID="litPayment" runat="server" />
    <asp:Panel ID="pnlPaymentForm" runat="server" Visible="false" CssClass="payment-submit-form">
      <div class="payment-flow-grid">
       <div class="payment-qr-pane"><asp:Literal ID="litPaymentQr" runat="server" /></div>
       <div class="payment-entry-pane"><asp:Literal ID="litPaymentInstructions" runat="server" />
      <asp:Label ID="lblUtrCaption" runat="server" AssociatedControlID="txtUtr" CssClass="lb" Text="UTR / संदर्भ क्रमांक * (६ ते ३० अक्षरे किंवा अंक)" />
      <asp:TextBox ID="txtUtr" runat="server" CssClass="in" MaxLength="30" inputmode="text" autocomplete="off" />
      <asp:Label ID="lblPaymentDateCaption" runat="server" AssociatedControlID="txtPaymentDate" CssClass="lb" Text="भरणा दिनांक *" />
      <asp:TextBox ID="txtPaymentDate" runat="server" CssClass="in" TextMode="Date" />
      <asp:Label ID="lblPaymentProofCaption" runat="server" AssociatedControlID="fuPaymentProof" CssClass="lb" Text="भरण्याचा पुरावा * (PDF / JPG / PNG, कमाल ५ MB)" />
      <asp:FileUpload ID="fuPaymentProof" runat="server" CssClass="in" accept=".pdf,.jpg,.jpeg,.png" />
      <div class="btns"><asp:Button ID="btnSubmitPayment" runat="server" CssClass="btn" Text="भरणा पडताळणीसाठी सादर करा" OnClick="btnSubmitPayment_Click" OnClientClick="return confirm('भरणा तपशील सादर करायचे आहेत का?');" data-once="1" /></div>
       </div>
      </div>
    </asp:Panel>
  </asp:Panel>
  <asp:Panel ID="pnlCorrection" runat="server" Visible="false" CssClass="card mt">
    <h3>दुरुस्ती आवश्यक</h3>
    <asp:Literal ID="litCorrection" runat="server" />
    <asp:Label ID="lblReplyCaption" runat="server" AssociatedControlID="txtReply" CssClass="lb" Text="आपले उत्तर / दुरुस्तीचा तपशील" />
    <asp:TextBox ID="txtReply" runat="server" CssClass="in" TextMode="MultiLine" Rows="3" MaxLength="1000" />
    <asp:Label ID="lblCorrFileCaption" runat="server" AssociatedControlID="fuCorrection" CssClass="lb" Text="दुरुस्त कागदपत्र (PDF / JPG / PNG, कमाल ५ MB)" />
    <asp:FileUpload ID="fuCorrection" runat="server" CssClass="in" accept=".pdf,.jpg,.jpeg,.png" />
    <div class="btns"><asp:Button ID="btnResubmit" runat="server" CssClass="btn" Text="दुरुस्त अर्ज पुन्हा सादर करा" OnClick="btnResubmit_Click" OnClientClick="return confirm('पुन्हा सादर करायचे का?');" data-once="1" /></div>
  </asp:Panel>
  <div class="row2 mt">
    <div class="card"><h3>प्रक्रिया इतिहास</h3><asp:Literal ID="litTimeline" runat="server" /></div>
    <div class="card"><h3>डाउनलोड</h3><asp:Literal ID="litDownloads" runat="server" /></div>
  </div>
</asp:Content>
