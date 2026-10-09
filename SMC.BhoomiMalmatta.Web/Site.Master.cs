using System;
using System.Web;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Web.Security;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Web
{
    public partial class SiteMaster : SmcMasterBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            var p = Context.User as SmcPrincipal;
            var me = p == null ? null : p.Me;
            phPublic.Visible = me == null;
            phCitizen.Visible = me != null && me.IsCitizen;
            phUser.Visible = me != null;
            var currentPath = Request.AppRelativeCurrentExecutionFilePath ?? string.Empty;
            var officerHome = currentPath.EndsWith("/Department/Applications.aspx", StringComparison.OrdinalIgnoreCase);
            // Bare pages: no dark site header; the slim admin row carries the user name and the single logout.
            var barePage = IsBarePage(currentPath);
            phAdminLinks.Visible = me != null && (me.Role == Roles.Admin || me.Role == Roles.Staff);
            mainContent.Attributes["class"] = officerHome ? "officer-main" : (currentPath.IndexOf("/Department/", StringComparison.OrdinalIgnoreCase) >= 0 ? "department-main" + (barePage ? " property-main" : string.Empty) : string.Empty);
            phSiteHeader.Visible = !barePage;
            phNavUser.Visible = barePage && me != null && phAdminLinks.Visible;
            if (officerHome) phUser.Visible = false;
            lnkLogout.Visible = !officerHome;
            if (me == null) return;
            litUser.Text = HttpUtility.HtmlEncode(me.IsCitizen ? "अर्ज " + me.ApplicationNumber : DisplayName.WithRole(me.FullName, me.Role));
            litNavUser.Text = litUser.Text;
        }

        /// <summary>Pages drawn without the dark site header (the reference layout of the Properties page).</summary>
        public static bool IsBarePage(string appRelativePath)
        { return (appRelativePath ?? string.Empty).EndsWith("/Department/Properties.aspx", StringComparison.OrdinalIgnoreCase); }

        public override void Notify(string message, bool isError)
        {
            pnlNotice.Visible = true; pnlNotice.CssClass = "notice " + (isError ? "bad" : "ok");
            litNotice.Text = HttpUtility.HtmlEncode(message);
        }

        protected void lnkLogout_Click(object sender, EventArgs e) { LogoutCurrentUser(); }

        public void LogoutCurrentUser()
        {
            var p = Context.User as SmcPrincipal;
            if (p != null && !p.Me.IsCitizen && AppServices.Auth != null)
                AppServices.Auth.Logout(new SMC.BhoomiMalmatta.Models.Dto.AuthenticatedUser { Id = p.Me.UserId, Username = p.Me.Name, Role = p.Me.Role }, Request.UserHostAddress);
            AuthCookies.SignOut(Context);
            Response.Redirect("~/Default.aspx", false);
            Context.ApplicationInstance.CompleteRequest();
        }
    }
}
