using System.Collections.Generic;
using System.Linq;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Models.Constants;

namespace SMC.BhoomiMalmatta.Web.Services
{
    /// <summary>
    /// Step layout of the three-step "मागणी अर्ज" wizard (Apply.aspx). Step indexes are 0-based (MultiView.ActiveViewIndex);
    /// validation groups are g1..g3. The server-side SubmissionValidator stays the single source of the rules.
    /// </summary>
    public static class ApplyWizard
    {
        public const int ServiceStep = 0;
        public const int AddressStep = 1;
        public const int DocumentsStep = 2;
        public static readonly string[] Titles = { "सेवेची माहिती", "पत्ता व जागा", "कागदपत्रे व सादरीकरण" };
        public static int Count { get { return Titles.Length; } }

        /// <summary>Applicant type stored for wizard submissions (the field is no longer asked; existing default "वैयक्तिक").</summary>
        public const string DefaultApplicantType = "वैयक्तिक";

        public static string ValidationGroup(int stepIndex) { return "g" + (stepIndex + 1); }

        /// <summary>Progress-bar width for a step, e.g. 33.33% / 66.67% / 100%.</summary>
        public static string ProgressWidth(int stepIndex)
        {
            return ((stepIndex + 1) * 100m / Count).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>The step that owns a validation field (errors are shown on, and block, that step).</summary>
        public static int StepOf(string field)
        {
            switch (field)
            {
                case "ServiceType": case "BusinessType": case "OtherBusinessType": return ServiceStep;
                case "ApplicantType": case "ApplicantName": case "Mobile": case "Email": case "IdentityNumber": case "PanNumber": case "GstNumber":
                case "PermanentAddress": case "CorrespondenceAddress":
                case "State": case "District": case "City": case "Taluka": case "PinCode": case "LengthFt": case "WidthFt": case "Pricing": case "StartDate": case "EndDate":
                case "Zone": case "Location": return AddressStep;
                default: return DocumentsStep; // DeclarationAccepted, Documents
            }
        }

        /// <summary>Web.config "Apply.CollectApplicantDetails" (default false): whether the wizard shows and requires name / mobile / e-mail.</summary>
        public const string CollectApplicantDetailsSetting = "Apply.CollectApplicantDetails";

        /// <summary>
        /// Server-side errors that block "Next" on <paramref name="stepIndex"/>: the full validator runs with the final-step fields
        /// (declaration, documents) treated as satisfied, and only errors of this or an earlier step are kept.
        /// </summary>
        public static List<FieldError> BlockingErrors(SubmissionData data, int stepIndex, ServiceCatalog catalog)
        { return BlockingErrors(data, stepIndex, catalog, true); }

        public static List<FieldError> BlockingErrors(SubmissionData data, int stepIndex, ServiceCatalog catalog, bool collectApplicantDetails)
        {
            var probe = data.Clone();
            probe.DeclarationAccepted = true; probe.UploadedDocumentTypes = DocumentTypes.RequiredForSubmission.ToList();
            return SubmissionValidator.Validate(SubmissionValidator.Normalize(probe), catalog, collectApplicantDetails).Where(x => StepOf(x.Field) <= stepIndex).ToList();
        }
    }
}
