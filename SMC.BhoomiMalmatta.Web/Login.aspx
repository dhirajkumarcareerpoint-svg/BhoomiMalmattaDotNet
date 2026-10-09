<%@ Page Title="Officer Login" Language="C#" MasterPageFile="~/Auth.Master" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="SMC.BhoomiMalmatta.Web.LoginPage" %>
<asp:Content ID="Content1" ContentPlaceHolderID="cphMain" runat="server">
  <h2>अधिकारी लॉगिन</h2>
  <p class="sub2">Admin / JE / OS / सहाय्यक आयुक्त / कर्मचारी</p>
  <asp:Label ID="lblUserCaption" runat="server" AssociatedControlID="txtUser" CssClass="lb" Text="वापरकर्ता नाव" />
  <asp:TextBox ID="txtUser" runat="server" CssClass="in" MaxLength="50" autocomplete="username" />
  <asp:RequiredFieldValidator ID="rfvUser" runat="server" ControlToValidate="txtUser" CssClass="err" Display="Dynamic" ErrorMessage="वापरकर्ता नाव आवश्यक आहे." />
  <asp:Label ID="lblPassCaption" runat="server" AssociatedControlID="txtPass" CssClass="lb" Text="पासवर्ड" />
  <asp:TextBox ID="txtPass" runat="server" CssClass="in" TextMode="Password" MaxLength="128" autocomplete="current-password" />
  <asp:RequiredFieldValidator ID="rfvPass" runat="server" ControlToValidate="txtPass" CssClass="err" Display="Dynamic" ErrorMessage="पासवर्ड आवश्यक आहे." />
  <asp:Label ID="lblError" runat="server" CssClass="err" Visible="false" role="alert" />
  <asp:Button ID="btnLogin" runat="server" CssClass="btn signin" Text="लॉगिन" OnClick="btnLogin_Click" />
  <p class="alt"><a href="Default.aspx">← मुख्यपृष्ठ</a></p>
</asp:Content>
