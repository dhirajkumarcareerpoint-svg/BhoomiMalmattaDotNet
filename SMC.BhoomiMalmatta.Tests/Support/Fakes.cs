using System;
using System.Collections.Generic;
using System.Linq;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Business.Workflow;
using SMC.BhoomiMalmatta.Models.Common;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;

namespace SMC.BhoomiMalmatta.Tests.Support
{
    public sealed class FakeClock : IClock { public DateTime UtcNow { get; set; } = new DateTime(2026, 10, 2, 6, 0, 0, DateTimeKind.Utc); }

    public sealed class FakeMasters : IMasterDataReader
    {
        public Dictionary<string, RateInfo> Rates = new Dictionary<string, RateInfo>
        {
            { "MajorGaala", new RateInfo { ServiceCode = "MajorGaala", Rate = 50000m, Unit = "fixed" } },
            { "MiniGaala", new RateInfo { ServiceCode = "MiniGaala", Rate = 20000m, Unit = "fixed" } },
            { "VariousBusinessSpace", new RateInfo { ServiceCode = "VariousBusinessSpace", Rate = null, Unit = "sq.ft." } },
        };
        public SmsTemplateRecord Template;
        public RateInfo GetActiveRate(string code) { RateInfo r; return Rates.TryGetValue(code, out r) ? r : null; }
        public ServiceCatalog GetCatalog() { return new ServiceCatalog(new HashSet<string>(Rates.Keys.Concat(new[] { "LandFee", "SamajMandir", "Abhyasika", "Gaale256", "TP3_23", "AdhikrutKhoke", "OtherMunicipalProperty" })), new HashSet<string> { "FirecrackerStall", "GanpatiIdolStall", "Other" }); }
        public List<ServiceItem> GetServices() { return new List<ServiceItem>(); }
        public List<RateItem> GetRates() { return new List<RateItem>(); }
        public List<BusinessTypeItem> GetBusinessTypes() { return new List<BusinessTypeItem>(); }
        public NumberConfigItem GetNumberConfig() { return null; }
        public List<SmsTemplateItem> GetSmsTemplates() { return new List<SmsTemplateItem>(); }
        public SmsTemplateRecord GetSmsTemplate(string eventType, string language) { return Template; }
        public PermitConfigRecord GetPermitConfig() { return new PermitConfigRecord { OrganizationName = "SMC" }; }
        public List<string> GetPermitTerms() { return new List<string>(); }
    }

    public sealed class FakeNumberStore : IApplicationNumberStore
    {
        public int Next = 5; public HashSet<string> Existing = new HashSet<string>();
        public NumberReservation ReserveNext() { return new NumberReservation { Prefix = "2026", YearStrategy = "Fixed", SequenceLength = 4, Number = Next++ }; }
        public bool Exists(string n) { return Existing.Contains(n); }
    }

    public sealed class FakeFiles : IFileStorage
    {
        public Dictionary<string, byte[]> Files = new Dictionary<string, byte[]>();
        public void Save(string p, byte[] c) { Files[p] = c; }
        public byte[] Read(string p) { byte[] b; if (!Files.TryGetValue(p, out b)) throw new System.IO.FileNotFoundException(); return b; }
        public bool Exists(string p) { return Files.ContainsKey(p); }
        public void Delete(string p) { Files.Remove(p); }
    }

    public sealed class FakeAppStore : IDemandApplicationStore
    {
        public List<NewApplicationRecord> Saved = new List<NewApplicationRecord>(); public bool FailNext;
        public Result<int> Create(NewApplicationRecord r)
        {
            if (FailNext) return Result.Failure<int>(new Error("DB", "db down"));
            if (Saved.Any(s => s.ApplicationNumber == r.ApplicationNumber)) return Result.Failure<int>(new Error("DUPLICATE", "dup"));
            Saved.Add(r); return Result.Success(Saved.Count);
        }
        public Result AddDocuments(int id, List<StoredDocument> d, string actor) { return Result.Success(); }
    }

    public sealed class FakeWorkflowStore : IZoneWorkflowStore
    {
        public ZoneWorkflowSnapshot Current; public List<AuditEntry> Audits = new List<AuditEntry>(); public int SaveCalls; public bool SimulateConcurrentWriter; public HashSet<string> OtherPaymentReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase); public List<StoredDocument> PaymentProofs = new List<StoredDocument>();
        public RemarkKind LastKind; public string CertificatePath; public ZoneWorkflowUpdate LastUpdate;
        public ZoneWorkflowSnapshot Load(int id) { return Current != null && Current.ApplicationId == id ? Current.Clone() : null; }
        public bool IsPaymentReferenceUsed(string reference, int exceptApplicationId) { return OtherPaymentReferences.Contains(reference); }
        public Result Save(ZoneWorkflowUpdate u)
        {
            SaveCalls++;
            if (SimulateConcurrentWriter || !u.Before.RowVersion.SequenceEqual(Current.RowVersion)) return Result.Failure(new Error("CONCURRENCY_CONFLICT", "बदल झाला"));
            Current.Stage = u.NewStage; Current.PaymentStatus = u.NewPaymentStatus; Current.PayableAmount = u.NewPayableAmount; Current.HistoryJson = u.NewHistoryJson;
            if (u.PaymentReference != null) Current.RecordedPaymentReference = u.PaymentReference;
            if (u.PaymentReference != null) Current.PaymentReferenceDate = u.PaymentDate;
            if (u.PaymentProof != null) PaymentProofs.Add(u.PaymentProof);
            Current.RowVersion = BitConverter.GetBytes(BitConverter.ToInt32(Current.RowVersion, 0) + 1);
            if (u.DocumentsVerified.HasValue) Current.DocumentsVerified = u.DocumentsVerified.Value;
            if (u.Audits != null) Audits.AddRange(u.Audits);
            LastUpdate = u;
            LastKind = u.RemarkKind; return Result.Success();
        }
        public Result SetCertificate(int id, string rel, string name, string by) { CertificatePath = rel; return Result.Success(); }

        public static ZoneWorkflowSnapshot Snapshot(string stage, string pay = PaymentStatuses.PendingOSAmount, int zone = 3, string route = ProcessingRoutes.Zone)
        {
            var first = new HistoryEntry { ApplicationId = 1, ApplicationNumber = "2026-0005", FromRole = Roles.Citizen, ToRole = Roles.JE, Action = AuditActions.ApplicationSubmitted, PreviousStatus = "", NewStatus = "Submitted", PreviousStage = "", NewStage = ZoneStages.JEPending, ActionByUserName = "A", ActionAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc).ToString("O") };
            var history = ActionHistory.Append("[]", first);
            if (stage == ZoneStages.PaymentRequired)
            {
                history = ActionHistory.Append(history, new HistoryEntry { ApplicationId = 1, ApplicationNumber = "2026-0005", FromRole = Roles.OS, ToRole = Roles.Citizen, Action = AuditActions.PaymentRequestSent, PreviousStatus = ApplicationStatuses.UnderScrutiny, NewStatus = ApplicationStatuses.FeePending, PreviousStage = ZoneStages.OSPending, NewStage = ZoneStages.PaymentRequired, ActionByUserName = "os.user", ActionAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc).ToString("O") });
            }
            return new ZoneWorkflowSnapshot { ApplicationId = 1, ApplicationNumber = "2026-0005", ProcessingRoute = route, Zone = zone, Stage = stage, PaymentStatus = pay, RecordedPaymentReference = pay == PaymentStatuses.PaymentSubmitted || pay == PaymentStatuses.PaymentDone || pay == PaymentStatuses.PaymentRejected ? "UTR123456" : null, PaymentReferenceDate = pay == PaymentStatuses.PaymentSubmitted || pay == PaymentStatuses.PaymentDone || pay == PaymentStatuses.PaymentRejected ? (DateTime?)new DateTime(2026, 10, 2) : null, HistoryJson = history, RowVersion = BitConverter.GetBytes(1), ApplicantName = "A", ApplicantMobile = "9876543210", ServiceType = "MajorGaala" };
        }
    }

    public sealed class FakeNotifier : INotifier { public List<NotificationRequest> Sent = new List<NotificationRequest>(); public void Notify(NotificationRequest r) { Sent.Add(r); } }
    public sealed class FakeAudit : IAuditWriter { public List<AuditEntry> Entries = new List<AuditEntry>(); public bool Throw; public void Write(AuditEntry e) { if (Throw) throw new Exception("audit down"); Entries.Add(e); } }
    public sealed class FakeVerifier : IPasswordVerifier { public bool Verify(string password, string hash) { return hash == "hash:" + password; } }

    public sealed class FakeUsers : IUserStore
    {
        public List<UserRecord> Users = new List<UserRecord>(); public List<int> LoggedIn = new List<int>();
        public UserRecord FindByUsername(string u) { return Users.FirstOrDefault(x => string.Equals(x.Username, u, StringComparison.OrdinalIgnoreCase)); }
        public void RecordLogin(int id, DateTime utc) { LoggedIn.Add(id); }
        public List<string> GetActiveMobilesByRole(string role) { return Users.Where(x => x.IsActive && x.Role == role).Select(x => x.Mobile).ToList(); }
    }

    public sealed class FakeSmsEvents : ISmsEventStore { public List<SmsRecord> Rows = new List<SmsRecord>(); public void Upsert(SmsRecord r) { Rows.RemoveAll(x => x.ApplicationNumber == r.ApplicationNumber && x.EventType == r.EventType); Rows.Add(r); } }
    public sealed class FakeGateway : ISmsGateway
    {
        public List<string> Texts = new List<string>(); public bool Ok = true; public bool Throw;
        public SmsSendResult Send(string mobile, string text, string dlt, string sender)
        { if (Throw) throw new Exception("network"); Texts.Add(text); return new SmsSendResult { Success = Ok, ProviderMessageId = Ok ? "MSG1" : null, Error = Ok ? null : "rejected" }; }
    }

    public static class Actors
    {
        public static ActorContext JE(int? zone = null) { return new ActorContext { UserId = 10, UserName = "je.user", Role = Roles.JE, ZoneNumber = zone, IpAddress = "10.0.0.1", CorrelationId = "corr-1" }; }
        public static ActorContext OS(int? zone = null) { return new ActorContext { UserId = 11, UserName = "os.user", Role = Roles.OS, ZoneNumber = zone, IpAddress = "10.0.0.2", CorrelationId = "corr-2" }; }
        public static ActorContext AC() { return new ActorContext { UserId = 12, UserName = "ac.user", Role = Roles.AssistantCommissioner, IpAddress = "10.0.0.3", CorrelationId = "corr-3" }; }
        public static ActorContext Staff() { return new ActorContext { UserId = 13, UserName = "staff.user", Role = Roles.Staff }; }
        public static ActorContext Citizen() { return new ActorContext { ApplicationId = 1, UserName = "citizen", Role = Roles.Citizen, IpAddress = "1.2.3.4" }; }
    }

    public static class TestData
    {
        public static readonly byte[] Pdf = { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 };
        public static SubmissionData Valid(string service = "MajorGaala", decimal? claimed = null)
        {
            return new SubmissionData
            {
                ServiceType = service, BusinessType = service == "VariousBusinessSpace" ? "FirecrackerStall" : null, ApplicantName = "  राम पाटील  ", Mobile = " 9876543210 ", Email = " Ram@Example.com ",
                PermanentAddress = "१२, सोलापूर", CorrespondenceAddress = "", SameAddress = true, State = "Maharashtra", District = "Solapur", City = "Solapur", Taluka = "उत्तर सोलापूर",
                PinCode = "413001", Zone = "3", Location = "मार्केट यार्ड", ServiceDescription = "स्टॉल", StartDate = new DateTime(2026, 10, 10), EndDate = new DateTime(2026, 10, 14),
                LengthFt = 10m, WidthFt = 5m, DeclarationAccepted = true, ClientClaimedAmount = claimed,
                UploadedDocumentTypes = new List<string> { DocumentTypes.IdentityProof, DocumentTypes.AddressProof }
            };
        }
        public static List<DocumentUpload> Docs()
        {
            return new List<DocumentUpload>
            {
                new DocumentUpload { DocumentType = DocumentTypes.IdentityProof, FileName = "aadhaar.pdf", ContentType = "application/pdf", Content = Pdf },
                new DocumentUpload { DocumentType = DocumentTypes.AddressProof, FileName = "address.pdf", ContentType = "application/pdf", Content = Pdf },
            };
        }
    }
}
