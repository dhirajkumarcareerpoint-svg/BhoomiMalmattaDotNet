using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Business.Citizen;
using SMC.BhoomiMalmatta.Business.Workflow;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;
using SMC.BhoomiMalmatta.Web.Security;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Web.Citizen
{
    /// <summary>The applicant sees only their own application (application id comes from the encrypted ticket) and never any internal officer data.</summary>
    public partial class DashboardPage : SmcPage
    {
        protected override string[] RequiredRoles { get { return new[] { Roles.Citizen }; } }
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private ApplicationDetail _detail;

        protected void Page_Load(object sender, EventArgs e)
        {
            _detail = AppServices.Query.GetDetail(Me.ApplicationId);
            if (_detail == null) { Response.Redirect("~/Status.aspx"); return; }
            if (!IsPostBack) ViewState["paymentRowVersion"] = _detail.RowVersion;
            EnsureCertificate();
            Render();
        }

        /// <summary>Approved but no letter yet (generation failed at approval time): try once more so a refresh leads to the download button.</summary>
        private void EnsureCertificate()
        {
            var d = _detail;
            if (d.IsLegacy || d.Stage != ZoneStages.Approved || d.HasCertificate || AppServices.Certificates == null) return;
            try
            {
                var issued = AppServices.Certificates.Issue(d.Id, Actor());
                if (issued.IsSuccess) _detail = AppServices.Query.GetDetail(Me.ApplicationId) ?? d;
                else ErrorLogger.Log(CorrelationId, "CertificateGeneration", new InvalidOperationException(issued.Error.Code));
            }
            catch (Exception ex) { ErrorLogger.Log(CorrelationId, "CertificateGeneration", ex); }
        }

        private static string StatusBadge(string status)
        {
            var cls = status == ApplicationStatuses.Approved ? "ok" : status == ApplicationStatuses.Rejected ? "bad" : status == ApplicationStatuses.FeePending ? "warn" : string.Empty;
            return "<span class=\"badge " + cls + "\">" + Enc(StatusMr(status)) + "</span>";
        }

        private static string StatusMr(string s)
        {
            switch (s)
            {
                case ApplicationStatuses.Submitted: return "सादर केला";
                case ApplicationStatuses.UnderScrutiny: return "छाननी सुरू";
                case ApplicationStatuses.FeePending: return "शुल्क भरणा प्रलंबित";
                case ApplicationStatuses.ApprovalPending: return "मंजुरी प्रलंबित";
                case ApplicationStatuses.Approved: return "मंजूर";
                case ApplicationStatuses.Rejected: return "नाकारला";
                default: return s ?? "—";
            }
        }

        private static string PaymentMr(string s)
        {
            return CitizenStatusMapper.PaymentLabel(s);
        }

        private static string LegacyPaymentLabel(string s)
        {
            switch (s)
            {
                case PaymentStatuses.PaymentDone: return "भरणा प्राप्त";
                case PaymentStatuses.PaymentRequired: return "भरणा आवश्यक";
                case PaymentStatuses.PaymentSubmitted: return "भरणा पडताळणीसाठी सादर";
                case PaymentStatuses.PaymentRejected: return "भरणा नाकारला — पुरावा पुन्हा सादर करा";
                case PaymentStatuses.NotRequired: return "लागू नाही";
                case PaymentStatuses.PendingOSAmount: return "रक्कम निश्चित केली जात आहे";
                default: return s ?? "—";
            }
        }

        private void Render()
        {
            var d = _detail;
            var v = ApplicantStatusViewMapper.Map(d);
            var sb = new StringBuilder();
            Action<string, string> row = (k, val) => sb.Append("<dt>").Append(Enc(k)).Append("</dt><dd>").Append(val).Append("</dd>");
            if (!d.IsLegacy)
            {
                sb.Append("<div class=\"citizen-status-summary\"><div><small>अर्ज क्रमांक</small><strong>").Append(Enc(d.ApplicationNumber))
                    .Append("</strong></div><div><small>सेवा</small><strong>").Append(Enc(v.ServiceName));
                if (!string.IsNullOrWhiteSpace(v.SubServiceName)) sb.Append("<small class=\"citizen-subservice\">उप-सेवा: ").Append(Enc(v.SubServiceName)).Append("</small>");
                sb.Append("</strong></div><div><small>सादर दिनांक</small><strong>")
                    .Append(Enc(d.SubmittedAt.HasValue ? ApplicantDateFormatter.Format(d.SubmittedAt.Value.ToLocalTime(), "dd/MM/yyyy HH:mm", ApplicantDateFormatter.UseDevanagariDigits) : "—"))
                    .Append("</strong></div><div><small>").Append(Enc(ApplicantText.CurrentStage)).Append("</small><strong>").Append(Enc(v.CurrentStageLine)).Append("</strong></div></div>")
                    .Append("<ol class=\"citizen-stepper\" aria-label=\"Application review stages\">");
                foreach (var level in v.Levels)
                {
                    sb.Append("<li class=\"citizen-step ").Append(Enc(level.State.ToLowerInvariant())).Append("\"");
                    if (level.State == "Active") sb.Append(" aria-current=\"step\"");
                    sb.Append("><span class=\"citizen-step-marker\" aria-hidden=\"true\">").Append(level.State == "Completed" ? "✓" : level.State == "Rejected" ? "×" : "•")
                        .Append("</span><div><h3>").Append(Enc(level.Level)).Append("</h3><p class=\"citizen-step-status\">").Append(Enc(level.Status)).Append("</p>");
                    if (!string.IsNullOrWhiteSpace(level.ForwardedTo)) sb.Append("<p>").Append(Enc(ApplicantText.ForwardedTo)).Append(": ").Append(Enc(level.ForwardedTo)).Append("</p>");
                    if (!string.IsNullOrWhiteSpace(level.NextStage)) sb.Append("<p>").Append(Enc(ApplicantText.NextStage)).Append(": ").Append(Enc(level.NextStage)).Append("</p>");
                    if (!string.IsNullOrWhiteSpace(level.Remarks))
                    {
                        sb.Append("<p class=\"citizen-step-remark\">");
                        if (level.State == "Correction" || level.State == "Rejected" || level.Status == ApplicantText.PaymentRejected)
                            sb.Append("<strong>").Append(Enc(level.Level == ApplicantText.Level1 ? ApplicantText.JeRemarks : ApplicantText.OsRemarks)).Append(": </strong>");
                        sb.Append(Enc(level.Remarks)).Append("</p>");
                    }
                    if (!string.IsNullOrWhiteSpace(level.ActionRequired)) sb.Append("<p class=\"citizen-step-action\">").Append(Enc(level.ActionRequired)).Append("</p>");
                    sb.Append("</div></li>");
                }
                sb.Append("</ol><dl class=\"kv citizen-application-details\">");
                row("कालावधी", Enc(ApplicantDateFormatter.Format(d.StartDate, "dd/MM/yyyy", ApplicantDateFormatter.UseDevanagariDigits) + " ते " + ApplicantDateFormatter.Format(d.EndDate, "dd/MM/yyyy", ApplicantDateFormatter.UseDevanagariDigits) + " (" + ApplicantDateDisplay.Digits(d.TotalDays.ToString(Inv)) + " दिवस)"));
                row("अर्जाची स्थिती", StatusBadge(v.Status));
                // Name / mobile / e-mail can be empty (Apply.CollectApplicantDetails=false): the rows are then hidden.
                if (!string.IsNullOrWhiteSpace(v.ApplicantName)) row("अर्जदाराचे नाव", Enc(v.ApplicantName));
                if (!string.IsNullOrWhiteSpace(v.Mobile)) row("मोबाईल", Enc(v.Mobile));
                if (!string.IsNullOrWhiteSpace(v.Email)) row("ई-मेल", Enc(v.Email));
                if (!string.IsNullOrWhiteSpace(v.BusinessType)) row("व्यवसायाचा प्रकार", Enc(v.BusinessType));
                if (!string.IsNullOrWhiteSpace(v.Address)) row("पत्रव्यवहाराचा पत्ता", Enc(v.Address));
                row(ApplicantText.PaymentStatus, Enc(PaymentMr(d.PaymentStatus)));
                row("रक्कम", Enc(d.FeeAmount.HasValue ? "₹ " + d.FeeAmount.Value.ToString("N2", Inv) : "निश्चित व्हायची आहे"));
                if (!string.IsNullOrWhiteSpace(d.PaymentReference)) row("UTR / संदर्भ क्रमांक", Enc(d.PaymentReference));
                if (d.PaymentDate.HasValue) row("भरणा दिनांक", Enc(ApplicantDateFormatter.Format(d.PaymentDate.Value, "dd/MM/yyyy", ApplicantDateFormatter.UseDevanagariDigits)));
                if (v.Payment != null && !string.IsNullOrWhiteSpace(v.Payment.ProofFileName)) row("भरणा पुरावा", Enc(v.Payment.ProofFileName));
                sb.Append("</dl>");
                if (v.Certificate != null && v.Certificate.IsApproved)
                {
                    sb.Append("<div class=\"citizen-certificate\"><p><strong>").Append(Enc(v.Certificate.StatusLine)).Append("</strong></p>");
                    if (v.Certificate.HasDownload)
                        sb.Append("<a class=\"btn citizen-certificate-download\" href=\"").Append(ResolveUrl("~/Download.ashx?type=cert")).Append("\">").Append(Enc(v.Certificate.DownloadLabel)).Append("</a>");
                    else
                        sb.Append("<p class=\"notice info\">").Append(Enc(v.Certificate.Text)).Append(". ").Append(Enc(v.Certificate.Hint)).Append("</p>");
                    sb.Append("</div>");
                }
            }
            else sb.Append("<dl class=\"kv\">");
            if (d.IsLegacy)
            {
                row("अर्ज क्रमांक", "<b>" + Enc(d.ApplicationNumber) + "</b>"); row("सेवा", Enc(d.ServiceName ?? d.ServiceType));
                row("सादर दिनांक", Enc(d.SubmittedAt.HasValue ? ApplicantDateFormatter.Format(d.SubmittedAt.Value.ToLocalTime(), "dd/MM/yyyy HH:mm", ApplicantDateFormatter.UseDevanagariDigits) : "—"));
                row("सध्याची स्थिती", StatusBadge(v.Status)); row("सध्याचा स्तर", Enc(v.CurrentLevel)); row("शुल्क स्थिती", Enc(LegacyPaymentLabel(d.PaymentStatus)));
                row("कालावधी", Enc(ApplicantDateFormatter.Format(d.StartDate, "dd/MM/yyyy", ApplicantDateFormatter.UseDevanagariDigits) + " ते " + ApplicantDateFormatter.Format(d.EndDate, "dd/MM/yyyy", ApplicantDateFormatter.UseDevanagariDigits) + " (" + ApplicantDateDisplay.Digits(d.TotalDays.ToString(Inv)) + " दिवस)"));
                row("रक्कम", Enc(d.FeeAmount.HasValue ? "₹ " + d.FeeAmount.Value.ToString("N2", Inv) : "निश्चित व्हायची आहे"));
                row("टीप", "हा जुना अर्ज आहे; तो केवळ पाहण्यासाठी उपलब्ध आहे."); sb.Append("</dl>");
            }
            litSummary.Text = sb.ToString();

            // timeline: only applicant-safe labels (never officer names / IPs / internal remarks)
            var tl = new StringBuilder("<div class=\"citizen-history-service\"><strong>").Append(Enc(v.ServiceName));
            if (!string.IsNullOrWhiteSpace(v.SubServiceName)) tl.Append("<small>उप-सेवा: ").Append(Enc(v.SubServiceName)).Append("</small>");
            tl.Append("</div><ul class=\"tl\">");
            foreach (var t in v.Timeline)
            {
                tl.Append("<li><b>").Append(Enc(t.Event)).Append("</b><small>").Append(Enc(ApplicantDateFormatter.Format(t.AtUtc.ToLocalTime(), "dd/MM/yyyy HH:mm", ApplicantDateFormatter.UseDevanagariDigits))).Append("</small><span class=\"citizen-history-level\">").Append(Enc(t.Level)).Append("</span>");
                if (!string.IsNullOrWhiteSpace(t.Remark)) tl.Append("<div>").Append(Enc(t.Remark)).Append("</div>");
                if (!string.IsNullOrWhiteSpace(t.PaymentReference)) tl.Append("<div>UTR: ").Append(Enc(t.PaymentReference)).Append("</div>");
                if (t.PaymentDate.HasValue) tl.Append("<div>Payment date: ").Append(Enc(ApplicantDateFormatter.Format(t.PaymentDate.Value.ToLocalTime(), "dd/MM/yyyy", ApplicantDateFormatter.UseDevanagariDigits))).Append("</div>");
                if (!string.IsNullOrWhiteSpace(t.ProofFileName)) tl.Append("<div>Proof: ").Append(Enc(t.ProofFileName)).Append("</div>");
                tl.Append("</li>");
            }
            if (v.Timeline.Count == 0) tl.Append("<li class=\"muted\">इतिहास उपलब्ध नाही.</li>");
            tl.Append("</ul>");
            litTimeline.Text = tl.ToString();

            var dl = new StringBuilder("<div class=\"btns\" style=\"flex-direction:column\">");
            Action<string, string> link = (type, text) => dl.Append("<a class=\"btn o\" href=\"").Append(ResolveUrl("~/Download.ashx?type=" + type)).Append("\">").Append(Enc(text)).Append("</a>");
            link("ack", "पोचपावती (Acknowledgement)"); link("application", "सादर केलेला अर्ज");
            if (d.PaymentStatus == PaymentStatuses.PaymentDone) link("receipt", "शुल्क पावती");
            if (v.Certificate != null && v.Certificate.HasDownload) link("cert", ApplicantText.CertificateDownload);
            foreach (var proof in d.Documents.Where(x => x.IsZoneDocument && x.DocumentType == "PaymentProof"))
                dl.Append("<a class=\"btn o\" href=\"").Append(ResolveUrl("~/Download.ashx?type=zdoc&id=" + proof.Id + "&appId=" + d.Id)).Append("\">").Append(Enc("भरणा पुरावा — " + proof.FileName)).Append("</a>");
            dl.Append("</div>");
            litDownloads.Text = dl.ToString();

            var stage = d.Stage ?? string.Empty;
            pnlPayment.Visible = !d.IsLegacy && stage == ZoneStages.PaymentRequired;
            pnlPaymentForm.Visible = pnlPayment.Visible && (d.PaymentStatus == PaymentStatuses.PaymentRequired || d.PaymentStatus == PaymentStatuses.PaymentRejected);
            litPaymentQr.Text = string.Empty;
            litPaymentInstructions.Text = string.Empty;
            if (pnlPayment.Visible)
            {
                var paymentHtml = new StringBuilder();
                var rejectionHtml = string.Empty;
                if (d.PaymentStatus == PaymentStatuses.PaymentSubmitted)
                {
                    paymentHtml.Append("<p class=\"notice info\">UTR ").Append(Enc(d.PaymentReference ?? "")).Append(" (भरणा दिनांक ").Append(Enc(d.PaymentDate.HasValue ? ApplicantDateFormatter.Format(d.PaymentDate.Value, "dd/MM/yyyy", ApplicantDateFormatter.UseDevanagariDigits) : "—"));
                    if (v.Payment.SubmittedAtUtc.HasValue) paymentHtml.Append("; सादर वेळ ").Append(Enc(ApplicantDateFormatter.Format(v.Payment.SubmittedAtUtc.Value.ToLocalTime(), "dd/MM/yyyy HH:mm", ApplicantDateFormatter.UseDevanagariDigits)));
                    if (!string.IsNullOrWhiteSpace(v.Payment.ProofFileName)) paymentHtml.Append("; पुरावा ").Append(Enc(v.Payment.ProofFileName));
                    paymentHtml.Append(") कार्यालयीन पडताळणीसाठी पाठवला आहे.</p>");
                }
                if (d.PaymentStatus == PaymentStatuses.PaymentRejected)
                {
                    var rejected = d.History.LastOrDefault(h => h.Action == AuditActions.PaymentRejectedByOs);
                    rejectionHtml = "<p class=\"notice bad\">" + Enc(rejected == null || string.IsNullOrWhiteSpace(rejected.Remarks) ? "भरणा पुरावा पुन्हा सादर करा." : rejected.Remarks) + "</p>";
                }
                litPayment.Text = paymentHtml.ToString();
                if (pnlPaymentForm.Visible)
                {
                    var configuredQr = AppServices.Setting("Payment.QrImagePath", "");
                    var root = System.Web.HttpContext.Current.Server.MapPath("~/");
                    var qr = new StringBuilder("<div class=\"payment-qr-card\">");
                    if (ApplicantQrAvailability.IsAvailable(configuredQr, root, System.IO.File.Exists))
                    {
                        var handler = ResolveUrl("~/PaymentQr.ashx");
                        qr.Append("<img class=\"payment-qr\" src=\"").Append(Enc(handler)).Append("\" alt=\"Payment QR code\" onerror=\"this.style.display='none';this.nextElementSibling.hidden=false;\" />")
                            .Append("<div class=\"notice payment-qr-missing\" hidden>").Append(Enc(ApplicantText.PaymentQrUnavailable)).Append("</div>")
                            .Append("<a class=\"payment-qr-download\" href=\"").Append(Enc(handler + "?download=1")).Append("\">").Append(Enc(ApplicantText.PaymentQrDownload)).Append("</a>")
                            .Append("<div class=\"payment-qr-caption\">").Append(Enc(ApplicantText.PaymentQrCaption)).Append("</div>");
                    }
                    else qr.Append("<div class=\"notice payment-qr-missing\">").Append(Enc(ApplicantText.PaymentQrUnavailable)).Append("</div>");
                    qr.Append("<div class=\"payment-amount\">").Append(Enc(ApplicantText.PayExactly)).Append(" ₹ ").Append(Enc(d.PayableAmount.ToString("N2", Inv))).Append("</div>")
                        .Append("<div class=\"muted\">अर्ज क्रमांक: ").Append(Enc(d.ApplicationNumber)).Append("</div></div>");
                    litPaymentQr.Text = qr.ToString();
                    litPaymentInstructions.Text = rejectionHtml + "<ol class=\"payment-instructions\"><li>" + Enc(ApplicantText.PaymentInstructionScan) + "</li><li>" + Enc(ApplicantText.PaymentInstructionReference) + "</li><li>" + Enc(ApplicantText.PaymentInstructionProof) + "</li><li>" + Enc(ApplicantText.PaymentInstructionSubmit) + "</li></ol>";
                    var requested = d.History.LastOrDefault(h => h.Action == AuditActions.PaymentRequestSent);
                    if (requested != null) txtPaymentDate.Attributes["min"] = requested.AtUtc.ToLocalTime().ToString("yyyy-MM-dd", Inv);
                    txtPaymentDate.Attributes["max"] = DateTime.Today.ToString("yyyy-MM-dd", Inv);
                }
            }
            var correction = !d.IsLegacy && (stage == ZoneStages.JECorrectionRequested || stage == ZoneStages.OSCorrectionRequested);
            pnlCorrection.Visible = correction;
            if (correction)
            {
                string remark = null;
                for (var i = d.History.Count - 1; i >= 0; i--) if (d.History[i].Action == AuditActions.JEReturnedForCorrection || d.History[i].Action == AuditActions.OSReturnedForCorrection) { remark = d.History[i].Remarks; break; }
                litCorrection.Text = "<p class=\"notice bad\">" + Enc(remark ?? "कृपया अर्जात दुरुस्ती करा.") + "</p>";
            }
        }

        protected void btnResubmit_Click(object sender, EventArgs e)
        {
            var d = AppServices.Query.GetDetail(Me.ApplicationId);
            var uploads = new List<DocumentUpload>();
            if (fuCorrection.HasFile)
            {
                if (fuCorrection.PostedFile.ContentLength > Business.Security.FileValidator.MaxBytes) { Notify("फाईलचा आकार ५ MB पेक्षा जास्त आहे.", true); return; }
                uploads.Add(new DocumentUpload { FileName = fuCorrection.FileName, ContentType = fuCorrection.PostedFile.ContentType, Content = fuCorrection.FileBytes });
            }
            var r = AppServices.Submission.Resubmit(d.Id, d.Stage, txtReply.Text, uploads, Actor(), AppServices.Workflow, d.ApplicationNumber);
            if (r.IsFailure) { Notify(r.Error.Message, true); return; }
            Notify("दुरुस्त अर्ज पुन्हा सादर केला आहे.");
            txtReply.Text = string.Empty;
            _detail = AppServices.Query.GetDetail(Me.ApplicationId); Render();
        }

        protected void btnSubmitPayment_Click(object sender, EventArgs e)
        {
            DateTime paymentDate;
            if (!DateTime.TryParseExact((txtPaymentDate.Text ?? "").Trim(), "yyyy-MM-dd", Inv, DateTimeStyles.None, out paymentDate))
            { Notify("वैध भरणा दिनांक निवडा.", true); return; }
            if (!fuPaymentProof.HasFile)
            { Notify("भरण्याचा पुरावा अपलोड करा.", true); return; }

            var posted = new DocumentUpload { DocumentType = "PaymentProof", FileName = fuPaymentProof.FileName, ContentType = fuPaymentProof.PostedFile.ContentType, Content = fuPaymentProof.FileBytes };
            var result = AppServices.Workflow.Execute(new WorkflowCommand
            {
                ApplicationId = Me.ApplicationId, Action = WorkflowActions.CitizenSubmitPayment, ExpectedStage = ZoneStages.PaymentRequired,
                ExpectedRowVersion = ViewState["paymentRowVersion"] as string, PaymentReference = txtUtr.Text, PaymentDate = paymentDate, PaymentProof = posted
            }, Actor());
            if (result.IsFailure) { Notify(result.Error.Message, true); return; }
            txtUtr.Text = string.Empty; txtPaymentDate.Text = string.Empty;
            _detail = AppServices.Query.GetDetail(Me.ApplicationId);
            ViewState["paymentRowVersion"] = _detail.RowVersion;
            Render();
            Notify("भरणा तपशील पडताळणीसाठी सादर केले.");
        }

    }
}
