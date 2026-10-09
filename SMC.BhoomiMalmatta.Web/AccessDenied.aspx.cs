using System;
using SMC.BhoomiMalmatta.Web.Security;

namespace SMC.BhoomiMalmatta.Web
{
    public partial class AccessDeniedPage : SmcPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Response.TrySkipIisCustomErrors = true;
            Response.StatusCode = 403;
            var id = CorrelationId;
            if (!string.IsNullOrEmpty(id)) { lblRef.Text = "संदर्भ क्रमांक: " + Enc(id); lblRef.Visible = true; }
        }
    }
}
