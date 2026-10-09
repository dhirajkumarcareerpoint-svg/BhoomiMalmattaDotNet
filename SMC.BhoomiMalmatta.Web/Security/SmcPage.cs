using System;
using System.Web;
using System.Web.UI;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Models.Constants;

namespace SMC.BhoomiMalmatta.Web.Security
{
    public abstract class SmcMasterBase : MasterPage
    {
        public abstract void Notify(string message, bool isError);
    }

    /// <summary>
    /// Base page: server-side role enforcement (never trusting the browser), ViewStateUserKey (CSRF defence for postbacks) and common helpers.
    /// RequiredRoles: null = anonymous allowed; an array = only those roles.
    /// </summary>
    public abstract class SmcPage : Page
    {
        protected virtual string[] RequiredRoles { get { return null; } }
        protected SmcIdentity Me { get { var p = User as SmcPrincipal; return p == null ? null : p.Me; } }
        protected string ClientIp { get { return Request.UserHostAddress; } }
        protected string CorrelationId { get { return Context.Items["CorrelationId"] as string; } }

        protected override void OnPreInit(EventArgs e)
        {
            base.OnPreInit(e);
            var roles = RequiredRoles;
            if (roles == null) return;
            if (Me == null) { Response.Redirect("~/Login.aspx?ReturnUrl=" + Server.UrlEncode(Request.RawUrl), true); return; }
            if (Array.IndexOf(roles, Me.Role) < 0)
            {
                Response.StatusCode = 403; Response.TrySkipIisCustomErrors = true;
                Response.ContentType = "text/plain; charset=utf-8"; Response.Write("Forbidden");
                Response.End();
            }
        }

        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            if (Session != null) ViewStateUserKey = Session.SessionID;
        }

        /// <summary>The acting user for workflow calls, built only from the authenticated ticket.</summary>
        protected ActorContext Actor()
        {
            var me = Me;
            if (me == null) return new ActorContext { UserName = "anonymous", Role = "Anonymous", IpAddress = ClientIp, CorrelationId = CorrelationId };
            if (me.IsCitizen) return new ActorContext { ApplicationId = me.ApplicationId, UserName = "citizen", Role = Roles.Citizen, IpAddress = ClientIp, CorrelationId = CorrelationId };
            return new ActorContext { UserId = me.UserId, UserName = me.Name, Role = me.Role, ZoneNumber = me.ZoneNumber, IpAddress = ClientIp, CorrelationId = CorrelationId };
        }

        protected void Notify(string message, bool isError = false)
        {
            var m = Master as SmcMasterBase;
            if (m != null) m.Notify(message, isError);
        }

        protected static string Enc(string s) { return HttpUtility.HtmlEncode(s ?? string.Empty); }
    }
}
