using System;

namespace SMC.BhoomiMalmatta.Models.Entities
{
    /// <summary>Maps existing table dbo.MR_CFC_DemandServiceRates. Never created or altered by the application.</summary>
    public class DemandServiceRate
    {
        public int Id { get; set; }
        public string ServiceCode { get; set; } = string.Empty;
        public decimal? Rate { get; set; }
        public string Unit { get; set; } = string.Empty;
        public bool IsActive { get; set; }
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
