using System;

namespace SMC.BhoomiMalmatta.Models.Entities
{
    /// <summary>Maps existing table dbo.MR_CFC_SmsTemplates. Never created or altered by the application.</summary>
    public class SmsTemplate
    {
        public int Id { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string TemplateName { get; set; } = string.Empty;
        public string MessageBody { get; set; } = string.Empty;
        public string VariableMapping { get; set; } = string.Empty;
        public string DltTemplateId { get; set; }
        public string SenderId { get; set; }
        public string Language { get; set; } = string.Empty;
        public int Version { get; set; }
        public bool IsActive { get; set; }
        public string ApprovalStatus { get; set; } = string.Empty;
        public string SampleMessage { get; set; }
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
