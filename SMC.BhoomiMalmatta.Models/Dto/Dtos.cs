using System;
using System.Collections.Generic;
using System.Linq;

namespace SMC.BhoomiMalmatta.Models.Dto
{
    public static class OfficerListViews
    {
        public const string Mine = "mine", All = "all", Processed = "processed", Approved = "approved", Rejected = "rejected", Correction = "correction";
        public static string DefaultForRole(string role)
        {
            return role == SMC.BhoomiMalmatta.Models.Constants.Roles.Admin || role == SMC.BhoomiMalmatta.Models.Constants.Roles.Staff ? All : Mine;
        }
    }

    public sealed class PagedResult<T>
    {
        public PagedResult() { Items = new List<T>(); }
        public List<T> Items { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public int TotalPages { get { return PageSize <= 0 ? 0 : (Total + PageSize - 1) / PageSize; } }
    }

    public sealed class PagerSummary
    {
        public int First { get; private set; }
        public int Last { get; private set; }
        public int Page { get; private set; }
        public int TotalPages { get; private set; }
        public int Total { get; private set; }
        public string RangeText { get { return First + " \u0924\u0947 " + Last + " \u092a\u0948\u0915\u0940 " + Total + " \u0928\u094b\u0902\u0926\u0940"; } }
        public string PageText { get { return "[" + Page + "] / " + TotalPages; } }
        public static PagerSummary Create(int total, int requestedPage, int pageSize)
        {
            total = Math.Max(0, total); pageSize = Math.Max(1, pageSize);
            var pages = Math.Max(1, (total + pageSize - 1) / pageSize); var page = Math.Max(1, Math.Min(pages, requestedPage));
            return new PagerSummary { Total = total, Page = page, TotalPages = pages, First = total == 0 ? 0 : ((page - 1) * pageSize) + 1, Last = Math.Min(page * pageSize, total) };
        }
    }

    public sealed class ApplicationFilter
    {
        public ApplicationFilter() { Page = 1; PageSize = 20; }
        public string Search { get; set; }
        public string Status { get; set; }
        /// <summary>Officer list view selector (mine, all, processed, approved, rejected, correction).</summary>
        public string OfficerView { get; set; }
        public string Stage { get; set; }
        public string ServiceType { get; set; }
        public int? Zone { get; set; }
        public string Route { get; set; }
        public string PaymentStatus { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        /// <summary>Set by the server from the signed-in officer; never taken from the browser.</summary>
        public int? ZoneScope { get; set; }
        public void Normalize()
        {
            Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
            Status = string.IsNullOrWhiteSpace(Status) ? null : Status.Trim();
            OfficerView = string.IsNullOrWhiteSpace(OfficerView) ? null : OfficerView.Trim();
            Stage = string.IsNullOrWhiteSpace(Stage) ? null : Stage.Trim();
            ServiceType = string.IsNullOrWhiteSpace(ServiceType) ? null : ServiceType.Trim();
            PaymentStatus = string.IsNullOrWhiteSpace(PaymentStatus) ? null : PaymentStatus.Trim();
            Route = string.IsNullOrWhiteSpace(Route) ? null : Route.Trim();
            From = From.HasValue ? From.Value.Date : (DateTime?)null;
            To = To.HasValue ? To.Value.Date : (DateTime?)null;
        }
    }

    public sealed class ApplicationListItem
    {
        public int Id { get; set; }
        public string ApplicationNumber { get; set; }
        public string ServiceType { get; set; }
        public string ServiceName { get; set; }
        public string SubServiceName { get; set; }
        public string BusinessType { get; set; }
        public string OtherBusinessType { get; set; }
        public string ApplicantName { get; set; }
        public string Mobile { get; set; }
        public string Zone { get; set; }
        public string Status { get; set; }
        public string Stage { get; set; }
        public string PaymentStatus { get; set; }
        public decimal? FeeAmount { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public string ProcessingRoute { get; set; }
        public string RowVersion { get; set; }
        public decimal? PayableAmount { get; set; }
    }

    public sealed class DocumentItem
    {
        public int Id { get; set; }
        public string DocumentType { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public long SizeBytes { get; set; }
        public string VerificationStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsZoneDocument { get; set; }
        public string RequestRemark { get; set; }
    }

    public sealed class HistoryItem
    {
        public int Sequence { get; set; }
        public DateTime AtUtc { get; set; }
        public string Action { get; set; }
        public string FromRole { get; set; }
        public string ToRole { get; set; }
        public string PreviousStatus { get; set; }
        public string NewStatus { get; set; }
        public string Remarks { get; set; }
        public string By { get; set; }
        /// <summary>UTR and payment date recorded on an applicant payment submission (null for other entries and for entries written before these fields existed).</summary>
        public string PaymentReference { get; set; }
        public DateTime? PaymentDate { get; set; }
    }

    public sealed class ApplicationDetail
    {
        public ApplicationDetail() { Documents = new List<DocumentItem>(); History = new List<HistoryItem>(); AllowedActions = new List<string>(); }
        public int Id { get; set; }
        public string ApplicationNumber { get; set; }
        public string ProcessingRoute { get; set; }
        public bool IsLegacy { get { return ProcessingRoute != "Zone"; } }
        public string ServiceType { get; set; }
        public string ServiceName { get; set; }
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
        public string State { get; set; }
        public string District { get; set; }
        public string City { get; set; }
        public string Taluka { get; set; }
        public string PinCode { get; set; }
        public string Zone { get; set; }
        public string Prabhag { get; set; }
        public string Location { get; set; }
        public string ServiceDescription { get; set; }
        public string AvailableSpace { get; set; }
        public string SpaceRequirement { get; set; }
        public string RequiredDuration { get; set; }
        public string RequiredTime { get; set; }
        public bool ElectricityRequired { get; set; }
        public bool WaterRequired { get; set; }
        public string OtherFacilities { get; set; }
        public string WasteManagement { get; set; }
        public string OtherInformation { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalDays { get; set; }
        public decimal? LengthFt { get; set; }
        public decimal? WidthFt { get; set; }
        public decimal? AreaSqFt { get; set; }
        public decimal? CalculatedRate { get; set; }
        public decimal? FeeAmount { get; set; }
        public string Status { get; set; }
        public string PaymentStatus { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public string Stage { get; set; }
        public decimal PayableAmount { get; set; }
        public string PaymentReference { get; set; }
        public DateTime? PaymentDate { get; set; }
        public DateTime? PaymentConfirmedAt { get; set; }
        public string PaymentVerifier { get; set; }
        public string CertificatePath { get; set; }
        public bool HasCertificate { get { return !string.IsNullOrEmpty(CertificatePath); } }
        /// <summary>Officer who gave the final approval (printed on the permission letter). Never part of the applicant view.</summary>
        public string ApprovedBy { get; set; }
        public string RowVersion { get; set; }
        public List<DocumentItem> Documents { get; set; }
        public List<HistoryItem> History { get; set; }
        public List<string> AllowedActions { get; set; }
    }

    /// <summary>Allow-listed officer detail response for the Applications page modal; intentionally excludes identity/PAN and storage paths.</summary>
    public sealed class OfficerApplicationView
    {
        public OfficerApplicationView() { Fields = new List<OfficerDetailField>(); Documents = new List<OfficerDocumentView>(); History = new List<OfficerHistoryView>(); AllowedActionLabels = new List<string>(); Payment = new OfficerPaymentView(); }
        public int Id { get; set; }
        public string ApplicationNumber { get; set; }
        public string ApplicantName { get; set; }
        public string Mobile { get; set; }
        public string Email { get; set; }
        public string StageLabel { get; set; }
        public string PaymentStatusLabel { get; set; }
        public decimal Amount { get; set; }
        public string AmountLabel { get; set; }
        public bool IsLegacy { get; set; }
        public bool ReadOnly { get; set; }
        public bool ShowPayment { get; set; }
        public bool CanUploadDocuments { get; set; }
        public bool CanVerifyDocuments { get; set; }
        public string SitePhotoUrl { get; set; }
        public string SitePhotoMessage { get; set; }
        public OfficerPaymentView Payment { get; set; }
        public List<OfficerDetailField> Fields { get; set; }
        public List<OfficerDocumentView> Documents { get; set; }
        public List<OfficerHistoryView> History { get; set; }
        public List<string> AllowedActionLabels { get; set; }
        /// <summary>Authorized download-handler link to the permission letter; set only for approved applications.</summary>
        public string CertificateUrl { get; set; }
        public string CertificateLabel { get; set; }
    }

    public sealed class OfficerDetailField { public string Label { get; set; } public string Value { get; set; } }
    public sealed class OfficerDocumentView
    {
        public int Id { get; set; }
        public string Type { get; set; }
        public string FileName { get; set; }
        public string VerificationStatus { get; set; }
        public string RequestRemark { get; set; }
        public string PreviewUrl { get; set; }
        public string DownloadUrl { get; set; }
    }
    public sealed class OfficerPaymentView
    {
        public string Reference { get; set; }
        public string Date { get; set; }
        public string ProofFileName { get; set; }
        public string ProofPreviewUrl { get; set; }
        public string ProofDownloadUrl { get; set; }
        public string VerificationState { get; set; }
        public string Verifier { get; set; }
        public string ProofPreviewLabel { get; set; }
    }
    public sealed class OfficerHistoryView
    {
        public int Sequence { get; set; }
        public DateTime AtUtc { get; set; }
        public string DateLabel { get; set; }
        public string Action { get; set; }
        public string Role { get; set; }
        public string OfficerName { get; set; }
        public string Remark { get; set; }
    }

    public sealed class DashboardCounts
    {
        public int Total { get; set; }
        public int Submitted { get; set; }
        public int UnderScrutiny { get; set; }
        public int FeePending { get; set; }
        public int ApprovalPending { get; set; }
        public int Approved { get; set; }
        public int Rejected { get; set; }
        public int JePending { get; set; }
        public int OsPending { get; set; }
        public int AcPending { get; set; }
        public int PaymentPending { get; set; }
        public int Legacy { get; set; }
    }

    public sealed class OfficerDashboardCounts { public int Total { get; set; } public int Pending { get; set; } public int Processed { get; set; } public int Approved { get; set; } public int Rejected { get; set; } }

    public sealed class ReportRow
    {
        public string Label { get; set; }
        public int Count { get; set; }
        public decimal Amount { get; set; }
    }

    public sealed class AuditLogItem
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string UserName { get; set; }
        public string Action { get; set; }
        public string EntityName { get; set; }
        public int EntityId { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string IpAddress { get; set; }
    }

    public sealed class UserListItem
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public string Designation { get; set; }
        public string Role { get; set; }
        public int? ZoneNumber { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }

    public sealed class ServiceItem { public int Value { get; set; } public string Code { get; set; } public string Name { get; set; } public string NameMr { get; set; } public int SortOrder { get; set; } }
    public sealed class RateItem { public string ServiceCode { get; set; } public decimal? Rate { get; set; } public string Unit { get; set; } }
    public sealed class BusinessTypeItem { public int Value { get; set; } public string Code { get; set; } public string Name { get; set; } public string NameMr { get; set; } }
    public sealed class NumberConfigItem { public string Prefix { get; set; } public string YearStrategy { get; set; } public int SequenceLength { get; set; } public int NextNumber { get; set; } }
    public sealed class SmsTemplateItem { public string EventType { get; set; } public string TemplateName { get; set; } public string Language { get; set; } public string ApprovalStatus { get; set; } public bool IsActive { get; set; } }

    public sealed class AuthenticatedUser
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
        public int? ZoneNumber { get; set; }
    }

    // ---- request models (API) ----
    public sealed class LoginRequest { public string Username { get; set; } public string Password { get; set; } }
    public sealed class WorkflowActionRequest
    {
        public int ApplicationId { get; set; }
        public string Action { get; set; }
        public string[] Actions { get; set; }
        public string ExpectedStage { get; set; }
        public string ExpectedRowVersion { get; set; }
        public string Remarks { get; set; }
        /// <summary>Ignored by the server (amounts are always recomputed); present only so older clients do not break.</summary>
        public decimal? Amount { get; set; }
        public decimal? OfficerRate { get; set; }
        public string PaymentReference { get; set; }
        public string PaymentMode { get; set; }
    }
    public sealed class PricingPreviewRequest
    {
        public string ServiceType { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal? LengthFt { get; set; }
        public decimal? WidthFt { get; set; }
    }
    public sealed class PricingPreviewResponse
    {
        public int TotalDays { get; set; }
        public decimal AreaSqFt { get; set; }
        public decimal? Rate { get; set; }
        public decimal? FinalAmount { get; set; }
        public bool PendingOfficerAmount { get; set; }
        public string Unit { get; set; }
        public string EffectiveEndDate { get; set; }
        public string Error { get; set; }
    }
}

namespace SMC.BhoomiMalmatta.Models.Dto
{
    public sealed class ApiError { public string Code { get; set; } public string Message { get; set; } }

    public sealed class DocumentPayload
    {
        public string DocumentType { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public string ContentBase64 { get; set; }
    }

    /// <summary>Public submission. State and District are NOT accepted: the server fixes them to Maharashtra / Solapur. No amount field exists.</summary>
    public sealed class SubmissionRequest
    {
        public SubmissionRequest() { Documents = new List<DocumentPayload>(); }
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
        public List<DocumentPayload> Documents { get; set; }
    }

    public sealed class FieldErrorDto { public string Field { get; set; } public string Message { get; set; } }

    public sealed class SubmissionResponse
    {
        public int ApplicationId { get; set; }
        public string ApplicationNumber { get; set; }
        public decimal? FinalAmount { get; set; }
        public int TotalDays { get; set; }
        public decimal AreaSqFt { get; set; }
        public bool PendingOfficerAmount { get; set; }
        public string PaymentStatus { get; set; }
    }

    public sealed class StatusLookupRequest { public string ApplicationNumber { get; set; } }
    public sealed class ResubmitRequest { public string Reply { get; set; } public List<DocumentPayload> Documents { get; set; } }
    public sealed class PaymentConfirmRequest { public string PaymentReference { get; set; } public string PaymentMode { get; set; } public string ExpectedStage { get; set; } public string ExpectedRowVersion { get; set; } }
    public sealed class ApplicantPaymentSubmitRequest { public string PaymentReference { get; set; } public DateTime? PaymentDate { get; set; } public string FileName { get; set; } public string ContentType { get; set; } public string ContentBase64 { get; set; } public string ExpectedStage { get; set; } public string ExpectedRowVersion { get; set; } }
    public sealed class PaymentVerificationRequest { public string PaymentMode { get; set; } public string ExpectedStage { get; set; } public string ExpectedRowVersion { get; set; } }
    public sealed class PaymentRejectionRequest { public string Remarks { get; set; } public string ExpectedStage { get; set; } public string ExpectedRowVersion { get; set; } }
    public sealed class WorkflowResponse { public string Stage { get; set; } public string Status { get; set; } public string PaymentStatus { get; set; } public bool AlreadyApplied { get; set; } public OfficerDashboardCounts Counts { get; set; } }
    public sealed class PaymentInfo { public string PaymentStatus { get; set; } public decimal PayableAmount { get; set; } public string Reference { get; set; } public string Mode { get; set; } }
    public sealed class ReportDto { public string Title { get; set; } public string[] Headers { get; set; } public List<string[]> Rows { get; set; } }
}

namespace SMC.BhoomiMalmatta.Models.Dto
{
    public static class OfficerDateFormatter
    {
        public static string Format(DateTime? value, bool includeTime, bool devanagariDigits)
        {
            if (!value.HasValue) return "Ã¢â‚¬â€";
            var text = includeTime ? value.Value.ToString("d/M/yyyy, h:mm:ss tt", System.Globalization.CultureInfo.InvariantCulture) : value.Value.ToString("d/M/yyyy", System.Globalization.CultureInfo.InvariantCulture);
            if (!devanagariDigits) return text;
            const string western = "0123456789"; const string native = "\u0966\u0967\u0968\u0969\u096A\u096B\u096C\u096D\u096E\u096F";
            for (var i = 0; i < western.Length; i++) text = text.Replace(western[i], native[i]);
            return text;
        }
    }
}

namespace SMC.BhoomiMalmatta.Models.Dto
{
    public static class OfficerDecisionSelection
    {
        public static bool TryGetSingle(System.Collections.Generic.IEnumerable<string> actions, out string action)
        {
            action = null; if (actions == null) return false;
            var values = actions.ToList();
            if (values.Count != 1 || string.IsNullOrWhiteSpace(values[0])) return false;
            action = values[0]; return true;
        }
    }
}
