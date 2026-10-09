using System;
using SMC.BhoomiMalmatta.Web.Security;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Web
{
    public partial class LoginPage : SmcPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack && Me != null && !Me.IsCitizen)
            {
                var target = AuthCookies.EmployeeLandingUrl(Request.QueryString["ReturnUrl"], Me.Role, ResolveUrl("~/Department/Dashboard.aspx"), ResolveUrl("~/Department/Applications.aspx"));
                Response.Redirect(target, false); Context.ApplicationInstance.CompleteRequest();
            }
        }

        protected void btnLogin_Click(object sender, EventArgs e)
        {
            Page.Validate();
            if (!Page.IsValid) return;
            if (!AppServices.Limiter.Allow("login:" + ClientIp, 30, TimeSpan.FromMinutes(10))) { Fail("अनेक प्रयत्न झाले. कृपया थोड्या वेळाने पुन्हा प्रयत्न करा."); return; }
            var result = AppServices.Auth.Login(txtUser.Text, txtPass.Text, ClientIp);
            if (result.IsFailure) { txtPass.Text = string.Empty; Fail(result.Error.Message); return; }
            AuthCookies.SignInEmployee(Context, result.Value);
            Response.Redirect(AuthCookies.EmployeeLandingUrl(Request.QueryString["ReturnUrl"], result.Value.Role, ResolveUrl("~/Department/Dashboard.aspx"), ResolveUrl("~/Department/Applications.aspx")), false);
            Context.ApplicationInstance.CompleteRequest();
        }

        private void Fail(string message) { lblError.Text = Enc(message); lblError.Visible = true; }
    }
}
