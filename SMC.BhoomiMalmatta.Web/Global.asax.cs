using System;
using System.Configuration;
using System.Data.Entity;
using System.Net;
using System.Web;
using System.Web.Http;
using SMC.BhoomiMalmatta.Data;
using SMC.BhoomiMalmatta.Data.Safety;
using SMC.BhoomiMalmatta.Web.Security;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Web
{
    public class Global : HttpApplication
    {
        protected void Application_Start(object sender, EventArgs e)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            // Defence in depth: the context already disables initialization in its static constructor and in Web.config.
            Database.SetInitializer<SmcDbContext>(null);

            var cs = ConfigurationManager.ConnectionStrings["SmcDatabase"];
            var check = DatabaseSafety.AssertSafe(cs == null ? null : cs.ConnectionString);
            if (check.IsFailure)
            {
                // Fail start-up with a clear message (served as HTTP 503 by Application_BeginRequest); nothing touches the database.
                AppServices.StartupError = check.Error.Message;
                ErrorLogger.Log("startup", "DatabaseSafety", new InvalidOperationException(check.Error.Code + " " + DatabaseSafety.Redact(cs == null ? null : cs.ConnectionString)));
                return;
            }
            // Production safeguard: outside development (compilation debug off) authentication cookies must be HTTPS-only.
            if (!Context.IsDebuggingEnabled && !System.Web.Security.FormsAuthentication.RequireSSL)
            {
                AppServices.StartupError = "Refusing to start: forms authentication requireSSL must be true when debug is off (use the Release web.config transform).";
                ErrorLogger.Log("startup", "RequireSSL", new InvalidOperationException("RequireSSL=false in a non-debug build"));
                return;
            }
            try
            {
                Composition.Wire();
                GlobalConfiguration.Configure(WebApiConfig.Register);
            }
            catch (Exception ex)
            {
                AppServices.StartupError = "Application start-up failed: " + ex.GetType().Name + ". See the error log.";
                ErrorLogger.Log("startup", "Composition", ex);
            }
        }

        protected void Session_Start(object sender, EventArgs e) { Session["init"] = 1; } // pins the session id (used as ViewStateUserKey)
        protected void Session_End(object sender, EventArgs e) { ApplyPage.ClearStagedAttachments(Session); }

        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            var id = Guid.NewGuid().ToString("N").Substring(0, 12);
            Context.Items["CorrelationId"] = id;
            Response.AppendHeader("X-Correlation-Id", id);
            if (AppServices.StartupError != null && !Request.Path.EndsWith("Health.ashx", StringComparison.OrdinalIgnoreCase))
            {
                Response.StatusCode = 503;
                Response.ContentType = "text/plain; charset=utf-8";
                Response.Write("SMC Bhoomi Malmatta - service unavailable.\r\n" + AppServices.StartupError + "\r\nReference: " + id);
                CompleteRequest();
            }
        }

        protected void Application_AuthenticateRequest(object sender, EventArgs e) { AuthCookies.RestorePrincipal(Context); }

        protected void Application_EndRequest(object sender, EventArgs e)
        {
            var principal = Context.User as SmcPrincipal;
            if (principal == null || principal.Me == null) return;
            var path = (Request.AppRelativeCurrentExecutionFilePath ?? string.Empty).TrimEnd('/');
            var adminOnlyPage = path.Equals("~/Department/Properties.aspx", StringComparison.OrdinalIgnoreCase)
                || path.Equals("~/Department/Calculations.aspx", StringComparison.OrdinalIgnoreCase)
                || path.Equals("~/Department/Recovery.aspx", StringComparison.OrdinalIgnoreCase)
                || path.Equals("~/Department/Transfers.aspx", StringComparison.OrdinalIgnoreCase)
                || path.Equals("~/Department/Tenders.aspx", StringComparison.OrdinalIgnoreCase)
                || path.Equals("~/Department/Schemes.aspx", StringComparison.OrdinalIgnoreCase);
            var staffPage = path.Equals("~/Department/Users.aspx", StringComparison.OrdinalIgnoreCase)
                || path.Equals("~/Department/Masters.aspx", StringComparison.OrdinalIgnoreCase);
            var allowed = principal.Me.Role == SMC.BhoomiMalmatta.Models.Constants.Roles.Admin
                || (staffPage && principal.Me.Role == SMC.BhoomiMalmatta.Models.Constants.Roles.Staff);
            if ((!adminOnlyPage && !staffPage) || allowed) return;

            // FormsAuthentication can turn Web.config's role-based 401 into a 302 to Login.aspx.
            // Keep authenticated role denials as an actual 403 for these restricted Department pages.
            var redirectedToLogin = Response.StatusCode == 302
                && (Response.RedirectLocation ?? string.Empty).IndexOf("Login.aspx", StringComparison.OrdinalIgnoreCase) >= 0;
            if (Response.StatusCode != 401 && !redirectedToLogin) return;
            Response.ClearContent();
            Response.RedirectLocation = null;
            Response.StatusCode = 403;
            Response.TrySkipIisCustomErrors = true;
            Response.ContentType = "text/plain; charset=utf-8";
            Response.Write("Forbidden");
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            var ex = Server.GetLastError();
            var id = Context.Items["CorrelationId"] as string ?? "n/a";
            if (ex is HttpException && ((HttpException)ex).GetHttpCode() == 404) return;
            ErrorLogger.Log(id, Request.Path, ex);
        }
    }
}
