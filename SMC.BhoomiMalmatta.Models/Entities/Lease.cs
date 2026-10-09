using System;

namespace SMC.BhoomiMalmatta.Models.Entities
{
    /// <summary>Maps existing table dbo.TR_AST_Leases. Never created or altered by the application.</summary>
    public class Lease
    {
        public int Id { get; set; }
        public int? PropertyId { get; set; }
        public string LesseeName { get; set; } = string.Empty;
        public string LesseeMobile { get; set; }
        public string LesseeAddress { get; set; }
        public string DeedNumber { get; set; } = string.Empty;
        public DateTime DeedDate { get; set; }
        public string DurationType { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal RentAmount { get; set; }
        public decimal? SecurityDeposit { get; set; }
        public string Status { get; set; } = string.Empty;
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
