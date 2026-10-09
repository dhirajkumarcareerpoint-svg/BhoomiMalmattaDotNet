using System;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web.UI.WebControls;
using SMC.BhoomiMalmatta.Web.Security;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Web.Department
{
    public partial class ReportsPage : DeptPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack) return;
            foreach (var c in ReportBuilder.CatalogForRole(Me.Role)) ddlReport.Items.Add(new ListItem(c.Value, c.Key));
            var r = Request.QueryString["r"];
            if (r != null && ddlReport.Items.FindByValue(r) != null) ddlReport.SelectedValue = r;
        }

        private static DateTime? Day(string s)
        {
            DateTime d; return DateTime.TryParseExact((s ?? "").Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) ? (DateTime?)d : null;
        }

        protected void btnRun_Click(object sender, EventArgs e)
        {
            var t = ReportBuilder.Build(ddlReport.SelectedValue, ZoneScope, Day(txtFrom.Text), Day(txtTo.Text), Me.Role);
            if (t == null) return;
            litTitle.Text = Enc(t.Title + " (" + t.Rows.Count + ")");
            var table = new DataTable();
            foreach (var h in t.Headers) table.Columns.Add(h);
            foreach (var r in t.Rows) table.Rows.Add((object[])r);
            gv.DataSource = table; gv.DataBind();
        }

        protected void btnCsv_Click(object sender, EventArgs e)
        {
            var t = ReportBuilder.Build(ddlReport.SelectedValue, ZoneScope, Day(txtFrom.Text), Day(txtTo.Text), Me.Role);
            if (t == null) return;
            Response.Clear();
            Response.ContentType = "text/csv; charset=utf-8";
            Response.AddHeader("Content-Disposition", "attachment; filename=\"report-" + ddlReport.SelectedValue + "-" + DateTime.Now.ToString("yyyyMMdd") + ".csv\"");
            Response.BinaryWrite(new UTF8Encoding(true).GetPreamble()); // BOM so Excel shows Marathi correctly
            Response.BinaryWrite(Encoding.UTF8.GetBytes(t.ToCsv()));
            Response.Flush(); Response.SuppressContent = true; Context.ApplicationInstance.CompleteRequest();
        }
    }
}
