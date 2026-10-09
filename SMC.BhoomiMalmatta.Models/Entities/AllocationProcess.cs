using System;

namespace SMC.BhoomiMalmatta.Models.Entities
{
    /// <summary>Maps existing table dbo.TR_AST_AllocationProcesses. Never created or altered by the application.</summary>
    public class AllocationProcess
    {
        public int Id { get; set; }
        public int? PropertyId { get; set; }
        public string Method { get; set; } = string.Empty;
        public string NoticeNumber { get; set; }
        public DateTime PublishDate { get; set; }
        public DateTime? LastDateToApply { get; set; }
        public DateTime? AuctionDate { get; set; }
        public decimal? ReserveAmount { get; set; }
        public decimal? HighestBidAmount { get; set; }
        public string HighestBidderName { get; set; }
        public string HighestBidderMobile { get; set; }
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
