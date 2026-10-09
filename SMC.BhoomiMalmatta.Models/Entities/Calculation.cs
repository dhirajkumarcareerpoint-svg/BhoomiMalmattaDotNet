using System;

namespace SMC.BhoomiMalmatta.Models.Entities
{
    /// <summary>Maps existing table dbo.TR_AST_Calculations. Never created or altered by the application.</summary>
    public class Calculation
    {
        public int Id { get; set; }
        public int PropertyId { get; set; }
        public decimal? Rate { get; set; }
        public int PeriodMonths { get; set; }
        public decimal? PreviousOutstanding { get; set; }
        public decimal? CurrentDemand { get; set; }
        public decimal CalculatedAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime CalculationDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Shera { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public string UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string DeletedBy { get; set; }
    }
}
