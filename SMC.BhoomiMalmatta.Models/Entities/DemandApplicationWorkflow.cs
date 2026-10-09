using System;

namespace SMC.BhoomiMalmatta.Models.Entities
{
    /// <summary>Maps existing table dbo.TR_CFC_DemandApplicationWorkflows. Never created or altered by the application.</summary>
    public class DemandApplicationWorkflow
    {
        public int Id { get; set; }
        public int DemandApplicationId { get; set; }
        public string Stage { get; set; } = string.Empty;
        public string RejectionReason { get; set; }
        public decimal PayableAmount { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public string PaymentLink { get; set; }
        public string Utr { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string PaymentScreenshotPath { get; set; }
        public string PaymentScreenshotFileName { get; set; }
        public long? PaymentScreenshotSizeBytes { get; set; }
        public string PaymentSubmittedBy { get; set; }
        public DateTime? PaymentSubmittedAt { get; set; }
        public DateTime? OsPaymentNotificationSentAt { get; set; }
        public string PaymentVerifiedBy { get; set; }
        public DateTime? PaymentVerifiedAt { get; set; }
        public string CertificateFilePath { get; set; }
        public string CertificateFileName { get; set; }
        public DateTime? CertificateGeneratedAt { get; set; }
        public string ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public string UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string DeletedBy { get; set; }
        public string Shera { get; set; }
        public string PaymentAccessToken { get; set; }
        public string RazorpayOrderId { get; set; }
        public string RazorpayPaymentId { get; set; }
        public string RazorpaySignature { get; set; }
    }
}
