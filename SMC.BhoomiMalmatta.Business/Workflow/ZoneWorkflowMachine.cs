using System.Collections.Generic;
using System.Linq;
using SMC.BhoomiMalmatta.Models.Common;
using SMC.BhoomiMalmatta.Models.Constants;

namespace SMC.BhoomiMalmatta.Business.Workflow
{
    public static class WorkflowActions
    {
        public const string JEVerifyForward = "JEVerifyForward";
        public const string JEReturnForCorrection = "JEReturnForCorrection";
        public const string JEReject = "JEReject";
        public const string CitizenResubmit = "CitizenResubmit";
        public const string OSRequestPayment = "OSRequestPayment";
        public const string OSMarkPaymentNotRequired = "OSMarkPaymentNotRequired";
        public const string OSConfirmPayment = "OSConfirmPayment";
        public const string OSRejectPayment = "OSRejectPayment";
        public const string CitizenSubmitPayment = "CitizenSubmitPayment";
        public const string OSForwardToAC = "OSForwardToAC";
        public const string OSReturnForCorrection = "OSReturnForCorrection";
        public const string OSReject = "OSReject";
        public const string ACApprove = "ACApprove";
        public const string ACReturn = "ACReturn";
        public const string ACReject = "ACReject";
        public const string AdminReopen = "AdminReopen";
    }

    public sealed class TransitionRule
    {
        public string FromStage { get; set; }
        public string Action { get; set; }
        public string[] Roles { get; set; }
        public string ToStage { get; set; }
        public string AuditAction { get; set; }
        public string ToRole { get; set; }
        public bool RemarkRequired { get; set; }
        public string[] PaymentStatusesAllowed { get; set; }
        public string SetPaymentStatus { get; set; }
        /// <summary>A rule that is applied automatically after this one in the same transaction (e.g. payment verified → forwarded to AC).</summary>
        public string ChainAction { get; set; }
    }

    /// <summary>One history/audit step of a transition. A chained transition produces several steps with the same timestamp.</summary>
    public sealed class TransitionStep
    {
        public string FromStage { get; set; }
        public string ToStage { get; set; }
        public string AuditAction { get; set; }
        public string FromRole { get; set; }
        public string ToRole { get; set; }
        public string PreviousStatus { get; set; }
        public string NewStatus { get; set; }
    }

    public sealed class TransitionOutcome
    {
        public TransitionOutcome() { Steps = new List<TransitionStep>(); }
        public string FromStage { get; set; }
        public string ToStage { get; set; }
        public string AuditAction { get; set; }
        public string FromRole { get; set; }
        public string ToRole { get; set; }
        public string PreviousStatus { get; set; }
        public string NewStatus { get; set; }
        public string NewPaymentStatus { get; set; }
        public List<TransitionStep> Steps { get; private set; }
    }

    /// <summary>Configurable workflow switches (Web.config appSettings). Immutable; <see cref="Default"/> is the production default.</summary>
    public sealed class WorkflowPolicy
    {
        public WorkflowPolicy(bool acAllowReturnReject) { AcAllowReturnReject = acAllowReturnReject; }
        /// <summary>Workflow.AcAllowReturnReject: when false the Assistant Commissioner can only approve.</summary>
        public bool AcAllowReturnReject { get; private set; }
        public static readonly WorkflowPolicy Default = new WorkflowPolicy(false);
        public bool IsEnabled(string action)
        {
            return AcAllowReturnReject || (action != WorkflowActions.ACReturn && action != WorkflowActions.ACReject);
        }
    }

    /// <summary>
    /// Zone workflow: Citizen → JE → OS → Assistant Commissioner → Final result. Pure and deterministic:
    /// levels cannot be skipped, JE cannot do OS work, OS cannot give final approval, and finalized applications
    /// accept no action except an explicit Admin/AC reopen of a rejection. OS payment verification forwards to the AC
    /// automatically; with the default <see cref="WorkflowPolicy"/> the AC can only approve.
    /// </summary>
    public static class ZoneWorkflowMachine
    {
        private static readonly string[] Paid = { PaymentStatuses.PaymentDone, PaymentStatuses.NotRequired };

        private static TransitionRule R(string from, string action, string[] roles, string to, string audit, string toRole, bool remark,
            string[] pay = null, string setPay = null, string chain = null)
        {
            return new TransitionRule { FromStage = from, Action = action, Roles = roles, ToStage = to, AuditAction = audit, ToRole = toRole, RemarkRequired = remark, PaymentStatusesAllowed = pay, SetPaymentStatus = setPay, ChainAction = chain };
        }

        public static readonly IList<TransitionRule> Rules = new List<TransitionRule>
        {
            R(ZoneStages.JEPending, WorkflowActions.JEVerifyForward, new[]{Roles.JE}, ZoneStages.OSPending, AuditActions.JEVerified, Roles.OS, false),
            R(ZoneStages.JEPending, WorkflowActions.JEReturnForCorrection, new[]{Roles.JE}, ZoneStages.JECorrectionRequested, AuditActions.JEReturnedForCorrection, Roles.Citizen, true),
            R(ZoneStages.JEPending, WorkflowActions.JEReject, new[]{Roles.JE}, ZoneStages.Rejected, AuditActions.JERejected, "None", true),
            R(ZoneStages.JECorrectionRequested, WorkflowActions.CitizenResubmit, new[]{Roles.Citizen}, ZoneStages.JEPending, AuditActions.CitizenResubmitted, Roles.JE, false),

            R(ZoneStages.OSPending, WorkflowActions.OSRequestPayment, new[]{Roles.OS}, ZoneStages.PaymentRequired, AuditActions.PaymentRequestSent, Roles.Citizen, false,
                new[]{PaymentStatuses.PendingOSAmount, PaymentStatuses.PaymentRequired}, PaymentStatuses.PaymentRequired),
            R(ZoneStages.OSPending, WorkflowActions.OSMarkPaymentNotRequired, new[]{Roles.OS}, ZoneStages.OSPending, AuditActions.PaymentNotRequired, Roles.OS, true,
                new[]{PaymentStatuses.PendingOSAmount}, PaymentStatuses.NotRequired),
            // Verify payment: "Payment Done" then, in the same transaction, the automatic "Forwarded to AC" step.
            R(ZoneStages.PaymentRequired, WorkflowActions.OSConfirmPayment, new[]{Roles.OS}, ZoneStages.OSPending, AuditActions.PaymentDone, Roles.OS, false,
                new[]{PaymentStatuses.PaymentSubmitted}, PaymentStatuses.PaymentDone, WorkflowActions.OSForwardToAC),
            R(ZoneStages.PaymentRequired, WorkflowActions.OSRejectPayment, new[]{Roles.OS}, ZoneStages.PaymentRequired, AuditActions.PaymentRejectedByOs, Roles.Citizen, true,
                new[]{PaymentStatuses.PaymentSubmitted}, PaymentStatuses.PaymentRejected),
            R(ZoneStages.PaymentRequired, WorkflowActions.CitizenSubmitPayment, new[]{Roles.Citizen}, ZoneStages.PaymentRequired, AuditActions.PaymentSubmittedByApplicant, Roles.OS, false,
                new[]{PaymentStatuses.PaymentRequired, PaymentStatuses.PaymentRejected}, PaymentStatuses.PaymentSubmitted),
            // Manual forward only remains for NotRequired payments and legacy-state rows already at OSPending with PaymentDone.
            R(ZoneStages.OSPending, WorkflowActions.OSForwardToAC, new[]{Roles.OS}, ZoneStages.AssistantCommissionerApprovalPending, AuditActions.ForwardedToAC, Roles.AssistantCommissioner, false, Paid),
            R(ZoneStages.OSPending, WorkflowActions.OSReturnForCorrection, new[]{Roles.OS}, ZoneStages.OSCorrectionRequested, AuditActions.OSReturnedForCorrection, Roles.Citizen, true),
            R(ZoneStages.OSPending, WorkflowActions.OSReject, new[]{Roles.OS}, ZoneStages.Rejected, AuditActions.OSRejected, "None", true),
            R(ZoneStages.OSCorrectionRequested, WorkflowActions.CitizenResubmit, new[]{Roles.Citizen}, ZoneStages.OSPending, AuditActions.CitizenResubmitted, Roles.OS, false),

            R(ZoneStages.AssistantCommissionerApprovalPending, WorkflowActions.ACApprove, new[]{Roles.AssistantCommissioner}, ZoneStages.Approved, AuditActions.FinalApproved, "None", false, Paid),
            R(ZoneStages.AssistantCommissionerApprovalPending, WorkflowActions.ACReturn, new[]{Roles.AssistantCommissioner}, ZoneStages.OSPending, AuditActions.ACReturned, Roles.OS, true, Paid),
            R(ZoneStages.AssistantCommissionerApprovalPending, WorkflowActions.ACReject, new[]{Roles.AssistantCommissioner}, ZoneStages.Rejected, AuditActions.ACRejected, "None", true, Paid),

            R(ZoneStages.Rejected, WorkflowActions.AdminReopen, new[]{Roles.Admin, Roles.AssistantCommissioner}, ZoneStages.JEPending, AuditActions.Reopened, Roles.JE, true),
        };

        /// <summary>Application.Status derived from the stage (validated against live counts).</summary>
        public static string StatusFor(string stage)
        {
            switch (stage)
            {
                case ZoneStages.JEPending: return ApplicationStatuses.Submitted;
                case ZoneStages.JECorrectionRequested:
                case ZoneStages.OSPending:
                case ZoneStages.OSCorrectionRequested: return ApplicationStatuses.UnderScrutiny;
                case ZoneStages.PaymentRequired: return ApplicationStatuses.FeePending;
                case ZoneStages.AssistantCommissionerApprovalPending: return ApplicationStatuses.ApprovalPending;
                case ZoneStages.Approved: return ApplicationStatuses.Approved;
                case ZoneStages.Rejected: return ApplicationStatuses.Rejected;
                default: return ApplicationStatuses.Submitted;
            }
        }

        public const string AcActionDisabledMessage = "सहाय्यक आयुक्त स्तरावर केवळ अंतिम मंजुरी देता येते; OS कडे परत पाठवणे किंवा अर्ज नाकारणे सध्या बंद आहे.";

        public static IEnumerable<string> AllowedActions(string stage, string role) { return AllowedActions(stage, role, WorkflowPolicy.Default); }

        public static IEnumerable<string> AllowedActions(string stage, string role, WorkflowPolicy policy)
        {
            policy = policy ?? WorkflowPolicy.Default;
            return Rules.Where(r => r.FromStage == stage && r.Roles.Contains(role) && policy.IsEnabled(r.Action)).Select(r => r.Action).ToList();
        }

        public static IEnumerable<string> AllowedActions(string stage, string role, string paymentStatus) { return AllowedActions(stage, role, paymentStatus, WorkflowPolicy.Default); }

        public static IEnumerable<string> AllowedActions(string stage, string role, string paymentStatus, WorkflowPolicy policy)
        {
            policy = policy ?? WorkflowPolicy.Default;
            return Rules.Where(r => r.FromStage == stage && r.Roles.Contains(role) && policy.IsEnabled(r.Action) &&
                (r.PaymentStatusesAllowed == null || r.PaymentStatusesAllowed.Contains(paymentStatus))).Select(r => r.Action).ToList();
        }

        public static IEnumerable<string> OfficerListActions(string route, string stage, string role, string paymentStatus)
        { return OfficerListActions(route, stage, role, paymentStatus, WorkflowPolicy.Default); }

        public static IEnumerable<string> OfficerListActions(string route, string stage, string role, string paymentStatus, WorkflowPolicy policy)
        {
            if (route != ProcessingRoutes.Zone || (role != Roles.JE && role != Roles.OS && role != Roles.AssistantCommissioner)) return new string[0];
            return AllowedActions(stage, role, paymentStatus, policy).Where(a => a != WorkflowActions.AdminReopen && a != WorkflowActions.OSMarkPaymentNotRequired).ToList();
        }

        public static Result<TransitionOutcome> Evaluate(string currentStage, string currentPaymentStatus, string action, string actorRole, string remarks)
        { return Evaluate(currentStage, currentPaymentStatus, action, actorRole, remarks, WorkflowPolicy.Default); }

        public static Result<TransitionOutcome> Evaluate(string currentStage, string currentPaymentStatus, string action, string actorRole, string remarks, WorkflowPolicy policy)
        {
            policy = policy ?? WorkflowPolicy.Default;
            if (ZoneStages.IsFinal(currentStage) && action != WorkflowActions.AdminReopen)
                return Result.Failure<TransitionOutcome>(new Error("WORKFLOW_FINALIZED", "अंतिम निर्णय झालेल्या अर्जावर पुढील कार्यवाही करता येत नाही."));

            var rule = Rules.FirstOrDefault(r => r.FromStage == currentStage && r.Action == action);
            if (rule == null)
                return Result.Failure<TransitionOutcome>(new Error("WORKFLOW_INVALID_TRANSITION", "ही कृती सध्याच्या टप्प्यावर करता येत नाही."));
            if (!policy.IsEnabled(action))
                return Result.Failure<TransitionOutcome>(new Error("WORKFLOW_FORBIDDEN", AcActionDisabledMessage));
            if (!rule.Roles.Contains(actorRole))
                return Result.Failure<TransitionOutcome>(new Error("WORKFLOW_FORBIDDEN", "या कृतीसाठी आपल्याला अधिकार नाहीत."));
            if (rule.RemarkRequired && string.IsNullOrWhiteSpace(remarks))
                return Result.Failure<TransitionOutcome>(new Error("REMARK_REQUIRED", "शेरा (Remarks) नमूद करणे आवश्यक आहे."));
            if (rule.PaymentStatusesAllowed != null && !rule.PaymentStatusesAllowed.Contains(currentPaymentStatus))
                return Result.Failure<TransitionOutcome>(new Error("PAYMENT_PRECONDITION", "देयक स्थिती या कृतीसाठी योग्य नाही (पेमेंट पूर्ण / लागू नाही असणे आवश्यक)."));

            var outcome = new TransitionOutcome
            {
                FromStage = currentStage, ToStage = rule.ToStage, AuditAction = rule.AuditAction, FromRole = actorRole, ToRole = rule.ToRole,
                PreviousStatus = StatusFor(currentStage), NewStatus = StatusFor(rule.ToStage), NewPaymentStatus = rule.SetPaymentStatus
            };
            outcome.Steps.Add(Step(rule, currentStage, actorRole));

            // Chained step (same actor, same transaction): it must itself be a valid rule for the resulting stage and payment status.
            if (rule.ChainAction != null)
            {
                var payAfter = rule.SetPaymentStatus ?? currentPaymentStatus;
                var next = Rules.FirstOrDefault(r => r.FromStage == rule.ToStage && r.Action == rule.ChainAction);
                if (next == null || !next.Roles.Contains(actorRole) || !policy.IsEnabled(next.Action) || (next.PaymentStatusesAllowed != null && !next.PaymentStatusesAllowed.Contains(payAfter)))
                    return Result.Failure<TransitionOutcome>(new Error("WORKFLOW_INVALID_TRANSITION", "ही कृती सध्याच्या टप्प्यावर करता येत नाही."));
                outcome.Steps.Add(Step(next, rule.ToStage, actorRole));
                outcome.ToStage = next.ToStage; outcome.ToRole = next.ToRole; outcome.NewStatus = StatusFor(next.ToStage);
                if (next.SetPaymentStatus != null) outcome.NewPaymentStatus = next.SetPaymentStatus;
            }
            return Result.Success(outcome);
        }

        private static TransitionStep Step(TransitionRule rule, string fromStage, string actorRole)
        {
            return new TransitionStep { FromStage = fromStage, ToStage = rule.ToStage, AuditAction = rule.AuditAction, FromRole = actorRole, ToRole = rule.ToRole, PreviousStatus = StatusFor(fromStage), NewStatus = StatusFor(rule.ToStage) };
        }
    }
}
