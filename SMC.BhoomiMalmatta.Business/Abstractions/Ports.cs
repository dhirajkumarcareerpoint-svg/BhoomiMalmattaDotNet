using System;
using System.Collections.Generic;
using SMC.BhoomiMalmatta.Business.Pricing;
using SMC.BhoomiMalmatta.Models.Common;
using SMC.BhoomiMalmatta.Models.Dto;

namespace SMC.BhoomiMalmatta.Business.Abstractions
{
    /// <summary>Strictly limited fields returned by anonymous application-number status lookup.</summary>
    public sealed class PublicApplicationStatus
    {
        public string ApplicationNumber { get; set; }
        public string Service { get; set; }
        public string CurrentStage { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public string StatusMessage { get; set; }
    }

    public sealed class RateInfo { public string ServiceCode { get; set; } public decimal? Rate { get; set; } public string Unit { get; set; } }
    public sealed class ServiceCatalog
    {
        public ServiceCatalog(ISet<string> services, ISet<string> businessTypes) { ServiceCodes = services; BusinessTypeCodes = businessTypes; }
        public ISet<string> ServiceCodes { get; private set; }
        public ISet<string> BusinessTypeCodes { get; private set; }
    }
    public sealed class SmsTemplateRecord { public string EventType { get; set; } public string MessageBody { get; set; } public string DltTemplateId { get; set; } public string SenderId { get; set; } }
    public sealed class PermitConfigRecord { public string OrganizationName { get; set; } public string DepartmentName { get; set; } public string CertificateTitle { get; set; } public string AuthorityDesignation { get; set; } public string FooterText { get; set; } }

    /// <summary>Read-only masters (MR_CFC_*).</summary>
    public interface IMasterDataReader
    {
        RateInfo GetActiveRate(string serviceCode);
        ServiceCatalog GetCatalog();
        List<ServiceItem> GetServices();
        List<RateItem> GetRates();
        List<BusinessTypeItem> GetBusinessTypes();
        NumberConfigItem GetNumberConfig();
        List<SmsTemplateItem> GetSmsTemplates();
        SmsTemplateRecord GetSmsTemplate(string eventType, string language);
        PermitConfigRecord GetPermitConfig();
        List<string> GetPermitTerms();
    }

    public sealed class NumberReservation { public string Prefix { get; set; } public string YearStrategy { get; set; } public int SequenceLength { get; set; } public int Number { get; set; } }
    public interface IApplicationNumberStore
    {
        /// <summary>Atomically takes the next sequence value (serializable transaction + UPDLOCK in the implementation).</summary>
        NumberReservation ReserveNext();
        bool Exists(string applicationNumber);
    }

    public sealed class AuditEntry
    {
        public string Action { get; set; }
        public string EntityName { get; set; }
        public int EntityId { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        /// <summary>TR_AST_AuditLogs.UserId is NOT NULL (FK to users): entries without a user are not written to the table.</summary>
        public int? UserId { get; set; }
        public string UserName { get; set; }
        public string IpAddress { get; set; }
        public DateTime TimestampUtc { get; set; }
    }

    public sealed class ActorContext
    {
        public int? ApplicationId { get; set; }
        public int? UserId { get; set; }
        public string UserName { get; set; }
        public string Role { get; set; }
        public int? ZoneNumber { get; set; }
        public string IpAddress { get; set; }
        public string CorrelationId { get; set; }
    }

    public sealed class StoredDocument
    {
        public string DocumentType { get; set; }
        public string OriginalFileName { get; set; }
        public string StoredFileName { get; set; }
        public string RelativePath { get; set; }
        public string ContentType { get; set; }
        public long SizeBytes { get; set; }
    }

    public sealed class DocumentUpload
    {
        public string DocumentType { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public byte[] Content { get; set; }
    }

    public sealed class NewApplicationRecord
    {
        public SubmissionData Data { get; set; }
        public string ApplicationNumber { get; set; }
        public string AccessTokenHash { get; set; }
        public PricingResult Pricing { get; set; }
        public decimal? FeeAmount { get; set; }
        public string PaymentStatus { get; set; }
        public string InitialHistoryJson { get; set; }
        public DateTime SubmittedAtUtc { get; set; }
        public List<StoredDocument> Documents { get; set; }
    }

    public interface IDemandApplicationStore
    {
        /// <summary>Inserts application + zone workflow + documents atomically (ProcessingRoute = Zone). Unique ApplicationNumber is the backstop.</summary>
        Result<int> Create(NewApplicationRecord record);
        Result AddDocuments(int applicationId, List<StoredDocument> documents, string actor);
    }

    public sealed class ZoneWorkflowSnapshot
    {
        public int ApplicationId { get; set; }
        public string ApplicationNumber { get; set; }
        public string ProcessingRoute { get; set; }
        public int Zone { get; set; }
        public string Stage { get; set; }
        public string PaymentStatus { get; set; }
        public decimal PayableAmount { get; set; }
        public string RecordedPaymentReference { get; set; }
        public DateTime? PaymentReferenceDate { get; set; }
        public bool DocumentsVerified { get; set; }
        public string HistoryJson { get; set; }
        public byte[] RowVersion { get; set; }
        public string ApplicantName { get; set; }
        public string ApplicantMobile { get; set; }
        public string ServiceType { get; set; }
        public decimal? FeeAmount { get; set; }
        public ZoneWorkflowSnapshot Clone() { return (ZoneWorkflowSnapshot)MemberwiseClone(); }
    }

    public enum RemarkKind { None, Correction, Approval, Rejection }

    public sealed class ZoneWorkflowUpdate
    {
        public ZoneWorkflowSnapshot Before { get; set; }
        public string NewStage { get; set; }
        public string NewStatus { get; set; }
        public string NewPaymentStatus { get; set; }
        public decimal NewPayableAmount { get; set; }
        public bool? DocumentsVerified { get; set; }
        public string NewHistoryJson { get; set; }
        public RemarkKind RemarkKind { get; set; }
        public string Remark { get; set; }
        public string PaymentReference { get; set; }
        public DateTime? PaymentDate { get; set; }
        public StoredDocument PaymentProof { get; set; }
        public bool ConfirmPayment { get; set; }
        public string PaymentMode { get; set; }
        public decimal? RateEnteredByOfficer { get; set; }
        public ActorContext Actor { get; set; }
        /// <summary>One audit row per transition step (a verified payment writes "Payment Done" and "Forwarded to AC"), saved in the same transaction.</summary>
        public List<AuditEntry> Audits { get; set; }
        public DateTime AtUtc { get; set; }
        /// <summary>
        /// Applicant resubmits payment proof after an OS rejection: the store soft-deletes the active proof row of the same Kind first
        /// (same transaction), because the filtered unique index (workflow, Kind, IsDeleted = 0) allows only one active proof.
        /// </summary>
        public bool ReplaceActivePaymentProof { get; set; }
    }

    public interface IZoneWorkflowStore
    {
        /// <summary>Null when the application does not exist. For Legacy applications ProcessingRoute is "Legacy".</summary>
        ZoneWorkflowSnapshot Load(int applicationId);
        bool IsPaymentReferenceUsed(string reference, int exceptApplicationId);
        /// <summary>One transaction; fails with Error("CONCURRENCY_CONFLICT") when the RowVersion changed.</summary>
        Result Save(ZoneWorkflowUpdate update);
        Result SetCertificate(int applicationId, string relativePath, string fileName, string approvedBy);
    }

    public sealed class DocumentRecord
    {
        public int Id { get; set; }
        public int ApplicationId { get; set; }
        public string ApplicationNumber { get; set; }
        public string DocumentType { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public string RelativePath { get; set; }
        public long SizeBytes { get; set; }
    }

    public sealed class UserRecord
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }
        public int? ZoneNumber { get; set; }
        public string Mobile { get; set; }
    }

    public interface IUserStore
    {
        UserRecord FindByUsername(string username);
        void RecordLogin(int userId, DateTime utc);
        List<string> GetActiveMobilesByRole(string role);
    }

    public interface IQueryService
    {
        PublicApplicationStatus GetPublicStatus(string applicationNumber);
        PagedResult<ApplicationListItem> Search(ApplicationFilter filter);
        ApplicationDetail GetDetail(int applicationId);
        int? FindIdByNumber(string applicationNumber);
        string GetRegisteredMobile(int applicationId);
        string GetAccessTokenHash(int applicationId);
        DashboardCounts GetDashboard(int? zoneScope);
        OfficerDashboardCounts GetOfficerDashboardCounts(string role, int? zoneScope, int userId);
        PagedResult<ApplicationListItem> SearchPendingForRole(string role, ApplicationFilter filter);
        PagedResult<ApplicationListItem> SearchOfficerApplications(string role, int userId, ApplicationFilter filter);
        List<ReportRow> ReportByStatus(int? zoneScope, DateTime? from, DateTime? to);
        List<ReportRow> ReportByService(int? zoneScope, DateTime? from, DateTime? to);
        List<ReportRow> ReportByZone(int? zoneScope, DateTime? from, DateTime? to);
        List<ReportRow> ReportByMonth(int? zoneScope, DateTime from, DateTime to);
        PagedResult<AuditLogItem> SearchAudit(DateTime? from, DateTime? to, string action, string userName, int page, int pageSize);
        List<UserListItem> GetUsers();
        DocumentRecord GetDocument(int documentId);
        DocumentRecord GetZoneDocument(int documentId);
    }

    public interface IAuditWriter { void Write(AuditEntry entry); }

    public sealed class SmsRecord
    {
        public string Mobile { get; set; }
        public string EventType { get; set; }
        public string ApplicationNumber { get; set; }
        public string TemplateReference { get; set; }
        public string Status { get; set; }
        public string ProviderMessageId { get; set; }
        public string FailureReason { get; set; }
        public DateTime? SentAtUtc { get; set; }
    }
    public interface ISmsEventStore { void Upsert(SmsRecord record); }

    public sealed class SmsSendResult { public bool Success { get; set; } public string ProviderMessageId { get; set; } public string Error { get; set; } }
    public interface ISmsGateway { SmsSendResult Send(string mobile, string text, string dltTemplateId, string senderId); }

    public interface IPasswordVerifier { bool Verify(string password, string storedHash); }
    public interface IFileStorage
    {
        void Save(string relativePath, byte[] content);
        byte[] Read(string relativePath);
        bool Exists(string relativePath);
        void Delete(string relativePath);
    }

    public sealed class NotificationRequest
    {
        public string EventType { get; set; }
        public string ApplicationNumber { get; set; }
        public string RecipientMobile { get; set; }
        /// <summary>When set, the notifier resolves all active users of this role instead of RecipientMobile.</summary>
        public string RecipientRole { get; set; }
        public Dictionary<string, string> Variables { get; set; }
    }
    public interface INotifier { void Notify(NotificationRequest request); }

    public enum OtpSmsSendStatus { Sent, Unavailable, Failed }
    public interface IOtpSmsSender
    {
        bool Enabled { get; }
        OtpSmsSendStatus SendAccessRecoveryOtp(string applicationNumber, string registeredMobile, string otp, int expiryMinutes);
    }
    public sealed class NullNotifier : INotifier { public void Notify(NotificationRequest request) { } }

    /// <summary>Error log for post-commit side effects (certificate generation, SMS). Must never throw.</summary>
    public interface IErrorLog { void Log(string correlationId, string area, Exception ex); }
    public sealed class NullErrorLog : IErrorLog { public void Log(string correlationId, string area, Exception ex) { } }

    public interface ICertificateIssuer { Result Issue(int applicationId, ActorContext actor); }

    public interface IPdfGenerator
    {
        byte[] Acknowledgement(ApplicationDetail d, PermitConfigRecord cfg);
        byte[] SubmittedApplication(ApplicationDetail d, PermitConfigRecord cfg);
        byte[] Receipt(ApplicationDetail d, PermitConfigRecord cfg);
        byte[] Certificate(ApplicationDetail d, PermitConfigRecord cfg, List<string> terms, string approvedBy);
    }
}
