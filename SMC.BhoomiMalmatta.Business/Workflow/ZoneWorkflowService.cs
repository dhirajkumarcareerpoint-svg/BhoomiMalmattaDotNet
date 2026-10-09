using System;
using System.Collections.Generic;
using System.Linq;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Business.Payments;
using SMC.BhoomiMalmatta.Business.Security;
using SMC.BhoomiMalmatta.Models.Common;
using SMC.BhoomiMalmatta.Models.Constants;

namespace SMC.BhoomiMalmatta.Business.Workflow
{
    public sealed class WorkflowCommand
    {
        public int ApplicationId { get; set; }
        public string Action { get; set; }
        public string ExpectedStage { get; set; }
        /// <summary>Optional base64 RowVersion read when the page was rendered; a mismatch means someone else changed the record.</summary>
        public string ExpectedRowVersion { get; set; }
        public string Remarks { get; set; }
        /// <summary>OSRequestPayment: the amount payable (OS verifies / enters it).</summary>
        public decimal? Amount { get; set; }
        /// <summary>CitizenSubmitPayment: applicant UTR / reference (stored in ReceiptNumber); OSConfirmPayment uses that stored value.</summary>
        public string PaymentReference { get; set; }
        public string PaymentMode { get; set; }
        /// <summary>OSRequestPayment: the rate the OS entered when the master rate is NULL (the amount itself is computed server-side).</summary>
        public decimal? OfficerRate { get; set; }
        public string AttachmentReference { get; set; }
        public DateTime? PaymentDate { get; set; }
        public DocumentUpload PaymentProof { get; set; }
    }

    public sealed class WorkflowResult
    {
        public string NewStage { get; set; }
        public string NewStatus { get; set; }
        public string NewPaymentStatus { get; set; }
        public bool WasAlreadyApplied { get; set; }
    }

    /// <summary>
    /// Executes one workflow action: legacy guard → zone check → duplicate/stale check → state-machine rules → append-only history →
    /// atomic save (optimistic concurrency on RowVersion) → notifications. Status, role, zone and amounts always come from the server.
    /// </summary>
    public sealed class ZoneWorkflowService
    {
        private readonly IZoneWorkflowStore _store; private readonly IClock _clock; private readonly INotifier _notifier; private readonly ICertificateIssuer _issuer; private readonly IFileStorage _files;
        private readonly WorkflowPolicy _policy; private readonly IErrorLog _log;
        public ZoneWorkflowService(IZoneWorkflowStore store, IClock clock, INotifier notifier = null, ICertificateIssuer issuer = null)
            : this(store, clock, notifier, issuer, null) { }
        public ZoneWorkflowService(IZoneWorkflowStore store, IClock clock, INotifier notifier, ICertificateIssuer issuer, IFileStorage files)
            : this(store, clock, notifier, issuer, files, null, null) { }
        public ZoneWorkflowService(IZoneWorkflowStore store, IClock clock, INotifier notifier, ICertificateIssuer issuer, IFileStorage files, WorkflowPolicy policy, IErrorLog log)
        { _store = store; _clock = clock; _notifier = notifier ?? new NullNotifier(); _issuer = issuer; _files = files; _policy = policy ?? WorkflowPolicy.Default; _log = log ?? new NullErrorLog(); }

        public WorkflowPolicy Policy { get { return _policy; } }

        public Result<WorkflowResult> Execute(WorkflowCommand c, ActorContext actor)
        {
            var snap = _store.Load(c.ApplicationId);
            if (snap == null) return Result.Failure<WorkflowResult>(new Error("NOT_FOUND", "अर्ज आढळला नाही."));
            // OS verification always uses the UTR the applicant submitted; a reference sent by the caller is only compared for idempotency.
            var claimedReference = string.IsNullOrWhiteSpace(c.PaymentReference) ? null : c.PaymentReference.Trim();
            if (c.Action == WorkflowActions.OSConfirmPayment) c.PaymentReference = snap.RecordedPaymentReference;

            // Existing Legacy applications are read-only: they are never modified by this system.
            if (!string.Equals(snap.ProcessingRoute, ProcessingRoutes.Zone, StringComparison.Ordinal))
                return Result.Failure<WorkflowResult>(new Error("LEGACY_READONLY", "जुना (Legacy) अर्ज केवळ पाहण्यासाठी आहे; त्यावर कार्यवाही करता येत नाही."));

            // Zone scoping: a user with a ZoneNumber only works in their zone; users without one (all live users today) see all zones.
            if (actor.ZoneNumber.HasValue && actor.ZoneNumber.Value != snap.Zone && actor.Role != Roles.Admin)
                return Result.Failure<WorkflowResult>(new Error("ZONE_FORBIDDEN", "हा अर्ज आपल्या झोनचा नाही."));

            // Idempotent payment confirmation: the same reference twice is a no-op success; a different one is a duplicate payment.
            if (c.Action == WorkflowActions.OSConfirmPayment && !string.IsNullOrWhiteSpace(c.PaymentReference) && snap.PaymentStatus == PaymentStatuses.PaymentDone)
            {
                var o = PaymentRules.Confirm(snap.PaymentStatus, snap.RecordedPaymentReference, claimedReference ?? c.PaymentReference.Trim());
                if (o == PaymentConfirmationOutcome.AlreadyApplied && actor.Role == Roles.OS)
                    return Result.Success(new WorkflowResult { NewStage = snap.Stage, NewStatus = ZoneWorkflowMachine.StatusFor(snap.Stage), NewPaymentStatus = snap.PaymentStatus, WasAlreadyApplied = true });
                return Result.Failure<WorkflowResult>(new Error("PAYMENT_DUPLICATE", "या अर्जासाठी वेगळ्या संदर्भासह पेमेंट आधीच नोंदवले आहे."));
            }

            // Duplicate / stale submission guard.
            if (!string.Equals(c.ExpectedStage, snap.Stage, StringComparison.Ordinal))
                return Result.Failure<WorkflowResult>(new Error("STALE_STAGE", "अर्जाची स्थिती बदललेली आहे. कृपया पान रिफ्रेश करा."));

            if (!string.IsNullOrEmpty(c.ExpectedRowVersion) && snap.RowVersion != null && c.ExpectedRowVersion != Convert.ToBase64String(snap.RowVersion))
                return Result.Failure<WorkflowResult>(new Error("STALE_STAGE", "अर्जाची स्थिती बदललेली आहे. कृपया पान रिफ्रेश करा."));

            StoredDocument paymentProof = null;
            string proofPath = null;
            if (c.Action == WorkflowActions.CitizenSubmitPayment)
            {
                if (actor.Role != Roles.Citizen || !actor.ApplicationId.HasValue || actor.ApplicationId.Value != snap.ApplicationId)
                    return Result.Failure<WorkflowResult>(new Error("WORKFLOW_FORBIDDEN", "या अर्जासाठी आपल्याला अधिकार नाहीत."));
                if (!PaymentRules.IsValidReference((c.PaymentReference ?? string.Empty).Trim()))
                    return Result.Failure<WorkflowResult>(new Error("PAYMENT_REFERENCE_INVALID", "UTR / संदर्भ ६ ते ३० अक्षरे किंवा अंक असावा."));
                if (!c.PaymentDate.HasValue) return Result.Failure<WorkflowResult>(new Error("PAYMENT_DATE_REQUIRED", "भरणा दिनांक निवडा."));
                var payDate = c.PaymentDate.Value.Date;
                if (payDate > _clock.UtcNow.ToLocalTime().Date) return Result.Failure<WorkflowResult>(new Error("PAYMENT_DATE_FUTURE", "भरणा दिनांक आजच्या पुढील असू शकत नाही."));
                var request = ActionHistory.Parse(snap.HistoryJson).LastOrDefault(h => h.Action == AuditActions.PaymentRequestSent);
                if (request == null || payDate < request.AtUtc.ToLocalTime().Date)
                    return Result.Failure<WorkflowResult>(new Error("PAYMENT_DATE_BEFORE_REQUEST", "भरणा दिनांक शुल्क मागणीच्या दिनांकापूर्वी असू शकत नाही."));
                if (c.PaymentProof == null || c.PaymentProof.Content == null)
                    return Result.Failure<WorkflowResult>(new Error("PAYMENT_PROOF_REQUIRED", "भरण्याचा पुरावा अपलोड करा."));
                var checkedFile = FileValidator.Validate(c.PaymentProof.FileName, c.PaymentProof.ContentType, c.PaymentProof.Content);
                if (checkedFile.IsFailure) return Result.Failure<WorkflowResult>(checkedFile.Error);
                var reference = c.PaymentReference.Trim();
                if (_store.IsPaymentReferenceUsed(reference, snap.ApplicationId))
                    return Result.Failure<WorkflowResult>(new Error("PAYMENT_REFERENCE_DUPLICATE", "हा UTR / संदर्भ दुसऱ्या अर्जासाठी नोंदवलेला आहे."));
                if (_files == null) return Result.Failure<WorkflowResult>(new Error("FILE_STORAGE_UNAVAILABLE", "भरणा पुरावा सध्या जतन करता येत नाही."));
                proofPath = "demand/payment/" + snap.ApplicationId + "/" + checkedFile.Value.StoredName;
                paymentProof = new StoredDocument { DocumentType = "PaymentProof", OriginalFileName = checkedFile.Value.SafeDisplayName, StoredFileName = checkedFile.Value.StoredName, RelativePath = proofPath, ContentType = checkedFile.Value.ContentType, SizeBytes = c.PaymentProof.Content.LongLength };
                c.PaymentReference = reference; c.PaymentDate = payDate;
            }

            var eval = ZoneWorkflowMachine.Evaluate(snap.Stage, snap.PaymentStatus, c.Action, actor.Role, c.Remarks, _policy);
            if (eval.IsFailure) return Result.Failure<WorkflowResult>(eval.Error);
            var o2 = eval.Value;

            var payable = snap.PayableAmount;
            if (c.Action == WorkflowActions.OSRequestPayment)
            {
                if (!(c.Amount.HasValue && c.Amount.Value > 0)) return Result.Failure<WorkflowResult>(new Error("AMOUNT_REQUIRED", "देय रक्कम शून्यापेक्षा जास्त असणे आवश्यक आहे."));
                payable = Math.Round(c.Amount.Value, 2, MidpointRounding.AwayFromZero);
            }
            if (c.Action == WorkflowActions.OSConfirmPayment && !PaymentRules.IsValidReference(c.PaymentReference))
                return Result.Failure<WorkflowResult>(new Error("PAYMENT_REFERENCE_INVALID", "वैध पेमेंट संदर्भ (UTR / Receipt, ६–३० अक्षरे/अंक) आवश्यक आहे."));

            var now = _clock.UtcNow;
            var newPay = o2.NewPaymentStatus ?? snap.PaymentStatus;
            // One hash-chained history entry and one audit row per step, all with the same timestamp; saved together below.
            var newJson = snap.HistoryJson;
            var audits = new List<AuditEntry>();
            for (var i = 0; i < o2.Steps.Count; i++)
            {
                var step = o2.Steps[i];
                newJson = ActionHistory.Append(newJson, new HistoryEntry
                {
                    ApplicationId = snap.ApplicationId, ApplicationNumber = snap.ApplicationNumber, FromRole = step.FromRole, ToRole = step.ToRole,
                    Action = step.AuditAction, PreviousStatus = step.PreviousStatus, NewStatus = step.NewStatus, PreviousStage = step.FromStage, NewStage = step.ToStage,
                    Remarks = i > 0 || string.IsNullOrWhiteSpace(c.Remarks) ? null : c.Remarks.Trim(), ActionByUserId = actor.UserId, ActionByUserName = actor.UserName,
                    ActionAtUtc = now.ToString("O"), IpAddress = actor.IpAddress, CorrelationId = actor.CorrelationId, AttachmentReference = i > 0 ? null : proofPath ?? c.AttachmentReference,
                    PaymentReference = i == 0 && c.Action == WorkflowActions.CitizenSubmitPayment ? c.PaymentReference : null,
                    PaymentDate = i == 0 && c.Action == WorkflowActions.CitizenSubmitPayment && c.PaymentDate.HasValue ? c.PaymentDate.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : null
                });
                if (actor.UserId.HasValue)
                    audits.Add(new AuditEntry
                    {
                        Action = step.AuditAction, EntityName = "DemandApplicationZoneWorkflow", EntityId = snap.ApplicationId, OldValue = step.FromStage, NewValue = step.ToStage,
                        UserId = actor.UserId, UserName = actor.UserName, IpAddress = actor.IpAddress, TimestampUtc = now
                    });
            }
            if (!ActionHistory.IsAppendOnly(snap.HistoryJson, newJson))
                return Result.Failure<WorkflowResult>(new Error("HISTORY_INTEGRITY", "कार्यवाही इतिहासाची अखंडता तपासणी अयशस्वी."));

            RemarkKind kind;
            switch (c.Action)
            {
                case WorkflowActions.JEReturnForCorrection:
                case WorkflowActions.OSReturnForCorrection:
                case WorkflowActions.ACReturn: kind = RemarkKind.Correction; break;
                case WorkflowActions.ACApprove: kind = RemarkKind.Approval; break;
                case WorkflowActions.JEReject:
                case WorkflowActions.OSReject:
                case WorkflowActions.ACReject: kind = RemarkKind.Rejection; break;
                default: kind = RemarkKind.None; break;
            }
            var update = new ZoneWorkflowUpdate
            {
                Before = snap, NewStage = o2.ToStage, NewStatus = o2.NewStatus, NewPaymentStatus = newPay, NewPayableAmount = payable,
                DocumentsVerified = c.Action == WorkflowActions.JEVerifyForward ? (bool?)true : null, NewHistoryJson = newJson, RemarkKind = kind,
                Remark = string.IsNullOrWhiteSpace(c.Remarks) ? null : c.Remarks.Trim(), PaymentReference = c.Action == WorkflowActions.CitizenSubmitPayment ? c.PaymentReference.Trim() : c.Action == WorkflowActions.OSConfirmPayment ? snap.RecordedPaymentReference : null,
                PaymentDate = c.Action == WorkflowActions.CitizenSubmitPayment ? c.PaymentDate : c.Action == WorkflowActions.OSConfirmPayment ? snap.PaymentReferenceDate : null, PaymentProof = paymentProof, ConfirmPayment = c.Action == WorkflowActions.OSConfirmPayment,
                PaymentMode = c.PaymentMode == null ? null : c.PaymentMode.Trim(), RateEnteredByOfficer = c.OfficerRate, Actor = actor, AtUtc = now,
                Audits = audits,
                ReplaceActivePaymentProof = c.Action == WorkflowActions.CitizenSubmitPayment && IsResubmissionAfterRejection(snap)
            };

            if (proofPath != null)
            {
                try { _files.Save(proofPath, c.PaymentProof.Content); }
                catch (Exception) { return Result.Failure<WorkflowResult>(new Error("FILE_STORE_FAILED", "भरणा पुरावा जतन करता आला नाही.")); }
            }
            Result saved;
            try { saved = _store.Save(update); }
            catch (Exception)
            {
                if (proofPath != null) { try { _files.Delete(proofPath); } catch (Exception) { } }
                return Result.Failure<WorkflowResult>(new Error("DB_ERROR", "भरणा नोंद जतन करता आली नाही. कृपया पुन्हा प्रयत्न करा."));
            }
            if (saved.IsFailure)
            {
                if (proofPath != null) { try { _files.Delete(proofPath); } catch (Exception) { } }
                return Result.Failure<WorkflowResult>(saved.Error);
            }

            // Side effects after the committed transaction: failures here never undo the workflow action; they are only logged.
            foreach (var n in Notifications(c.Action, snap, payable))
            {
                try { _notifier.Notify(n); }
                catch (Exception ex) { _log.Log(actor.CorrelationId, "WorkflowSms:" + n.EventType, ex); }
            }
            if (c.Action == WorkflowActions.ACApprove && _issuer != null)
            {
                // The approval stays committed; a missing letter is generated lazily on the first download.
                try
                {
                    var issued = _issuer.Issue(snap.ApplicationId, actor);
                    if (issued != null && issued.IsFailure) _log.Log(actor.CorrelationId, "CertificateGeneration", new InvalidOperationException(issued.Error.Code + ": " + issued.Error.Message));
                }
                catch (Exception ex) { _log.Log(actor.CorrelationId, "CertificateGeneration", ex); }
            }
            return Result.Success(new WorkflowResult { NewStage = o2.ToStage, NewStatus = o2.NewStatus, NewPaymentStatus = newPay });
        }

        /// <summary>
        /// True when the applicant is submitting proof again after an OS "Reject payment": the payment status is PaymentRejected, or the last
        /// rejection in the history is newer than the last applicant submission. A first submission returns false (insert only).
        /// </summary>
        public static bool IsResubmissionAfterRejection(ZoneWorkflowSnapshot s)
        {
            if (s == null) return false;
            if (s.PaymentStatus == PaymentStatuses.PaymentRejected) return true;
            var history = ActionHistory.Parse(s.HistoryJson);
            var rejected = history.LastOrDefault(h => h.Action == AuditActions.PaymentRejectedByOs);
            if (rejected == null) return false;
            var submitted = history.LastOrDefault(h => h.Action == AuditActions.PaymentSubmittedByApplicant);
            return submitted == null || submitted.Sequence < rejected.Sequence;
        }

        private static IEnumerable<NotificationRequest> Notifications(string action, ZoneWorkflowSnapshot s, decimal payable)
        {
            var vars = new Dictionary<string, string> { { "ApplicationNumber", s.ApplicationNumber }, { "ApplicantName", s.ApplicantName ?? "" }, { "Amount", payable.ToString("0.00") } };
            Func<NotificationRequest> toAc = () => new NotificationRequest { EventType = SmsEventTypes.AssistantCommissionerNotification, ApplicationNumber = s.ApplicationNumber, RecipientRole = Roles.AssistantCommissioner, Variables = vars };
            switch (action)
            {
                case WorkflowActions.OSRequestPayment:
                    return new[] { new NotificationRequest { EventType = SmsEventTypes.PaymentRequired, ApplicationNumber = s.ApplicationNumber, RecipientMobile = s.ApplicantMobile, Variables = vars } };
                case WorkflowActions.OSConfirmPayment:
                    return new[] { new NotificationRequest { EventType = SmsEventTypes.PaymentDone, ApplicationNumber = s.ApplicationNumber, RecipientMobile = s.ApplicantMobile, Variables = vars }, toAc() };
                case WorkflowActions.OSForwardToAC:
                    return new[] { toAc() };
                case WorkflowActions.ACApprove:
                    return new[] { new NotificationRequest { EventType = SmsEventTypes.ApplicationApproved, ApplicationNumber = s.ApplicationNumber, RecipientMobile = s.ApplicantMobile, Variables = vars } };
                default:
                    return new NotificationRequest[0];
            }
        }
    }
}
