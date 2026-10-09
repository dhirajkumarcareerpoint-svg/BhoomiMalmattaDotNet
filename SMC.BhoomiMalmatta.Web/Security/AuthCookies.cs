using System;
using System.Configuration;
using System.Web;
using System.Web.Security;
using SMC.BhoomiMalmatta.Models.Dto;
using SMC.BhoomiMalmatta.Models.Constants;

namespace SMC.BhoomiMalmatta.Web.Security
{
    public static class AuthCookies
    {
        public static int TimeoutMinutes
        {
            get { int m; return int.TryParse(ConfigurationManager.AppSettings["Auth.TimeoutMinutes"], out m) && m >= 5 && m <= 240 ? m : 20; }
        }

        private static int CitizenTimeoutDays
        {
            get { int d; return int.TryParse(ConfigurationManager.AppSettings["Auth.CitizenTimeoutDays"], out d) && d >= 1 && d <= 365 ? d : 30; }
        }

        public static void SignInEmployee(HttpContext ctx, AuthenticatedUser u)
        { Issue(ctx, u.Username, SmcIdentity.EmployeeData(u.Id, u.Role, u.ZoneNumber, u.FullName), DateTime.Now.AddMinutes(TimeoutMinutes), false); }

        public static void SignInCitizen(HttpContext ctx, int applicationId, string applicationNumber)
        { Issue(ctx, "citizen:" + applicationNumber, SmcIdentity.CitizenData(applicationId, applicationNumber), DateTime.Now.AddDays(CitizenTimeoutDays), true); }

        private static void Issue(HttpContext ctx, string name, string userData, DateTime expiresAt, bool persistent)
        {
            // New session id on every sign-in (session-fixation defence).
            if (ctx.Session != null) ctx.Session.Abandon();
            var now = DateTime.Now;
            var ticket = new FormsAuthenticationTicket(2, name, now, expiresAt, persistent, userData, FormsAuthentication.FormsCookiePath);
            var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, FormsAuthentication.Encrypt(ticket))
            {
                HttpOnly = true, Secure = FormsAuthentication.RequireSSL, Path = FormsAuthentication.FormsCookiePath, SameSite = SameSiteMode.Lax
            };
            if (persistent) cookie.Expires = ticket.Expiration;
            ctx.Response.Cookies.Add(cookie);
        }

        public static void SignOut(HttpContext ctx)
        {
            FormsAuthentication.SignOut();
            if (ctx.Session != null) ctx.Session.Abandon();
            ExpireCookie(ctx, FormsAuthentication.FormsCookieName);
            ExpireCookie(ctx, "SMC.Session");
        }

        private static void ExpireCookie(HttpContext ctx, string name)
        {
            ctx.Response.Cookies.Add(new HttpCookie(name, string.Empty) { Expires = DateTime.Now.AddYears(-1), HttpOnly = true, Secure = FormsAuthentication.RequireSSL, Path = "/" });
        }

        /// <summary>Runs after FormsAuthenticationModule: replaces its GenericPrincipal with the role-aware SmcPrincipal.</summary>
        public static void RestorePrincipal(HttpContext ctx)
        {
            var fi = ctx.User == null ? null : ctx.User.Identity as FormsIdentity;
            if (fi == null || fi.Ticket == null || fi.Ticket.Expired) return;
            var me = SmcIdentity.Parse(fi.Name, fi.Ticket.UserData);
            if (me == null) return;
            var principal = new SmcPrincipal(me);
            ctx.User = principal;
            System.Threading.Thread.CurrentPrincipal = principal;
        }

        /// <summary>Only local, absolute-path redirects are accepted (open-redirect defence).</summary>
        public static string SafeReturnUrl(string url, string fallback)
        {
            if (string.IsNullOrWhiteSpace(url)) return fallback;
            if (!url.StartsWith("/") && !url.StartsWith("~/")) return fallback;
            if (url.StartsWith("//") || url.StartsWith("/\\") || url.IndexOf("://", StringComparison.Ordinal) >= 0) return fallback;
            return url;
        }

        /// <summary>Officer sessions may return only to the demand-application flow and reports available to their role.</summary>
        public static string SafeOfficerReturnUrl(string url, string role, string fallback)
        {
            var safe = SafeReturnUrl(url, fallback);
            if (safe == fallback) return fallback;
            var path = safe.Split('?')[0];
            var parts = path.Replace('~', '/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !string.Equals(parts[parts.Length - 2], "Department", StringComparison.OrdinalIgnoreCase)) return fallback;
            var page = parts[parts.Length - 1];
            if (string.Equals(page, "Applications.aspx", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(page, "ApplicationDetail.aspx", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(page, "Reports.aspx", StringComparison.OrdinalIgnoreCase)) return safe;
            return fallback;
        }

        public static string EmployeeLandingUrl(string returnUrl, string role, string dashboardUrl, string applicationUrl)
        {
            if (role == SMC.BhoomiMalmatta.Models.Constants.Roles.JE || role == SMC.BhoomiMalmatta.Models.Constants.Roles.OS || role == SMC.BhoomiMalmatta.Models.Constants.Roles.AssistantCommissioner)
                return SafeOfficerReturnUrl(returnUrl, role, applicationUrl);
            return SafeReturnUrl(returnUrl, dashboardUrl);
        }
    }
}
