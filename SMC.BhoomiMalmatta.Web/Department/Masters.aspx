<%@ Page Title="Master Data" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Masters.aspx.cs" Inherits="SMC.BhoomiMalmatta.Web.Department.MastersPage" %>
<asp:Content ID="Content1" ContentPlaceHolderID="cphMain" runat="server">
  <h2>Master Data</h2>
  <p class="sub2">केवळ वाचनीय. बदल करण्यासाठी विद्यमान प्रक्रिया वापरा.</p>
  <div class="card tscroll"><h3>सेवा</h3>
    <asp:GridView ID="gvServices" runat="server" AutoGenerateColumns="false" CssClass="tbl" GridLines="None" UseAccessibleHeader="true">
      <Columns><asp:BoundField HeaderText="कोड" DataField="Code" /><asp:BoundField HeaderText="नाव" DataField="Name" /><asp:BoundField HeaderText="मराठी नाव" DataField="NameMr" /><asp:BoundField HeaderText="क्रम" DataField="SortOrder" /></Columns>
    </asp:GridView></div>
  <div class="card tscroll mt"><h3>दर (Rates)</h3>
    <asp:GridView ID="gvRates" runat="server" AutoGenerateColumns="false" CssClass="tbl" GridLines="None" UseAccessibleHeader="true">
      <Columns><asp:BoundField HeaderText="सेवा कोड" DataField="ServiceCode" /><asp:BoundField HeaderText="दर (₹)" DataField="Rate" DataFormatString="{0:N2}" NullDisplayText="निश्चित नाही (OS दर भरतो)" /><asp:BoundField HeaderText="एकक" DataField="Unit" /></Columns>
    </asp:GridView></div>
  <div class="card tscroll mt"><h3>व्यवसाय प्रकार</h3>
    <asp:GridView ID="gvBusiness" runat="server" AutoGenerateColumns="false" CssClass="tbl" GridLines="None" UseAccessibleHeader="true">
      <Columns><asp:BoundField HeaderText="कोड" DataField="Code" /><asp:BoundField HeaderText="नाव" DataField="Name" /><asp:BoundField HeaderText="मराठी नाव" DataField="NameMr" /></Columns>
    </asp:GridView></div>
  <div class="card mt"><h3>अर्ज क्रमांक संरचना</h3><asp:Literal ID="litNumber" runat="server" /></div>
  <div class="card tscroll mt"><h3>SMS टेम्पलेट</h3>
    <asp:GridView ID="gvSms" runat="server" AutoGenerateColumns="false" CssClass="tbl" GridLines="None" UseAccessibleHeader="true">
      <Columns><asp:BoundField HeaderText="घटना" DataField="EventType" /><asp:BoundField HeaderText="नाव" DataField="TemplateName" /><asp:BoundField HeaderText="भाषा" DataField="Language" /><asp:BoundField HeaderText="मंजुरी" DataField="ApprovalStatus" /><asp:CheckBoxField HeaderText="सक्रिय" DataField="IsActive" /></Columns>
    </asp:GridView></div>
</asp:Content>
