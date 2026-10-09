using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Business.Numbering;
using SMC.BhoomiMalmatta.Business.Pricing;
using SMC.BhoomiMalmatta.Business.Security;
using SMC.BhoomiMalmatta.Business.Submission;
using SMC.BhoomiMalmatta.Business.Workflow;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Tests.Support;

namespace SMC.BhoomiMalmatta.Tests
{
    [TestClass]
    public class SubmissionTests
    {
        private FakeAppStore _store; private FakeNumberStore _nums; private FakeFiles _files; private FakeNotifier _notifier; private DemandSubmissionService _svc;

        [TestInitialize] public void Init()
        {
            _nums = new FakeNumberStore(); _store = new FakeAppStore(); _files = new FakeFiles(); _notifier = new FakeNotifier(); var clock = new FakeClock();
            _svc = new DemandSubmissionService(new FakeMasters(), new ApplicationNumberService(_nums, clock), _store, _files, clock, _notifier);
        }
        private static List<FieldError> Errs(SubmissionData d) { return SubmissionValidator.Validate(SubmissionValidator.Normalize(d), new FakeMasters().GetCatalog()); }
        private static bool Has(IEnumerable<FieldError> e, string field) { return e.Any(x => x.Field == field); }

        [TestMethod] public void Valid_Submission_Has_No_Errors_And_Values_Are_Trimmed()
        {
            var n = SubmissionValidator.Normalize(TestData.Valid());
            var validationErrors = SubmissionValidator.Validate(n, new FakeMasters().GetCatalog());
            Assert.AreEqual(0, validationErrors.Count, string.Join(" | ", validationErrors.Select(x => x.Field + ": " + x.Message)));
            Assert.AreEqual("राम पाटील", n.ApplicantName); Assert.AreEqual("9876543210", n.Mobile); Assert.AreEqual("ram@example.com", n.Email);
            Assert.AreEqual(n.PermanentAddress, n.CorrespondenceAddress);
        }
        [TestMethod] public void End_Date_Before_Start_Date_Is_Rejected() { var d = TestData.Valid("VariousBusinessSpace"); d.EndDate = new System.DateTime(2026, 10, 1); Assert.IsTrue(Has(Errs(d), "EndDate")); }
        [DataTestMethod] [DataRow("12345")] [DataRow("0123456789")] [DataRow("98765abcde")] [DataRow("")]
        public void Invalid_Mobile_Is_Rejected(string m) { var d = TestData.Valid(); d.Mobile = m; Assert.IsTrue(Has(Errs(d), "Mobile")); }
        [DataTestMethod] [DataRow("not-an-email")] [DataRow("a@b")]
        public void Invalid_Email_Is_Rejected(string m) { var d = TestData.Valid(); d.Email = m; Assert.IsTrue(Has(Errs(d), "Email")); }
        [TestMethod] public void Optional_Applicant_Identifiers_Are_Valid_When_Absent_And_Preserved_When_Entered()
        {
            var d = TestData.Valid(); d.Email = ""; d.IdentityNumber = ""; d.PanNumber = ""; d.GstNumber = "";
            Assert.IsFalse(Has(Errs(d), "Email")); Assert.IsFalse(Has(Errs(d), "IdentityNumber")); Assert.IsFalse(Has(Errs(d), "PanNumber")); Assert.IsFalse(Has(Errs(d), "GstNumber"));
            d.ApplicantType = "वैयक्तिक"; d.IdentityNumber = "123456789012"; d.PanNumber = "abcde1234f";
            var n = SubmissionValidator.Normalize(d);
            Assert.IsFalse(Has(Errs(n), "Mobile")); Assert.AreEqual("वैयक्तिक", n.ApplicantType); Assert.AreEqual("123456789012", n.IdentityNumber); Assert.AreEqual("ABCDE1234F", n.PanNumber);
            d.IdentityNumber = "123"; d.PanNumber = "bad"; var invalid = Errs(d);
            Assert.IsTrue(Has(invalid, "IdentityNumber")); Assert.IsTrue(Has(invalid, "PanNumber"));
        }
        [TestMethod] public void State_And_District_Are_Locked() { var d = TestData.Valid(); d.State = "Karnataka"; d.District = "Pune"; var e = Errs(d); Assert.IsTrue(Has(e, "State")); Assert.IsTrue(Has(e, "District")); }
        [TestMethod] public void City_Is_Forced_To_Solapur_In_New_Submissions()
        {
            var d = TestData.Valid("MiniGaala"); d.City = "Tampered City";
            var result = _svc.Submit(d, TestData.Docs(), null, null);
            Assert.IsNull(result.Item2); Assert.AreEqual("सोलापूर", _store.Saved[0].Data.City);
        }
        [TestMethod] public void Required_Fields_And_Marathi_Messages()
        {
            var d = TestData.Valid(); d.ApplicantName = " "; d.PermanentAddress = ""; d.Location = ""; d.DeclarationAccepted = false; var e = Errs(d);
            Assert.IsTrue(e.Any(x => x.Field == "ApplicantName" && x.Message.Contains("आवश्यक")));
            Assert.IsTrue(Has(e, "PermanentAddress")); Assert.IsTrue(Has(e, "Location")); Assert.IsTrue(Has(e, "DeclarationAccepted"));
        }
        [TestMethod] public void Required_Documents_Are_Enforced() { var d = TestData.Valid(); d.UploadedDocumentTypes = new List<string> { DocumentTypes.IdentityProof }; Assert.IsTrue(Has(Errs(d), "Documents")); }
        [TestMethod] public void Unknown_Service_Is_Rejected() { Assert.IsTrue(Has(Errs(TestData.Valid("Nope")), "ServiceType")); }
        [TestMethod] public void VariousBusinessSpace_Requires_Valid_BusinessType() { var d = TestData.Valid("VariousBusinessSpace"); d.BusinessType = null; Assert.IsTrue(Has(Errs(d), "BusinessType")); }
        [TestMethod] public void Zone_Must_Be_1_To_8() { var d = TestData.Valid(); d.Zone = "9"; Assert.IsTrue(Has(Errs(d), "Zone")); d.Zone = ""; Assert.IsTrue(Has(Errs(d), "Zone")); }
        [DataTestMethod] [DataRow(0, 5)] [DataRow(5, 0)] [DataRow(-1, 5)] [DataRow(5, -1)]
        public void NonPositive_Dimensions_Are_Rejected_By_Submission_Validation(int length, int width)
        { var d = TestData.Valid("VariousBusinessSpace"); d.LengthFt = length; d.WidthFt = width; var errors = Errs(d); Assert.IsTrue(Has(errors, "LengthFt") || length > 0); Assert.IsTrue(Has(errors, "WidthFt") || width > 0); }

        [TestMethod] public void Backend_Recalculates_Fixed_Amount_And_Stores_Documents()
        {
            var r = _svc.Submit(TestData.Valid("MajorGaala"), TestData.Docs(), "1.1.1.1", "c");
            Assert.IsNull(r.Item2); Assert.AreEqual(50000m, r.Item1.Pricing.FinalAmount.Value); Assert.AreEqual(366, r.Item1.Pricing.TotalDays);
            Assert.AreEqual(1, _store.Saved.Count); Assert.AreEqual(64, _store.Saved[0].AccessTokenHash.Length); Assert.AreEqual(2, _store.Saved[0].Documents.Count);
            Assert.AreEqual(2, _files.Files.Count);
            Assert.IsTrue(_store.Saved[0].Documents.All(d => d.RelativePath.StartsWith("documents/") && !d.RelativePath.Contains("..") && !d.RelativePath.Contains(":")));
            Assert.AreEqual(SmsEventTypes.ApplicationSubmitted, _notifier.Sent[0].EventType);
        }
        [TestMethod] public void Application_Submission_Accepts_Validated_Attachments_Above_Old_Five_MiB_Limit()
        {
            var docs = TestData.Docs(); docs[0].Content = new byte[6 * 1024 * 1024];
            Array.Copy(TestData.Pdf, docs[0].Content, TestData.Pdf.Length);
            var result = _svc.Submit(TestData.Valid(), docs, null, null, FileValidator.MaxApplicationAttachmentBytes);
            Assert.IsNull(result.Item2); Assert.AreEqual(1, _store.Saved.Count); Assert.AreEqual(6 * 1024 * 1024, _files.Files.Values.First(x => x.Length > 1024).Length);
        }
        [TestMethod] public void Existing_Submission_EntryPoint_Keeps_Its_Five_MiB_Limit()
        {
            var docs = TestData.Docs(); docs[0].Content = new byte[6 * 1024 * 1024];
            Array.Copy(TestData.Pdf, docs[0].Content, TestData.Pdf.Length);
            var result = _svc.Submit(TestData.Valid(), docs, null, null);
            Assert.IsNull(result.Item1); Assert.IsTrue(result.Item2.Errors.Any(e => e.Field == "Documents" && e.Message.Contains("५ MB")));
            Assert.AreEqual(0, _store.Saved.Count);
        }
        [TestMethod] public void Fixed_Annual_Pricing_Ignores_Tampered_Amount_And_EndDate()
        {
            var d = TestData.Valid("MajorGaala", 1m); d.StartDate = new DateTime(2026, 10, 6); d.EndDate = new DateTime(2030, 4, 1);
            var r = _svc.Submit(d, TestData.Docs(), null, null);
            Assert.IsNull(r.Item2); Assert.AreEqual(50000m, r.Item1.Pricing.FinalAmount.Value);
            Assert.AreEqual(new DateTime(2027, 10, 6), _store.Saved[0].Data.EndDate);
        }
        [DataTestMethod]
        [DataRow("MiniGaala", 20000)] [DataRow("MajorGaala", 50000)] [DataRow("LandFee", 20000)]
        public void All_Fixed_Services_Use_Explicit_Annual_Fees(string code, int expected)
        {
            var data = TestData.Valid(code); data.StartDate = new DateTime(2026, 10, 6); data.EndDate = new DateTime(2032, 1, 1);
            data.LengthFt = 99m; data.WidthFt = 88m;
            var result = _svc.Preview(data);
            Assert.IsTrue(result.IsSuccess); Assert.AreEqual((decimal)expected, result.Value.FinalAmount.Value);
            Assert.AreEqual(new DateTime(2027, 10, 6), DemandServicePricingRules.EffectiveEndDate(code, data.StartDate, data.EndDate));
        }
        [TestMethod] public void VariousBusinessSpace_Uses_Approved_OneRupeeAreaDailyRule_And_Persists_The_Total()
        {
            var oneDay = TestData.Valid("VariousBusinessSpace"); oneDay.LengthFt = 10m; oneDay.WidthFt = 10m; oneDay.EndDate = oneDay.StartDate;
            var preview = _svc.Preview(oneDay);
            Assert.IsTrue(preview.IsSuccess); Assert.AreEqual(100m, preview.Value.AreaSqFt); Assert.AreEqual(100m, preview.Value.DailyRate.Value); Assert.AreEqual(100m, preview.Value.FinalAmount.Value);

            var threeDays = TestData.Valid("VariousBusinessSpace"); threeDays.LengthFt = 10m; threeDays.WidthFt = 10m; threeDays.EndDate = threeDays.StartDate.AddDays(2);
            var submitted = _svc.Submit(threeDays, TestData.Docs(), null, null);
            Assert.IsNull(submitted.Item2); Assert.AreEqual(100m, submitted.Item1.Pricing.DailyRate.Value); Assert.AreEqual(300m, submitted.Item1.Pricing.FinalAmount.Value);
            Assert.AreEqual(1, _store.Saved.Count); Assert.AreEqual(300m, _store.Saved[0].FeeAmount.Value);
        }
        [TestMethod] public void Submission_Starts_Zone_History_At_JEPending()
        {
            _svc.Submit(TestData.Valid(), TestData.Docs(), "9.9.9.9", "cid");
            var h = ActionHistory.Parse(_store.Saved[0].InitialHistoryJson);
            Assert.AreEqual(1, h.Count); Assert.AreEqual(ZoneStages.JEPending, h[0].NewStage); Assert.AreEqual("cid", h[0].CorrelationId);
        }
        [TestMethod] public void Application_Numbers_Are_Unique_And_Skip_Existing()
        {
            _nums.Existing.Add("2026-0005"); _nums.Existing.Add("2026-0006");
            var seen = new HashSet<string>();
            for (var i = 0; i < 25; i++) Assert.IsTrue(seen.Add(_svc.Submit(TestData.Valid(), TestData.Docs(), null, null).Item1.ApplicationNumber));
            Assert.IsFalse(seen.Contains("2026-0005")); Assert.AreEqual(25, _store.Saved.Select(s => s.ApplicationNumber).Distinct().Count());
        }
        [TestMethod] public void Invalid_Submission_Saves_Nothing() { var d = TestData.Valid(); d.Mobile = "1"; var r = _svc.Submit(d, TestData.Docs(), null, null); Assert.IsNull(r.Item1); Assert.AreEqual(0, _store.Saved.Count); Assert.AreEqual(0, _files.Files.Count); }
        [TestMethod] public void Missing_Or_Fake_Documents_Are_Rejected_Before_Anything_Is_Stored()
        {
            Assert.IsNotNull(_svc.Submit(TestData.Valid(), new List<DocumentUpload>(), null, null).Item2);
            var bad = TestData.Docs(); bad[0].Content = new byte[] { 0x4D, 0x5A, 0x90, 0 };
            var r = _svc.Submit(TestData.Valid(), bad, null, null);
            Assert.IsNotNull(r.Item2); Assert.AreEqual(0, _files.Files.Count); Assert.AreEqual(0, _store.Saved.Count);
        }
        [TestMethod] public void Database_Failure_Removes_Uploaded_Files()
        {
            _store.FailNext = true; var r = _svc.Submit(TestData.Valid(), TestData.Docs(), null, null);
            Assert.IsNotNull(r.Item2); Assert.AreEqual(0, _files.Files.Count);
        }
    }

    [TestClass]
    public class WorkflowServiceTests
    {
        private static void Build(string stage, string pay, out ZoneWorkflowService svc, out FakeWorkflowStore store, out FakeNotifier notifier)
        {
            store = new FakeWorkflowStore { Current = FakeWorkflowStore.Snapshot(stage, pay) }; notifier = new FakeNotifier();
            svc = new ZoneWorkflowService(store, new FakeClock(), notifier, null, new FakeFiles());
        }
        private static ZoneWorkflowService Build(string stage, string pay, out FakeWorkflowStore store) { FakeNotifier n; ZoneWorkflowService s; Build(stage, pay, out s, out store, out n); return s; }
        private static WorkflowCommand Cmd(string action, string stage, string remarks = null, decimal? amount = null, string utr = null)
        { return new WorkflowCommand { ApplicationId = 1, Action = action, ExpectedStage = stage, Remarks = remarks, Amount = amount, PaymentReference = utr }; }
        private static WorkflowCommand PaymentSubmissionCommand(string utr, DateTime date)
        { return new WorkflowCommand { ApplicationId = 1, Action = WorkflowActions.CitizenSubmitPayment, ExpectedStage = ZoneStages.PaymentRequired, PaymentReference = utr, PaymentDate = date, PaymentProof = new DocumentUpload { FileName = "receipt.pdf", ContentType = "application/pdf", Content = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D } } }; }

        [TestMethod] public void JE_To_OS_Writes_History_Audit_And_Status()
        {
            FakeWorkflowStore store; var svc = Build(ZoneStages.JEPending, PaymentStatuses.PendingOSAmount, out store);
            var r = svc.Execute(Cmd(WorkflowActions.JEVerifyForward, ZoneStages.JEPending, "तपासले"), Actors.JE());
            Assert.IsTrue(r.IsSuccess); Assert.AreEqual(ZoneStages.OSPending, store.Current.Stage); Assert.IsTrue(store.Current.DocumentsVerified);
            var h = ActionHistory.Parse(store.Current.HistoryJson);
            Assert.AreEqual(2, h.Count); Assert.AreEqual(AuditActions.JEVerified, h[1].Action); Assert.AreEqual("je.user", h[1].ActionByUserName);
            Assert.AreEqual("10.0.0.1", h[1].IpAddress); Assert.AreEqual("corr-1", h[1].CorrelationId); Assert.AreEqual(Roles.JE, h[1].FromRole); Assert.AreEqual(Roles.OS, h[1].ToRole);
            Assert.AreEqual(ApplicationStatuses.Submitted, h[1].PreviousStatus); Assert.AreEqual(ApplicationStatuses.UnderScrutiny, h[1].NewStatus);
            Assert.AreEqual(1, store.Audits.Count);
        }
        [TestMethod] public void Full_Three_Level_Flow_Keeps_Complete_Immutable_History_And_Notifies()
        {
            FakeWorkflowStore store; FakeNotifier n; ZoneWorkflowService svc; Build(ZoneStages.JEPending, PaymentStatuses.PendingOSAmount, out svc, out store, out n);
            Assert.IsTrue(svc.Execute(Cmd(WorkflowActions.JEVerifyForward, ZoneStages.JEPending), Actors.JE()).IsSuccess);
            Assert.IsTrue(svc.Execute(Cmd(WorkflowActions.OSRequestPayment, ZoneStages.OSPending, amount: 250m), Actors.OS()).IsSuccess);
            Assert.AreEqual(250m, store.Current.PayableAmount);
            Assert.IsTrue(svc.Execute(PaymentSubmissionCommand("UTR123456", new DateTime(2026, 10, 2)), Actors.Citizen()).IsSuccess);
            Assert.AreEqual(PaymentStatuses.PaymentSubmitted, store.Current.PaymentStatus);
            Assert.AreEqual(1, store.PaymentProofs.Count);
            // Verify payment forwards to the AC automatically: no separate OSForwardToAC call.
            var verified = svc.Execute(Cmd(WorkflowActions.OSConfirmPayment, ZoneStages.PaymentRequired), Actors.OS());
            Assert.IsTrue(verified.IsSuccess); Assert.AreEqual(ZoneStages.AssistantCommissionerApprovalPending, verified.Value.NewStage);
            var fin = svc.Execute(Cmd(WorkflowActions.ACApprove, ZoneStages.AssistantCommissionerApprovalPending), Actors.AC());
            Assert.IsTrue(fin.IsSuccess); Assert.AreEqual(ApplicationStatuses.Approved, fin.Value.NewStatus);
            var h = ActionHistory.Parse(store.Current.HistoryJson);
            Assert.AreEqual(7, h.Count); Assert.IsTrue(ActionHistory.VerifyChain(store.Current.HistoryJson));
            CollectionAssert.AreEqual(new[] { Roles.Citizen, Roles.JE, Roles.OS, Roles.Citizen, Roles.OS, Roles.OS, Roles.AssistantCommissioner }, h.Select(x => x.FromRole).ToArray());
            CollectionAssert.AreEqual(new[] { SmsEventTypes.PaymentRequired, SmsEventTypes.PaymentDone, SmsEventTypes.AssistantCommissionerNotification, SmsEventTypes.ApplicationApproved }, n.Sent.Select(x => x.EventType).ToArray());
        }
        [TestMethod] public void JE_Cannot_Do_OS_Action_And_Nothing_Is_Saved()
        {
            FakeWorkflowStore store; var svc = Build(ZoneStages.OSPending, PaymentStatuses.PaymentDone, out store);
            var r = svc.Execute(Cmd(WorkflowActions.OSForwardToAC, ZoneStages.OSPending), Actors.JE());
            Assert.AreEqual("WORKFLOW_FORBIDDEN", r.Error.Code); Assert.AreEqual(0, store.SaveCalls);
        }
        [TestMethod] public void OS_Cannot_Approve_Final()
        { FakeWorkflowStore s; Assert.AreEqual("WORKFLOW_FORBIDDEN", Build(ZoneStages.AssistantCommissionerApprovalPending, PaymentStatuses.PaymentDone, out s).Execute(Cmd(WorkflowActions.ACApprove, ZoneStages.AssistantCommissionerApprovalPending, "x"), Actors.OS()).Error.Code); }
        [TestMethod] public void Staff_Cannot_Act()
        {
            FakeWorkflowStore store; var svc = Build(ZoneStages.JEPending, PaymentStatuses.PendingOSAmount, out store);
            Assert.AreEqual("WORKFLOW_FORBIDDEN", svc.Execute(Cmd(WorkflowActions.JEVerifyForward, ZoneStages.JEPending), Actors.Staff()).Error.Code); Assert.AreEqual(0, store.SaveCalls);
        }
        [TestMethod] public void Duplicate_Submission_Is_Rejected_As_Stale()
        {
            FakeWorkflowStore store; var svc = Build(ZoneStages.JEPending, PaymentStatuses.PendingOSAmount, out store);
            Assert.IsTrue(svc.Execute(Cmd(WorkflowActions.JEVerifyForward, ZoneStages.JEPending), Actors.JE()).IsSuccess);
            Assert.AreEqual("STALE_STAGE", svc.Execute(Cmd(WorkflowActions.JEVerifyForward, ZoneStages.JEPending), Actors.JE()).Error.Code);
            Assert.AreEqual(1, store.SaveCalls); Assert.AreEqual(2, ActionHistory.Parse(store.Current.HistoryJson).Count);
        }
        [TestMethod] public void Skipping_A_Level_Is_Impossible()
        {
            FakeWorkflowStore s; var svc = Build(ZoneStages.JEPending, PaymentStatuses.PendingOSAmount, out s);
            Assert.AreEqual("WORKFLOW_INVALID_TRANSITION", svc.Execute(Cmd(WorkflowActions.OSForwardToAC, ZoneStages.JEPending), Actors.OS()).Error.Code);
            Assert.AreEqual("WORKFLOW_INVALID_TRANSITION", svc.Execute(Cmd(WorkflowActions.ACApprove, ZoneStages.JEPending, "x"), Actors.AC()).Error.Code);
        }
        [TestMethod] public void Concurrent_Writer_Causes_Conflict_Not_Overwrite()
        {
            FakeWorkflowStore store; var svc = Build(ZoneStages.JEPending, PaymentStatuses.PendingOSAmount, out store); store.SimulateConcurrentWriter = true;
            Assert.AreEqual("CONCURRENCY_CONFLICT", svc.Execute(Cmd(WorkflowActions.JEVerifyForward, ZoneStages.JEPending), Actors.JE()).Error.Code);
            Assert.AreEqual(ZoneStages.JEPending, store.Current.Stage);
        }
        [TestMethod] public void Zone_Scoped_Officer_Cannot_Act_On_Another_Zone()
        {
            FakeWorkflowStore s; var svc = Build(ZoneStages.JEPending, PaymentStatuses.PendingOSAmount, out s);
            Assert.AreEqual("ZONE_FORBIDDEN", svc.Execute(Cmd(WorkflowActions.JEVerifyForward, ZoneStages.JEPending), Actors.JE(5)).Error.Code);
            Assert.IsTrue(svc.Execute(Cmd(WorkflowActions.JEVerifyForward, ZoneStages.JEPending), Actors.JE(3)).IsSuccess);
        }
        [TestMethod] public void Legacy_Applications_Are_Read_Only()
        {
            var store = new FakeWorkflowStore { Current = FakeWorkflowStore.Snapshot(ZoneStages.JEPending, PaymentStatuses.PendingOSAmount, 3, ProcessingRoutes.Legacy) };
            var svc = new ZoneWorkflowService(store, new FakeClock());
            Assert.AreEqual("LEGACY_READONLY", svc.Execute(Cmd(WorkflowActions.JEVerifyForward, ZoneStages.JEPending), Actors.JE()).Error.Code); Assert.AreEqual(0, store.SaveCalls);
        }
        [TestMethod] public void Payment_Request_Needs_Positive_Amount_And_Confirmation_Needs_Valid_Reference()
        {
            FakeWorkflowStore s; Assert.AreEqual("AMOUNT_REQUIRED", Build(ZoneStages.OSPending, PaymentStatuses.PendingOSAmount, out s).Execute(Cmd(WorkflowActions.OSRequestPayment, ZoneStages.OSPending, amount: 0m), Actors.OS()).Error.Code);
            var svc2 = Build(ZoneStages.PaymentRequired, PaymentStatuses.PaymentSubmitted, out s);
            s.Current.RecordedPaymentReference = null;
            Assert.AreEqual("PAYMENT_REFERENCE_INVALID", svc2.Execute(Cmd(WorkflowActions.OSConfirmPayment, ZoneStages.PaymentRequired), Actors.OS()).Error.Code);
            Assert.AreEqual("PAYMENT_REFERENCE_INVALID", svc2.Execute(Cmd(WorkflowActions.OSConfirmPayment, ZoneStages.PaymentRequired, utr: "12"), Actors.OS()).Error.Code);
        }
        [TestMethod] public void Payment_Confirmation_Is_Idempotent_And_Detects_Duplicates()
        {
            FakeWorkflowStore store; var svc = Build(ZoneStages.PaymentRequired, PaymentStatuses.PaymentSubmitted, out store);
            Assert.IsTrue(svc.Execute(Cmd(WorkflowActions.OSConfirmPayment, ZoneStages.PaymentRequired), Actors.OS()).IsSuccess);
            var calls = store.SaveCalls; var hist = ActionHistory.Parse(store.Current.HistoryJson).Count;
            var repeat = svc.Execute(Cmd(WorkflowActions.OSConfirmPayment, ZoneStages.PaymentRequired), Actors.OS());
            Assert.IsTrue(repeat.IsSuccess); Assert.IsTrue(repeat.Value.WasAlreadyApplied);
            Assert.AreEqual(calls, store.SaveCalls); Assert.AreEqual(hist, ActionHistory.Parse(store.Current.HistoryJson).Count);
            // A different UTR after the payment is done is a conflict (PaymentRules.Confirm), not a silent success.
            Assert.AreEqual("PAYMENT_DUPLICATE", svc.Execute(Cmd(WorkflowActions.OSConfirmPayment, ZoneStages.PaymentRequired, utr: "OTHER123456"), Actors.OS()).Error.Code);
            Assert.AreEqual(calls, store.SaveCalls);
        }
        [TestMethod] public void OS_Cannot_Verify_Until_Applicant_Submits_Proof()
        {
            FakeWorkflowStore store; var svc = Build(ZoneStages.PaymentRequired, PaymentStatuses.PaymentRequired, out store);
            Assert.AreEqual("PAYMENT_PRECONDITION", svc.Execute(Cmd(WorkflowActions.OSConfirmPayment, ZoneStages.PaymentRequired), Actors.OS()).Error.Code);
        }
        [TestMethod] public void Payment_Verification_Is_OS_Only_and_Zone_Scoped()
        {
            FakeWorkflowStore store; var svc = Build(ZoneStages.PaymentRequired, PaymentStatuses.PaymentSubmitted, out store);
            Assert.AreEqual("WORKFLOW_FORBIDDEN", svc.Execute(Cmd(WorkflowActions.OSConfirmPayment, ZoneStages.PaymentRequired), Actors.JE()).Error.Code);
            Assert.AreEqual("ZONE_FORBIDDEN", svc.Execute(Cmd(WorkflowActions.OSConfirmPayment, ZoneStages.PaymentRequired), Actors.OS(4)).Error.Code);
            Assert.IsTrue(svc.Execute(Cmd(WorkflowActions.OSConfirmPayment, ZoneStages.PaymentRequired), Actors.OS(3)).IsSuccess);
        }
        [TestMethod] public void Applicant_Payment_Requires_Own_Ticket_and_Valid_Request_Date()
        {
            FakeWorkflowStore store; var svc = Build(ZoneStages.PaymentRequired, PaymentStatuses.PaymentRequired, out store);
            var cmd = PaymentSubmissionCommand("UTR123456", new DateTime(2026, 10, 2));
            Assert.AreEqual("WORKFLOW_FORBIDDEN", svc.Execute(cmd, new ActorContext { Role = Roles.Citizen, ApplicationId = 2 }).Error.Code);
            cmd = PaymentSubmissionCommand("UTR123456", new DateTime(2026, 9, 30));
            Assert.AreEqual("PAYMENT_DATE_BEFORE_REQUEST", svc.Execute(cmd, Actors.Citizen()).Error.Code);
            cmd = PaymentSubmissionCommand("UTR123456", new DateTime(2026, 10, 3));
            Assert.AreEqual("PAYMENT_DATE_FUTURE", svc.Execute(cmd, Actors.Citizen()).Error.Code);
            Assert.AreEqual(0, store.SaveCalls);
        }
        [TestMethod] public void Applicant_Submits_After_OS_Rejection_And_Duplicate_UTR_Is_Refused()
        {
            FakeWorkflowStore store; var svc = Build(ZoneStages.PaymentRequired, PaymentStatuses.PaymentRejected, out store);
            store.OtherPaymentReferences.Add("UTR123456");
            Assert.AreEqual("PAYMENT_REFERENCE_DUPLICATE", svc.Execute(PaymentSubmissionCommand("UTR123456", new DateTime(2026, 10, 2)), Actors.Citizen()).Error.Code);
            var accepted = svc.Execute(PaymentSubmissionCommand("UTR654321", new DateTime(2026, 10, 2)), Actors.Citizen());
            Assert.IsTrue(accepted.IsSuccess); Assert.AreEqual(PaymentStatuses.PaymentSubmitted, store.Current.PaymentStatus);
            Assert.AreEqual("UTR654321", store.Current.RecordedPaymentReference); Assert.AreEqual(1, store.PaymentProofs.Count);
        }
        [TestMethod] public void Applicant_Payment_Submission_Is_Once_While_Pending_And_Proof_Is_Validated()
        {
            FakeWorkflowStore store; var svc = Build(ZoneStages.PaymentRequired, PaymentStatuses.PaymentRequired, out store);
            var invalid = PaymentSubmissionCommand("UTR111111", new DateTime(2026, 10, 2));
            invalid.PaymentProof = new DocumentUpload { FileName = "receipt.pdf", ContentType = "application/pdf", Content = new byte[] { 1, 2, 3, 4, 5 } };
            Assert.AreEqual("FILE_SIGNATURE", svc.Execute(invalid, Actors.Citizen()).Error.Code);
            Assert.IsTrue(svc.Execute(PaymentSubmissionCommand("UTR111111", new DateTime(2026, 10, 2)), Actors.Citizen()).IsSuccess);
            var saves = store.SaveCalls;
            Assert.AreEqual("PAYMENT_PRECONDITION", svc.Execute(PaymentSubmissionCommand("UTR222222", new DateTime(2026, 10, 2)), Actors.Citizen()).Error.Code);
            Assert.AreEqual(saves, store.SaveCalls); Assert.AreEqual("UTR111111", store.Current.RecordedPaymentReference);
        }
        [TestMethod] public void Payment_Submission_Rejects_Other_Verifier_Roles_And_AC_Cannot_Approve_Before_Payment()
        {
            FakeWorkflowStore store; var svc = Build(ZoneStages.PaymentRequired, PaymentStatuses.PaymentSubmitted, out store);
            Assert.AreEqual("WORKFLOW_FORBIDDEN", svc.Execute(Cmd(WorkflowActions.OSConfirmPayment, ZoneStages.PaymentRequired), Actors.JE()).Error.Code);
            Assert.AreEqual("WORKFLOW_FORBIDDEN", svc.Execute(Cmd(WorkflowActions.OSConfirmPayment, ZoneStages.PaymentRequired), Actors.AC()).Error.Code);
            Assert.AreEqual("WORKFLOW_FORBIDDEN", svc.Execute(Cmd(WorkflowActions.OSConfirmPayment, ZoneStages.PaymentRequired), Actors.Citizen()).Error.Code);
            Assert.AreEqual("WORKFLOW_INVALID_TRANSITION", svc.Execute(Cmd(WorkflowActions.ACApprove, ZoneStages.PaymentRequired, "मंजूर"), Actors.AC()).Error.Code);
        }
        [TestMethod] public void Payment_Verification_Rejects_Stale_RowVersion()
        {
            FakeWorkflowStore store; var svc = Build(ZoneStages.PaymentRequired, PaymentStatuses.PaymentSubmitted, out store);
            var stale = Cmd(WorkflowActions.OSConfirmPayment, ZoneStages.PaymentRequired); stale.ExpectedRowVersion = Convert.ToBase64String(BitConverter.GetBytes(88));
            Assert.AreEqual("STALE_STAGE", svc.Execute(stale, Actors.OS()).Error.Code); Assert.AreEqual(0, store.SaveCalls);
        }
        [TestMethod] public void OS_Rejection_Requires_Remark_and_Allows_Applicant_Resubmission()
        {
            FakeWorkflowStore store; var svc = Build(ZoneStages.PaymentRequired, PaymentStatuses.PaymentSubmitted, out store);
            Assert.AreEqual("REMARK_REQUIRED", svc.Execute(Cmd(WorkflowActions.OSRejectPayment, ZoneStages.PaymentRequired), Actors.OS()).Error.Code);
            Assert.IsTrue(svc.Execute(Cmd(WorkflowActions.OSRejectPayment, ZoneStages.PaymentRequired, "पावती अस्पष्ट आहे"), Actors.OS()).IsSuccess);
            Assert.AreEqual(PaymentStatuses.PaymentRejected, store.Current.PaymentStatus);
            Assert.AreEqual(ZoneStages.PaymentRequired, store.Current.Stage);
            Assert.IsTrue(svc.Execute(PaymentSubmissionCommand("UTR654321", new DateTime(2026, 10, 2)), Actors.Citizen()).IsSuccess);
            Assert.AreEqual(PaymentStatuses.PaymentSubmitted, store.Current.PaymentStatus);
        }
        [TestMethod] public void Failed_Payment_Save_Removes_Staged_Proof_File()
        {
            var store = new FakeWorkflowStore { Current = FakeWorkflowStore.Snapshot(ZoneStages.PaymentRequired, PaymentStatuses.PaymentRequired), SimulateConcurrentWriter = true };
            var files = new FakeFiles(); var svc = new ZoneWorkflowService(store, new FakeClock(), null, null, files);
            Assert.AreEqual("CONCURRENCY_CONFLICT", svc.Execute(PaymentSubmissionCommand("UTR777777", new DateTime(2026, 10, 2)), Actors.Citizen()).Error.Code);
            Assert.AreEqual(0, files.Files.Count);
        }
        [TestMethod] public void Finalized_Application_Cannot_Be_Modified()
        {
            FakeWorkflowStore store; var svc = Build(ZoneStages.Approved, PaymentStatuses.PaymentDone, out store);
            Assert.AreEqual("WORKFLOW_FINALIZED", svc.Execute(Cmd(WorkflowActions.ACReject, ZoneStages.Approved, "x"), Actors.AC()).Error.Code); Assert.AreEqual(0, store.SaveCalls);
        }
        [TestMethod] public void Return_For_Correction_Requires_Remark_And_Records_Correction_Kind()
        {
            FakeWorkflowStore store; var svc = Build(ZoneStages.JEPending, PaymentStatuses.PendingOSAmount, out store);
            Assert.AreEqual("REMARK_REQUIRED", svc.Execute(Cmd(WorkflowActions.JEReturnForCorrection, ZoneStages.JEPending), Actors.JE()).Error.Code);
            Assert.IsTrue(svc.Execute(Cmd(WorkflowActions.JEReturnForCorrection, ZoneStages.JEPending, "पत्ता दुरुस्त करा"), Actors.JE()).IsSuccess);
            Assert.AreEqual(RemarkKind.Correction, store.LastKind); Assert.AreEqual(ZoneStages.JECorrectionRequested, store.Current.Stage);
        }
        [TestMethod] public void Citizen_Can_Only_Resubmit_And_Writes_No_Audit_Row()
        {
            FakeWorkflowStore store; var svc = Build(ZoneStages.JECorrectionRequested, PaymentStatuses.PendingOSAmount, out store);
            Assert.AreEqual("WORKFLOW_INVALID_TRANSITION", svc.Execute(Cmd(WorkflowActions.JEVerifyForward, ZoneStages.JECorrectionRequested), Actors.Citizen()).Error.Code);
            FakeWorkflowStore s2; var svc2 = Build(ZoneStages.JEPending, PaymentStatuses.PendingOSAmount, out s2);
            Assert.AreEqual("WORKFLOW_FORBIDDEN", svc2.Execute(Cmd(WorkflowActions.JEVerifyForward, ZoneStages.JEPending), Actors.Citizen()).Error.Code);
            Assert.IsTrue(svc.Execute(Cmd(WorkflowActions.CitizenResubmit, ZoneStages.JECorrectionRequested, "दुरुस्त केले"), Actors.Citizen()).IsSuccess);
            Assert.AreEqual(ZoneStages.JEPending, store.Current.Stage); Assert.AreEqual(0, store.Audits.Count); // TR_AST_AuditLogs.UserId is NOT NULL
        }
        [TestMethod] public void Stale_RowVersion_From_An_Old_Page_Is_Rejected()
        {
            FakeWorkflowStore store; var svc = Build(ZoneStages.JEPending, PaymentStatuses.PendingOSAmount, out store);
            var stale = Cmd(WorkflowActions.JEVerifyForward, ZoneStages.JEPending); stale.ExpectedRowVersion = Convert.ToBase64String(BitConverter.GetBytes(99));
            Assert.AreEqual("STALE_STAGE", svc.Execute(stale, Actors.JE()).Error.Code); Assert.AreEqual(0, store.SaveCalls);
            var fresh = Cmd(WorkflowActions.JEVerifyForward, ZoneStages.JEPending); fresh.ExpectedRowVersion = Convert.ToBase64String(store.Current.RowVersion);
            Assert.IsTrue(svc.Execute(fresh, Actors.JE()).IsSuccess);
        }
        [TestMethod] public void Missing_Application_Returns_NotFound()
        { FakeWorkflowStore s; var svc = Build(ZoneStages.JEPending, PaymentStatuses.PendingOSAmount, out s); Assert.AreEqual("NOT_FOUND", svc.Execute(new WorkflowCommand { ApplicationId = 99, Action = WorkflowActions.JEVerifyForward, ExpectedStage = ZoneStages.JEPending }, Actors.JE()).Error.Code); }
    }
}
