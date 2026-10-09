using System;

namespace SMC.BhoomiMalmatta.Models.Entities
{
    /// <summary>Maps existing table dbo.MR_CFC_PermitConfigurations. Never created or altered by the application.</summary>
    public class PermitConfiguration
    {
        public int Id { get; set; }
        public string OrganizationName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string CertificateTitle { get; set; } = string.Empty;
        public string AuthorityDesignation { get; set; } = string.Empty;
        public string FooterText { get; set; } = string.Empty;
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
