using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Business.Citizen;
using SMC.BhoomiMalmatta.Business.Workflow;
using SMC.BhoomiMalmatta.Data.Repositories;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;
using SMC.BhoomiMalmatta.Models.Entities;
using SMC.BhoomiMalmatta.Tests.Support;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Tests
{
    /// <summary>
    /// Records every database operation ZoneWorkflowStore.Persist issues and models the filtered unique index
    /// IX_TR_CFC_DemandApplicationZoneDocuments_DemandApplicationZoneWorkflowId_Kind (one active row per workflow + Kind).
    /// Worst case on purpose: inside one SaveChanges the INSERTs run before the UPDATEs, so a soft delete that is not flushed first fails.
    /// This is a model of SQL Server, not SQL Server: the real index was not exercised by these tests.
    /// </summary>
    internal sealed class RecordingWorkflowPersistence : IZoneWorkflowPersistence
    {
        public readonly List<string> Log = new List<string>();
        public DemandApplicationZoneWorkflow Workflow; public DemandApplication Application;
        public readonly List<DemandApplicationZoneDocument> Rows = new List<DemandApplicationZoneDocument>();
        private readonly List<DemandApplicationZoneDocument> _pendingInserts = new List<DemandApplicationZoneDocument>();
        private readonly Dictionary<DemandApplicationZoneDocument, bool> _storedIsDeleted = new Dictionary<DemandApplicationZoneDocument, bool>();
        private List<Tuple<DemandApplicationZoneDocument, bool, DateTime?, string>> _atBegin;
        public int FailOnSaveNumber; private int _saves;
        public bool Committed, RolledBack;

        public RecordingWorkflowPersistence(int workflowId, string paymentStatus, params DemandApplicationZoneDocument[] existing)
        {
            Workflow = new DemandApplicationZoneWorkflow { Id = workflowId, DemandApplicationId = 1, Zone = 3, ZoneStage = ZoneStages.PaymentRequired, PaymentStatus = paymentStatus, ActionHistoryJson = "[]", RowVersion = BitConverter.GetBytes(1) };
            Application = new DemandApplication { Id = 1, ApplicationNumber = "2026-0005", ProcessingRoute = ProcessingRoutes.Zone, PaymentStatus = paymentStatus, Status = ApplicationStatuses.FeePending };
            foreach (var row in existing) { Rows.Add(row); _storedIsDeleted[row] = row.IsDeleted; }
            _atBegin = Rows.Select(r => Tuple.Create(r, r.IsDeleted, r.DeletedAt, r.DeletedBy)).ToList();
        }

        public DemandApplicationZoneWorkflow FindWorkflow(int applicationId) { Log.Add("FindWorkflow"); return Workflow; }
        public DemandApplication FindApplication(int applicationId) { Log.Add("FindApplication"); return Application; }
        public bool IsPaymentReferenceUsedByOther(int applicationId, string reference) { Log.Add("IsPaymentReferenceUsedByOther"); return false; }
        public void ExpectRowVersion(DemandApplicationZoneWorkflow workflow, byte[] rowVersion) { Log.Add("ExpectRowVersion"); }
        public int SoftDeleteActiveZoneDocuments(int workflowId, string kind, DateTime atUtc, string deletedBy)
        {
            Log.Add("SoftDelete:" + kind);
            var rows = Rows.Where(r => r.DemandApplicationZoneWorkflowId == workflowId && r.Kind == kind && !r.IsDeleted).ToList();
            foreach (var r in rows) { r.IsDeleted = true; r.DeletedAt = atUtc; r.DeletedBy = deletedBy; }
            return rows.Count;
        }
        public void AddZoneDocument(DemandApplicationZoneDocument row) { Log.Add("Insert:" + row.Kind); _pendingInserts.Add(row); }
        public void AddAudit(AuditLog row) { Log.Add("Audit"); }
        public void SaveChanges()
        {
            Log.Add("SaveChanges");
            if (++_saves == FailOnSaveNumber) throw new DbUpdateException("simulated failure in SaveChanges #" + _saves);
            foreach (var insert in _pendingInserts) // INSERTs first (worst case), checked against what is stored so far
                if (Rows.Any(r => !_storedIsDeleted[r] && r.DemandApplicationZoneWorkflowId == insert.DemandApplicationZoneWorkflowId && r.Kind == insert.Kind))
                    throw new DbUpdateException("Cannot insert duplicate key row in object 'dbo.TR_CFC_DemandApplicationZoneDocuments' with unique index 'IX_TR_CFC_DemandApplicationZoneDocuments_DemandApplicationZoneWorkflowId_Kind'.");
            foreach (var insert in _pendingInserts) { Rows.Add(insert); _storedIsDeleted[insert] = insert.IsDeleted; }
            _pendingInserts.Clear();
            foreach (var r in Rows) _storedIsDeleted[r] = r.IsDeleted; // then the UPDATEs
        }
        public void Commit() { Log.Add("Commit"); Committed = true; }
        public void Rollback()
        {
            Log.Add("Rollback"); RolledBack = true;
            Rows.RemoveAll(r => _atBegin.All(b => !ReferenceEquals(b.Item1, r)));
            foreach (var b in _atBegin) { b.Item1.IsDeleted = b.Item2; b.Item1.DeletedAt = b.Item3; b.Item1.DeletedBy = b.Item4; _storedIsDeleted[b.Item1] = b.Item2; }
            _pendingInserts.Clear();
        }
        public IEnumerable<DemandApplicationZoneDocument> Active { get { return Rows.Where(r => !r.IsDeleted); } }
    }

    [TestClass]
    public class PaymentProofResubmissionTests
    {
        private static readonly DateTime At = new DateTime(2026, 10, 3, 5, 0, 0, DateTimeKind.Utc);

        private static DemandApplicationZoneDocument OldProof() { return new DemandApplicationZoneDocument { Id = 70, DemandApplicationZoneWorkflowId = 7, Kind = "PaymentProof", FileName = "first.pdf", FilePath = "demand/payment/1/a.pdf", ContentType = "application/pdf", SizeBytes = 6, CreatedBy = "citizen", CreatedAt = new DateTime(2026, 10, 2) }; }

        private static ZoneWorkflowUpdate Update(bool replace)
        {
            return new ZoneWorkflowUpdate
            {
                Before = new ZoneWorkflowSnapshot { ApplicationId = 1, Stage = ZoneStages.PaymentRequired, PaymentStatus = replace ? PaymentStatuses.PaymentRejected : PaymentStatuses.PaymentRequired, RowVersion = BitConverter.GetBytes(1) },
                NewStage = ZoneStages.PaymentRequired, NewStatus = ApplicationStatuses.FeePending, NewPaymentStatus = PaymentStatuses.PaymentSubmitted, NewPayableAmount = 250m, NewHistoryJson = "[]",
                PaymentReference = "UTR654321", PaymentDate = new DateTime(2026, 10, 3),
                PaymentProof = new StoredDocument { DocumentType = "PaymentProof", OriginalFileName = "second.pdf", StoredFileName = "b.pdf", RelativePath = "demand/payment/1/b.pdf", ContentType = "application/pdf", SizeBytes = 6 },
                Actor = Actors.Citizen(), AtUtc = At, ReplaceActivePaymentProof = replace, Audits = new List<AuditEntry>()
            };
        }

        [TestMethod]
        public void Resubmission_Soft_Deletes_The_Old_Proof_Flushes_Then_Inserts_The_New_One_In_One_Transaction()
        {
            var old = OldProof();
            var p = new RecordingWorkflowPersistence(7, PaymentStatuses.PaymentRejected, old);
            var r = ZoneWorkflowStore.Persist(p, Update(true));
            Assert.IsTrue(r.IsSuccess, r.IsFailure ? r.Error.Code : null);
            var writes = p.Log.Where(x => x.StartsWith("SoftDelete") || x.StartsWith("Insert") || x == "SaveChanges" || x == "Commit" || x == "Rollback").ToList();
            CollectionAssert.AreEqual(new[] { "SoftDelete:PaymentProof", "SaveChanges", "Insert:PaymentProof", "SaveChanges", "Commit" }, writes, string.Join(" > ", p.Log));
            Assert.AreEqual(1, p.Log.Count(x => x == "Commit"), "one transaction: exactly one commit, after both steps");
            Assert.IsFalse(p.RolledBack);
            // The old row is kept (never hard-deleted) and marked deleted by the citizen at the submission time.
            Assert.IsTrue(p.Rows.Contains(old)); Assert.IsTrue(old.IsDeleted); Assert.AreEqual(At, old.DeletedAt); Assert.AreEqual("Citizen", old.DeletedBy);
            Assert.AreEqual(ZoneWorkflowStore.ReplacedProofDeletedBy, old.DeletedBy);
            var active = p.Active.ToList();
            Assert.AreEqual(1, active.Count); Assert.AreEqual("second.pdf", active[0].FileName); Assert.AreEqual(7, active[0].DemandApplicationZoneWorkflowId);
            Assert.AreEqual(2, p.Rows.Count);
            Assert.AreEqual("UTR654321", p.Workflow.ReceiptNumber); Assert.AreEqual(PaymentStatuses.PaymentSubmitted, p.Workflow.PaymentStatus);
        }

        [TestMethod]
        public void The_Recording_Fake_Enforces_The_Filtered_Unique_Index_So_The_Ordering_Test_Is_Meaningful()
        {
            var p = new RecordingWorkflowPersistence(7, PaymentStatuses.PaymentRejected, OldProof());
            p.AddZoneDocument(new DemandApplicationZoneDocument { DemandApplicationZoneWorkflowId = 7, Kind = "PaymentProof", FileName = "x.pdf" });
            Assert.ThrowsException<DbUpdateException>(() => p.SaveChanges(), "a second active PaymentProof row without a prior soft delete violates the index");
            // Soft delete in the same SaveChanges is not enough either (the fake runs INSERTs first, like an unlucky EF command order).
            var q = new RecordingWorkflowPersistence(7, PaymentStatuses.PaymentRejected, OldProof());
            q.SoftDeleteActiveZoneDocuments(7, "PaymentProof", At, "Citizen");
            q.AddZoneDocument(new DemandApplicationZoneDocument { DemandApplicationZoneWorkflowId = 7, Kind = "PaymentProof", FileName = "x.pdf" });
            Assert.ThrowsException<DbUpdateException>(() => q.SaveChanges());
        }

        [TestMethod]
        public void First_Submission_Inserts_Only()
        {
            var p = new RecordingWorkflowPersistence(7, PaymentStatuses.PaymentRequired);
            var r = ZoneWorkflowStore.Persist(p, Update(false));
            Assert.IsTrue(r.IsSuccess);
            Assert.IsFalse(p.Log.Any(x => x.StartsWith("SoftDelete")), string.Join(" > ", p.Log));
            CollectionAssert.AreEqual(new[] { "Insert:PaymentProof", "SaveChanges", "Commit" }, p.Log.Where(x => x.StartsWith("Insert") || x == "SaveChanges" || x == "Commit").ToList());
            Assert.AreEqual(1, p.Active.Count());
        }

        [TestMethod]
        public void A_Failure_After_The_Soft_Delete_Rolls_It_Back_And_Inserts_Nothing()
        {
            var old = OldProof();
            var p = new RecordingWorkflowPersistence(7, PaymentStatuses.PaymentRejected, old) { FailOnSaveNumber = 2 }; // the SaveChanges that carries the INSERT
            var r = ZoneWorkflowStore.Persist(p, Update(true));
            Assert.IsTrue(r.IsFailure); Assert.AreEqual("DB_ERROR", r.Error.Code);
            Assert.IsTrue(p.RolledBack); Assert.IsFalse(p.Committed);
            Assert.AreEqual("Rollback", p.Log.Last());
            Assert.IsFalse(old.IsDeleted); Assert.IsNull(old.DeletedAt); Assert.IsNull(old.DeletedBy);
            Assert.AreEqual(1, p.Rows.Count); CollectionAssert.AreEqual(new[] { old }, p.Active.ToList());
        }

        [TestMethod]
        public void A_Failing_Soft_Delete_Flush_Rolls_Back_Before_Any_Insert()
        {
            var old = OldProof();
            var p = new RecordingWorkflowPersistence(7, PaymentStatuses.PaymentRejected, old) { FailOnSaveNumber = 1 };
            var r = ZoneWorkflowStore.Persist(p, Update(true));
            Assert.AreEqual("DB_ERROR", r.Error.Code);
            Assert.IsFalse(p.Log.Any(x => x.StartsWith("Insert"))); Assert.IsFalse(old.IsDeleted); Assert.IsFalse(p.Committed);
        }

        // ------------------------------------------------ workflow service: when to replace, and history ------------------------------------------------

        private static WorkflowCommand Submit(string utr, DateTime date)
        {
            return new WorkflowCommand
            {
                ApplicationId = 1, Action = WorkflowActions.CitizenSubmitPayment, ExpectedStage = ZoneStages.PaymentRequired, PaymentReference = utr, PaymentDate = date,
                PaymentProof = new DocumentUpload { FileName = "proof.pdf", ContentType = "application/pdf", Content = TestData.Pdf }
            };
        }

        private static ZoneWorkflowService Service(string pay, out FakeWorkflowStore store)
        {
            store = new FakeWorkflowStore { Current = FakeWorkflowStore.Snapshot(ZoneStages.PaymentRequired, pay) };
            return new ZoneWorkflowService(store, new FakeClock(), new FakeNotifier(), null, new FakeFiles());
        }

        [TestMethod]
        public void Only_A_Resubmission_After_Rejection_Asks_The_Store_To_Replace_The_Proof()
        {
            FakeWorkflowStore store;
            var first = Service(PaymentStatuses.PaymentRequired, out store);
            Assert.IsTrue(first.Execute(Submit("UTR111111", new DateTime(2026, 10, 2)), Actors.Citizen()).IsSuccess);
            Assert.IsFalse(store.LastUpdate.ReplaceActivePaymentProof, "first submission: insert only");

            var again = Service(PaymentStatuses.PaymentRejected, out store);
            Assert.IsTrue(again.Execute(Submit("UTR222222", new DateTime(2026, 10, 2)), Actors.Citizen()).IsSuccess);
            Assert.IsTrue(store.LastUpdate.ReplaceActivePaymentProof, "after an OS rejection: replace");

            // PaymentRequired again AFTER a rejection event (history newer than the last submission) is also a resubmission.
            var snap = FakeWorkflowStore.Snapshot(ZoneStages.PaymentRequired, PaymentStatuses.PaymentRequired);
            snap.HistoryJson = ActionHistory.Append(snap.HistoryJson, new HistoryEntry { Action = AuditActions.PaymentSubmittedByApplicant, FromRole = Roles.Citizen, ActionAtUtc = "2026-10-02T01:00:00.0000000Z" });
            snap.HistoryJson = ActionHistory.Append(snap.HistoryJson, new HistoryEntry { Action = AuditActions.PaymentRejectedByOs, FromRole = Roles.OS, ActionAtUtc = "2026-10-02T02:00:00.0000000Z" });
            Assert.IsTrue(ZoneWorkflowService.IsResubmissionAfterRejection(snap));
            snap.HistoryJson = ActionHistory.Append(snap.HistoryJson, new HistoryEntry { Action = AuditActions.PaymentSubmittedByApplicant, FromRole = Roles.Citizen, ActionAtUtc = "2026-10-02T03:00:00.0000000Z" });
            Assert.IsFalse(ZoneWorkflowService.IsResubmissionAfterRejection(snap), "already resubmitted after the last rejection");
            Assert.IsFalse(ZoneWorkflowService.IsResubmissionAfterRejection(FakeWorkflowStore.Snapshot(ZoneStages.PaymentRequired, PaymentStatuses.PaymentRequired)));
        }

        [TestMethod]
        public void Duplicate_Utr_RowVersion_And_Pending_Rules_Are_Unchanged_For_A_Resubmission()
        {
            FakeWorkflowStore store; var svc = Service(PaymentStatuses.PaymentRejected, out store);
            store.OtherPaymentReferences.Add("UTR999999");
            Assert.AreEqual("PAYMENT_REFERENCE_DUPLICATE", svc.Execute(Submit("UTR999999", new DateTime(2026, 10, 2)), Actors.Citizen()).Error.Code);
            var stale = Submit("UTR222222", new DateTime(2026, 10, 2)); stale.ExpectedRowVersion = Convert.ToBase64String(BitConverter.GetBytes(99));
            Assert.AreEqual("STALE_STAGE", svc.Execute(stale, Actors.Citizen()).Error.Code);
            Assert.IsTrue(svc.Execute(Submit("UTR222222", new DateTime(2026, 10, 2)), Actors.Citizen()).IsSuccess);
            Assert.IsTrue(svc.Execute(Submit("UTR333333", new DateTime(2026, 10, 2)), Actors.Citizen()).IsFailure, "a second submission while one is pending is still refused");
        }

        [TestMethod]
        public void History_Keeps_The_Earlier_Utr_And_Date_And_The_Views_Show_Only_The_Active_Proof()
        {
            FakeWorkflowStore store; var svc = Service(PaymentStatuses.PaymentRequired, out store);
            Assert.IsTrue(svc.Execute(Submit("UTR111111", new DateTime(2026, 10, 1)), Actors.Citizen()).IsSuccess);
            Assert.IsTrue(svc.Execute(new WorkflowCommand { ApplicationId = 1, Action = WorkflowActions.OSRejectPayment, ExpectedStage = ZoneStages.PaymentRequired, Remarks = "पावती अस्पष्ट" }, Actors.OS()).IsSuccess);
            Assert.AreEqual(PaymentStatuses.PaymentRejected, store.Current.PaymentStatus);
            Assert.IsTrue(svc.Execute(Submit("UTR222222", new DateTime(2026, 10, 2)), Actors.Citizen()).IsSuccess);
            Assert.IsTrue(store.LastUpdate.ReplaceActivePaymentProof);

            var entries = ActionHistory.Parse(store.Current.HistoryJson);
            Assert.IsTrue(ActionHistory.VerifyChain(store.Current.HistoryJson), "the chain still verifies with the new fields");
            var submissions = entries.Where(e => e.Action == AuditActions.PaymentSubmittedByApplicant).ToList();
            Assert.AreEqual(2, submissions.Count);
            Assert.AreEqual("UTR111111", submissions[0].PaymentReference); Assert.AreEqual("2026-10-01", submissions[0].PaymentDate);
            Assert.AreEqual("UTR222222", submissions[1].PaymentReference); Assert.AreEqual("2026-10-02", submissions[1].PaymentDate);
            Assert.IsTrue(entries.Where(e => e.Action != AuditActions.PaymentSubmittedByApplicant).All(e => e.PaymentReference == null && e.PaymentDate == null));
            // Entries written before the fields existed keep their original hash (nothing is appended to the hash material).
            var legacy = entries[0].Clone(); Assert.IsNull(legacy.PaymentReference); Assert.AreEqual(legacy.Hash, ActionHistory.ComputeHash(WithoutHash(legacy)));

            // Detail as QueryService builds it: history items carry each submission's own UTR/date; only the active proof is listed.
            var detail = new ApplicationDetail
            {
                Id = 1, ApplicationNumber = "2026-0005", ProcessingRoute = ProcessingRoutes.Zone, Stage = ZoneStages.PaymentRequired, PaymentStatus = PaymentStatuses.PaymentSubmitted,
                PaymentReference = "UTR222222", PaymentDate = new DateTime(2026, 10, 2), PayableAmount = 250m, StartDate = new DateTime(2026, 10, 6), EndDate = new DateTime(2027, 10, 5),
                History = entries.Select(h => new HistoryItem { Sequence = h.Sequence, AtUtc = h.AtUtc, Action = h.Action, FromRole = h.FromRole, ToRole = h.ToRole, Remarks = h.Remarks, By = h.ActionByUserName, PaymentReference = h.PaymentReference, PaymentDate = CitizenStatusMapper.ParsePaymentDate(h.PaymentDate) }).ToList(),
                Documents = new List<DocumentItem> { new DocumentItem { Id = 71, IsZoneDocument = true, DocumentType = "PaymentProof", FileName = "second.pdf", ContentType = "application/pdf", CreatedAt = new DateTime(2026, 10, 2) } }
            };
            var applicant = CitizenStatusMapper.MapDetail(detail);
            var paid = applicant.Timeline.Where(t => t.Event == ApplicantText.PaymentSubmittedEvent).ToList();
            Assert.AreEqual(2, paid.Count);
            Assert.AreEqual("UTR111111", paid[0].PaymentReference); Assert.AreEqual(new DateTime(2026, 10, 1), paid[0].PaymentDate); Assert.IsNull(paid[0].ProofFileName, "the replaced proof is no longer offered");
            Assert.AreEqual("UTR222222", paid[1].PaymentReference); Assert.AreEqual("second.pdf", paid[1].ProofFileName);
            Assert.AreEqual(1, applicant.Documents.Count(d => d.FileName.EndsWith(".pdf")));

            var officer = OfficerApplicationViewMapper.Build(detail, Roles.OS, "");
            Assert.AreEqual("second.pdf", officer.Payment.ProofFileName); Assert.AreEqual("UTR222222", officer.Payment.Reference);
            Assert.AreEqual(1, officer.Documents.Count(d => d.FileName == "second.pdf")); Assert.IsFalse(officer.Documents.Any(d => d.FileName == "first.pdf"));
            Assert.IsTrue(officer.History.Any(h => (h.Remark ?? "").Contains("UTR111111")), "the earlier UTR stays readable in the officer history");
        }

        private static HistoryEntry WithoutHash(HistoryEntry e) { var c = e.Clone(); c.Hash = string.Empty; return c; }

        [TestMethod]
        public void The_Queries_Behind_Both_Views_Return_Only_Active_Proof_Rows()
        {
            var query = File.ReadAllText(Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Data", "Repositories", "QueryService.cs"));
            StringAssert.Contains(query, "db.DemandApplicationZoneDocuments.AsNoTracking().Where(x => !x.IsDeleted)", "GetDetail (applicant dashboard + officer modal)");
            StringAssert.Contains(query, "where d.Id == documentId && !d.IsDeleted && d.Kind == \"PaymentProof\"", "GetZoneDocument (download)");
        }
    }
}
