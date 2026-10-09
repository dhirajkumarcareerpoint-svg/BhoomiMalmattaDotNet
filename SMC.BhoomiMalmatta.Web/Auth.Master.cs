using System.Web;
using SMC.BhoomiMalmatta.Web.Security;

namespace SMC.BhoomiMalmatta.Web
{
    public partial class AuthMaster : SmcMasterBase
    {
        public override void Notify(string message, bool isError)
        {
            pnlNotice.Visible = true; pnlNotice.CssClass = "notice " + (isError ? "bad" : "ok");
            litNotice.Text = HttpUtility.HtmlEncode(message);
        }
    }
}
