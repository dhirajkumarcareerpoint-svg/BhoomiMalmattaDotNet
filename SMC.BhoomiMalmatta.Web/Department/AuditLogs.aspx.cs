using System;
using System.Globalization;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Web.Security;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Web.Department
{
    public partial class AuditLogsPage : DeptPage
    {
        protected override string[] RequiredRoles { get { return Roles.OfficerRoles; } }
        private int CurrentPage { get { return ViewState["p"] == null ? 1 : (int)ViewState["p"]; } set { ViewState["p"] = value; } }

        protected void Page_Load(object sender, EventArgs e) { if (!IsPostBack) Bind(); }

        private static DateTime? Day(string s)
        {
            DateTime d; return DateTime.TryParseExact((s ?? "").Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) ? (DateTime?)d : null;
        }

        private void Bind()
        {
            var r = AppServices.Query.SearchAudit(Day(txtFrom.Text), Day(txtTo.Text), txtAction.Text, txtUser.Text, CurrentPage, 25);
            gv.DataSource = r.Items; gv.DataBind();
            lblCount.Text = Enc("एकूण " + r.Total + " नोंदी");
            lblPage.Text = Enc("पृष्ठ " + r.Page + " / " + Math.Max(1, r.TotalPages));
            btnPrev.Enabled = r.Page > 1; btnNextPage.Enabled = r.Page < r.TotalPages;
        }

        protected void btnFilter_Click(object sender, EventArgs e) { CurrentPage = 1; Bind(); }
        protected void btnPrev_Click(object sender, EventArgs e) { CurrentPage = Math.Max(1, CurrentPage - 1); Bind(); }
        protected void btnNextPage_Click(object sender, EventArgs e) { CurrentPage = CurrentPage + 1; Bind(); }
    }
}
