<%@ Page Title="पान आढळले नाही" Language="C#" MasterPageFile="~/Auth.Master" AutoEventWireup="true" CodeBehind="NotFound.aspx.cs" Inherits="SMC.BhoomiMalmatta.Web.NotFoundPage" %>
<asp:Content ID="Content1" ContentPlaceHolderID="cphMain" runat="server">
  <h2>पान आढळले नाही</h2>
  <p class="sub2">आपण शोधत असलेले पान उपलब्ध नाही.</p>
  <asp:Label ID="lblRef" runat="server" CssClass="hint" Visible="false" />
  <p class="alt"><a href="~/Default.aspx" runat="server">← मुख्यपृष्ठ</a></p>
</asp:Content>
