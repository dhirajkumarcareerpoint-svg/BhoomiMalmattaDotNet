using System;

namespace SMC.BhoomiMalmatta.Models.Entities
{
    /// <summary>Maps existing table dbo.TR_CFC_DemandApplicationZoneWorkflows. Never created or altered by the application.</summary>
    public class DemandApplicationZoneWorkflow
    {
        public int Id { get; set; }
        public int DemandApplicationId { get; set; }
        public int Zone { get; set; }
        public int? ZoneOfficerUserId { get; set; }
        public string ZoneStage { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public decimal PayableAmount { get; set; }
        public bool DocumentsVerified { get; set; }
        public string CorrectionRemark { get; set; }
        public int CorrectionVersion { get; set; }
        public DateTime? CorrectionRequestedAt { get; set; }
        public DateTime? CorrectionResubmittedAt { get; set; }
        public string ApplicantReply { get; set; }
        public string ApprovalRemark { get; set; }
        public string RejectionRemark { get; set; }
        public string PaymentMode { get; set; }
        public string ReceiptNumber { get; set; }
        public DateTime? ReceiptDate { get; set; }
        public string ReceivedBy { get; set; }
        public DateTime? PaymentConfirmedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string ApprovedBy { get; set; }
        public string CertificatePath { get; set; }
        public string CertificateFileName { get; set; }
        public string ActionHistoryJson { get; set; } = string.Empty;
        public byte[] RowVersion { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public string UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string DeletedBy { get; set; }
        public string Shera { get; set; }
    }
}
