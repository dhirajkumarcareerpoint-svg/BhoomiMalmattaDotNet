using System;
using System.Collections.Generic;

namespace SMC.BhoomiMalmatta.Business.Abstractions
{
    /// <summary>Citizen input after trimming. ClientClaimedAmount is untrusted and never determines a saved fee.</summary>
    public sealed class SubmissionData
    {
        public string ServiceType { get; set; }
        public string BusinessType { get; set; }
        public string OtherBusinessType { get; set; }
        public string ApplicantType { get; set; }
        public string ApplicantName { get; set; }
        public string Mobile { get; set; }
        public string Email { get; set; }
        public string GstNumber { get; set; }
        public string IdentityNumber { get; set; }
        public string PanNumber { get; set; }
        public string PermanentAddress { get; set; }
        public string CorrespondenceAddress { get; set; }
        public bool SameAddress { get; set; }
        public string State { get; set; }
        public string District { get; set; }
        public string City { get; set; }
        public string Taluka { get; set; }
        public string PinCode { get; set; }
        public string Zone { get; set; }
        public string Prabhag { get; set; }
        public string Location { get; set; }
        public string ServiceDescription { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal? LengthFt { get; set; }
        public decimal? WidthFt { get; set; }
        public bool DeclarationAccepted { get; set; }
        public List<string> UploadedDocumentTypes { get; set; }
        public decimal? ClientClaimedAmount { get; set; }

        public SubmissionData Clone() { return (SubmissionData)MemberwiseClone(); }
    }

    public sealed class FieldError
    {
        public FieldError(string field, string message) { Field = field; Message = message; }
        public string Field { get; private set; }
        public string Message { get; private set; }
    }
}
