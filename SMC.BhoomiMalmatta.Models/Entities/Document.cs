using System;

namespace SMC.BhoomiMalmatta.Models.Entities
{
    /// <summary>Maps existing table dbo.TR_AST_Documents. Never created or altered by the application.</summary>
    public class Document
    {
        public int Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string StoredFileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public int? PropertyId { get; set; }
        public int? LeaseId { get; set; }
        public int? RecoveryCaseId { get; set; }
        public int? SchemeApplicationId { get; set; }
        public int? AllocationProcessId { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public string UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string DeletedBy { get; set; }
        public string Shera { get; set; }
        public int? CalculationId { get; set; }
        public int? ComplaintId { get; set; }
        public int? LandRegistrationId { get; set; }
    }
}
