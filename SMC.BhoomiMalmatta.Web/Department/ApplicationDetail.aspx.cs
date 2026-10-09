using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web.UI.WebControls;
using SMC.BhoomiMalmatta.Business.Workflow;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;
using SMC.BhoomiMalmatta.Web.Security;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Web.Department
{
    /// <summary>
    /// Officer view of one application. The actions offered are computed from the server-side state machine for the signed-in role and zone;
    /// the same checks run again when an action is posted (the browser's selection is never trusted).
    /// </summary>
    public partial class ApplicationDetailPage : DeptPage
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private ApplicationDetail _d;
        private int AppId { get { int id; return int.TryParse(Request.QueryString["id"], out id) ? id : 0; } }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!LoadDetail()) return;
            if (!IsPostBack)
            {
                foreach (var m in new[] { "रोख (Cash)", "UPI", "NEFT / RTGS", "DD / Cheque", "इतर" }) ddlMode.Items.Add(new ListItem(m, m));
                BuildActions();
            }
            Render();
        }

        private bool LoadDetail()
        {
            _d = AppId == 0 ? null : AppServices.Query.GetDetail(AppId);
            // zone-scoped officers cannot open other zones even by editing the URL
            if (_d == null || (ZoneScope.HasValue && _d.Zone != ZoneScope.Value.ToString())) { Response.Redirect("~/Department/Applications.aspx"); return false; }
            return true;
        }

        private System.Collections.Generic.List<string> Allowed()
        {
            if (_d.IsLegacy) return new System.Collections.Generic.List<string>();
            return ZoneWorkflowMachine.AllowedActions(_d.Stage, Me.Role, _d.PaymentStatus, AppServices.WorkflowPolicy).Where(a => a != WorkflowActions.CitizenResubmit).ToList();
        }

        private void BuildActions()
        {
            ddlAction.Items.Clear();
            foreach (var a in Allowed()) ddlAction.Items.Add(new ListItem(WorkflowRunner.Label(a), a));
            pnlAction.Visible = ddlAction.Items.Count > 0;
            pnlRate.Visible = Me.Role == Roles.OS && Allowed().Contains(WorkflowActions.OSRequestPayment);
            pnlPay.Visible = Allowed().Contains(WorkflowActions.OSConfirmPayment) || Allowed().Contains(WorkflowActions.OSRejectPayment);
        }

        protected void btnExecute_Click(object sender, EventArgs e)
        {
            var action = ddlAction.SelectedValue;
            decimal rate; decimal? r = decimal.TryParse((txtRate.Text ?? "").Trim(), NumberStyles.Number, Inv, out rate) ? (decimal?)rate : null;
            var result = WorkflowRunner.Run(new WorkflowInput
            {
                ApplicationId = _d.Id, Action = action, ExpectedStage = _d.Stage, ExpectedRowVersion = _d.RowVersion, Remarks = txtRemarks.Text,
                OfficerRate = r, PaymentReference = pnlPay.Visible ? _d.PaymentReference : null, PaymentMode = ddlMode.SelectedValue
            }, Actor());
            if (result.IsFailure) { Notify(result.Error.Message, true); _d = AppServices.Query.GetDetail(AppId); BuildActions(); Render(); return; }
            Notify(result.Value.WasAlreadyApplied ? "हा भरणा आधीच नोंदवलेला आहे." : OfficerText.Success(action, _d.ApplicationNumber));
            txtRemarks.Text = string.Empty; txtReference.Text = string.Empty; txtRate.Text = string.Empty;
            _d = AppServices.Query.GetDetail(AppId); BuildActions(); Render();
        }

        protected void btnCert_Click(object sender, EventArgs e)
        {
            if (Me.Role != Roles.AssistantCommissioner && Me.Role != Roles.Admin) return;
            var r = AppServices.Certificates.Issue(_d.Id, Actor());
            Notify(r.IsSuccess ? "प्रमाणपत्र तयार केले." : r.Error.Message, r.IsFailure);
            _d = AppServices.Query.GetDetail(AppId); Render();
        }

        private static string Money(decimal? v) { return v.HasValue ? "₹ " + v.Value.ToString("N2", Inv) : "—"; }

        private void Render()
        {
            var d = _d;
            txtReference.Text = d.PaymentReference ?? string.Empty;
            pnlLegacy.Visible = d.IsLegacy;
            var sb = new StringBuilder("<dl class=\"kv\">");
            Action<string, string> row = (k, v) => sb.Append("<dt>").Append(Enc(k)).Append("</dt><dd>").Append(Enc(v)).Append("</dd>");
            row("अर्ज क्रमांक", d.ApplicationNumber); row("मार्ग", d.ProcessingRoute); row("सेवा", (d.ServiceName ?? d.ServiceType) + (string.IsNullOrEmpty(d.BusinessType) ? "" : " / " + d.BusinessType));
            row("अर्जदाराचा प्रकार", d.ApplicantType); row("अर्जदाराचे पूर्ण नाव", OfficerText.OrDash(d.ApplicantName)); row("मोबाईल", OfficerText.OrDash(d.Mobile));
            row("ई-मेल", OfficerText.OrDash(d.Email)); row("आधार/ओळखपत्र क्रमांक", d.IdentityNumber); row("PAN", d.PanNumber); row("GST", d.GstNumber);
            row("कायमचा पत्ता", d.PermanentAddress); row("पत्रव्यवहार पत्ता", d.CorrespondenceAddress);
            row("राज्य / जिल्हा", d.State + " / " + d.District); row("शहर / तालुका / पिन", d.City + " / " + d.Taluka + " / " + d.PinCode);
            row("झोन", d.Zone); row("जागा", d.Location); row("वर्णन", d.ServiceDescription);
            row("कालावधी", d.StartDate.ToString("dd/MM/yyyy", Inv) + " ते " + d.EndDate.ToString("dd/MM/yyyy", Inv) + " (" + d.TotalDays + " दिवस)");
            row("लांबी × रुंदी = क्षेत्रफळ", (d.LengthFt.HasValue ? d.LengthFt.Value.ToString("0.##", Inv) : "—") + " × " + (d.WidthFt.HasValue ? d.WidthFt.Value.ToString("0.##", Inv) : "—") + " = " + (d.AreaSqFt.HasValue ? d.AreaSqFt.Value.ToString("0.##", Inv) : "—"));
            row("दर", Money(d.CalculatedRate)); row("रक्कम", Money(d.FeeAmount)); row("देय रक्कम (कार्यालय)", Money(d.PayableAmount));
            row("स्थिती / टप्पा", d.Status + " / " + (d.Stage ?? "—")); row("शुल्क स्थिती", PaymentLabel(d.PaymentStatus)); row("UTR / संदर्भ", d.PaymentReference ?? "—");
            row("भरणा दिनांक", d.PaymentDate.HasValue ? d.PaymentDate.Value.ToString("dd/MM/yyyy", Inv) : "—");
            row("पडताळणी अधिकारी", d.PaymentVerifier ?? "—"); row("पडताळणी दिनांक", d.PaymentConfirmedAt.HasValue ? d.PaymentConfirmedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Inv) : "—");
            sb.Append("</dl>");
            litDetail.Text = sb.ToString();

            var docs = new StringBuilder();
            foreach (var x in d.Documents)
            {
                var href = x.IsZoneDocument ? "~/Download.ashx?type=zdoc&id=" + x.Id + "&appId=" + d.Id : "~/Download.ashx?type=doc&id=" + x.Id;
                docs.Append("<div><a href=\"").Append(ResolveUrl(href)).Append("\">").Append(Enc(x.DocumentType + " — " + x.FileName)).Append("</a>");
                if (x.IsZoneDocument && (x.ContentType == "image/jpeg" || x.ContentType == "image/png"))
                    docs.Append("<div><img class=\"payment-proof-preview\" src=\"").Append(ResolveUrl(href + "&inline=1")).Append("\" alt=\"भरणा पुरावा\" loading=\"lazy\" /></div>");
                if (!string.IsNullOrEmpty(x.VerificationStatus)) docs.Append(" <span class=\"badge\">").Append(Enc(x.VerificationStatus)).Append("</span>");
                docs.Append("</div>");
            }
            litDocs.Text = d.Documents.Count == 0 ? "<span class=\"muted\">कागदपत्रे नाहीत.</span>" : docs.ToString();

            var hist = new StringBuilder("<ul class=\"tl\">");
            foreach (var h in d.History)
            {
                hist.Append("<li><b>").Append(Enc(h.Action)).Append("</b><small>").Append(Enc(h.AtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Inv))).Append(" — ").Append(Enc(h.By ?? "")).Append(string.IsNullOrEmpty(h.FromRole) ? "" : " (" + Enc(h.FromRole) + " → " + Enc(h.ToRole ?? "") + ")").Append("</small>");
                if (!string.IsNullOrEmpty(h.PreviousStatus) || !string.IsNullOrEmpty(h.NewStatus)) hist.Append("<small>").Append(Enc(h.PreviousStatus ?? "")).Append(" → ").Append(Enc(h.NewStatus ?? "")).Append("</small>");
                if (!string.IsNullOrWhiteSpace(h.Remarks)) hist.Append("<div>").Append(Enc(h.Remarks)).Append("</div>");
                hist.Append("</li>");
            }
            hist.Append("</ul>");
            litHistory.Text = d.History.Count == 0 ? "<span class=\"muted\">इतिहास उपलब्ध नाही.</span>" : hist.ToString();

            // The download handler generates a missing letter on first request, so an approved application always gets a link.
            pnlCert.Visible = !d.IsLegacy && d.Stage == ZoneStages.Approved;
            btnCert.Visible = Me.Role == Roles.AssistantCommissioner || Me.Role == Roles.Admin;
            if (pnlCert.Visible)
                litCert.Text = "<p><a href=\"" + ResolveUrl("~/Download.ashx?type=cert&appId=" + d.Id) + "\">" + Enc(OfficerText.CertificateLink) + "</a></p>";
        }

        private static string PaymentLabel(string status)
        {
            switch (status)
            {
                case PaymentStatuses.NotRequired: return "लागू नाही";
                case PaymentStatuses.PaymentRequired: return "भरणा आवश्यक";
                case PaymentStatuses.PaymentSubmitted: return "OS पडताळणी प्रलंबित";
                case PaymentStatuses.PaymentRejected: return "भरणा नाकारला — पुरावा पुन्हा सादर करा";
                case PaymentStatuses.PaymentDone: return "भरणा प्राप्त";
                case PaymentStatuses.PendingOSAmount: return "रक्कम निश्चिती प्रलंबित";
                default: return status ?? "—";
            }
        }
    }
}
