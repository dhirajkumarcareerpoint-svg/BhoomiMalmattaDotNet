using System;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Web.Security;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Web.Department
{
    public partial class UsersPage : SmcPage
    {
        protected override string[] RequiredRoles { get { return new[] { Roles.Admin, Roles.Staff }; } }
        protected void Page_Load(object sender, EventArgs e) { gv.DataSource = AppServices.Query.GetUsers(); gv.DataBind(); }
    }
}
