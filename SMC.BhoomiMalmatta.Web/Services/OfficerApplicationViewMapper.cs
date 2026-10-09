using System;
using System.Globalization;
using System.Linq;
using SMC.BhoomiMalmatta.Business.Citizen;
using SMC.BhoomiMalmatta.Business.Workflow;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;

namespace SMC.BhoomiMalmatta.Web.Services
{
    /// <summary>Builds an allow-listed, read-only response for the officer detail dialog.</summary>
    public static class OfficerApplicationViewMapper
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static bool ZoneAllows(string role, int? ticketZone, string applicationZone)
        {
            if (role == Roles.Admin) return true;
            if (!Roles.OfficerRoles.Contains(role) || !ticketZone.HasValue) return Roles.OfficerRoles.Contains(role);
            return string.Equals(applicationZone, ticketZone.Value.ToString(Inv), StringComparison.Ordinal);
        }

        public static OfficerApplicationView Build(ApplicationDetail d, string role, string applicationRoot)
        { return Build(d, role, applicationRoot, WorkflowPolicy.Default); }

        public static OfficerApplicationView Build(ApplicationDetail d, string role, string applicationRoot, WorkflowPolicy policy)
        {
            if (d == null) throw new ArgumentNullException("d");
            var root = (applicationRoot ?? string.Empty).TrimEnd('/');
            var view = new OfficerApplicationView
            {
                Id = d.Id, ApplicationNumber = d.ApplicationNumber, ApplicantName = OfficerText.OrDash(d.ApplicantName), Mobile = OfficerText.OrDash(d.Mobile), Email = OfficerText.OrDash(d.Email),
                StageLabel = StageLabel(d.Stage, d.Status, d.PaymentStatus),
                PaymentStatusLabel = PaymentLabel(d.PaymentStatus), Amount = d.PayableAmount != 0 ? d.PayableAmount : d.FeeAmount ?? 0,
                AmountLabel = "₹ " + (d.PayableAmount != 0 ? d.PayableAmount : d.FeeAmount ?? 0).ToString("N2", Inv),
                IsLegacy = d.IsLegacy, ReadOnly = d.IsLegacy || role == Roles.Admin || role == Roles.Staff,
                CanUploadDocuments = false, CanVerifyDocuments = false,
                ShowPayment = d.PaymentStatus != PaymentStatuses.NotRequired || !string.IsNullOrWhiteSpace(d.PaymentReference) || d.PaymentDate.HasValue
            };
            var serviceDisplay = ServiceDisplay.Get(d.ServiceType, d.BusinessType, d.OtherBusinessType);
            Add(view, "सेवा", string.Equals(serviceDisplay.ServiceName, d.ServiceType, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(d.ServiceName) ? d.ServiceName : serviceDisplay.ServiceName);
            Add(view, "अर्जदाराचा प्रकार", d.ApplicantType); Add(view, "व्यवसायाचा प्रकार", serviceDisplay.SubServiceName); Add(view, "GST", d.GstNumber);
            Add(view, "लांबी (ft)", Number(d.LengthFt)); Add(view, "रुंदी (ft)", Number(d.WidthFt)); Add(view, "एकूण क्षेत्रफळ (sq ft)", Number(d.AreaSqFt));
            Add(view, "गणना दर / रक्कम", (d.CalculatedRate.HasValue ? "₹ " + d.CalculatedRate.Value.ToString("N2", Inv) : "—") + " / " + Money(d.FeeAmount));
            Add(view, "जागेची आवश्यकता", d.SpaceRequirement); Add(view, "ठिकाण", d.Location); Add(view, "उपलब्ध जागा", d.AvailableSpace);
            Add(view, "मागणीचा कालावधी", d.RequiredDuration); Add(view, "सुविधा", Facilities(d));
            Add(view, "सुरुवात", ApplicantDateFormatter.Format(d.StartDate, "dd/MM/yyyy", ApplicantDateFormatter.UseDevanagariDigits));
            Add(view, "शेवट", ApplicantDateFormatter.Format(d.EndDate, "dd/MM/yyyy", ApplicantDateFormatter.UseDevanagariDigits));
            Add(view, "एकूण दिवस", DemandDays(d).ToString(Inv)); Add(view, "झोन", d.Zone);
            Add(view, "पत्ता", d.CorrespondenceAddress); Add(view, "कायमचा पत्ता", d.PermanentAddress);
            Add(view, "राज्य / जिल्हा", Join(d.State, d.District)); Add(view, "शहर / तालुका / पिन", Join(d.City, d.Taluka, d.PinCode)); Add(view, "प्रभाग", d.Prabhag); Add(view, "वर्णन", d.ServiceDescription);
            Add(view, "इतर माहिती", d.OtherInformation);

            var documents = (d.Documents ?? new System.Collections.Generic.List<DocumentItem>()).Where(x => role != Roles.Staff || !x.IsZoneDocument).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToList();
            view.Documents = documents.Select(x => Document(x, d.Id, root)).ToList();
            var photo = documents.FirstOrDefault(x => IsSitePhoto(x.DocumentType) && (x.ContentType == "image/jpeg" || x.ContentType == "image/png"));
            view.SitePhotoUrl = photo == null ? null : DocumentUrl(photo, d.Id, root, true);
            view.SitePhotoMessage = photo == null ? "प्रत्यक्ष जागेचा फोटो उपलब्ध नाही." : null;
            var proof = documents.LastOrDefault(x => x.IsZoneDocument && (x.DocumentType == "PaymentProof" || x.DocumentType == "Payment Proof"));
            view.Payment = new OfficerPaymentView
            {
                Reference = EmptyDash(d.PaymentReference),
                Date = d.PaymentDate.HasValue ? ApplicantDateFormatter.Format(d.PaymentDate.Value, "dd/MM/yyyy", ApplicantDateFormatter.UseDevanagariDigits) : "-",
                ProofFileName = proof == null ? "-" : proof.FileName,
                VerificationState = PaymentLabel(d.PaymentStatus),
                Verifier = EmptyDash(d.PaymentVerifier),
                ProofPreviewLabel = OfficerText.PreviewProof
            };
            // Approved zone applications always have a letter (generated lazily by the download handler if needed).
            if ((!d.IsLegacy && d.Stage == ZoneStages.Approved) || (d.IsLegacy && d.HasCertificate))
            {
                view.CertificateUrl = root + "/Download.ashx?type=cert&appId=" + Uri.EscapeDataString(d.Id.ToString(Inv));
                view.CertificateLabel = OfficerText.CertificateLink;
            }
            if (proof != null)
            {
                var mappedProof = view.Documents.FirstOrDefault(x => x.Id == proof.Id);
                if (mappedProof != null) { view.Payment.ProofPreviewUrl = mappedProof.PreviewUrl; view.Payment.ProofDownloadUrl = mappedProof.DownloadUrl; }
            }
            view.History = (d.History ?? new System.Collections.Generic.List<HistoryItem>()).OrderBy(x => x.Sequence).ThenBy(x => x.AtUtc)
                .Select(x => new OfficerHistoryView { Sequence = x.Sequence, AtUtc = x.AtUtc, DateLabel = ApplicantDateFormatter.Format(x.AtUtc.ToLocalTime(), "dd/MM/yyyy HH:mm", ApplicantDateFormatter.UseDevanagariDigits), Action = ActionMr(x.Action), Role = x.FromRole, OfficerName = x.By, Remark = !string.IsNullOrWhiteSpace(x.Remarks) ? x.Remarks : PaymentNote(x) }).ToList();
            if (!view.ReadOnly)
                view.AllowedActionLabels = ZoneWorkflowMachine.OfficerListActions(d.ProcessingRoute, d.Stage, role, d.PaymentStatus, policy).Select(OfficerText.Action).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            return view;
        }

        private static OfficerDocumentView Document(DocumentItem x, int appId, string root)
        {
            return new OfficerDocumentView { Id = x.Id, Type = DocumentTypeMr(x.DocumentType), FileName = x.FileName,
                VerificationStatus = x.VerificationStatus, RequestRemark = x.RequestRemark,
                PreviewUrl = DocumentUrl(x, appId, root, true), DownloadUrl = DocumentUrl(x, appId, root, false) };
        }

        private static string DocumentUrl(DocumentItem x, int appId, string root, bool preview)
        {
            var id = Uri.EscapeDataString(x.Id.ToString(Inv));
            var url = x.IsZoneDocument ? root + "/Download.ashx?type=zdoc&id=" + id + "&appId=" + Uri.EscapeDataString(appId.ToString(Inv)) : root + "/Download.ashx?type=doc&id=" + id;
            return preview && (x.ContentType == "application/pdf" || x.ContentType == "image/jpeg" || x.ContentType == "image/png") ? url + "&inline=1" : url;
        }

        private static void Add(OfficerApplicationView view, string label, string value)
        { view.Fields.Add(new OfficerDetailField { Label = label, Value = string.IsNullOrWhiteSpace(value) ? "-" : value }); }
        private static string Number(decimal? value) { return value.HasValue ? value.Value.ToString("0.##", Inv) : "-"; }
        private static string EmptyDash(string value) { return string.IsNullOrWhiteSpace(value) ? "-" : value; }

        /// <summary>UTR and date of an applicant payment submission, so an earlier (rejected) submission stays readable in the history.</summary>
        private static string PaymentNote(HistoryItem x)
        {
            if (string.IsNullOrWhiteSpace(x.PaymentReference)) return null;
            return "UTR " + x.PaymentReference + (x.PaymentDate.HasValue ? ", " + ApplicantDateFormatter.Format(x.PaymentDate.Value, "dd/MM/yyyy", ApplicantDateFormatter.UseDevanagariDigits) : string.Empty);
        }
        private static string Money(decimal? value) { return value.HasValue ? "₹ " + value.Value.ToString("N2", Inv) : "-"; }
        private static int DemandDays(ApplicationDetail d) { return d.TotalDays > 0 ? d.TotalDays : (d.EndDate.Date - d.StartDate.Date).Days + 1; }
        private static string Join(params string[] values) { return string.Join(" / ", values.Where(x => !string.IsNullOrWhiteSpace(x))); }
        private static string Facilities(ApplicationDetail d)
        {
            var values = new System.Collections.Generic.List<string>();
            if (d.ElectricityRequired) values.Add("वीज"); if (d.WaterRequired) values.Add("पाणी");
            if (!string.IsNullOrWhiteSpace(d.RequiredTime)) values.Add(d.RequiredTime);
            if (!string.IsNullOrWhiteSpace(d.OtherFacilities)) values.Add(d.OtherFacilities);
            if (!string.IsNullOrWhiteSpace(d.WasteManagement)) values.Add(d.WasteManagement);
            return string.Join(", ", values);
        }
        private static bool IsSitePhoto(string type)
        { return !string.IsNullOrWhiteSpace(type) && (type.IndexOf("photo", StringComparison.OrdinalIgnoreCase) >= 0 || type.IndexOf("फोटो", StringComparison.OrdinalIgnoreCase) >= 0); }
        private static string DocumentTypeMr(string type)
        {
            switch (type ?? string.Empty)
            {
                case DocumentTypes.IdentityProof: return "ओळखपत्र";
                case DocumentTypes.AddressProof: return "पत्त्याचा पुरावा";
                case DocumentTypes.Level1Correction: return "JE दुरुस्ती कागदपत्र";
                case DocumentTypes.Level2Correction: return "OS दुरुस्ती कागदपत्र";
                case "PaymentProof": return "भरणा पुरावा";
                default: return type ?? "कागदपत्र";
            }
        }
        public static string StageLabel(string stage, string status, string paymentStatus)
        {
            switch (stage ?? string.Empty)
            {
                case ZoneStages.JEPending: return "JE तपासणीसाठी प्रलंबित";
                case ZoneStages.JECorrectionRequested: case ZoneStages.OSCorrectionRequested: return "अर्जदाराकडून दुरुस्ती आवश्यक";
                case ZoneStages.OSPending: return "OS तपासणीसाठी प्रलंबित";
                case ZoneStages.PaymentRequired: return OfficerText.PaymentStagePill(paymentStatus);
                case ZoneStages.AssistantCommissionerApprovalPending: return "सहाय्यक आयुक्त मंजुरीसाठी प्रलंबित";
                case ZoneStages.Approved: return "मंजूर";
                case ZoneStages.Rejected: return "नामंजूर";
                default: return status == ApplicationStatuses.Approved ? "मंजूर (जुना अर्ज)" : status == ApplicationStatuses.Rejected ? "नामंजूर (जुना अर्ज)" : "जुना अर्ज — वाचनापुरता";
            }
        }
        private static string PaymentLabel(string status)
        {
            switch (status ?? string.Empty)
            {
                case PaymentStatuses.NotRequired: return "लागू नाही";
                case PaymentStatuses.PaymentRequired: case PaymentStatuses.FeePending: case PaymentStatuses.PendingOSAmount: return "भरणा आवश्यक";
                case PaymentStatuses.PaymentSubmitted: return "भरणा पडताळणी प्रलंबित";
                case PaymentStatuses.PaymentRejected: return "भरणा नाकारला";
                case PaymentStatuses.PaymentDone: return "भरणा प्राप्त";
                default: return "उपलब्ध नाही";
            }
        }
        private static string ActionMr(string action)
        {
            switch (action ?? string.Empty)
            {
                case AuditActions.ApplicationSubmitted: return "अर्ज सादर केला";
                case AuditActions.JEVerified: return "अर्ज स्वीकारून OS कडे पाठवला";
                case AuditActions.JEReturnedForCorrection: return "JE कडून दुरुस्तीसाठी परत";
                case AuditActions.OSReturnedForCorrection: return "OS कडून दुरुस्तीसाठी परत";
                case AuditActions.JERejected: return "JE ने अर्ज नाकारला";
                case AuditActions.OSRejected: return "OS ने अर्ज नाकारला";
                case AuditActions.ACRejected: return "AC ने अर्ज नाकारला";
                case AuditActions.CitizenResubmitted: return "अर्जदाराने दुरुस्त अर्ज पुन्हा सादर केला";
                case AuditActions.PaymentRequestSent: return "भरणा विनंती पाठवली";
                case AuditActions.PaymentNotRequired: return "भरणा लागू नाही";
                case AuditActions.PaymentSubmittedByApplicant: return "अर्जदाराने भरणा तपशील सादर केले";
                case AuditActions.PaymentRejectedByOs: return "OS ने भरणा पुरावा नाकारला";
                case AuditActions.OSVerified: return "OS ने कागदपत्र पडताळले";
                case AuditActions.PaymentDone: return "भरणा पडताळला";
                case AuditActions.ForwardedToAC: return "AC कडे पाठवले";
                case AuditActions.FinalApproved: return "अंतिम मंजुरी दिली";
                case AuditActions.ACReturned: return "AC कडून OS कडे परत";
                case AuditActions.CertificateGenerated: return "प्रमाणपत्र तयार केले";
                case AuditActions.Reopened: return "अर्ज पुन्हा उघडला";
                default: return action ?? string.Empty;
            }
        }
    }
}
