<%@ Page Title="अर्जाची स्थिती तपासा" Language="C#" MasterPageFile="~/Auth.Master" AutoEventWireup="true" CodeBehind="Status.aspx.cs" Inherits="SMC.BhoomiMalmatta.Web.StatusPage" %>
<asp:Content ID="Content1" ContentPlaceHolderID="cphMain" runat="server">
  <h2>अर्जाची स्थिती तपासा</h2>
  <p class="sub2">अर्ज क्रमांक वापरून स्थिती पहा.</p>
  <asp:Label ID="lblNumberCaption" runat="server" AssociatedControlID="txtNumber" CssClass="lb" Text="अर्ज क्रमांक" />
  <asp:TextBox ID="txtNumber" runat="server" CssClass="in" MaxLength="40" required="required" pattern="[A-Za-z0-9]{1,40}" autocomplete="off" />
  <asp:Label ID="lblError" runat="server" CssClass="err" Visible="false" role="alert" />
  <asp:Button ID="btnCheck" runat="server" CssClass="btn signin" Text="स्थिती पहा" OnClick="btnCheck_Click" />
  <p class="alt"><a href="Default.aspx">← मुख्यपृष्ठ</a></p>
</asp:Content>
