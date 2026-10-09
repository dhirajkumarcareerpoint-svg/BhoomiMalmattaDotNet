using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Business.Citizen;
using SMC.BhoomiMalmatta.Business.Documents;
using SMC.BhoomiMalmatta.Business.Workflow;
using SMC.BhoomiMalmatta.Data.Repositories;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;
using SMC.BhoomiMalmatta.Tests.Support;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Tests
{
    internal sealed class ThrowingNotifier : INotifier { public int Calls; public void Notify(NotificationRequest r) { Calls++; throw new InvalidOperationException("sms gateway down"); } }
    internal sealed class RecordingLog : IErrorLog { public List<string> Areas = new List<string>(); public List<string> Correlations = new List<string>(); public void Log(string c, string a, Exception ex) { Correlations.Add(c); Areas.Add(a); } }
    internal sealed class CertPdf : IPdfGenerator
    {
        public bool Throw; public int CertificateCalls; public string LastApprovedBy;
        public byte[] Acknowledgement(ApplicationDetail d, PermitConfigRecord c) { return TestData.Pdf; }
        public byte[] SubmittedApplication(ApplicationDetail d, PermitConfigRecord c) { return TestData.Pdf; }
        public byte[] Receipt(ApplicationDetail d, PermitConfigRecord c) { return TestData.Pdf; }
        public byte[] Certificate(ApplicationDetail d, PermitConfigRecord c, List<string> t, string by)
        { CertificateCalls++; LastApprovedBy = by; if (Throw) throw new IOException("font missing"); return TestData.Pdf; }
    }

    /// <summary>OS "Verify payment" forwards to the AC in one transaction; the AC only approves (default policy); the approval always yields a letter.</summary>
    [TestClass]
    public class PaymentVerificationFlowTests
    {
        private static ZoneWorkflowService Svc(string stage, string pay, out FakeWorkflowStore store, INotifier notifier = null, WorkflowPolicy policy = null, IErrorLog log = null, ICertificateIssuer issuer = null)
        {
            store = new FakeWorkflowStore { Current = FakeWorkflowStore.Snapshot(stage, pay) };
            return new ZoneWorkflowService(store, new FakeClock(), notifier ?? new FakeNotifier(), issuer, new FakeFiles(), policy, log);
        }
        private static WorkflowCommand Cmd(string action, string stage, string remarks = null, string utr = null)
        { return new WorkflowCommand { ApplicationId = 1, Action = action, ExpectedStage = stage, Remarks = remarks, PaymentReference = utr }; }
        private static WorkflowCommand Verify() { return Cmd(WorkflowActions.OSConfirmPayment, ZoneStages.PaymentRequired); }

        [TestMethod]
        public void Verify_From_PaymentSubmitted_Moves_To_AC_With_Two_Chained_History_Entries_In_One_Save()
        {
            FakeWorkflowStore store; var svc = Svc(ZoneStages.PaymentRequired, PaymentStatuses.PaymentSubmitted, out store);
            var before = ActionHistory.Parse(store.Current.HistoryJson).Count;
            var r = svc.Execute(Verify(), Actors.OS());
            Assert.IsTrue(r.IsSuccess, r.IsFailure ? r.Error.Code : null);
            Assert.AreEqual(ZoneStages.AssistantCommissionerApprovalPending, r.Value.NewStage); Assert.AreEqual(ApplicationStatuses.ApprovalPending, r.Value.NewStatus);
            Assert.AreEqual(PaymentStatuses.PaymentDone, r.Value.NewPaymentStatus);
            Assert.AreEqual(1, store.SaveCalls, "payment done + forward must be one RowVersion-guarded save");
            Assert.AreEqual(ZoneStages.AssistantCommissionerApprovalPending, store.Current.Stage); Assert.AreEqual(PaymentStatuses.PaymentDone, store.Current.PaymentStatus);
            Assert.IsTrue(store.LastUpdate.ConfirmPayment, "verifier / confirmed-at are stored");
            Assert.AreEqual(ApplicationStatuses.ApprovalPending, store.LastUpdate.NewStatus);

            var h = ActionHistory.Parse(store.Current.HistoryJson);
            Assert.AreEqual(before + 2, h.Count); Assert.IsTrue(ActionHistory.VerifyChain(store.Current.HistoryJson));
            var paid = h[h.Count - 2]; var fwd = h[h.Count - 1];
            Assert.AreEqual(AuditActions.PaymentDone, paid.Action); Assert.AreEqual(AuditActions.ForwardedToAC, fwd.Action);
            Assert.AreEqual(ZoneStages.PaymentRequired, paid.PreviousStage); Assert.AreEqual(ZoneStages.OSPending, paid.NewStage);
            Assert.AreEqual(ZoneStages.OSPending, fwd.PreviousStage); Assert.AreEqual(ZoneStages.AssistantCommissionerApprovalPending, fwd.NewStage);
            Assert.AreEqual(Roles.OS, paid.FromRole); Assert.AreEqual(Roles.OS, fwd.FromRole); Assert.AreEqual(Roles.AssistantCommissioner, fwd.ToRole);
            Assert.AreEqual(paid.ActionAtUtc, fwd.ActionAtUtc); Assert.AreEqual(paid.Hash, fwd.PreviousHash);
            foreach (var e in new[] { paid, fwd }) { Assert.AreEqual("os.user", e.ActionByUserName); Assert.AreEqual("10.0.0.2", e.IpAddress); Assert.AreEqual("corr-2", e.CorrelationId); }

            CollectionAssert.AreEqual(new[] { AuditActions.PaymentDone, AuditActions.ForwardedToAC }, store.Audits.Select(a => a.Action).ToArray());
            CollectionAssert.AreEqual(new[] { ZoneStages.PaymentRequired, ZoneStages.OSPending }, store.Audits.Select(a => a.OldValue).ToArray());
            CollectionAssert.AreEqual(new[] { ZoneStages.OSPending, ZoneStages.AssistantCommissionerApprovalPending }, store.Audits.Select(a => a.NewValue).ToArray());
        }

        [DataTestMethod]
        [DataRow(PaymentStatuses.PaymentRequired)] [DataRow(PaymentStatuses.PaymentRejected)]
        public void Verify_Before_The_Applicant_Has_Submitted_Is_Rejected(string pay)
        {
            FakeWorkflowStore store; var svc = Svc(ZoneStages.PaymentRequired, pay, out store);
            Assert.AreEqual("PAYMENT_PRECONDITION", svc.Execute(Verify(), Actors.OS()).Error.Code); Assert.AreEqual(0, store.SaveCalls);
        }

        [DataTestMethod]
        [DataRow(Roles.JE)] [DataRow(Roles.AssistantCommissioner)] [DataRow(Roles.Citizen)] [DataRow(Roles.Staff)]
        public void Verify_By_Anyone_But_OS_Is_Rejected(string role)
        {
            FakeWorkflowStore store; var svc = Svc(ZoneStages.PaymentRequired, PaymentStatuses.PaymentSubmitted, out store);
            var actor = role == Roles.Citizen ? Actors.Citizen() : new ActorContext { UserId = 50, UserName = "x", Role = role };
            Assert.AreEqual("WORKFLOW_FORBIDDEN", svc.Execute(Verify(), actor).Error.Code); Assert.AreEqual(0, store.SaveCalls);
        }

        [TestMethod]
        public void Verify_Is_Zone_Scoped_And_Rejects_Stale_Stage_RowVersion_And_Concurrent_Writers()
        {
            FakeWorkflowStore store; var svc = Svc(ZoneStages.PaymentRequired, PaymentStatuses.PaymentSubmitted, out store);
            Assert.AreEqual("ZONE_FORBIDDEN", svc.Execute(Verify(), Actors.OS(4)).Error.Code);
            Assert.AreEqual("STALE_STAGE", svc.Execute(Cmd(WorkflowActions.OSConfirmPayment, ZoneStages.OSPending), Actors.OS()).Error.Code);
            var stale = Verify(); stale.ExpectedRowVersion = Convert.ToBase64String(BitConverter.GetBytes(77));
            Assert.AreEqual("STALE_STAGE", svc.Execute(stale, Actors.OS()).Error.Code);
            Assert.AreEqual(0, store.SaveCalls);
            store.SimulateConcurrentWriter = true;
            Assert.AreEqual("CONCURRENCY_CONFLICT", svc.Execute(Verify(), Actors.OS(3)).Error.Code);
            Assert.AreEqual(ZoneStages.PaymentRequired, store.Current.Stage); Assert.AreEqual(PaymentStatuses.PaymentSubmitted, store.Current.PaymentStatus);
            Assert.AreEqual(0, store.Audits.Count);
            store.SimulateConcurrentWriter = false;
            var fresh = Verify(); fresh.ExpectedRowVersion = Convert.ToBase64String(store.Current.RowVersion);
            Assert.IsTrue(svc.Execute(fresh, Actors.OS(3)).IsSuccess);
        }

        [TestMethod]
        public void Repeated_Verify_With_Same_Utr_Is_Idempotent_And_A_Different_Utr_Conflicts()
        {
            FakeWorkflowStore store; var svc = Svc(ZoneStages.PaymentRequired, PaymentStatuses.PaymentSubmitted, out store);
            Assert.IsTrue(svc.Execute(Verify(), Actors.OS()).IsSuccess);
            var saves = store.SaveCalls; var count = ActionHistory.Parse(store.Current.HistoryJson).Count;
            foreach (var utr in new[] { null, "UTR123456" })
            {
                var again = svc.Execute(Cmd(WorkflowActions.OSConfirmPayment, ZoneStages.PaymentRequired, utr: utr), Actors.OS());
                Assert.IsTrue(again.IsSuccess); Assert.IsTrue(again.Value.WasAlreadyApplied);
                Assert.AreEqual(ZoneStages.AssistantCommissionerApprovalPending, again.Value.NewStage);
            }
            Assert.AreEqual("PAYMENT_DUPLICATE", svc.Execute(Cmd(WorkflowActions.OSConfirmPayment, ZoneStages.PaymentRequired, utr: "UTR999999"), Actors.OS()).Error.Code);
            Assert.AreEqual(saves, store.SaveCalls); Assert.AreEqual(count, ActionHistory.Parse(store.Current.HistoryJson).Count);
        }

        [TestMethod]
        public void Sms_Failure_After_Commit_Does_Not_Affect_The_Transition_And_Is_Logged()
        {
            FakeWorkflowStore store; var sms = new ThrowingNotifier(); var log = new RecordingLog();
            var svc = Svc(ZoneStages.PaymentRequired, PaymentStatuses.PaymentSubmitted, out store, sms, null, log);
            var r = svc.Execute(Verify(), Actors.OS());
            Assert.IsTrue(r.IsSuccess); Assert.AreEqual(ZoneStages.AssistantCommissionerApprovalPending, store.Current.Stage);
            Assert.AreEqual(2, sms.Calls, "PaymentDone + AssistantCommissionerNotification were both attempted");
            Assert.AreEqual(2, log.Areas.Count); Assert.IsTrue(log.Correlations.All(c => c == "corr-2"));
        }

        [TestMethod]
        public void Verify_Queues_PaymentDone_And_AC_Notification_After_Commit()
        {
            FakeWorkflowStore store; var n = new FakeNotifier(); var svc = Svc(ZoneStages.PaymentRequired, PaymentStatuses.PaymentSubmitted, out store, n);
            Assert.IsTrue(svc.Execute(Verify(), Actors.OS()).IsSuccess);
            CollectionAssert.AreEqual(new[] { SmsEventTypes.PaymentDone, SmsEventTypes.AssistantCommissionerNotification }, n.Sent.Select(x => x.EventType).ToArray());
            Assert.AreEqual("9876543210", n.Sent[0].RecipientMobile); Assert.AreEqual(Roles.AssistantCommissioner, n.Sent[1].RecipientRole);
        }

        [DataTestMethod]
        [DataRow(ZoneStages.OSPending, PaymentStatuses.PendingOSAmount, "PAYMENT_PRECONDITION")]
        [DataRow(ZoneStages.OSPending, PaymentStatuses.PaymentRequired, "PAYMENT_PRECONDITION")]
        [DataRow(ZoneStages.OSPending, PaymentStatuses.PaymentSubmitted, "PAYMENT_PRECONDITION")]
        [DataRow(ZoneStages.OSPending, PaymentStatuses.PaymentRejected, "PAYMENT_PRECONDITION")]
        [DataRow(ZoneStages.PaymentRequired, PaymentStatuses.PaymentSubmitted, "WORKFLOW_INVALID_TRANSITION")]
        [DataRow(ZoneStages.PaymentRequired, PaymentStatuses.PaymentRequired, "WORKFLOW_INVALID_TRANSITION")]
        [DataRow(ZoneStages.JEPending, PaymentStatuses.PendingOSAmount, "WORKFLOW_INVALID_TRANSITION")]
        [DataRow(ZoneStages.AssistantCommissionerApprovalPending, PaymentStatuses.PaymentDone, "WORKFLOW_INVALID_TRANSITION")]
        [DataRow(ZoneStages.OSCorrectionRequested, PaymentStatuses.PaymentDone, "WORKFLOW_INVALID_TRANSITION")]
        [DataRow(ZoneStages.Approved, PaymentStatuses.PaymentDone, "WORKFLOW_FINALIZED")]
        public void Manual_Forward_To_AC_Is_Rejected_Outside_NotRequired_Or_Legacy_PaymentDone(string stage, string pay, string code)
        {
            FakeWorkflowStore store; var svc = Svc(stage, pay, out store);
            Assert.AreEqual(code, svc.Execute(Cmd(WorkflowActions.OSForwardToAC, stage), Actors.OS()).Error.Code);
            Assert.AreEqual(0, store.SaveCalls);
            Assert.IsFalse(ZoneWorkflowMachine.OfficerListActions(ProcessingRoutes.Zone, stage, Roles.OS, pay).Contains(WorkflowActions.OSForwardToAC));
        }

        [DataTestMethod]
        [DataRow(PaymentStatuses.NotRequired)] [DataRow(PaymentStatuses.PaymentDone)]
        public void Manual_Forward_To_AC_Remains_For_NotRequired_And_Legacy_PaymentDone(string pay)
        {
            FakeWorkflowStore store; var svc = Svc(ZoneStages.OSPending, pay, out store);
            var r = svc.Execute(Cmd(WorkflowActions.OSForwardToAC, ZoneStages.OSPending), Actors.OS());
            Assert.IsTrue(r.IsSuccess); Assert.AreEqual(ZoneStages.AssistantCommissionerApprovalPending, store.Current.Stage);
            Assert.AreEqual(AuditActions.ForwardedToAC, ActionHistory.Parse(store.Current.HistoryJson).Last().Action);
        }

        [DataTestMethod]
        [DataRow(WorkflowActions.ACReturn)] [DataRow(WorkflowActions.ACReject)]
        public void AC_Return_And_Reject_Are_Refused_Server_Side_By_Default_And_Allowed_When_Switched_On(string action)
        {
            FakeWorkflowStore store; var svc = Svc(ZoneStages.AssistantCommissionerApprovalPending, PaymentStatuses.PaymentDone, out store);
            var refused = svc.Execute(Cmd(action, ZoneStages.AssistantCommissionerApprovalPending, "शेरा"), Actors.AC());
            Assert.AreEqual("WORKFLOW_FORBIDDEN", refused.Error.Code); Assert.AreEqual(ZoneWorkflowMachine.AcActionDisabledMessage, refused.Error.Message);
            Assert.AreEqual(0, store.SaveCalls);

            FakeWorkflowStore open; var on = Svc(ZoneStages.AssistantCommissionerApprovalPending, PaymentStatuses.PaymentDone, out open, policy: new WorkflowPolicy(true));
            Assert.AreEqual("REMARK_REQUIRED", on.Execute(Cmd(action, ZoneStages.AssistantCommissionerApprovalPending), Actors.AC()).Error.Code);
            Assert.IsTrue(on.Execute(Cmd(action, ZoneStages.AssistantCommissionerApprovalPending, "शेरा"), Actors.AC()).IsSuccess);
            Assert.AreEqual(action == WorkflowActions.ACReturn ? ZoneStages.OSPending : ZoneStages.Rejected, open.Current.Stage);
        }

        [TestMethod]
        public void AC_Approve_Needs_No_Remark_But_Needs_Payment_And_The_AC_Role()
        {
            FakeWorkflowStore unpaid; var s1 = Svc(ZoneStages.AssistantCommissionerApprovalPending, PaymentStatuses.PaymentRequired, out unpaid);
            Assert.AreEqual("PAYMENT_PRECONDITION", s1.Execute(Cmd(WorkflowActions.ACApprove, ZoneStages.AssistantCommissionerApprovalPending), Actors.AC()).Error.Code);
            foreach (var actor in new[] { Actors.OS(), Actors.JE() })
            {
                FakeWorkflowStore s; var svc = Svc(ZoneStages.AssistantCommissionerApprovalPending, PaymentStatuses.PaymentDone, out s);
                Assert.AreEqual("WORKFLOW_FORBIDDEN", svc.Execute(Cmd(WorkflowActions.ACApprove, ZoneStages.AssistantCommissionerApprovalPending), actor).Error.Code);
            }
            foreach (var pay in new[] { PaymentStatuses.PaymentDone, PaymentStatuses.NotRequired })
            {
                FakeWorkflowStore store; var svc = Svc(ZoneStages.AssistantCommissionerApprovalPending, pay, out store);
                var r = svc.Execute(Cmd(WorkflowActions.ACApprove, ZoneStages.AssistantCommissionerApprovalPending, "  "), Actors.AC());
                Assert.IsTrue(r.IsSuccess); Assert.AreEqual(ZoneStages.Approved, store.Current.Stage); Assert.AreEqual(ApplicationStatuses.Approved, r.Value.NewStatus);
                Assert.AreEqual(RemarkKind.Approval, store.LastKind, "ApprovedAt / ApprovedBy are stored as before");
                var last = ActionHistory.Parse(store.Current.HistoryJson).Last();
                Assert.AreEqual(AuditActions.FinalApproved, last.Action); Assert.IsNull(last.Remarks);
            }
        }

        [TestMethod]
        public void Approval_Generates_And_Stores_The_Letter_And_Queues_Sms()
        {
            var q = new DocumentAndCertificateTests.FakeQuery { Detail = ApprovedDetail() }; var files = new FakeFiles(); var pdf = new CertPdf(); var n = new FakeNotifier();
            var store = new FakeWorkflowStore { Current = FakeWorkflowStore.Snapshot(ZoneStages.AssistantCommissionerApprovalPending, PaymentStatuses.PaymentDone) };
            var certs = new CertificateService(q, new FakeMasters(), pdf, files, store, n, new FakeAudit(), new FakeClock());
            var svc = new ZoneWorkflowService(store, new FakeClock(), n, certs, files, null, new RecordingLog());
            Assert.IsTrue(svc.Execute(Cmd(WorkflowActions.ACApprove, ZoneStages.AssistantCommissionerApprovalPending), Actors.AC()).IsSuccess);
            Assert.AreEqual(ZoneStages.Approved, store.Current.Stage);
            Assert.AreEqual("certificates/2026-0005/certificate.pdf", store.CertificatePath); Assert.IsTrue(files.Exists(store.CertificatePath));
            Assert.IsFalse(Path.IsPathRooted(store.CertificatePath)); Assert.IsFalse(store.CertificatePath.Contains(".."));
            CollectionAssert.AreEqual(new[] { SmsEventTypes.ApplicationApproved, SmsEventTypes.CertificateAvailable }, n.Sent.Select(x => x.EventType).ToArray());
        }

        [TestMethod]
        public void Letter_Generation_Failure_Keeps_The_Approval_And_The_First_Download_Generates_It()
        {
            var q = new DocumentAndCertificateTests.FakeQuery { Detail = ApprovedDetail() }; var files = new FakeFiles(); var pdf = new CertPdf { Throw = true }; var log = new RecordingLog();
            var store = new FakeWorkflowStore { Current = FakeWorkflowStore.Snapshot(ZoneStages.AssistantCommissionerApprovalPending, PaymentStatuses.PaymentDone) };
            var certs = new CertificateService(q, new FakeMasters(), pdf, files, store, new FakeNotifier(), new FakeAudit(), new FakeClock());
            var svc = new ZoneWorkflowService(store, new FakeClock(), new FakeNotifier(), certs, files, null, log);
            var r = svc.Execute(Cmd(WorkflowActions.ACApprove, ZoneStages.AssistantCommissionerApprovalPending), Actors.AC());
            Assert.IsTrue(r.IsSuccess); Assert.AreEqual(ZoneStages.Approved, store.Current.Stage);
            Assert.IsNull(store.CertificatePath); CollectionAssert.Contains(log.Areas, "CertificateGeneration"); CollectionAssert.Contains(log.Correlations, "corr-3");

            pdf.Throw = false;
            var download = certs.OpenForDownload(1, Roles.Citizen, 1, Actors.Citizen());
            Assert.IsTrue(download.IsSuccess); Assert.AreEqual("application/pdf", download.Value.ContentType);
            CollectionAssert.AreEqual(TestData.Pdf, download.Value.Content);
            Assert.AreEqual("certificates/2026-0005/certificate.pdf", store.CertificatePath); Assert.AreEqual("ac.user", pdf.LastApprovedBy, "the approver, not the downloader, is printed");
        }

        [TestMethod]
        public void Letter_Download_Requires_The_Citizen_Ticket_Of_That_Application_Or_An_Officer_Role()
        {
            var q = new DocumentAndCertificateTests.FakeQuery { Detail = ApprovedDetail() }; var files = new FakeFiles(); var store = new FakeWorkflowStore();
            q.Detail.CertificatePath = "certificates/2026-0005/certificate.pdf"; files.Files[q.Detail.CertificatePath] = TestData.Pdf;
            var certs = new CertificateService(q, new FakeMasters(), new CertPdf(), files, store, new FakeNotifier(), new FakeAudit(), new FakeClock());
            Assert.IsTrue(certs.OpenForDownload(1, Roles.Citizen, 1, Actors.Citizen()).IsSuccess);
            Assert.AreEqual("FORBIDDEN", certs.OpenForDownload(1, Roles.Citizen, 2, Actors.Citizen()).Error.Code);
            Assert.AreEqual("FORBIDDEN", certs.OpenForDownload(1, Roles.Citizen, null, Actors.Citizen()).Error.Code);
            Assert.AreEqual("FORBIDDEN", certs.OpenForDownload(1, "Anonymous", null, Actors.Citizen()).Error.Code);
            foreach (var role in new[] { Roles.JE, Roles.OS, Roles.AssistantCommissioner, Roles.Admin, Roles.Staff })
                Assert.IsTrue(certs.OpenForDownload(1, role, null, Actors.AC()).IsSuccess, role);
            q.Detail.Stage = ZoneStages.AssistantCommissionerApprovalPending;
            Assert.AreEqual("NOT_APPROVED", certs.OpenForDownload(1, Roles.Citizen, 1, Actors.Citizen()).Error.Code);
            var handler = File.ReadAllText(Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Web", "Download.ashx.cs"));
            StringAssert.Contains(handler, "AppServices.Certificates.OpenForDownload(appId, me.Role, me.IsCitizen ? (int?)me.ApplicationId : null, actor)");
            StringAssert.Contains(handler, "(inline ? \"inline\" : \"attachment\")");
        }

        private static ApplicationDetail ApprovedDetail()
        {
            return new ApplicationDetail { Id = 1, ApplicationNumber = "2026-0005", ProcessingRoute = ProcessingRoutes.Zone, Stage = ZoneStages.Approved, Status = ApplicationStatuses.Approved,
                PaymentStatus = PaymentStatuses.PaymentDone, Mobile = "9876543210", ApplicantName = "A", ApprovedBy = "ac.user" };
        }
    }

    [TestClass]
    public class OfficerOptionsAndQueryTests
    {
        [DataTestMethod]
        [DataRow(ZoneStages.PaymentRequired, PaymentStatuses.PaymentSubmitted, "OSConfirmPayment,OSRejectPayment")]
        [DataRow(ZoneStages.PaymentRequired, PaymentStatuses.PaymentRequired, "")]
        [DataRow(ZoneStages.PaymentRequired, PaymentStatuses.PaymentRejected, "")]
        [DataRow(ZoneStages.OSPending, PaymentStatuses.NotRequired, "OSForwardToAC,OSReturnForCorrection,OSReject")]
        [DataRow(ZoneStages.OSPending, PaymentStatuses.PaymentDone, "OSForwardToAC,OSReturnForCorrection,OSReject")]
        [DataRow(ZoneStages.OSPending, PaymentStatuses.PendingOSAmount, "OSRequestPayment,OSReturnForCorrection,OSReject")]
        public void OS_Options_Per_State(string stage, string pay, string expected)
        {
            var actual = ZoneWorkflowMachine.OfficerListActions(ProcessingRoutes.Zone, stage, Roles.OS, pay).OrderBy(x => x).ToArray();
            CollectionAssert.AreEqual(expected.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).OrderBy(x => x).ToArray(), actual);
        }

        [TestMethod]
        public void Officer_Modal_And_Row_Texts_Follow_The_New_Flow()
        {
            var submitted = Detail(ZoneStages.PaymentRequired, PaymentStatuses.PaymentSubmitted);
            submitted.PaymentReference = "UTR123456"; submitted.PaymentDate = new DateTime(2026, 10, 2);
            submitted.Documents.Add(new DocumentItem { Id = 5, DocumentType = "PaymentProof", FileName = "proof.pdf", ContentType = "application/pdf", IsZoneDocument = true });
            var os = OfficerApplicationViewMapper.Build(submitted, Roles.OS, "/smc");
            CollectionAssert.AreEqual(new[] { "Verify payment", "Reject payment" }, os.AllowedActionLabels);
            Assert.AreEqual(OfficerText.PaymentVerificationPending, os.StageLabel); Assert.AreEqual("UTR123456", os.Payment.Reference);
            Assert.AreEqual(OfficerText.PreviewProof, os.Payment.ProofPreviewLabel); StringAssert.StartsWith(os.Payment.ProofPreviewUrl, "/smc/Download.ashx?type=zdoc&id=5");
            Assert.IsNull(os.CertificateUrl);

            var unpaid = OfficerApplicationViewMapper.Build(Detail(ZoneStages.PaymentRequired, PaymentStatuses.PaymentRequired), Roles.OS, "");
            Assert.AreEqual(0, unpaid.AllowedActionLabels.Count); Assert.AreEqual(OfficerText.AwaitingApplicantPayment, unpaid.StageLabel);

            var ac = Detail(ZoneStages.AssistantCommissionerApprovalPending, PaymentStatuses.PaymentDone);
            CollectionAssert.AreEqual(new[] { "Approve" }, OfficerApplicationViewMapper.Build(ac, Roles.AssistantCommissioner, "").AllowedActionLabels);
            Assert.AreEqual(3, OfficerApplicationViewMapper.Build(ac, Roles.AssistantCommissioner, "", new WorkflowPolicy(true)).AllowedActionLabels.Count);

            var approved = OfficerApplicationViewMapper.Build(Detail(ZoneStages.Approved, PaymentStatuses.PaymentDone), Roles.JE, "/smc");
            Assert.AreEqual("/smc/Download.ashx?type=cert&appId=7", approved.CertificateUrl); Assert.AreEqual(OfficerText.CertificateLink, approved.CertificateLabel);

            Assert.AreEqual("Are you sure you want to approve application 20260007?", OfficerText.Confirmation(WorkflowActions.ACApprove, "20260007"));
            Assert.AreEqual("Application 20260007 approved. Permission letter is available to the applicant.", OfficerText.Success(WorkflowActions.ACApprove, "20260007"));
            Assert.AreEqual("Payment verified. Application forwarded to Assistant Commissioner.", OfficerText.Success(WorkflowActions.OSConfirmPayment, "20260007"));
        }

        [TestMethod]
        public void AC_Row_Renders_A_Single_Approve_Button_With_Optional_Remark_And_No_Choice_Group()
        {
            var web = Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Web");
            var grid = File.ReadAllText(Path.Combine(web, "Department", "Controls", "OfficerPendingGrid.ascx"));
            var page = File.ReadAllText(Path.Combine(web, "Department", "Applications.aspx.cs"));
            var js = File.ReadAllText(Path.Combine(web, "Scripts", "site.js"));
            StringAssert.Contains(grid, "ID=\"phDecisionChoices\""); StringAssert.Contains(grid, "class=\"officer-add-remark\"");
            StringAssert.Contains(page, "choicePanel.Visible = false"); StringAssert.Contains(page, "button.Attributes[\"data-single-action\"] = WorkflowActions.ACApprove");
            StringAssert.Contains(page, "button.Attributes[\"data-messages\"]"); StringAssert.Contains(page, "AppServices.WorkflowPolicy");
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(js, @"requiresRemark = function \(action\) \{ return /[^/]*ACApprove"), "Approve remark must be optional");
            StringAssert.Contains(js, "data-single-action"); StringAssert.Contains(js, "messages.success");
            StringAssert.Contains(js, "row.parentNode.removeChild(row)"); StringAssert.Contains(js, "setCounts(counts)");
            var config = File.ReadAllText(Path.Combine(web, "Web.config"));
            StringAssert.Contains(config, "<add key=\"Workflow.AcAllowReturnReject\" value=\"false\" />");
        }

        [TestMethod]
        public void Pending_Predicates_Include_Only_Actionable_Rows_And_OS_Kpi_Counts_Submitted_Payments()
        {
            var rows = new List<QueryService.AppRow>
            {
                Row(1, ZoneStages.OSPending, PaymentStatuses.PendingOSAmount), Row(2, ZoneStages.PaymentRequired, PaymentStatuses.PaymentSubmitted),
                Row(3, ZoneStages.PaymentRequired, PaymentStatuses.PaymentRequired), Row(4, ZoneStages.PaymentRequired, PaymentStatuses.PaymentRejected),
                Row(5, ZoneStages.AssistantCommissionerApprovalPending, PaymentStatuses.PaymentDone), Row(6, ZoneStages.AssistantCommissionerApprovalPending, PaymentStatuses.NotRequired),
                Row(7, ZoneStages.AssistantCommissionerApprovalPending, PaymentStatuses.PaymentRequired), Row(8, ZoneStages.Approved, PaymentStatuses.PaymentDone),
                Row(9, ZoneStages.PaymentRequired, PaymentStatuses.PaymentSubmitted, ProcessingRoutes.Legacy)
            };
            CollectionAssert.AreEqual(new[] { 1, 2 }, QueryService.WherePendingFor(rows.AsQueryable(), Roles.OS).Select(x => x.Id).ToArray());
            CollectionAssert.AreEqual(new[] { 5, 6 }, QueryService.WherePendingFor(rows.AsQueryable(), Roles.AssistantCommissioner).Select(x => x.Id).ToArray());
            Assert.AreEqual(0, QueryService.WherePendingFor(rows.AsQueryable(), Roles.Staff).Count());
            var osKpi = QueryService.BuildOfficerKpiAggregateQuery(QueryService.BuildOfficerKpiFlags(rows.AsQueryable(), QueryService.OfficerPendingPredicate(Roles.OS))).First();
            Assert.AreEqual(2, osKpi.Pending); Assert.AreEqual(9, osKpi.Total);
        }

        private static QueryService.AppRow Row(int id, string stage, string pay, string route = ProcessingRoutes.Zone)
        { return new QueryService.AppRow { Id = id, Stage = stage, PaymentStatus = pay, Route = route, Status = ZoneWorkflowMachine.StatusFor(stage) }; }

        private static ApplicationDetail Detail(string stage, string pay)
        {
            return new ApplicationDetail { Id = 7, ApplicationNumber = "20260007", ProcessingRoute = ProcessingRoutes.Zone, Stage = stage, Status = ZoneWorkflowMachine.StatusFor(stage), PaymentStatus = pay,
                StartDate = new DateTime(2026, 10, 6), EndDate = new DateTime(2026, 10, 8) };
        }
    }

    [TestClass]
    public class ApplicantFlowViewTests
    {
        private static ApplicationDetail Detail(string stage, string pay)
        {
            return new ApplicationDetail { Id = 1, ApplicationNumber = "20260005", ProcessingRoute = ProcessingRoutes.Zone, Stage = stage, Status = ZoneWorkflowMachine.StatusFor(stage), PaymentStatus = pay,
                PayableAmount = 250m, PaymentReference = "UTR123456", PaymentDate = new DateTime(2026, 10, 2), StartDate = new DateTime(2026, 10, 6), EndDate = new DateTime(2027, 10, 5) };
        }
        private static string Dump(CitizenStatusView v) { return new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(v); }

        [TestMethod]
        public void Payment_Submitted_Shows_Under_Verification_With_Utr_Date_And_Proof()
        {
            var d = Detail(ZoneStages.PaymentRequired, PaymentStatuses.PaymentSubmitted);
            d.Documents.Add(new DocumentItem { IsZoneDocument = true, DocumentType = "PaymentProof", FileName = "receipt.pdf", CreatedAt = DateTime.UtcNow });
            var v = CitizenStatusMapper.MapDetail(d);
            Assert.AreEqual(ApplicantText.PaymentSubmitted, v.Levels[1].Status); Assert.AreEqual("Payment Submitted / Under Verification", v.Levels[1].Status);
            Assert.AreEqual("UTR123456", v.Payment.Reference); Assert.AreEqual(new DateTime(2026, 10, 2), v.Payment.PaymentDate); Assert.AreEqual("receipt.pdf", v.Payment.ProofFileName);
            Assert.IsFalse(v.Payment.ShowForm);
        }

        [TestMethod]
        public void After_Verification_L2_Is_Done_And_L3_Is_Pending_Immediately()
        {
            var v = CitizenStatusMapper.MapDetail(Detail(ZoneStages.AssistantCommissionerApprovalPending, PaymentStatuses.PaymentDone));
            Assert.AreEqual("Approved / Payment Done", v.Levels[1].Status); Assert.AreEqual("Completed", v.Levels[1].State);
            Assert.AreEqual("Payment Done", v.PaymentStatus); Assert.AreEqual("Assistant Commissioner Approval", v.Levels[1].NextStage);
            Assert.AreEqual(ApplicantText.Pending, v.Levels[2].Status); Assert.AreEqual("Active", v.Levels[2].State);
            Assert.IsFalse(Dump(v).Contains("Awaiting forwarding"));
            Assert.IsFalse(v.Certificate.IsApproved);
        }

        [TestMethod]
        public void Approved_Shows_Status_And_Download_Button_Or_Preparing_With_Refresh_Hint()
        {
            var ready = Detail(ZoneStages.Approved, PaymentStatuses.PaymentDone); ready.CertificatePath = "certificates/20260005/certificate.pdf";
            var v = CitizenStatusMapper.MapDetail(ready);
            Assert.AreEqual(ApplicantText.Approved, v.Levels[2].Status); Assert.AreEqual("Completed", v.Levels[2].State);
            Assert.AreEqual("Application Status: Approved", v.Certificate.StatusLine);
            Assert.IsTrue(v.Certificate.HasDownload); Assert.AreEqual("मंजुरी पत्र डाउनलोड करा", v.Certificate.DownloadLabel); Assert.IsNull(v.Certificate.Hint);
            Assert.IsFalse(Dump(v).Contains("certificates/"), "storage paths never reach the applicant");

            var pending = CitizenStatusMapper.MapDetail(Detail(ZoneStages.Approved, PaymentStatuses.PaymentDone));
            Assert.IsFalse(pending.Certificate.HasDownload); Assert.AreEqual("मंजुरी पत्र तयार होत आहे", pending.Certificate.Text);
            Assert.AreEqual(ApplicantText.CertificateRefreshHint, pending.Certificate.Hint);

            var page = File.ReadAllText(Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Web", "Citizen", "Dashboard.aspx.cs"));
            StringAssert.Contains(page, "~/Download.ashx?type=cert"); StringAssert.Contains(page, "v.Certificate.DownloadLabel"); StringAssert.Contains(page, "EnsureCertificate();");
        }

        [TestMethod]
        public void History_Shows_Payment_Done_And_Forwarded_To_AC_Without_Officer_Identity()
        {
            var store = new FakeWorkflowStore { Current = FakeWorkflowStore.Snapshot(ZoneStages.PaymentRequired, PaymentStatuses.PaymentSubmitted) };
            var svc = new ZoneWorkflowService(store, new FakeClock());
            Assert.IsTrue(svc.Execute(new WorkflowCommand { ApplicationId = 1, Action = WorkflowActions.OSConfirmPayment, ExpectedStage = ZoneStages.PaymentRequired }, Actors.OS()).IsSuccess);
            var v = CitizenStatusMapper.Map("2026-0005", store.Current.Stage, store.Current.PaymentStatus, null, store.Current.HistoryJson);
            var events = v.Timeline.Select(t => t.Event).ToList();
            CollectionAssert.IsSubsetOf(new[] { ApplicantText.PaymentDoneEvent, ApplicantText.ForwardedAcEvent }, events);
            Assert.IsTrue(events.IndexOf(ApplicantText.PaymentDoneEvent) < events.IndexOf(ApplicantText.ForwardedAcEvent));
            var dump = Dump(v);
            foreach (var secret in new[] { "os.user", "10.0.0.2", "corr-2" }) Assert.IsFalse(dump.Contains(secret), secret);
            Assert.AreEqual(ApplicantText.Pending, v.Levels[2].Status);
        }
    }
}
