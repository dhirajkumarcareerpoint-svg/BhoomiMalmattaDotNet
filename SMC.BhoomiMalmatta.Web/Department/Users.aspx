<%@ Page Title="Users" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Users.aspx.cs" Inherits="SMC.BhoomiMalmatta.Web.Department.UsersPage" %>
<asp:Content ID="Content1" ContentPlaceHolderID="cphMain" runat="server">
  <h2>Users</h2>
  <p class="sub2">केवळ वाचनीय यादी. पासवर्ड कधीही दाखवले जात नाहीत.</p>
  <div class="card tscroll">
    <asp:GridView ID="gv" runat="server" AutoGenerateColumns="false" CssClass="tbl" GridLines="None" EmptyDataText="वापरकर्ते नाहीत." UseAccessibleHeader="true">
      <Columns>
        <asp:BoundField HeaderText="वापरकर्ता नाव" DataField="Username" />
        <asp:BoundField HeaderText="पूर्ण नाव" DataField="FullName" />
        <asp:BoundField HeaderText="पदनाम" DataField="Designation" />
        <asp:BoundField HeaderText="भूमिका" DataField="Role" />
        <asp:BoundField HeaderText="झोन" DataField="ZoneNumber" NullDisplayText="सर्व" />
        <asp:CheckBoxField HeaderText="सक्रिय" DataField="IsActive" />
        <asp:BoundField HeaderText="शेवटचा लॉगिन" DataField="LastLoginAt" DataFormatString="{0:dd/MM/yyyy HH:mm}" NullDisplayText="—" />
      </Columns>
    </asp:GridView>
  </div>
</asp:Content>
