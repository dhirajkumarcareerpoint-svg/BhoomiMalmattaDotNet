using System.Text.RegularExpressions;
using SMC.BhoomiMalmatta.Models.Constants;

namespace SMC.BhoomiMalmatta.Business.Payments
{
    public enum PaymentConfirmationOutcome { Applied, AlreadyApplied, Conflict, NotAwaitingPayment }

    /// <summary>Idempotent OS confirmation of the reference already submitted by the applicant.</summary>
    public static class PaymentRules
    {
        private static readonly Regex Reference = new Regex(@"^[A-Za-z0-9]{6,30}$", RegexOptions.Compiled);

        /// <summary>UTR / bank reference: 6–30 letters or digits only.</summary>
        public static bool IsValidReference(string reference) { return reference != null && Reference.IsMatch(reference.Trim()); }

        public static PaymentConfirmationOutcome Confirm(string currentPaymentStatus, string recordedReference, string incomingReference)
        {
            if (currentPaymentStatus == PaymentStatuses.PaymentDone)
                return string.Equals(recordedReference, incomingReference, System.StringComparison.Ordinal) ? PaymentConfirmationOutcome.AlreadyApplied : PaymentConfirmationOutcome.Conflict;
            return currentPaymentStatus == PaymentStatuses.PaymentSubmitted ? PaymentConfirmationOutcome.Applied : PaymentConfirmationOutcome.NotAwaitingPayment;
        }
    }
}
