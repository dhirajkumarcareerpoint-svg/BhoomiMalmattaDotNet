using System;
using System.Text;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Web.Security;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Web.Department
{
    public partial class DashboardPage : DeptPage
    {
        private readonly StringBuilder _html = new StringBuilder();

        private void Card(string label, int value, string query)
        {
            _html.Append("<a class=\"card stat\" style=\"text-decoration:none;color:inherit\" href=\"")
                .Append(Enc(ResolveUrl("~/Department/Applications.aspx" + (string.IsNullOrEmpty(query) ? "" : "?" + query))))
                .Append("\"><span class=\"muted\">").Append(Enc(label)).Append("</span><b>")
                .Append(Enc(value.ToString("N0"))).Append("</b></a>");
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Me.Role == Roles.JE || Me.Role == Roles.OS || Me.Role == Roles.AssistantCommissioner)
            {
                Response.Redirect("~/Department/Applications.aspx", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            var counts = AppServices.Query.GetDashboard(ZoneScope);
            litWho.Text = Enc(DisplayName.WithRole(Me.FullName, Me.Role) + (ZoneScope.HasValue ? " — झोन " + ZoneScope.Value : ""));
            Card("एकूण अर्ज", counts.Total, "view=all");
            Card("JE कडे प्रलंबित", counts.JePending, "view=all&stage=" + ZoneStages.JEPending);
            Card("OS कडे प्रलंबित", counts.OsPending, "view=all&stage=" + ZoneStages.OSPending);
            Card("शुल्क भरणा प्रलंबित", counts.PaymentPending, "view=all&stage=" + ZoneStages.PaymentRequired);
            Card("सहाय्यक आयुक्तांकडे प्रलंबित", counts.AcPending, "view=all&stage=" + ZoneStages.AssistantCommissionerApprovalPending);
            Card("मंजूर", counts.Approved, "view=all&stage=" + ZoneStages.Approved);
            Card("नाकारलेले", counts.Rejected, "view=all&stage=" + ZoneStages.Rejected);
            Card("जुने Legacy — केवळ वाचनीय", counts.Legacy, "view=all&route=" + ProcessingRoutes.Legacy);
            litCards.Text = _html.ToString();
        }
    }
}
