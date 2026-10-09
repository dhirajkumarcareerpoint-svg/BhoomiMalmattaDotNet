using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Business.Citizen;
using SMC.BhoomiMalmatta.Business.Documents;
using SMC.BhoomiMalmatta.Business.Reports;
using SMC.BhoomiMalmatta.Business.Security;
using SMC.BhoomiMalmatta.Business.Sms;
using SMC.BhoomiMalmatta.Business.Storage;
using SMC.BhoomiMalmatta.Business.Workflow;
using SMC.BhoomiMalmatta.Models.Common;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;
using SMC.BhoomiMalmatta.Tests.Support;

namespace SMC.BhoomiMalmatta.Tests
{
    [TestClass]
    public class FileAndAccessTests
    {
        private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 };
        private static readonly byte[] Exe = { 0x4D, 0x5A, 0x90, 0 };

        [TestMethod] public void Valid_Pdf_Gets_Generated_Storage_Name()
        {
            var r = FileValidator.Validate("आधार कार्ड.pdf", "application/pdf", TestData.Pdf);
            Assert.IsTrue(r.IsSuccess); Assert.IsTrue(r.Value.StoredName.EndsWith(".pdf")); Assert.AreNotEqual("आधार कार्ड.pdf", r.Value.StoredName); Assert.AreEqual(36, r.Value.StoredName.Length);
        }
        [TestMethod] public void Executable_Disguised_As_Pdf_Is_Rejected_By_Signature() { Assert.AreEqual("FILE_SIGNATURE", FileValidator.Validate("a.pdf", "application/pdf", Exe).Error.Code); }
        [DataTestMethod] [DataRow("virus.exe")] [DataRow("page.html")] [DataRow("a.pdf.exe")] [DataRow("noext")] [DataRow("evil.exe.pdf")]
        public void Dangerous_Or_Unknown_Types_Are_Rejected(string name) { Assert.IsTrue(FileValidator.Validate(name, "application/pdf", TestData.Pdf).IsFailure); }
        [TestMethod] public void Mime_Mismatch_Is_Rejected() { Assert.AreEqual("FILE_MIME", FileValidator.Validate("a.png", "application/pdf", Png).Error.Code); }
        [TestMethod] public void Oversize_And_Empty_Are_Rejected()
        {
            var big = new byte[FileValidator.MaxBytes + 1]; Array.Copy(TestData.Pdf, big, TestData.Pdf.Length);
            Assert.AreEqual("FILE_TOO_LARGE", FileValidator.Validate("a.pdf", "application/pdf", big).Error.Code);
            Assert.AreEqual("FILE_EMPTY", FileValidator.Validate("a.pdf", "application/pdf", new byte[0]).Error.Code);
        }
        [TestMethod] public void Application_Attachments_Accept_Up_To_20_MiB_And_Reject_Larger_Files()
        {
            var limit = FileValidator.MaxApplicationAttachmentBytes;
            var accepted = new byte[limit]; Array.Copy(TestData.Pdf, accepted, TestData.Pdf.Length);
            Assert.IsTrue(FileValidator.Validate("a.pdf", "application/pdf", accepted, limit).IsSuccess);
            var rejected = new byte[limit + 1]; Array.Copy(TestData.Pdf, rejected, TestData.Pdf.Length);
            Assert.AreEqual("FILE_TOO_LARGE", FileValidator.Validate("a.pdf", "application/pdf", rejected, limit).Error.Code);
        }
        [DataTestMethod] [DataRow("../../etc/passwd.pdf")] [DataRow("..\\..\\windows\\system32\\x.pdf")] [DataRow("C:\\temp\\a.pdf")]
        public void Path_Traversal_Is_Neutralised(string name)
        {
            var s = FileValidator.SanitizeName(name);
            Assert.IsFalse(s.Contains("..")); Assert.IsFalse(s.Contains("/")); Assert.IsFalse(s.Contains("\\")); Assert.IsFalse(s.Contains(":"));
            Assert.IsFalse(FileValidator.Validate(name, "application/pdf", TestData.Pdf).Value.StoredName.Contains("/"));
        }
        [TestMethod] public void Access_Token_Hash_Matches_Column_Width_And_Verifies()
        {
            var token = AccessToken.NewToken(); var hash = AccessToken.Hash(token);
            Assert.AreEqual(64, hash.Length); Assert.IsTrue(AccessToken.Verify(token, hash)); Assert.IsTrue(AccessToken.Verify(token, hash.ToUpperInvariant()));
            Assert.IsFalse(AccessToken.Verify(token + "x", hash)); Assert.IsFalse(AccessToken.Verify(null, hash)); Assert.IsFalse(AccessToken.Verify(token, null));
            Assert.AreNotEqual(token, AccessToken.NewToken());
        }
        [TestMethod] public void Document_Access_Rules()
        {
            Assert.IsTrue(DocumentAccessPolicy.CanDownload(Roles.JE, DocumentVisibility.OfficerOnly, false));
            Assert.IsTrue(DocumentAccessPolicy.CanDownload(Roles.AssistantCommissioner, DocumentVisibility.OfficerOnly, false));
            Assert.IsFalse(DocumentAccessPolicy.CanDownload(Roles.Staff, DocumentVisibility.OfficerOnly, false));
            Assert.IsTrue(DocumentAccessPolicy.CanDownload(Roles.Staff, DocumentVisibility.CitizenVisible, false));
            Assert.IsTrue(DocumentAccessPolicy.CanDownload(Roles.Citizen, DocumentVisibility.CitizenVisible, true));
            Assert.IsFalse(DocumentAccessPolicy.CanDownload(Roles.Citizen, DocumentVisibility.CitizenVisible, false));
            Assert.IsFalse(DocumentAccessPolicy.CanDownload(Roles.Citizen, DocumentVisibility.OfficerOnly, true));
            Assert.IsFalse(DocumentAccessPolicy.CanDownload("Unknown", DocumentVisibility.CitizenVisible, true));
        }
        [TestMethod] public void Citizen_Status_View_Never_Leaks_Internal_Data()
        {
            var j = ActionHistory.Append("[]", new HistoryEntry { ApplicationId = 1, ApplicationNumber = "2026-0005", FromRole = Roles.JE, ToRole = Roles.OS, Action = AuditActions.JEVerified, PreviousStatus = "Submitted", NewStatus = "UnderScrutiny", PreviousStage = "JEPending", NewStage = "OSPending",
                Remarks = "अंतर्गत गोपनीय शेरा", ActionByUserId = 77, ActionByUserName = "je.secret.name", ActionAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc).ToString("O"), IpAddress = "10.9.8.7", CorrelationId = "corr-xyz" });
            j = ActionHistory.Append(j, new HistoryEntry { ApplicationId = 1, ApplicationNumber = "2026-0005", FromRole = Roles.JE, ToRole = Roles.Citizen, Action = AuditActions.JEReturnedForCorrection, PreviousStatus = "UnderScrutiny", NewStatus = "UnderScrutiny", PreviousStage = "OSPending", NewStage = "JECorrectionRequested",
                Remarks = "पत्ता दुरुस्त करा", ActionByUserId = 77, ActionByUserName = "je.secret.name", ActionAtUtc = new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc).ToString("O"), IpAddress = "10.9.8.7", CorrelationId = "corr-xyz" });
            var v = CitizenStatusMapper.Map("2026-0005", ZoneStages.JECorrectionRequested, PaymentStatuses.PendingOSAmount, null, j);
            var dump = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(v);
            foreach (var secret in new[] { "je.secret.name", "10.9.8.7", "corr-xyz" }) Assert.IsFalse(dump.Contains(secret), secret);
            Assert.IsFalse(v.Timeline.Any(t => t.Remark == "अंतर्गत गोपनीय शेरा"));
            Assert.IsTrue(v.Timeline.Any(t => t.Remark == "पत्ता दुरुस्त करा"));
            Assert.IsFalse(v.IsFinal); Assert.AreEqual(2, v.Timeline.Count);
        }
        [TestMethod] public void Public_Status_Response_Has_Only_Approved_Fields()
        {
            var properties = typeof(PublicApplicationStatus).GetProperties().Select(p => p.Name).OrderBy(x => x).ToArray();
            CollectionAssert.AreEqual(new[] { "ApplicationNumber", "CurrentStage", "Service", "StatusMessage", "SubmittedAt" }.OrderBy(x => x).ToArray(), properties);
        }
    }

    [TestClass]
    public class StorageAndCsvTests
    {
        [DataTestMethod] [DataRow("../x.pdf")] [DataRow("a/../../x.pdf")] [DataRow("/etc/passwd")] [DataRow("C:\\x.pdf")] [DataRow("a\\..\\..\\x.pdf")] [DataRow("")]
        public void Storage_Rejects_Paths_That_Escape_The_Root(string rel)
        {
            var root = Path.Combine(Path.GetTempPath(), "smc-test-" + Guid.NewGuid().ToString("N"));
            var fs = new LocalFileStorage(root);
            try { fs.Resolve(rel); Assert.Fail("expected rejection of " + rel); } catch (InvalidOperationException) { }
        }
        [TestMethod] public void Storage_Round_Trips_Inside_The_Root_Only()
        {
            var root = Path.Combine(Path.GetTempPath(), "smc-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var fs = new LocalFileStorage(root);
                fs.Save("documents/2026-0005/a.pdf", TestData.Pdf);
                Assert.IsTrue(fs.Exists("documents/2026-0005/a.pdf"));
                CollectionAssert.AreEqual(TestData.Pdf, fs.Read("documents/2026-0005/a.pdf"));
                Assert.IsTrue(fs.Resolve("documents/2026-0005/a.pdf").StartsWith(Path.GetFullPath(root)));
                fs.Delete("documents/2026-0005/a.pdf"); Assert.IsFalse(fs.Exists("documents/2026-0005/a.pdf"));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        [TestMethod] public void Csv_Escapes_And_Blocks_Formula_Injection()
        {
            Assert.AreEqual("\"a,b\"", CsvWriter.Cell("a,b")); Assert.AreEqual("\"he said \"\"hi\"\"\"", CsvWriter.Cell("he said \"hi\""));
            Assert.AreEqual("'=1+1", CsvWriter.Cell("=1+1")); Assert.AreEqual("'@SUM(A1)", CsvWriter.Cell("@SUM(A1)"));
            Assert.AreEqual("राम", CsvWriter.Cell("राम"));
            var csv = CsvWriter.Write(new[] { "Label", "Count" }, new List<string[]> { new[] { "Submitted", "5" } });
            Assert.AreEqual("Label,Count\r\nSubmitted,5\r\n", csv);
        }
    }

    [TestClass]
    public class AuthTests
    {
        private FakeUsers _users; private FakeAudit _audit; private FakeClock _clock; private AuthService _svc;
        [TestInitialize] public void Init()
        {
            _users = new FakeUsers(); _audit = new FakeAudit(); _clock = new FakeClock();
            _users.Users.Add(new UserRecord { Id = 5, Username = "je1", PasswordHash = "hash:secret", FullName = "JE One", Role = Roles.JE, IsActive = true, ZoneNumber = 3 });
            _users.Users.Add(new UserRecord { Id = 6, Username = "off", PasswordHash = "hash:secret", FullName = "Gone", Role = Roles.OS, IsActive = false });
            _users.Users.Add(new UserRecord { Id = 7, Username = "odd", PasswordHash = "hash:secret", FullName = "Odd", Role = "Citizen", IsActive = true });
            _svc = new AuthService(_users, new FakeVerifier(), _audit, _clock, new LoginThrottle(3, 15));
        }
        [TestMethod] public void Valid_Login_Returns_Role_And_Zone_And_Audits()
        {
            var r = _svc.Login(" je1 ", "secret", "9.9.9.9");
            Assert.IsTrue(r.IsSuccess); Assert.AreEqual(Roles.JE, r.Value.Role); Assert.AreEqual(3, r.Value.ZoneNumber.Value);
            CollectionAssert.AreEqual(new[] { 5 }, _users.LoggedIn); Assert.AreEqual(AuditActions.LoginSuccess, _audit.Entries[0].Action); Assert.AreEqual(5, _audit.Entries[0].UserId.Value);
        }
        [TestMethod] public void Wrong_Password_Is_Generic_And_Audited()
        {
            var r = _svc.Login("je1", "bad", "9.9.9.9");
            Assert.AreEqual("LOGIN_INVALID", r.Error.Code); Assert.AreEqual(AuditActions.LoginFailed, _audit.Entries[0].Action); Assert.AreEqual(0, _users.LoggedIn.Count);
            Assert.AreEqual(_svc.Login("nobody", "x", "9.9.9.9").Error.Message, r.Error.Message); // no user enumeration
        }
        [TestMethod] public void Inactive_Or_Unknown_Role_Cannot_Login() { Assert.AreEqual("LOGIN_INVALID", _svc.Login("off", "secret", "1.1.1.1").Error.Code); Assert.AreEqual("LOGIN_INVALID", _svc.Login("odd", "secret", "1.1.1.1").Error.Code); }
        [TestMethod] public void Repeated_Failures_Lock_Even_The_Correct_Password_Then_Expire()
        {
            for (var i = 0; i < 3; i++) _svc.Login("je1", "bad", "2.2.2.2");
            Assert.AreEqual("LOGIN_LOCKED", _svc.Login("je1", "secret", "2.2.2.2").Error.Code);
            Assert.IsTrue(_svc.Login("je1", "secret", "3.3.3.3").IsSuccess); // different IP key
            _clock.UtcNow = _clock.UtcNow.AddMinutes(16);
            Assert.IsTrue(_svc.Login("je1", "secret", "2.2.2.2").IsSuccess);
        }
        [TestMethod] public void Audit_Failure_Does_Not_Break_Login() { _audit.Throw = true; Assert.IsTrue(_svc.Login("je1", "secret", "1.1.1.1").IsSuccess); }
        [TestMethod] public void Empty_Credentials_Are_Rejected() { Assert.AreEqual("LOGIN_INVALID", _svc.Login("", "", null).Error.Code); }
    }

    [TestClass]
    public class SmsTests
    {
        private FakeMasters _m; private FakeGateway _g; private FakeSmsEvents _e; private FakeUsers _u;
        [TestInitialize] public void Init()
        {
            _m = new FakeMasters { Template = new SmsTemplateRecord { EventType = "ApplicationSubmitted", MessageBody = "अर्ज क्र. {ApplicationNumber} प्राप्त झाला. {{ApplicantName}}", DltTemplateId = "T1", SenderId = "MAHGOV" } };
            _g = new FakeGateway(); _e = new FakeSmsEvents(); _u = new FakeUsers();
        }
        private SmsService Svc(bool enabled) { return new SmsService(_m, _g, _e, _u, new FakeClock(), enabled, "MAHGOV"); }
        private static NotificationRequest Req() { return new NotificationRequest { EventType = "ApplicationSubmitted", ApplicationNumber = "2026-0005", RecipientMobile = "9876543210", Variables = new Dictionary<string, string> { { "ApplicationNumber", "2026-0005" }, { "ApplicantName", "राम" } } }; }

        [TestMethod] public void Renders_Template_And_Records_Sent()
        {
            Svc(true).Notify(Req());
            Assert.AreEqual("अर्ज क्र. 2026-0005 प्राप्त झाला. राम", _g.Texts[0]); Assert.AreEqual(SmsStatuses.Sent, _e.Rows[0].Status); Assert.AreEqual("MSG1", _e.Rows[0].ProviderMessageId);
        }
        [TestMethod] public void Gateway_Failure_Is_Recorded_Not_Thrown() { _g.Ok = false; Svc(true).Notify(Req()); Assert.AreEqual(SmsStatuses.Failed, _e.Rows[0].Status); Assert.IsNotNull(_e.Rows[0].FailureReason); }
        [TestMethod] public void Gateway_Exception_Never_Escapes() { _g.Throw = true; Svc(true).Notify(Req()); Assert.AreEqual(SmsStatuses.Failed, _e.Rows[0].Status); }
        [TestMethod] public void Disabled_Or_Missing_Template_Is_Suppressed_And_Sends_Nothing()
        {
            Svc(false).Notify(Req()); Assert.AreEqual(SmsStatuses.Suppressed, _e.Rows[0].Status); Assert.AreEqual(0, _g.Texts.Count);
            _m.Template = null; Svc(true).Notify(Req()); Assert.AreEqual(SmsStatuses.Suppressed, _e.Rows[0].Status);
        }
        [TestMethod] public void Unresolved_Placeholders_Are_Suppressed() { _m.Template.MessageBody = "क्र. {#var#} {ApplicationNumber}"; Svc(true).Notify(Req()); Assert.AreEqual(SmsStatuses.Suppressed, _e.Rows[0].Status); Assert.AreEqual(0, _g.Texts.Count); }
        [TestMethod] public void Retry_Updates_The_Single_Row_Per_Application_And_Event() { Svc(true).Notify(Req()); Svc(true).Notify(Req()); Assert.AreEqual(1, _e.Rows.Count); }
        [TestMethod] public void Role_Recipients_Are_Resolved_From_Users()
        {
            _u.Users.Add(new UserRecord { Id = 1, Username = "ac", Role = Roles.AssistantCommissioner, IsActive = true, Mobile = "9000000001" });
            _m.Template.EventType = "AssistantCommissionerNotification";
            var r = new NotificationRequest { EventType = "AssistantCommissionerNotification", ApplicationNumber = "2026-0005", RecipientRole = Roles.AssistantCommissioner, Variables = new Dictionary<string, string> { { "ApplicationNumber", "x" }, { "ApplicantName", "y" } } };
            Svc(true).Notify(r); Assert.AreEqual(1, _g.Texts.Count); Assert.AreEqual("9000000001", _e.Rows[0].Mobile);
        }

        [TestMethod] public void Access_Recovery_Otp_Uses_Only_Approved_Template_And_Reports_Gateway_Acceptance()
        {
            _m.Template = new SmsTemplateRecord { EventType = SmsEventTypes.AccessCodeRecovery, MessageBody = "OTP {{Otp}} वैध {{ExpiryMinutes}} मिनिटे", DltTemplateId = "OTP-T1", SenderId = "MAHGOV" };
            var status = Svc(true).SendAccessRecoveryOtp("20260001", "9876543210", "123456", 10);
            Assert.AreEqual(OtpSmsSendStatus.Sent, status); Assert.AreEqual("OTP 123456 वैध 10 मिनिटे", _g.Texts[0]);
            Assert.AreEqual(SmsEventTypes.AccessCodeRecovery, _e.Rows[0].EventType); Assert.AreEqual(SmsStatuses.Sent, _e.Rows[0].Status);
        }

        [TestMethod] public void Access_Recovery_Otp_Is_Not_Claimed_Sent_When_Disabled_TemplateMissing_Or_GatewayRejects()
        {
            _m.Template = new SmsTemplateRecord { EventType = SmsEventTypes.AccessCodeRecovery, MessageBody = "OTP {{Otp}}", DltTemplateId = "OTP-T1" };
            Assert.AreEqual(OtpSmsSendStatus.Unavailable, Svc(false).SendAccessRecoveryOtp("20260001", "9876543210", "123456", 10));
            _m.Template = null;
            Assert.AreEqual(OtpSmsSendStatus.Unavailable, Svc(true).SendAccessRecoveryOtp("20260001", "9876543210", "123456", 10));
            _m.Template = new SmsTemplateRecord { EventType = SmsEventTypes.AccessCodeRecovery, MessageBody = "OTP {{Otp}}", DltTemplateId = "OTP-T1" };
            _g.Ok = false;
            Assert.AreEqual(OtpSmsSendStatus.Failed, Svc(true).SendAccessRecoveryOtp("20260001", "9876543210", "123456", 10));
        }
    }

    [TestClass]
    public class DocumentAndCertificateTests
    {
        internal sealed class FakeQuery : IQueryService
        {
            public DocumentRecord Doc; public ApplicationDetail Detail; public int OfficerListCalls; public int OfficerKpiCalls; public int DashboardCalls; public bool ThrowOfficerKpi;
            public PagedResult<ApplicationListItem> Search(ApplicationFilter f) { return new PagedResult<ApplicationListItem>(); }
            public ApplicationDetail GetDetail(int id) { return Detail; }
            public PublicApplicationStatus GetPublicStatus(string n) { return null; }
            public int? FindIdByNumber(string n) { return null; }
            public string GetRegisteredMobile(int id) { return null; }
            public string GetAccessTokenHash(int id) { return null; }
            public DashboardCounts GetDashboard(int? z) { DashboardCalls++; return new DashboardCounts(); }
            public OfficerDashboardCounts GetOfficerDashboardCounts(string r, int? z, int u) { OfficerKpiCalls++; if (ThrowOfficerKpi) throw new NotSupportedException("translation failed"); return new OfficerDashboardCounts(); }
            public PagedResult<ApplicationListItem> SearchPendingForRole(string r, ApplicationFilter f) { return new PagedResult<ApplicationListItem>(); }
            public PagedResult<ApplicationListItem> SearchOfficerApplications(string r, int u, ApplicationFilter f) { OfficerListCalls++; return new PagedResult<ApplicationListItem>(); }
            public List<ReportRow> ReportByStatus(int? z, DateTime? f, DateTime? t) { return new List<ReportRow>(); }
            public List<ReportRow> ReportByService(int? z, DateTime? f, DateTime? t) { return new List<ReportRow>(); }
            public List<ReportRow> ReportByZone(int? z, DateTime? f, DateTime? t) { return new List<ReportRow>(); }
            public List<ReportRow> ReportByMonth(int? z, DateTime f, DateTime t) { return new List<ReportRow>(); }
            public PagedResult<AuditLogItem> SearchAudit(DateTime? f, DateTime? t, string a, string u, int p, int s) { return new PagedResult<AuditLogItem>(); }
            public List<UserListItem> GetUsers() { return new List<UserListItem>(); }
            public DocumentRecord GetDocument(int id) { return Doc != null && Doc.Id == id ? Doc : null; }
            public DocumentRecord GetZoneDocument(int id) { return null; }
        }
        private sealed class FakePdf : IPdfGenerator
        {
            public byte[] Acknowledgement(ApplicationDetail d, PermitConfigRecord c) { return TestData.Pdf; }
            public byte[] SubmittedApplication(ApplicationDetail d, PermitConfigRecord c) { return TestData.Pdf; }
            public byte[] Receipt(ApplicationDetail d, PermitConfigRecord c) { return TestData.Pdf; }
            public byte[] Certificate(ApplicationDetail d, PermitConfigRecord c, List<string> t, string by) { return TestData.Pdf; }
        }

        private FakeQuery _q; private FakeFiles _files; private FakeAudit _audit; private DocumentService _svc;
        [TestInitialize] public void Init()
        {
            _q = new FakeQuery { Doc = new DocumentRecord { Id = 9, ApplicationId = 1, ApplicationNumber = "2026-0005", DocumentType = "IdentityProof", FileName = "../../aadhaar.pdf", ContentType = "application/pdf", RelativePath = "documents/2026-0005/x.pdf" } };
            _files = new FakeFiles(); _files.Files["documents/2026-0005/x.pdf"] = TestData.Pdf; _audit = new FakeAudit();
            _svc = new DocumentService(_q, _files, _audit, new FakeClock());
        }
        [TestMethod] public void Citizen_With_Token_For_The_Same_Application_Can_Download() { var r = _svc.Open(9, Roles.Citizen, null, "citizen", "1.1.1.1", 1); Assert.IsTrue(r.IsSuccess); Assert.IsFalse(r.Value.FileName.Contains("..")); Assert.AreEqual(0, _audit.Entries.Count); }
        [TestMethod] public void Citizen_Cannot_Open_Another_Applications_Document() { Assert.AreEqual("FORBIDDEN", _svc.Open(9, Roles.Citizen, null, "c", null, 2).Error.Code); Assert.AreEqual("FORBIDDEN", _svc.Open(9, Roles.Citizen, null, "c", null, null).Error.Code); }
        [TestMethod] public void Officer_Download_Is_Audited_And_Unknown_Role_Is_Denied()
        {
            Assert.IsTrue(_svc.Open(9, Roles.JE, 10, "je.user", "1.1.1.1", null).IsSuccess);
            Assert.AreEqual("Document Downloaded", _audit.Entries[0].Action); Assert.AreEqual(10, _audit.Entries[0].UserId.Value);
            Assert.AreEqual("FORBIDDEN", _svc.Open(9, "Hacker", 1, "x", null, null).Error.Code);
        }
        [TestMethod] public void Missing_Document_Or_File_Is_Handled() { Assert.AreEqual("NOT_FOUND", _svc.Open(404, Roles.Admin, 1, "a", null, null).Error.Code); _files.Files.Clear(); Assert.AreEqual("FILE_MISSING", _svc.Open(9, Roles.Admin, 1, "a", null, null).Error.Code); }

        [TestMethod] public void Certificate_Is_Issued_Only_For_Approved_Zone_Applications()
        {
            var store = new FakeWorkflowStore { Current = FakeWorkflowStore.Snapshot(ZoneStages.Approved, PaymentStatuses.PaymentDone) }; var n = new FakeNotifier();
            var svc = new CertificateService(_q, new FakeMasters(), new FakePdf(), _files, store, n, _audit, new FakeClock());
            _q.Detail = new ApplicationDetail { Id = 1, ApplicationNumber = "2026-0005", ProcessingRoute = "Zone", Stage = ZoneStages.Approved, Mobile = "9876543210", ApplicantName = "A" };
            Assert.IsTrue(svc.Issue(1, Actors.AC()).IsSuccess);
            Assert.AreEqual("certificates/2026-0005/certificate.pdf", store.CertificatePath); Assert.IsTrue(_files.Exists(store.CertificatePath));
            Assert.AreEqual(SmsEventTypes.CertificateAvailable, n.Sent[0].EventType); Assert.AreEqual(AuditActions.CertificateGenerated, _audit.Entries.Last().Action);
            _q.Detail.Stage = ZoneStages.OSPending; Assert.AreEqual("NOT_APPROVED", svc.Issue(1, Actors.AC()).Error.Code);
            _q.Detail.Stage = ZoneStages.Approved; _q.Detail.ProcessingRoute = "Legacy"; Assert.AreEqual("NOT_APPROVED", svc.Issue(1, Actors.AC()).Error.Code);
        }
    }
}
