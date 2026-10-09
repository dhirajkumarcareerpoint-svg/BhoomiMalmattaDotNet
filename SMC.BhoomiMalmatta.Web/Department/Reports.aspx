<%@ Page Title="अहवाल" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Reports.aspx.cs" Inherits="SMC.BhoomiMalmatta.Web.Department.ReportsPage" %>
<asp:Content ID="Content1" ContentPlaceHolderID="cphMain" runat="server">
  <h2>अहवाल</h2>
  <div class="card">
    <div class="filters">
      <div><asp:Label ID="lblReportCaption" runat="server" AssociatedControlID="ddlReport" CssClass="lb" Text="अहवाल" /><asp:DropDownList ID="ddlReport" runat="server" CssClass="in" /></div>
      <div><asp:Label ID="lblFromCaption" runat="server" AssociatedControlID="txtFrom" CssClass="lb" Text="दिनांक पासून" /><asp:TextBox ID="txtFrom" runat="server" CssClass="in" TextMode="Date" /></div>
      <div><asp:Label ID="lblToCaption" runat="server" AssociatedControlID="txtTo" CssClass="lb" Text="दिनांक पर्यंत" /><asp:TextBox ID="txtTo" runat="server" CssClass="in" TextMode="Date" /></div>
    </div>
    <div class="btns"><asp:Button ID="btnRun" runat="server" CssClass="btn" Text="अहवाल दाखवा" OnClick="btnRun_Click" /><asp:Button ID="btnCsv" runat="server" CssClass="btn o" Text="CSV डाउनलोड" OnClick="btnCsv_Click" /></div>
  </div>
  <div class="card mt">
    <h3><asp:Literal ID="litTitle" runat="server" /></h3>
    <div class="tscroll"><asp:GridView ID="gv" runat="server" CssClass="tbl" GridLines="None" EmptyDataText="माहिती उपलब्ध नाही." UseAccessibleHeader="true" /></div>
  </div>
</asp:Content>
