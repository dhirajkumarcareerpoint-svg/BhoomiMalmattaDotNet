using System;

namespace SMC.BhoomiMalmatta.Models.Entities
{
    /// <summary>Maps existing table dbo.TR_CFC_SmsEvents. Never created or altered by the application.</summary>
    public class SmsEvent
    {
        public int Id { get; set; }
        public string RecipientMobile { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string ApplicationNumber { get; set; }
        public string TemplateReference { get; set; }
        public string Status { get; set; } = string.Empty;
        public string ProviderMessageId { get; set; }
        public DateTime? SentAt { get; set; }
        public string FailureReason { get; set; }
        public int RetryCount { get; set; }
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
