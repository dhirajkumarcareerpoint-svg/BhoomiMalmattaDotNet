using System;

namespace SMC.BhoomiMalmatta.Models.Entities
{
    /// <summary>Maps existing table dbo.TR_AST_SchemeApplications. Never created or altered by the application.</summary>
    public class SchemeApplication
    {
        public int Id { get; set; }
        public int? PropertyId { get; set; }
        public string SchemeType { get; set; } = string.Empty;
        public string ApplicantName { get; set; } = string.Empty;
        public string ApplicantMobile { get; set; }
        public DateTime ApplicationDate { get; set; }
        public decimal OriginalOutstanding { get; set; }
        public decimal WaivedAmount { get; set; }
        public decimal PayableAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? DecisionDate { get; set; }
        public string ApprovedBy { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public string UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string DeletedBy { get; set; }
        public string Shera { get; set; }
        public string PropertyCategory { get; set; }
    }
}
