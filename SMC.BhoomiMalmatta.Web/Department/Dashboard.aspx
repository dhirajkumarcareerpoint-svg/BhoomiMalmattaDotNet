<%@ Page Title="Dashboard" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Dashboard.aspx.cs" Inherits="SMC.BhoomiMalmatta.Web.Department.DashboardPage" %>
<asp:Content ID="Content1" ContentPlaceHolderID="cphMain" runat="server">
  <h2>मागणी अर्ज</h2>
  <p class="sub2"><asp:Literal ID="litWho" runat="server" /></p>
  <div class="grid" aria-label="मागणी अर्ज स्थिती">
    <asp:Literal ID="litCards" runat="server" />
  </div>
</asp:Content>
