using System;
using System.Globalization;
using System.Linq;
using System.Web;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;
using SMC.BhoomiMalmatta.Business.Workflow;
using SMC.BhoomiMalmatta.Web.Security;
using SMC.BhoomiMalmatta.Web.Services;
using System.Web.UI.WebControls;
using SMC.BhoomiMalmatta.Web.Department.Controls;

namespace SMC.BhoomiMalmatta.Web.Department
{
    public partial class ApplicationsPage : DeptPage
    {
        private const int PageSize = 8;
        private int CurrentPage { get { return ViewState["p"] == null ? 1 : (int)ViewState["p"]; } set { ViewState["p"] = value; } }

        protected void Page_Init(object sender, EventArgs e)
        {
            officerFilters.SearchRequested += btnFilter_Click;
            officerFilters.ResetRequested += btnClear_Click;
            pendingPager.FirstPage += btnFirst_Click; pendingPager.PreviousPage += btnPrev_Click; pendingPager.NextPage += btnNextPage_Click; pendingPager.LastPage += btnLast_Click;
            officerFilters.ShowAdvanced(Me.Role == Roles.Admin);
            pendingGrid.View.RowDataBound += gv_RowDataBound; pendingGrid.View.RowCommand += gv_RowCommand;
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack) return;
            litOfficerTitle.Text = Enc("अधिकारी मागणी अर्ज — " + RoleLabel(Me.Role));
            litRoleBadge.Text = Enc(RoleLabel(Me.Role)); litOfficerName.Text = Enc(Me.FullName);
            officerFilters.AddStatus("प्रलंबित (माझ्याकडे)", "mine");
            officerFilters.AddStatus("सर्व अर्ज", "all");
            officerFilters.AddStatus("पुढे पाठवलेले", "processed");
            officerFilters.AddStatus("मंजूर", "approved");
            officerFilters.AddStatus("नाकारलेले", "rejected");
            officerFilters.AddStatus("दुरुस्तीसाठी परत", "correction");
            officerFilters.AddStage("सर्व टप्पे", string.Empty); foreach (var s in ZoneStages.All) officerFilters.AddStage(StageOptionLabel(s), s);
            officerFilters.AddService("सर्व सेवा", string.Empty);
            foreach (var s in AppServices.Masters.GetServices()) officerFilters.AddService(s.NameMr, s.Code);
            officerFilters.AddZone("सर्व झोन", string.Empty);
            for (var z = 1; z <= 8; z++) officerFilters.AddZone("झोन " + z, z.ToString());
            officerFilters.AddRoute("सर्व मार्ग", string.Empty); officerFilters.AddRoute("नवीन प्रक्रिया", ProcessingRoutes.Zone); officerFilters.AddRoute("जुने अर्ज", ProcessingRoutes.Legacy);
            officerFilters.ZonePanel.Visible = !ZoneScope.HasValue; // a zone-scoped officer cannot widen the scope from the browser
            var stage = Request.QueryString["stage"]; var route = Request.QueryString["route"];
            var view = Request.QueryString["view"];
            if (string.IsNullOrWhiteSpace(view)) view = OfficerListViews.DefaultForRole(Me.Role);
            if (officerFilters.StatusList.Items.FindByValue(view) == null) view = OfficerListViews.DefaultForRole(Me.Role);
            officerFilters.SelectRoute(route); officerFilters.SelectStage(stage); officerFilters.SelectStatus(view);
            Bind();
        }

        private static string StageOptionLabel(string value)
        {
            switch (value)
            {
                case ZoneStages.JEPending: return "JE तपासणी प्रलंबित";
                case ZoneStages.JECorrectionRequested: return "JE कडून दुरुस्ती मागितली";
                case ZoneStages.OSPending: return "OS तपासणी प्रलंबित";
                case ZoneStages.OSCorrectionRequested: return "OS कडून दुरुस्ती मागितली";
                case ZoneStages.PaymentRequired: return "अर्जदाराचा भरणा प्रलंबित";
                case ZoneStages.AssistantCommissionerApprovalPending: return "सहाय्यक आयुक्त मंजुरी प्रलंबित";
                case ZoneStages.Approved: return "मंजूर";
                case ZoneStages.Rejected: return "नामंजूर";
                default: return value;
            }
        }

        private static DateTime? Day(string s)
        {
            DateTime d; return DateTime.TryParseExact((s ?? "").Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) ? (DateTime?)d : null;
        }

        private ApplicationFilter Filter()
        {
            int z;
            return new ApplicationFilter
            {
                Search = officerFilters.SearchText, OfficerView = officerFilters.StatusList.SelectedValue, Stage = officerFilters.StageList.SelectedValue, ServiceType = officerFilters.ServiceList.SelectedValue, Route = officerFilters.RouteList.SelectedValue,
                Zone = officerFilters.ZonePanel.Visible && int.TryParse(officerFilters.ZoneList.SelectedValue, out z) ? (int?)z : null, From = Day(officerFilters.FromText), To = Day(officerFilters.ToText),
                Page = CurrentPage, PageSize = PageSize, ZoneScope = ZoneScope
            };
        }

        private void Bind()
        {
            var filter = Filter();
            var data = OfficerApplicationPageData.Load(AppServices.Query, Me.Role, Me.UserId, ZoneScope, filter, ex =>
            {
                ErrorLogger.Log(CorrelationId, "OfficerDashboardCounts", ex);
                Notify("आकडेवारी तात्पुरती उपलब्ध नाही. कृपया रिफ्रेश करा.", true);
            });
            var r = data.Applications;
            var pageSummary = PagerSummary.Create(r.Total, r.Page, r.PageSize); CurrentPage = pageSummary.Page;
            pendingGrid.View.DataSource = r.Items; pendingGrid.View.DataBind();
            litApplicationCount.Text = Enc(r.Total.ToString(CultureInfo.InvariantCulture));
            pendingPager.Show(pageSummary);
            officerKpis.Bind(data.Counts); officerKpis.Visible = true;
        }

        private static bool UseDevanagariDigits() { bool enabled; return !bool.TryParse(System.Configuration.ConfigurationManager.AppSettings["UseDevanagariDigits"], out enabled) || enabled; }
        protected void btnLogout_Click(object sender, EventArgs e) { ((SMC.BhoomiMalmatta.Web.SiteMaster)Master).LogoutCurrentUser(); }

        private static string RoleLabel(string role) { if (role == Roles.JE) return "JE"; if (role == Roles.OS) return "OS"; if (role == Roles.AssistantCommissioner) return "AC"; if (role == Roles.Admin) return "Admin"; return "Staff"; }
        protected void gv_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType != DataControlRowType.DataRow) return;
            var item = (ApplicationListItem)e.Row.DataItem;
            var serviceDisplay = ServiceDisplay.Get(item.ServiceType, item.BusinessType, item.OtherBusinessType);
            item.SubServiceName = serviceDisplay.SubServiceName;
            var serviceHtml = Enc(serviceDisplay.ServiceName);
            if (!string.IsNullOrWhiteSpace(serviceDisplay.SubServiceName))
                serviceHtml += "<small class=\"officer-subservice\">उप-सेवा: " + Enc(serviceDisplay.SubServiceName) + "</small>";
            e.Row.Cells[2].Text = serviceHtml;
            e.Row.Cells[1].Text = Enc(OfficerText.OrDash(item.ApplicantName));
            e.Row.Cells[3].Text = Enc(OfficerText.OrDash(item.Mobile));
            var fallback = ResolveUrl("~/Department/ApplicationDetail.aspx?id=" + HttpUtility.UrlEncode(item.Id.ToString(CultureInfo.InvariantCulture)));
            var numberLink = e.Row.FindControl("lnkNumber") as HyperLink;
            if (numberLink != null) { numberLink.Text = item.ApplicationNumber; numberLink.NavigateUrl = fallback; numberLink.Attributes["data-officer-detail-id"] = item.Id.ToString(CultureInfo.InvariantCulture); }
            var viewLink = e.Row.FindControl("lnkView") as HyperLink;
            if (viewLink != null) { viewLink.NavigateUrl = fallback; viewLink.Attributes["data-officer-detail-id"] = item.Id.ToString(CultureInfo.InvariantCulture); }
            e.Row.Cells[4].Text = Enc(OfficerDateFormatter.Format(item.SubmittedAt, false, UseDevanagariDigits()));
            e.Row.Cells[5].Text = "<span class=\"badge " + StageClass(item.Stage, item.Status, item.PaymentStatus) + "\">" + Enc(StageLabel(item.Stage, item.Status, item.PaymentStatus)) + "</span>";
            e.Row.Cells[6].Text = "<span class=\"badge " + Enc(PaymentClass(item.PaymentStatus)) + "\">" + Enc(OfficerText.Payment(item.PaymentStatus)) + "</span>";
            var certLink = e.Row.FindControl("lnkCert") as HyperLink;
            if (certLink != null && item.ProcessingRoute == ProcessingRoutes.Zone && item.Stage == ZoneStages.Approved)
            {
                certLink.Visible = true; certLink.Text = OfficerText.CertificateLink;
                certLink.NavigateUrl = ResolveUrl("~/Download.ashx?type=cert&appId=" + HttpUtility.UrlEncode(item.Id.ToString(CultureInfo.InvariantCulture)));
            }
            var panel = e.Row.FindControl("phRowAction") as PlaceHolder;
            var choices = e.Row.FindControl("cblAction") as CheckBoxList;
            var button = e.Row.FindControl("btnRowAction") as Button;
            var remark = e.Row.FindControl("txtActionRemark") as TextBox;
            if (panel == null || choices == null || button == null) return;
            var options = RowOptions(item.ProcessingRoute, item.Stage, item.PaymentStatus);
            panel.Visible = options.Count > 0;
            foreach (var action in options) choices.Items.Add(new ListItem(OfficerText.Action(action), action));
            // Per-action confirmation and success texts (from OfficerText) for the AJAX path; no extra query per row.
            button.Attributes["data-messages"] = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(
                options.ToDictionary(a => a, a => new { confirm = OfficerText.Confirmation(a, item.ApplicationNumber), success = OfficerText.Success(a, item.ApplicationNumber) }));
            if (IsSingleApprove(options))
            {
                // AC with only "Approve": no radio/checkbox group, one full-width button, optional remark behind "Add remark".
                var choicePanel = e.Row.FindControl("phDecisionChoices") as PlaceHolder; if (choicePanel != null) choicePanel.Visible = false;
                var addRemark = e.Row.FindControl("phAddRemark") as PlaceHolder; if (addRemark != null) addRemark.Visible = true;
                var addRemarkText = e.Row.FindControl("litAddRemark") as Literal; if (addRemarkText != null) addRemarkText.Text = Enc(OfficerText.AddRemark);
                var remarkLabel = e.Row.FindControl("litRemarkLabel") as Literal; if (remarkLabel != null) remarkLabel.Text = Enc(OfficerText.OptionalRemarks);
                button.Text = OfficerText.Action(WorkflowActions.ACApprove);
                button.CssClass = "btn officer-submit officer-approve";
                button.Attributes["data-single-action"] = WorkflowActions.ACApprove;
            }
            button.CommandArgument = e.Row.RowIndex.ToString(CultureInfo.InvariantCulture);
            button.Attributes["data-once"] = "1";
            button.Attributes.Remove("disabled");
            button.Attributes["aria-label"] = "Submit action for application " + item.ApplicationNumber;
            button.Attributes["data-application-number"] = item.ApplicationNumber;
            e.Row.Attributes["data-application-id"] = item.Id.ToString(CultureInfo.InvariantCulture);
            e.Row.Attributes["data-expected-stage"] = item.Stage;
            e.Row.Attributes["data-row-version"] = item.RowVersion;
            button.Attributes["data-confirm-start"] = OfficerText.ConfirmStart; button.Attributes["data-confirm-end"] = OfficerText.ConfirmEnd; button.Attributes["data-confirm-question"] = OfficerText.ConfirmQuestion;
            if (remark != null) remark.Attributes["aria-label"] = "Remarks for application " + item.ApplicationNumber;
        }

        protected void gv_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != "OfficerAction") return;
            var gv = pendingGrid.View;
            int rowIndex; if (!int.TryParse(Convert.ToString(e.CommandArgument, CultureInfo.InvariantCulture), out rowIndex) || rowIndex < 0 || rowIndex >= gv.Rows.Count) { Notify("अर्जाची ओळ अवैध आहे. पुन्हा प्रयत्न करा.", true); return; }
            var keys = gv.DataKeys[rowIndex];
            var id = Convert.ToInt32(keys.Values["Id"], CultureInfo.InvariantCulture);
            var postedStage = Convert.ToString(keys.Values["Stage"], CultureInfo.InvariantCulture);
            var postedVersion = Convert.ToString(keys.Values["RowVersion"], CultureInfo.InvariantCulture);
            var row = gv.Rows[rowIndex];
            var choices = row.FindControl("cblAction") as CheckBoxList;
            var remark = row.FindControl("txtActionRemark") as TextBox;
            string action;
            var options = RowOptions(Convert.ToString(keys.Values["ProcessingRoute"], CultureInfo.InvariantCulture), postedStage, Convert.ToString(keys.Values["PaymentStatus"], CultureInfo.InvariantCulture));
            if (IsSingleApprove(options)) action = WorkflowActions.ACApprove; // single-button row: no checkbox group is rendered
            else if (choices == null || !OfficerDecisionSelection.TryGetSingle(choices.Items.Cast<ListItem>().Where(x => x.Selected).Select(x => x.Value), out action)) { Notify("Select exactly one action.", true); return; }
            var number = Convert.ToString(keys.Values["ApplicationNumber"], CultureInfo.InvariantCulture);
            var result = WorkflowRunner.Run(new WorkflowInput { ApplicationId = id, Action = action, ExpectedStage = postedStage, ExpectedRowVersion = postedVersion, Remarks = remark == null ? null : remark.Text }, Actor());
            if (result.IsFailure) { Notify(result.Error.Code == "REMARK_REQUIRED" ? OfficerText.RemarksRequired : result.Error.Code == "CONCURRENCY_CONFLICT" || result.Error.Code == "STALE_STAGE" ? OfficerText.Changed : result.Error.Message, true); return; }
            Notify(OfficerText.Success(action, number)); CurrentPage = 1; Bind();
        }

        private System.Collections.Generic.List<string> RowOptions(string route, string stage, string paymentStatus)
        {
            var roleCanAct = Me.Role == Roles.JE || Me.Role == Roles.OS || Me.Role == Roles.AssistantCommissioner;
            return roleCanAct ? ZoneWorkflowMachine.OfficerListActions(route, stage, Me.Role, paymentStatus, AppServices.WorkflowPolicy).ToList() : new System.Collections.Generic.List<string>();
        }

        private static bool IsSingleApprove(System.Collections.Generic.IList<string> options)
        { return options.Count == 1 && options[0] == WorkflowActions.ACApprove; }

        private static string StageLabel(string stage, string status, string paymentStatus)
        {
            switch (stage ?? string.Empty)
            {
                case ZoneStages.JEPending: return "JE तपासणीसाठी प्रलंबित";
                case ZoneStages.JECorrectionRequested: return "अर्जदाराकडून दुरुस्ती आवश्यक";
                case ZoneStages.OSPending: return "OS तपासणीसाठी प्रलंबित";
                case ZoneStages.OSCorrectionRequested: return "अर्जदाराकडून दुरुस्ती आवश्यक";
                case ZoneStages.PaymentRequired: return OfficerText.PaymentStagePill(paymentStatus);
                case ZoneStages.AssistantCommissionerApprovalPending: return "सहाय्यक आयुक्त मंजुरीसाठी प्रलंबित";
                case ZoneStages.Approved: return "मंजूर";
                case ZoneStages.Rejected: return "नामंजूर";
                default: return status == ApplicationStatuses.Approved ? "मंजूर (जुना अर्ज)" : status == ApplicationStatuses.Rejected ? "नामंजूर (जुना अर्ज)" : "जुना अर्ज — वाचनापुरता";
            }
        }

        private static string PaymentClass(string status)
        {
            if (status == PaymentStatuses.PaymentDone || status == PaymentStatuses.NotRequired) return "ok";
            if (status == PaymentStatuses.PaymentRequired || status == PaymentStatuses.FeePending) return "warn";
            return string.IsNullOrEmpty(status) ? string.Empty : "info";
        }

        private static string StageClass(string stage, string status, string paymentStatus)
        {
            if (stage == ZoneStages.Approved || status == ApplicationStatuses.Approved) return "ok";
            if (stage == ZoneStages.Rejected || status == ApplicationStatuses.Rejected) return "bad";
            if (stage == ZoneStages.AssistantCommissionerApprovalPending) return "pending-ac";
            if (stage == ZoneStages.JECorrectionRequested || stage == ZoneStages.OSCorrectionRequested) return "correction";
            if (stage == ZoneStages.PaymentRequired) return paymentStatus == PaymentStatuses.PaymentSubmitted ? "payment-verify" : "payment-pending";
            return "warn";
        }

        protected void btnFilter_Click(object sender, EventArgs e) { CurrentPage = 1; Bind(); }
        protected void btnClear_Click(object sender, EventArgs e) { Response.Redirect("~/Department/Applications.aspx"); }
        protected void btnPrev_Click(object sender, EventArgs e) { CurrentPage = Math.Max(1, CurrentPage - 1); Bind(); }
        protected void btnNextPage_Click(object sender, EventArgs e) { CurrentPage = CurrentPage + 1; Bind(); }
        protected void btnFirst_Click(object sender, EventArgs e) { CurrentPage = 1; Bind(); }
        protected void btnLast_Click(object sender, EventArgs e) { var f = Filter(); f.Page = 1; CurrentPage = Math.Max(1, (int)Math.Ceiling((double)AppServices.Query.SearchOfficerApplications(Me.Role, Me.UserId, f).Total / PageSize)); Bind(); }
        protected void btnRefresh_Click(object sender, EventArgs e) { Bind(); }
    }
}
