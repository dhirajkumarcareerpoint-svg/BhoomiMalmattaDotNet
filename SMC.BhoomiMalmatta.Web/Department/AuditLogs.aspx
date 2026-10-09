<%@ Page Title="Audit इतिहास" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="AuditLogs.aspx.cs" Inherits="SMC.BhoomiMalmatta.Web.Department.AuditLogsPage" %>
<asp:Content ID="Content1" ContentPlaceHolderID="cphMain" runat="server">
  <h2>Audit इतिहास</h2>
  <div class="card">
    <div class="filters">
      <div><asp:Label ID="lblActionCaption" runat="server" AssociatedControlID="txtAction" CssClass="lb" Text="कृती" /><asp:TextBox ID="txtAction" runat="server" CssClass="in" MaxLength="30" /></div>
      <div><asp:Label ID="lblUserCaption" runat="server" AssociatedControlID="txtUser" CssClass="lb" Text="वापरकर्ता" /><asp:TextBox ID="txtUser" runat="server" CssClass="in" MaxLength="100" /></div>
      <div><asp:Label ID="lblFromCaption" runat="server" AssociatedControlID="txtFrom" CssClass="lb" Text="दिनांक पासून" /><asp:TextBox ID="txtFrom" runat="server" CssClass="in" TextMode="Date" /></div>
      <div><asp:Label ID="lblToCaption" runat="server" AssociatedControlID="txtTo" CssClass="lb" Text="दिनांक पर्यंत" /><asp:TextBox ID="txtTo" runat="server" CssClass="in" TextMode="Date" /></div>
    </div>
    <div class="btns"><asp:Button ID="btnFilter" runat="server" CssClass="btn" Text="शोधा" OnClick="btnFilter_Click" /></div>
  </div>
  <div class="card mt">
    <asp:Label ID="lblCount" runat="server" CssClass="muted" />
    <div class="tscroll">
      <asp:GridView ID="gv" runat="server" AutoGenerateColumns="false" CssClass="tbl" GridLines="None" EmptyDataText="नोंदी आढळल्या नाहीत." UseAccessibleHeader="true">
        <Columns>
          <asp:BoundField HeaderText="दिनांक / वेळ" DataField="Timestamp" DataFormatString="{0:dd/MM/yyyy HH:mm:ss}" />
          <asp:BoundField HeaderText="वापरकर्ता" DataField="UserName" />
          <asp:BoundField HeaderText="कृती" DataField="Action" />
          <asp:BoundField HeaderText="घटक" DataField="EntityName" />
          <asp:BoundField HeaderText="ID" DataField="EntityId" />
          <asp:BoundField HeaderText="जुने मूल्य" DataField="OldValue" />
          <asp:BoundField HeaderText="नवे मूल्य" DataField="NewValue" />
          <asp:BoundField HeaderText="IP" DataField="IpAddress" />
        </Columns>
      </asp:GridView>
    </div>
    <div class="pager">
      <asp:Button ID="btnPrev" runat="server" CssClass="btn o" Text="← मागील" OnClick="btnPrev_Click" />
      <asp:Label ID="lblPage" runat="server" />
      <asp:Button ID="btnNextPage" runat="server" CssClass="btn o" Text="पुढील →" OnClick="btnNextPage_Click" />
    </div>
  </div>
</asp:Content>
