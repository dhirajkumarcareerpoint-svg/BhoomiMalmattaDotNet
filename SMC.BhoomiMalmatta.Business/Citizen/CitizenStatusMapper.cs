using System;
using System.Collections.Generic;
using System.Linq;
using SMC.BhoomiMalmatta.Business.Workflow;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;

namespace SMC.BhoomiMalmatta.Business.Citizen
{
    /// <summary>All applicant-facing English workflow text is centralized here for a future Marathi translation.</summary>
    public static class ApplicantText
    {
        public const string Level1 = "Level 1 – JE";
        public const string Level2 = "Level 2 – OS";
        public const string Level3 = "Level 3 – Assistant Commissioner";
        public const string Applicant = "Applicant";
        public const string OsReview = "OS Review";
        public const string AcApproval = "Assistant Commissioner Approval";
        public const string Waiting = "Waiting";
        public const string Pending = "Pending / Under Review";
        public const string CorrectionRequired = "Correction Required";
        public const string CorrectionAction = "Action Required: Please correct the application and resubmit.";
        public const string ResubmittedJe = "Resubmitted / Pending JE Review";
        public const string ResubmittedOs = "Resubmitted / Pending OS Review";
        public const string Approved = "Approved";
        public const string PaymentRequired = "Payment Required";
        public const string PaymentRejected = "Payment Rejected";
        public const string PaymentSubmitted = "Payment Submitted / Under Verification";
        public const string PaymentDone = "Approved / Payment Done";
        public const string ReturnedToOs = "Returned for re-review at Level 2";
        public const string PayExactly = "Pay exactly";
        public const string PaymentQrCaption = "QR स्कॅन करून पेमेंट करा";
        public const string PaymentQrDownload = "QR डाउनलोड करा";
        public const string PaymentQrUnavailable = "QR कोड सध्या उपलब्ध नाही. कृपया कार्यालयाशी संपर्क करा.";
        public const string PaymentInstructionScan = "QR स्कॅन करून वरील रक्कम भरा.";
        public const string PaymentInstructionReference = "UTR / संदर्भ क्रमांक आणि भरणा दिनांक नोंदवा.";
        public const string PaymentInstructionProof = "पावतीचा स्पष्ट फोटो किंवा PDF जोडा.";
        public const string PaymentInstructionSubmit = "भरणा पडताळणीसाठी सादर करा दाबा.";
        public const string PaymentForm = "Submit payment details";
        public const string CertificatePreparing = "मंजुरी पत्र तयार होत आहे";
        public const string CertificateRefreshHint = "कृपया काही वेळाने हे पान रिफ्रेश करा.";
        public const string CertificateLabel = "Certificate / मंजुरी पत्र";
        public const string CertificateDownload = "मंजुरी पत्र डाउनलोड करा";
        public const string ApplicationStatusLabel = "Application Status";
        public const string CurrentStage = "Current stage";
        public const string ForwardedTo = "Forwarded To";
        public const string NextStage = "Next Stage";
        public const string JeRemarks = "JE Remarks";
        public const string OsRemarks = "OS Remarks";
        public const string PaymentStatus = "देयक स्थिती";
        public const string PaymentDoneLabel = "Payment Done";
        public const string PaymentSubmittedLabel = "Payment Submitted";
        public const string PaymentRequiredLabel = "Payment Required";
        public const string PaymentRejectedLabel = "Payment Rejected";
        public const string NotRequiredLabel = "Not required";
        public const string PendingOsAmountLabel = "Pending OS amount";
        public const string UnknownPaymentStatus = "Payment status unavailable";
        public const string Rejected = "Rejected";
        public const string Submitted = "Submitted";
        public const string CorrectionEvent = "Correction Required";
        public const string ResubmittedEvent = "Resubmitted";
        public const string JeForwardedEvent = "Approved - forwarded to OS";
        public const string PaymentRequestEvent = "Payment Required";
        public const string PaymentSubmittedEvent = "Payment Submitted";
        public const string PaymentDoneEvent = "Payment Done";
        public const string ForwardedAcEvent = "Forwarded to AC";
        public const string FinalApprovedEvent = "Final Approved";
        public const string RejectedEvent = "Rejected";
        public const string PaymentNotRequired = "Payment Not Required";
    }

    public sealed class CitizenTimelineItem
    {
        public DateTime AtUtc { get; set; }
        public string ActionMr { get; set; }
        public string Level { get; set; }
        public string Event { get; set; }
        public string Remark { get; set; }
        public string PaymentReference { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string ProofFileName { get; set; }
    }

    public sealed class ApplicantLevelCard
    {
        public string Level { get; set; }
        public string Status { get; set; }
        public string State { get; set; }
        public string Remarks { get; set; }
        public string ActionRequired { get; set; }
        public string NextStage { get; set; }
        public string ForwardedTo { get; set; }
    }

    public sealed class ApplicantPaymentBlock
    {
        public string Status { get; set; }
        public decimal Amount { get; set; }
        public string Reference { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string ProofFileName { get; set; }
        public DateTime? SubmittedAtUtc { get; set; }
        public bool ShowForm { get; set; }
        public bool IsRecord { get; set; }
    }

    public sealed class ApplicantCertificateBlock
    {
        public bool IsApproved { get; set; }
        public bool HasDownload { get; set; }
        public string Text { get; set; }
        /// <summary>"Application Status: Approved" line shown with the letter.</summary>
        public string StatusLine { get; set; }
        public string DownloadLabel { get; set; }
        /// <summary>Refresh hint while the letter is still being prepared; null once it can be downloaded.</summary>
        public string Hint { get; set; }
    }

    public sealed class ApplicantDocumentItem
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public long SizeBytes { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class CitizenStatusView
    {
        public string ApplicationNumber { get; set; }
        public string Status { get; set; }
        public string CurrentLevel { get; set; }
        public string PaymentStatus { get; set; }
        public DateTime? SubmittedAtUtc { get; set; }
        public List<CitizenTimelineItem> Timeline { get; set; }
        public bool IsFinal { get; set; }
        public bool IsLegacy { get; set; }
        public bool ShowCorrectionForm { get; set; }
        public string ServiceName { get; set; }
        public string SubServiceName { get; set; }
        public string CurrentStageLine { get; set; }
        public List<ApplicantLevelCard> Levels { get; set; }
        public ApplicantPaymentBlock Payment { get; set; }
        public ApplicantCertificateBlock Certificate { get; set; }
        public List<ApplicantDocumentItem> Documents { get; set; }
        public string ApplicantName { get; set; }
        public string BusinessType { get; set; }
        public string Mobile { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalDays { get; set; }
        public decimal? Amount { get; set; }
    }

    /// <summary>Pure applicant-safe projection. It intentionally drops officer identity, IP, correlation, hashes and AC-return remarks.</summary>
    public static class CitizenStatusMapper
    {
        private static readonly HashSet<string> RemarkVisible = new HashSet<string>
        { AuditActions.JEReturnedForCorrection, AuditActions.OSReturnedForCorrection, AuditActions.JERejected, AuditActions.OSRejected, AuditActions.PaymentRejectedByOs, AuditActions.ACRejected, AuditActions.CitizenResubmitted };

        private static readonly Dictionary<string, string> EventLabels = new Dictionary<string, string>
        {
            { AuditActions.ApplicationSubmitted, ApplicantText.Submitted },
            { AuditActions.JEVerified, ApplicantText.JeForwardedEvent },
            { AuditActions.JEReturnedForCorrection, ApplicantText.CorrectionEvent },
            { AuditActions.OSReturnedForCorrection, ApplicantText.CorrectionEvent },
            { AuditActions.JERejected, ApplicantText.RejectedEvent },
            { AuditActions.OSRejected, ApplicantText.RejectedEvent },
            { AuditActions.ACRejected, ApplicantText.RejectedEvent },
            { AuditActions.CitizenResubmitted, ApplicantText.ResubmittedEvent },
            { AuditActions.PaymentRequestSent, ApplicantText.PaymentRequestEvent },
            { AuditActions.PaymentSubmittedByApplicant, ApplicantText.PaymentSubmittedEvent },
            { AuditActions.PaymentDone, ApplicantText.PaymentDoneEvent },
            { AuditActions.ForwardedToAC, ApplicantText.ForwardedAcEvent },
            { AuditActions.FinalApproved, ApplicantText.FinalApprovedEvent },
            { AuditActions.PaymentRejectedByOs, ApplicantText.CorrectionEvent },
            { AuditActions.PaymentNotRequired, ApplicantText.PaymentNotRequired }
        };

        public static string LevelFor(string stage)
        {
            switch (stage)
            {
                case ZoneStages.JEPending: case ZoneStages.JECorrectionRequested: return ApplicantText.Level1;
                case ZoneStages.OSPending: case ZoneStages.OSCorrectionRequested: case ZoneStages.PaymentRequired: return ApplicantText.Level2;
                case ZoneStages.AssistantCommissionerApprovalPending: case ZoneStages.Approved: case ZoneStages.Rejected: return ApplicantText.Level3;
                default: return "—";
            }
        }

        public static string PaymentLabel(string status)
        {
            switch (status)
            {
                case PaymentStatuses.PaymentDone: return ApplicantText.PaymentDoneLabel;
                case PaymentStatuses.PaymentSubmitted: return ApplicantText.PaymentSubmittedLabel;
                case PaymentStatuses.PaymentRequired: return ApplicantText.PaymentRequiredLabel;
                case PaymentStatuses.PaymentRejected: return ApplicantText.PaymentRejectedLabel;
                case PaymentStatuses.NotRequired: return ApplicantText.NotRequiredLabel;
                case PaymentStatuses.PendingOSAmount: return ApplicantText.PendingOsAmountLabel;
                default: return ApplicantText.UnknownPaymentStatus;
            }
        }

        public static CitizenStatusView Map(string applicationNumber, string stage, string paymentStatus, DateTime? submittedAtUtc, string historyJson)
        {
            var entries = ActionHistory.Parse(historyJson);
            var history = entries.Select(ToTimeline).Where(x => x != null).ToList();
            return Build(applicationNumber, stage, paymentStatus, submittedAtUtc, history, null, entries.Any(x => x.Action == AuditActions.ACReturned));
        }

        public static CitizenStatusView MapDetail(ApplicationDetail d)
        {
            if (d == null) throw new ArgumentNullException("d");
            var history = new List<CitizenTimelineItem>();
            var wasAcReturned = false;
            if (!d.IsLegacy)
                foreach (var h in d.History ?? new List<HistoryItem>())
                {
                    if (h.Action == AuditActions.ACReturned) wasAcReturned = true;
                    var item = ToTimeline(h);
                    if (item != null) history.Add(item);
                }
            var result = Build(d.ApplicationNumber, d.Stage, d.PaymentStatus, d.SubmittedAt, history, d, wasAcReturned);
            result.IsLegacy = d.IsLegacy;
            result.ShowCorrectionForm = !d.IsLegacy && (d.Stage == ZoneStages.JECorrectionRequested || d.Stage == ZoneStages.OSCorrectionRequested);
            result.ServiceName = d.ServiceName ?? d.ServiceType;
            result.Documents = (d.Documents ?? new List<DocumentItem>()).Where(x => !x.IsZoneDocument || x.DocumentType == "PaymentProof")
                .Select(x => new ApplicantDocumentItem { Id = x.Id, FileName = x.FileName, ContentType = x.ContentType, SizeBytes = x.SizeBytes, CreatedAt = x.CreatedAt }).ToList();
            result.ApplicantName = d.ApplicantName ?? string.Empty; result.BusinessType = d.BusinessType; result.Mobile = MaskMobile(d.Mobile); result.Email = MaskEmail(d.Email);
            result.Address = d.CorrespondenceAddress;
            result.StartDate = d.StartDate; result.EndDate = d.EndDate; result.TotalDays = d.TotalDays; result.Amount = d.FeeAmount;
            var proof = d.Documents.Where(x => x.IsZoneDocument && x.DocumentType == "PaymentProof").OrderByDescending(x => x.CreatedAt).Select(x => x.FileName).FirstOrDefault();
            // A submission recorded with its own UTR/date keeps them (an earlier, rejected submission stays visible); older entries without them
            // show the current values. Only the active proof is listed, so its name is shown only on the entry that carries the current UTR.
            foreach (var item in result.Timeline.Where(x => x.Event == ApplicantText.PaymentSubmittedEvent || x.Event == ApplicantText.PaymentDoneEvent))
            {
                if (item.PaymentReference == null) { item.PaymentReference = d.PaymentReference; item.PaymentDate = d.PaymentDate; }
                item.ProofFileName = string.Equals(item.PaymentReference, d.PaymentReference, StringComparison.Ordinal) ? proof : null;
            }
            if (d.IsLegacy) { result.Levels = new List<ApplicantLevelCard>(); result.CurrentStageLine = d.Status; result.Payment = null; result.Certificate = null; result.ShowCorrectionForm = false; result.PaymentStatus = d.PaymentStatus; }
            return result;
        }

        private static CitizenStatusView Build(string number, string stage, string paymentStatus, DateTime? submitted, List<CitizenTimelineItem> history, ApplicationDetail d, bool wasAcReturned)
        {
            stage = stage ?? string.Empty;
            var last = history == null || history.Count == 0 ? null : history[history.Count - 1];
            var levels = new List<ApplicantLevelCard>
            {
                Card(ApplicantText.Level1, ApplicantText.Waiting, "Waiting"),
                Card(ApplicantText.Level2, ApplicantText.Waiting, "Waiting"),
                Card(ApplicantText.Level3, ApplicantText.Waiting, "Waiting")
            };
            var pay = new ApplicantPaymentBlock { Status = paymentStatus, Amount = d == null ? 0 : d.PayableAmount, Reference = d == null ? null : d.PaymentReference, PaymentDate = d == null ? null : d.PaymentDate };
            pay.IsRecord = paymentStatus == PaymentStatuses.PaymentDone;
            var certApproved = stage == ZoneStages.Approved;
            var certReady = certApproved && d != null && d.HasCertificate;
            var certificate = new ApplicantCertificateBlock
            {
                IsApproved = certApproved, HasDownload = certReady, Text = certApproved && !certReady ? ApplicantText.CertificatePreparing : ApplicantText.CertificateLabel,
                StatusLine = certApproved ? ApplicantText.ApplicationStatusLabel + ": " + ApplicantText.Approved : null,
                DownloadLabel = certReady ? ApplicantText.CertificateDownload : null, Hint = certApproved && !certReady ? ApplicantText.CertificateRefreshHint : null
            };
            var currentLine = LevelFor(stage);

            switch (stage)
            {
                case ZoneStages.JEPending:
                    levels[0] = Card(ApplicantText.Level1, last != null && last.Event == ApplicantText.ResubmittedEvent ? ApplicantText.ResubmittedJe : ApplicantText.Pending, "Active");
                    break;
                case ZoneStages.JECorrectionRequested:
                    levels[0] = Card(ApplicantText.Level1, ApplicantText.CorrectionRequired, "Correction"); levels[0].Remarks = LatestRemark(history, ApplicantText.Level1, ApplicantText.CorrectionEvent); levels[0].ActionRequired = ApplicantText.CorrectionAction;
                    break;
                case ZoneStages.OSPending:
                    levels[0] = Card(ApplicantText.Level1, ApplicantText.Approved, "Completed"); levels[0].ForwardedTo = ApplicantText.Level2; levels[0].NextStage = ApplicantText.OsReview;
                    levels[1] = Card(ApplicantText.Level2, last != null && last.Event == ApplicantText.ResubmittedEvent ? ApplicantText.ResubmittedOs : ApplicantText.Pending, "Active");
                    // Legacy-state rows only (verification now forwards to AC automatically): no "awaiting forwarding" line.
                    if (paymentStatus == PaymentStatuses.PaymentDone) { levels[1].Status = ApplicantText.PaymentDone; levels[1].NextStage = ApplicantText.AcApproval; }
                    if (d != null && d.History != null) pay.SubmittedAtUtc = UtcOfLast(d.History, AuditActions.PaymentSubmittedByApplicant);
                    break;
                case ZoneStages.OSCorrectionRequested:
                    levels[0] = Card(ApplicantText.Level1, ApplicantText.Approved, "Completed");
                    levels[1] = Card(ApplicantText.Level2, ApplicantText.CorrectionRequired, "Correction"); levels[1].Remarks = LatestRemark(history, ApplicantText.Level2, ApplicantText.CorrectionEvent); levels[1].ActionRequired = ApplicantText.CorrectionAction;
                    break;
                case ZoneStages.PaymentRequired:
                    levels[0] = Card(ApplicantText.Level1, ApplicantText.Approved, "Completed");
                    levels[1] = Card(ApplicantText.Level2, paymentStatus == PaymentStatuses.PaymentSubmitted ? ApplicantText.PaymentSubmitted : paymentStatus == PaymentStatuses.PaymentRejected ? ApplicantText.PaymentRejected : ApplicantText.PaymentRequired, paymentStatus == PaymentStatuses.PaymentSubmitted ? "Active" : "Payment");
                    pay.ShowForm = paymentStatus == PaymentStatuses.PaymentRequired || paymentStatus == PaymentStatuses.PaymentRejected;
                    pay.IsRecord = paymentStatus == PaymentStatuses.PaymentSubmitted;
                    pay.SubmittedAtUtc = d == null ? (DateTime?)null : UtcOfLast(d.History, AuditActions.PaymentSubmittedByApplicant);
                    pay.ProofFileName = d == null ? null : d.Documents.Where(x => x.IsZoneDocument && x.DocumentType == "PaymentProof").OrderByDescending(x => x.CreatedAt).Select(x => x.FileName).FirstOrDefault();
                    var paymentReject = LatestRemark(history, ApplicantText.Level2, ApplicantText.CorrectionEvent);
                    if (!string.IsNullOrWhiteSpace(paymentReject)) levels[1].Remarks = paymentReject;
                    break;
                case ZoneStages.AssistantCommissionerApprovalPending:
                    // Directly after OS payment verification: L2 completed, L3 pending immediately.
                    levels[0] = Card(ApplicantText.Level1, ApplicantText.Approved, "Completed");
                    levels[1] = Card(ApplicantText.Level2, paymentStatus == PaymentStatuses.NotRequired ? ApplicantText.Approved : ApplicantText.PaymentDone, "Completed");
                    levels[1].ForwardedTo = ApplicantText.Level3; levels[1].NextStage = ApplicantText.AcApproval;
                    levels[2] = Card(ApplicantText.Level3, ApplicantText.Pending, "Active"); break;
                case ZoneStages.Approved:
                    levels[0] = Card(ApplicantText.Level1, ApplicantText.Approved, "Completed"); levels[1] = Card(ApplicantText.Level2, ApplicantText.PaymentDone, "Completed"); levels[2] = Card(ApplicantText.Level3, ApplicantText.Approved, "Completed");
                    pay.IsRecord = true; break;
                case ZoneStages.Rejected:
                    var reject = history.LastOrDefault(x => x.Event == ApplicantText.RejectedEvent);
                    var rejectLevel = reject == null ? 0 : reject.Level == ApplicantText.Level1 ? 0 : reject.Level == ApplicantText.Level2 ? 1 : 2;
                    for (var i = 0; i < rejectLevel; i++) { levels[i].Status = i == 1 ? ApplicantText.PaymentDone : ApplicantText.Approved; levels[i].State = "Completed"; }
                    levels[rejectLevel] = Card(levels[rejectLevel].Level, ApplicantText.Rejected, "Rejected"); levels[rejectLevel].Remarks = reject == null ? null : reject.Remark; break;
            }

            // AC returns go back to OS and must not expose the AC's internal remark.
            if (wasAcReturned)
            { levels[2] = Card(ApplicantText.Level3, ApplicantText.ReturnedToOs, "Waiting"); levels[1] = Card(ApplicantText.Level2, ApplicantText.Pending, "Active"); }

            return new CitizenStatusView
            {
                ApplicationNumber = number, Status = d == null ? ZoneWorkflowMachine.StatusFor(stage) : d.Status,
                CurrentLevel = LevelFor(stage), PaymentStatus = PaymentLabel(paymentStatus), SubmittedAtUtc = submitted,
                Timeline = history ?? new List<CitizenTimelineItem>(), IsFinal = ZoneStages.IsFinal(stage),
                CurrentStageLine = currentLine, Levels = levels, Payment = pay, Certificate = certificate
            };
        }

        private static ApplicantLevelCard Card(string level, string status, string state) { return new ApplicantLevelCard { Level = level, Status = status, State = state }; }
        public static string MaskMobile(string value)
        {
            var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
            if (digits.Length == 0) return string.Empty;
            if (digits.Length <= 4) return new string('*', digits.Length); // too short to reveal any digit
            var visible = digits.Substring(digits.Length - 4);
            return new string('*', Math.Max(6, digits.Length - visible.Length)) + visible;
        }
        public static string MaskEmail(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var email = value.Trim(); var at = email.LastIndexOf('@');
            if (at <= 0 || at == email.Length - 1) return email.Length <= 2 ? new string('*', email.Length) : email.Substring(0, 2) + "***";
            var local = email.Substring(0, at); var domain = email.Substring(at + 1);
            return local.Substring(0, Math.Min(2, local.Length)) + "***@" + domain;
        }
        private static string LatestRemark(List<CitizenTimelineItem> history, string level, string eventName)
        { var x = history == null ? null : history.LastOrDefault(i => i.Level == level && i.Event == eventName && !string.IsNullOrWhiteSpace(i.Remark)); return x == null ? null : x.Remark; }
        private static DateTime? UtcOfLast(IEnumerable<HistoryItem> history, string action)
        { var h = history == null ? null : history.LastOrDefault(x => x.Action == action); return h == null ? (DateTime?)null : h.AtUtc; }

        /// <summary>"yyyy-MM-dd" as written by ZoneWorkflowService; null when absent or unreadable.</summary>
        public static DateTime? ParsePaymentDate(string value)
        {
            DateTime d;
            return !string.IsNullOrEmpty(value) && DateTime.TryParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out d) ? d : (DateTime?)null;
        }

        private static CitizenTimelineItem ToTimeline(HistoryEntry h)
        {
            if (h == null) return null;
            HistoryItem i = new HistoryItem { AtUtc = h.AtUtc, Action = h.Action, FromRole = h.FromRole, ToRole = h.ToRole, Remarks = h.Remarks, PaymentReference = h.PaymentReference, PaymentDate = ParsePaymentDate(h.PaymentDate) };
            return ToTimeline(i);
        }
        private static CitizenTimelineItem ToTimeline(HistoryItem h)
        {
            string label;
            if (h == null || !EventLabels.TryGetValue(h.Action ?? string.Empty, out label)) return null;
            var correction = h.Action == AuditActions.JEReturnedForCorrection || h.Action == AuditActions.OSReturnedForCorrection;
            var rejection = h.Action == AuditActions.JERejected || h.Action == AuditActions.OSRejected || h.Action == AuditActions.ACRejected;
            var role = h.FromRole == Roles.JE ? ApplicantText.Level1 : h.FromRole == Roles.OS ? ApplicantText.Level2 : h.FromRole == Roles.AssistantCommissioner ? ApplicantText.Level3 : ApplicantText.Applicant;
            // Never expose the AC-return remark, or remarks on ordinary processing events.
            return new CitizenTimelineItem { AtUtc = h.AtUtc, ActionMr = label, Event = label, PaymentReference = h.PaymentReference, PaymentDate = h.PaymentDate,
                Level = role, Remark = (correction || rejection || h.Action == AuditActions.CitizenResubmitted || h.Action == AuditActions.PaymentRejectedByOs) && RemarkVisible.Contains(h.Action) ? h.Remarks : null };
        }
    }
}
