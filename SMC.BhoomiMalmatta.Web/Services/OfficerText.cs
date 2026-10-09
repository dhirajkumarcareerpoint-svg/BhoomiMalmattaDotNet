using System;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Business.Workflow;
namespace SMC.BhoomiMalmatta.Web.Services
{
    /// <summary>All officer-facing English text for the application list and modal (single place for a future translation).</summary>
    public static class OfficerText
    {
        public const string RemarksRequired = "Remarks are required.";
        public const string Changed = "The application was changed by another user. Please refresh.";
        public const string ConfirmStart = "Are you sure you want to ";
        public const string ConfirmEnd = " application ";
        public const string ConfirmQuestion = "?";
        public const string AddRemark = "Add remark";
        public const string OptionalRemarks = "Remarks (optional)";
        public const string PreviewProof = "Preview proof";
        public const string CertificateLink = "मंजुरी पत्र डाउनलोड करा";
        /// <summary>OS status pill: the applicant submitted UTR/date/proof and the OS must verify it.</summary>
        public const string PaymentVerificationPending = "पेमेंट पडताळणी प्रलंबित";
        /// <summary>Status pill while the applicant has not paid yet (not actionable for the OS).</summary>
        public const string AwaitingApplicantPayment = "अर्जदाराकडून पेमेंट प्रलंबित";
        /// <summary>Shown for an empty applicant name / mobile / e-mail (not collected when Apply.CollectApplicantDetails=false).</summary>
        public const string Dash = "—";
        public static string OrDash(string value) { return string.IsNullOrWhiteSpace(value) ? Dash : value; }
        public static string Action(string action)
        {
            switch (action)
            {
                case WorkflowActions.JEVerifyForward: return "Accept";
                case WorkflowActions.JEReturnForCorrection: case WorkflowActions.OSReturnForCorrection: return "Return for correction";
                case WorkflowActions.JEReject: case WorkflowActions.OSReject: case WorkflowActions.ACReject: return "Reject";
                case WorkflowActions.OSRequestPayment: return "Request payment";
                case WorkflowActions.OSConfirmPayment: return "Verify payment";
                case WorkflowActions.OSRejectPayment: return "Reject payment";
                case WorkflowActions.OSForwardToAC: return "Forward to Assistant Commissioner";
                case WorkflowActions.ACApprove: return "Approve";
                case WorkflowActions.ACReturn: return "Return to OS";
                default: return string.Empty;
            }
        }
        public static string Payment(string status)
        {
            switch (status ?? string.Empty)
            {
                case PaymentStatuses.NotRequired: return "Not required";
                case PaymentStatuses.PaymentRequired: case PaymentStatuses.FeePending: case PaymentStatuses.PendingOSAmount: return "Payment required";
                case PaymentStatuses.PaymentSubmitted: return "Payment submitted";
                case PaymentStatuses.PaymentDone: return "Payment done";
                default: return string.IsNullOrEmpty(status) ? "?" : status;
            }
        }
        /// <summary>Stage pill for PaymentRequired rows, which depends on whether the applicant already submitted the payment.</summary>
        public static string PaymentStagePill(string paymentStatus)
        {
            return paymentStatus == PaymentStatuses.PaymentSubmitted ? PaymentVerificationPending : AwaitingApplicantPayment;
        }
        public static string Confirmation(string action, string number)
        {
            if (action == WorkflowActions.ACApprove) return "Are you sure you want to approve application " + number + "?";
            return "Are you sure you want to " + Action(action) + " application " + number + "?";
        }
        /// <summary>Success message after an officer action (shared by the postback fallback and the AJAX path).</summary>
        public static string Success(string action, string number)
        {
            switch (action ?? string.Empty)
            {
                case WorkflowActions.JEVerifyForward: return "Application " + number + " forwarded to OS.";
                case WorkflowActions.OSConfirmPayment: return "Payment verified. Application forwarded to Assistant Commissioner.";
                case WorkflowActions.ACApprove: return "Application " + number + " approved. Permission letter is available to the applicant.";
                default: return "Application " + number + " updated.";
            }
        }
        public static string ProcessedAction(string action)
        {
            switch (action ?? string.Empty)
            {
                case "JE ने तपासणी पूर्ण करून OS कडे पाठवले": return "Accepted";
                case "JE ने दुरुस्तीसाठी परत पाठवले": case "OS ने दुरुस्तीसाठी परत पाठवले": return "Returned for correction";
                case "JE ने अर्ज नाकारला": case "OS ने अर्ज नाकारला": case "AC ने अर्ज नाकारला": return "Rejected";
                case "भरणा विनंती पाठवली": return "Payment requested";
                case "अर्जदाराने भरणा तपशील सादर केले": return "Payment submitted";
                case "OS ने भरणा पुरावा नाकारला": return "Payment rejected";
                case "OS ने भरणा पडताळला": return "Payment verified";
                case "AC कडे पाठवले": return "Forwarded to AC";
                case "AC ने OS कडे परत पाठवले": return "Returned to OS";
                case "अंतिम मंजुरी दिली": return "Final approved";
                default: return action ?? string.Empty;
            }
        }
    }
}
