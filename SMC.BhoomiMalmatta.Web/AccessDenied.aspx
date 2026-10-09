<%@ Page Title="प्रवेश नाकारला" Language="C#" MasterPageFile="~/Auth.Master" AutoEventWireup="true" CodeBehind="AccessDenied.aspx.cs" Inherits="SMC.BhoomiMalmatta.Web.AccessDeniedPage" %>
<asp:Content ID="Content1" ContentPlaceHolderID="cphMain" runat="server">
  <h2>प्रवेश नाकारला</h2>
  <p class="sub2">हे पान पाहण्याची आपल्याला परवानगी नाही.</p>
  <asp:Label ID="lblRef" runat="server" CssClass="hint" Visible="false" />
  <p class="alt"><a href="~/Default.aspx" runat="server">← मुख्यपृष्ठ</a></p>
</asp:Content>
