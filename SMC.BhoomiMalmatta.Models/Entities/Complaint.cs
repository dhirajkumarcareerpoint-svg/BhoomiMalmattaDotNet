using System;

namespace SMC.BhoomiMalmatta.Models.Entities
{
    /// <summary>Maps existing table dbo.TR_CFC_Complaints. Never created or altered by the application.</summary>
    public class Complaint
    {
        public int Id { get; set; }
        public string RecordNumber { get; set; }
        public string ComplaintType { get; set; } = string.Empty;
        public int? PropertyId { get; set; }
        public string ComplainantName { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string ComplaintDetails { get; set; } = string.Empty;
        public DateTime ComplaintDate { get; set; }
        public string AssignedOfficer { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? ResolutionDate { get; set; }
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
