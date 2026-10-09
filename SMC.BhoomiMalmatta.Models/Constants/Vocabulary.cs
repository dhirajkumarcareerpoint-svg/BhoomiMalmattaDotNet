namespace SMC.BhoomiMalmatta.Models.Constants
{
    /// <summary>Role names exactly as stored in MR_AST_Users.Role (verified against the live vocabulary).</summary>
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string JE = "JE";
        public const string OS = "OS";
        public const string AssistantCommissioner = "AssistantCommissioner";
        /// <summary>Existing generic officer role ("Officer"/"Staff"): read-only, no workflow actions.</summary>
        public const string Staff = "Staff";
        public const string Citizen = "Citizen";
        public const string System = "System";
        public static readonly string[] OfficerRoles = { Admin, JE, OS, AssistantCommissioner, Staff };
    }

    public static class ApplicationStatuses
    {
        public const string Submitted = "Submitted";
        public const string UnderScrutiny = "UnderScrutiny";
        public const string FeePending = "FeePending";
        public const string ApprovalPending = "ApprovalPending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public static readonly string[] All = { Submitted, UnderScrutiny, FeePending, ApprovalPending, Approved, Rejected };
    }

    public static class ZoneStages
    {
        public const string JEPending = "JEPending";
        public const string JECorrectionRequested = "JECorrectionRequested";
        public const string OSPending = "OSPending";
        public const string OSCorrectionRequested = "OSCorrectionRequested";
        public const string PaymentRequired = "PaymentRequired";
        public const string AssistantCommissionerApprovalPending = "AssistantCommissionerApprovalPending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public static readonly string[] All = { JEPending, JECorrectionRequested, OSPending, OSCorrectionRequested, PaymentRequired, AssistantCommissionerApprovalPending, Approved, Rejected };
        public static bool IsFinal(string stage) { return stage == Approved || stage == Rejected; }
    }

    public static class PaymentStatuses
    {
        public const string NotRequired = "NotRequired";
        public const string PaymentRequired = "PaymentRequired";
        public const string PaymentDone = "PaymentDone";
        public const string PaymentSubmitted = "PaymentSubmitted";
        public const string PaymentRejected = "PaymentRejected";
        public const string PendingOSAmount = "PendingOSAmount";
        public const string FeePending = "Fee Pending";
    }

    public static class ProcessingRoutes { public const string Legacy = "Legacy"; public const string Zone = "Zone"; }

    public static class DocumentTypes
    {
        public const string IdentityProof = "IdentityProof";
        public const string AddressProof = "AddressProof";
        public const string Level1Correction = "Level1Correction";
        public const string Level2Correction = "Level2Correction";
        public static readonly string[] RequiredForSubmission = { IdentityProof, AddressProof };
    }

    public static class VerificationStatuses
    {
        public const string Unchecked = "Unchecked";
        public const string Checked = "Checked";
        public const string Responded = "Responded";
        public const string Resubmitted = "Resubmitted";
    }

    /// <summary>Audit action labels (TR_AST_AuditLogs.Action is nvarchar(30)). Existing labels reused verbatim.</summary>
    public static class AuditActions
    {
        public const string ApplicationSubmitted = "Application Submitted";
        public const string JEVerified = "JE Verified";
        public const string JEReturnedForCorrection = "JE Returned for Correction";
        public const string JERejected = "JE Rejected";
        public const string CitizenResubmitted = "Citizen Resubmitted";
        public const string PaymentRequestSent = "Payment Request Sent";
        public const string PaymentNotRequired = "Payment Not Required";
        public const string PaymentDone = "Payment Done";
        public const string PaymentSubmittedByApplicant = "Payment Submitted by Applicant";
        public const string PaymentRejectedByOs = "Payment Rejected by OS";
        public const string OSVerified = "OS verified document";
        public const string OSReturnedForCorrection = "OS Returned for Correction";
        public const string OSRejected = "OS Rejected";
        public const string ForwardedToAC = "Forwarded to AC";
        public const string FinalApproved = "Final Approved";
        public const string ACReturned = "AC Returned";
        public const string ACRejected = "AC Rejected";
        public const string CertificateGenerated = "Certificate Generated";
        public const string Reopened = "Reopened";
        public const string LoginSuccess = "Login Success";
        public const string LoginFailed = "Login Failed";
        public const string Logout = "Logout";
    }

    public static class SmsEventTypes
    {
        public const string ApplicationSubmitted = "ApplicationSubmitted";
        public const string PaymentRequired = "PaymentRequired";
        public const string PaymentDone = "PaymentDone";
        public const string AssistantCommissionerNotification = "AssistantCommissionerNotification";
        public const string ApplicationApproved = "ApplicationApproved";
        public const string CertificateAvailable = "CertificateAvailable";
        public const string AccessCodeRecovery = "AccessCodeRecovery";
    }

    public static class SmsStatuses { public const string Sent = "Sent"; public const string Failed = "Failed"; public const string Suppressed = "Suppressed"; }
}
