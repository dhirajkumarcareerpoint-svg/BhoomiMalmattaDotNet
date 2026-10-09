using System;

namespace SMC.BhoomiMalmatta.Models.Entities
{
    /// <summary>Maps existing table dbo.TR_CFC_DemandApplications. Never created or altered by the application.</summary>
    public class DemandApplication
    {
        public int Id { get; set; }
        public string ApplicationNumber { get; set; } = string.Empty;
        public string ServiceType { get; set; } = string.Empty;
        public string BusinessType { get; set; }
        public string OtherBusinessType { get; set; }
        public string ApplicantType { get; set; }
        public string ApplicantName { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string IdentityNumber { get; set; }
        public string PanNumber { get; set; }
        public string GstNumber { get; set; } = string.Empty;
        public string PermanentAddress { get; set; } = string.Empty;
        public string CorrespondenceAddress { get; set; } = string.Empty;
        public bool SameAddress { get; set; }
        public string State { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Taluka { get; set; } = string.Empty;
        public string PinCode { get; set; } = string.Empty;
        public string Zone { get; set; } = string.Empty;
        public string Prabhag { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string AvailableSpace { get; set; } = string.Empty;
        public decimal? AreaSqFt { get; set; }
        public string ServiceDescription { get; set; } = string.Empty;
        public string SpaceRequirement { get; set; } = string.Empty;
        public string OtherInformation { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string RequiredDuration { get; set; } = string.Empty;
        public string RequiredTime { get; set; } = string.Empty;
        public bool ElectricityRequired { get; set; }
        public bool WaterRequired { get; set; }
        public string OtherFacilities { get; set; } = string.Empty;
        public string WasteManagement { get; set; } = string.Empty;
        public bool DeclarationAccepted { get; set; }
        public decimal? FeeAmount { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? SubmittedAt { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public string UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string DeletedBy { get; set; }
        public string Shera { get; set; }
        public string ApplicantAccessTokenHash { get; set; }
        public decimal? CalculatedRate { get; set; }
        public decimal? LengthFt { get; set; }
        public decimal? WidthFt { get; set; }
        public string ProcessingRoute { get; set; } = string.Empty;
    }
}
