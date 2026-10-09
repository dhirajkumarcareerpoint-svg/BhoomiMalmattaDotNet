<%@ Page Title="अर्जाचा तपशील" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ApplicationDetail.aspx.cs" Inherits="SMC.BhoomiMalmatta.Web.Department.ApplicationDetailPage" %>
<asp:Content ID="Content1" ContentPlaceHolderID="cphMain" runat="server">
  <p><a href="Applications.aspx">← अर्जांची यादी</a></p>
  <div class="card"><h2>अर्ज तपशील</h2><asp:Literal ID="litDetail" runat="server" /></div>
  <asp:Panel ID="pnlLegacy" runat="server" Visible="false" CssClass="notice info mt">हा जुना (Legacy) अर्ज आहे. तो केवळ पाहण्यासाठी आहे; यावर कोणतीही कार्यवाही करता येत नाही.</asp:Panel>
  <asp:Panel ID="pnlAction" runat="server" Visible="false" CssClass="card mt">
    <h3>कार्यवाही</h3>
    <asp:Label ID="lblActionCaption" runat="server" AssociatedControlID="ddlAction" CssClass="lb" Text="कृती *" />
    <asp:DropDownList ID="ddlAction" runat="server" CssClass="in" />
    <asp:Panel ID="pnlRate" runat="server" Visible="false">
      <asp:Label ID="lblRateCaption" runat="server" AssociatedControlID="txtRate" CssClass="lb" Text="दर (₹) — फक्त जेव्हा सेवेचा दर निश्चित नाही" />
      <asp:TextBox ID="txtRate" runat="server" CssClass="in" inputmode="decimal" />
      <span class="hint">रक्कम सर्व्हर मोजतो: क्षेत्रफळ × दर × दिवस (sq.ft.) / निश्चित दर.</span>
    </asp:Panel>
    <asp:Panel ID="pnlPay" runat="server" Visible="false">
      <div class="two">
        <div><asp:Label ID="lblRefCaption" runat="server" AssociatedControlID="txtReference" CssClass="lb" Text="अर्जदाराने दिलेला UTR / संदर्भ" /><asp:TextBox ID="txtReference" runat="server" CssClass="in" MaxLength="30" ReadOnly="true" /></div>
        <div><asp:Label ID="lblModeCaption" runat="server" AssociatedControlID="ddlMode" CssClass="lb" Text="भरणा पद्धत" /><asp:DropDownList ID="ddlMode" runat="server" CssClass="in" /></div>
      </div>
    </asp:Panel>
    <asp:Label ID="lblRemarksCaption" runat="server" AssociatedControlID="txtRemarks" CssClass="lb" Text="शेरा (Remarks)" />
    <asp:TextBox ID="txtRemarks" runat="server" CssClass="in" TextMode="MultiLine" Rows="3" MaxLength="1000" />
    <div class="btns"><asp:Button ID="btnExecute" runat="server" CssClass="btn" Text="कृती करा" OnClick="btnExecute_Click" OnClientClick="return confirm('ही कृती निश्चित करायची आहे का?');" data-once="1" /></div>
  </asp:Panel>
  <asp:Panel ID="pnlCert" runat="server" Visible="false" CssClass="card mt">
    <h3>प्रमाणपत्र</h3>
    <asp:Literal ID="litCert" runat="server" />
    <asp:Button ID="btnCert" runat="server" CssClass="btn o" Text="प्रमाणपत्र पुन्हा तयार करा" OnClick="btnCert_Click" />
  </asp:Panel>
  <div class="row2 mt">
    <div class="card"><h3>कागदपत्रे</h3><asp:Literal ID="litDocs" runat="server" /></div>
    <div class="card"><h3>प्रक्रिया इतिहास</h3><asp:Literal ID="litHistory" runat="server" /></div>
  </div>
</asp:Content>
