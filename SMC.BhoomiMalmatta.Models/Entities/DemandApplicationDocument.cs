using System;

namespace SMC.BhoomiMalmatta.Models.Entities
{
    /// <summary>Maps existing table dbo.TR_CFC_DemandApplicationDocuments. Never created or altered by the application.</summary>
    public class DemandApplicationDocument
    {
        public int Id { get; set; }
        public int DemandApplicationId { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string StoredFileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public string UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string DeletedBy { get; set; }
        public string Shera { get; set; }
        public string VerificationStatus { get; set; } = string.Empty;
        public string RequestRemark { get; set; }
        public DateTime? RequestedAt { get; set; }
        public string RequestedBy { get; set; }
        public DateTime? RespondedAt { get; set; }
        public string RequestTokenHash { get; set; }
        public DateTime? RequestTokenCreatedAt { get; set; }
        public DateTime? RequestTokenConsumedAt { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public string VerifiedBy { get; set; }
        public string VerifiedRole { get; set; }
    }
}
