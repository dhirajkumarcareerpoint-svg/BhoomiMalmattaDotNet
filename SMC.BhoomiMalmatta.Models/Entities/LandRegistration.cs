using System;

namespace SMC.BhoomiMalmatta.Models.Entities
{
    /// <summary>Maps existing table dbo.TR_AST_LandRegistrations. Never created or altered by the application.</summary>
    public class LandRegistration
    {
        public int Id { get; set; }
        public string RecordNumber { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public int? PropertyId { get; set; }
        public string SurveyNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal Area { get; set; }
        public string PurchaseDeedNumber { get; set; } = string.Empty;
        public DateTime? PurchaseDeedDate { get; set; }
        public string OwnerDetails { get; set; } = string.Empty;
        public string RegistrationStatus { get; set; } = string.Empty;
        public DateTime? RegistrationDate { get; set; }
        public string Status { get; set; } = string.Empty;
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
