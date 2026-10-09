using System;

namespace SMC.BhoomiMalmatta.Models.Entities
{
    /// <summary>Maps existing table dbo.TR_AST_RecoveryCases. Never created or altered by the application.</summary>
    public class RecoveryCase
    {
        public int Id { get; set; }
        public int? PropertyId { get; set; }
        public int? LeaseId { get; set; }
        public int MonthsOverdue { get; set; }
        public decimal OutstandingAmount { get; set; }
        public string Stage { get; set; } = string.Empty;
        public string NoticeNumber { get; set; }
        public DateTime? NoticeDate { get; set; }
        public decimal RecoveredAmount { get; set; }
        public DateTime? RecoveryDate { get; set; }
        public DateTime? SealDate { get; set; }
        public DateTime? ReAuctionDate { get; set; }
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
