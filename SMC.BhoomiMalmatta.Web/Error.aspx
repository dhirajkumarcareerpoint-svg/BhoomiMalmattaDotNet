<%@ Page Title="त्रुटी" Language="C#" MasterPageFile="~/Auth.Master" AutoEventWireup="true" CodeBehind="Error.aspx.cs" Inherits="SMC.BhoomiMalmatta.Web.ErrorPage" %>
<asp:Content ID="Content1" ContentPlaceHolderID="cphMain" runat="server">
  <h2>त्रुटी</h2>
  <p class="sub2">क्षमस्व, काहीतरी चूक झाली. कृपया पुन्हा प्रयत्न करा. समस्या कायम राहिल्यास खालील संदर्भ क्रमांक कार्यालयास कळवा.</p>
  <asp:Label ID="lblRef" runat="server" CssClass="hint" Visible="false" />
  <p class="alt"><a href="~/Default.aspx" runat="server">← मुख्यपृष्ठ</a></p>
</asp:Content>
