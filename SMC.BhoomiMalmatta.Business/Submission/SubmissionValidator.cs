using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text.RegularExpressions;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Business.Pricing;

namespace SMC.BhoomiMalmatta.Business.Abstractions
{
    public static class SubmissionValidator
    {
        public const string LockedState = "Maharashtra";
        public const string LockedDistrict = "Solapur";
        public const string LockedCity = "सोलापूर";
        public static readonly string[] AllowedTalukas = { "मध्य सोलापूर", "दक्षिण सोलापूर", "उत्तर सोलापूर" };
        private static readonly Regex Mobile = new Regex(@"^[6-9][0-9]{9}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex Pin = new Regex(@"^\d{6}$", RegexOptions.Compiled);
        private static readonly Regex Gst = new Regex(@"^\d{2}[A-Z]{5}\d{4}[A-Z][1-9A-Z]Z[0-9A-Z]$", RegexOptions.Compiled);
        private static readonly Regex Aadhaar = new Regex(@"^[0-9]{12}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex Pan = new Regex(@"^[A-Z]{5}[0-9]{4}[A-Z]$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static string T(string s) { return (s ?? string.Empty).Trim(); }
        private static string NullIfBlank(string s) { return string.IsNullOrWhiteSpace(s) ? null : s.Trim(); }

        /// <summary>Trims every text value; copies the permanent address when SameAddress is set.</summary>
        public static SubmissionData Normalize(SubmissionData d)
        {
            var n = d.Clone();
            var perm = T(d.PermanentAddress);
            n.ServiceType = T(d.ServiceType);
            n.EndDate = DemandServicePricingRules.EffectiveEndDate(n.ServiceType, d.StartDate, d.EndDate);
            n.BusinessType = NullIfBlank(d.BusinessType);
            n.OtherBusinessType = NullIfBlank(d.OtherBusinessType);
            n.ApplicantType = NullIfBlank(d.ApplicantType);
            n.ApplicantName = T(d.ApplicantName);
            n.Mobile = T(d.Mobile);
            n.Email = T(d.Email).ToLowerInvariant();
            n.GstNumber = NullIfBlank(d.GstNumber) == null ? null : T(d.GstNumber).ToUpperInvariant();
            n.IdentityNumber = NullIfBlank(d.IdentityNumber);
            n.PanNumber = NullIfBlank(d.PanNumber) == null ? null : T(d.PanNumber).ToUpperInvariant();
            n.PermanentAddress = perm;
            n.CorrespondenceAddress = d.SameAddress ? perm : T(d.CorrespondenceAddress);
            n.State = T(d.State); n.District = T(d.District); n.City = LockedCity; n.Taluka = T(d.Taluka); n.PinCode = T(d.PinCode);
            n.Zone = T(d.Zone); n.Prabhag = NullIfBlank(d.Prabhag); n.Location = T(d.Location); n.ServiceDescription = T(d.ServiceDescription);
            if (n.UploadedDocumentTypes == null) n.UploadedDocumentTypes = new List<string>();
            return n;
        }

        public static List<FieldError> Validate(SubmissionData d, ServiceCatalog catalog) { return Validate(d, catalog, true); }

        /// <param name="applicantDetailsRequired">
        /// Apply.CollectApplicantDetails: when false, name and mobile are not required (they will come from a separate form);
        /// a mobile or e-mail that is supplied anyway is still format-checked.
        /// </param>
        public static List<FieldError> Validate(SubmissionData d, ServiceCatalog catalog, bool applicantDetailsRequired)
        {
            var e = new List<FieldError>();
            Action<string, string, string> req = (field, v, label) => { if (string.IsNullOrWhiteSpace(v)) e.Add(new FieldError(field, label + " आवश्यक आहे.")); };

            if (string.IsNullOrWhiteSpace(d.ServiceType) || !catalog.ServiceCodes.Contains(d.ServiceType)) e.Add(new FieldError("ServiceType", "कृपया वैध सेवा निवडा."));
            var areaBased = DemandServicePricingRules.IsAreaBased(d.ServiceType);
            if (areaBased)
            {
                if (string.IsNullOrWhiteSpace(d.BusinessType) || !catalog.BusinessTypeCodes.Contains(d.BusinessType)) e.Add(new FieldError("BusinessType", "कृपया वैध व्यवसाय प्रकार निवडा."));
                else if (d.BusinessType == "Other" && string.IsNullOrWhiteSpace(d.OtherBusinessType)) e.Add(new FieldError("OtherBusinessType", "इतर व्यवसायाचा तपशील आवश्यक आहे."));
            }
            if (applicantDetailsRequired) req("ApplicantName", d.ApplicantName, "अर्जदाराचे नाव");
            if ((applicantDetailsRequired || !string.IsNullOrWhiteSpace(d.Mobile)) && !Mobile.IsMatch(d.Mobile ?? "")) e.Add(new FieldError("Mobile", "मोबाइल क्रमांक १० अंकी आणि ६, ७, ८ किंवा ९ ने सुरू असावा."));
            if (!string.IsNullOrWhiteSpace(d.Email) && !IsEmail(d.Email)) e.Add(new FieldError("Email", "कृपया वैध ई-मेल पत्ता भरा."));
            if (d.GstNumber != null && !Gst.IsMatch(d.GstNumber)) e.Add(new FieldError("GstNumber", "GST क्रमांक वैध नाही."));
            if (d.IdentityNumber != null && !Aadhaar.IsMatch(d.IdentityNumber)) e.Add(new FieldError("IdentityNumber", "आधार/ओळखपत्र क्रमांक १२ अंकी असावा."));
            if (d.PanNumber != null && !Pan.IsMatch(d.PanNumber)) e.Add(new FieldError("PanNumber", "PAN क्रमांक वैध नाही."));
            req("PermanentAddress", d.PermanentAddress, "कायमचा पत्ता");
            req("CorrespondenceAddress", d.CorrespondenceAddress, "पत्रव्यवहाराचा पत्ता");
            if (!string.Equals(d.State, LockedState, StringComparison.Ordinal)) e.Add(new FieldError("State", "राज्य 'महाराष्ट्र' असणे आवश्यक आहे."));
            if (!string.Equals(d.District, LockedDistrict, StringComparison.Ordinal)) e.Add(new FieldError("District", "जिल्हा 'सोलापूर' असणे आवश्यक आहे."));
            if (!string.Equals(d.City, LockedCity, StringComparison.Ordinal)) e.Add(new FieldError("City", "शहर 'सोलापूर' असणे आवश्यक आहे."));
            if (string.IsNullOrWhiteSpace(d.Taluka) || !AllowedTalukas.Contains(d.Taluka)) e.Add(new FieldError("Taluka", "कृपया वैध तालुका निवडा."));
            if (!Pin.IsMatch(d.PinCode ?? "")) e.Add(new FieldError("PinCode", "कृपया वैध ६ अंकी पिनकोड भरा."));
            if (d.StartDate == default(DateTime)) e.Add(new FieldError("StartDate", "प्रारंभ तारीख आवश्यक आहे."));
            if (areaBased)
            {
                if (d.EndDate == default(DateTime) || d.EndDate.Date < d.StartDate.Date) e.Add(new FieldError("EndDate", "कृपया वैध समाप्ती तारीख निवडा."));
                if (!d.LengthFt.HasValue || d.LengthFt.Value <= 0) e.Add(new FieldError("LengthFt", "लांबी शून्यापेक्षा जास्त असणे आवश्यक आहे."));
                if (!d.WidthFt.HasValue || d.WidthFt.Value <= 0) e.Add(new FieldError("WidthFt", "रुंदी शून्यापेक्षा जास्त असणे आवश्यक आहे."));
            }
            int z;
            if (!int.TryParse(d.Zone, out z) || z < 1 || z > 8) e.Add(new FieldError("Zone", "झोन १ ते ८ मधील असणे आवश्यक आहे."));
            req("Location", d.Location, "जागेचे ठिकाण");
            if (!d.DeclarationAccepted) e.Add(new FieldError("DeclarationAccepted", "घोषणापत्र मान्य करणे आवश्यक आहे."));
            foreach (var required in DocumentTypes.RequiredForSubmission)
                if (!d.UploadedDocumentTypes.Contains(required)) e.Add(new FieldError("Documents", "आवश्यक कागदपत्र जोडलेले नाही: " + required));
            return e;
        }

        private static bool IsEmail(string s)
        {
            try { var a = new MailAddress(s); return a.Address == s && s.IndexOf('.') >= 0 && s.Length <= 200; } catch (FormatException) { return false; }
        }
    }
}
