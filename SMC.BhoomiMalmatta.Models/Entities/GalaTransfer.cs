using System;

namespace SMC.BhoomiMalmatta.Models.Entities
{
    /// <summary>Maps existing table dbo.TR_AST_GalaTransfers. Never created or altered by the application.</summary>
    public class GalaTransfer
    {
        public int Id { get; set; }
        public int PropertyId { get; set; }
        public string Category { get; set; } = string.Empty;
        public string ExistingHolder { get; set; } = string.Empty;
        public string CurrentOccupant { get; set; } = string.Empty;
        public string NewHolder { get; set; } = string.Empty;
        public string TransferType { get; set; } = string.Empty;
        public bool IsBreach { get; set; }
        public decimal TransferFee { get; set; }
        public decimal Penalty { get; set; }
        public string Stage { get; set; } = string.Empty;
        public string PaymentReference { get; set; }
        public DateTime? PaymentVerifiedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string ApprovedBy { get; set; }
        public string DecisionRemark { get; set; }
        public byte[] Version { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public string UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string DeletedBy { get; set; }
        public string Shera { get; set; }
        public string PropertyHolderAtRequest { get; set; }
    }
}
