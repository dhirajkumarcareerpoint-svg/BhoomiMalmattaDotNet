using System;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Models.Common;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Repositories
{
    public sealed class ZoneWorkflowStore : IZoneWorkflowStore
    {
        public ZoneWorkflowSnapshot Load(int applicationId)
        {
            using (var db = new SmcDbContext())
            {
                var app = db.DemandApplications.AsNoTracking().FirstOrDefault(a => a.Id == applicationId && !a.IsDeleted);
                if (app == null) return null;
                var snap = new ZoneWorkflowSnapshot
                {
                    ApplicationId = app.Id, ApplicationNumber = app.ApplicationNumber, ProcessingRoute = app.ProcessingRoute, ApplicantName = app.ApplicantName,
                    ApplicantMobile = app.Mobile, ServiceType = app.ServiceType, FeeAmount = app.FeeAmount, Stage = string.Empty, PaymentStatus = app.PaymentStatus, HistoryJson = "[]", RowVersion = new byte[0]
                };
                if (app.ProcessingRoute != ProcessingRoutes.Zone) return snap; // Legacy: read-only, never loaded for modification

                var wf = db.DemandApplicationZoneWorkflows.AsNoTracking().FirstOrDefault(w => w.DemandApplicationId == applicationId && !w.IsDeleted);
                if (wf == null) return null;
                snap.Zone = wf.Zone; snap.Stage = wf.ZoneStage; snap.PaymentStatus = wf.PaymentStatus; snap.PayableAmount = wf.PayableAmount;
                snap.RecordedPaymentReference = wf.ReceiptNumber; snap.DocumentsVerified = wf.DocumentsVerified; snap.HistoryJson = wf.ActionHistoryJson; snap.RowVersion = wf.RowVersion;
                snap.PaymentReferenceDate = wf.ReceiptDate;
                return snap;
            }
        }

        public bool IsPaymentReferenceUsed(string reference, int exceptApplicationId)
        {
            if (string.IsNullOrWhiteSpace(reference)) return false;
            using (var db = new SmcDbContext())
                return db.DemandApplicationZoneWorkflows.AsNoTracking().Any(w => !w.IsDeleted && w.DemandApplicationId != exceptApplicationId && w.ReceiptNumber == reference.Trim());
        }

        /// <summary>One transaction: workflow row + application status + audit rows (one per step). Optimistic concurrency via the existing RowVersion column.</summary>
        public Result Save(ZoneWorkflowUpdate u)
        {
            using (var db = new SmcDbContext())
            using (var tx = db.Database.BeginTransaction(u.PaymentProof == null ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable))
                return Persist(new EfZoneWorkflowPersistence(db, tx), u);
        }

        /// <summary>DeletedBy written on a proof row replaced by the applicant's resubmission.</summary>
        public const string ReplacedProofDeletedBy = "Citizen";

        /// <summary>
        /// The body of <see cref="Save"/>, run inside the caller's transaction. On a payment resubmission after an OS rejection
        /// (<see cref="ZoneWorkflowUpdate.ReplaceActivePaymentProof"/>) the active proof row of the same Kind is soft-deleted and that change is
        /// flushed BEFORE the new row is inserted: the filtered unique index (DemandApplicationZoneWorkflowId, Kind) WHERE IsDeleted = 0 would
        /// otherwise reject the insert, and EF does not guarantee update-before-insert within one SaveChanges. Both happen in the same
        /// transaction, so any later failure rolls the soft delete back. The old row is never hard-deleted.
        /// </summary>
        internal static Result Persist(IZoneWorkflowPersistence p, ZoneWorkflowUpdate u)
        {
            try
            {
                var id = u.Before.ApplicationId;
                var wf = p.FindWorkflow(id);
                var app = p.FindApplication(id);
                if (wf == null || app == null || app.ProcessingRoute != ProcessingRoutes.Zone) { p.Rollback(); return Result.Failure(new Error("NOT_FOUND", "अर्ज आढळला नाही.")); }
                if (u.PaymentProof != null && p.IsPaymentReferenceUsedByOther(id, u.PaymentReference))
                { p.Rollback(); return Result.Failure(new Error("PAYMENT_REFERENCE_DUPLICATE", "हा UTR / संदर्भ दुसऱ्या अर्जासाठी नोंदवलेला आहे.")); }

                // Optimistic concurrency: the UPDATE only succeeds if RowVersion is still what the caller read.
                p.ExpectRowVersion(wf, u.Before.RowVersion);

                var by = u.Actor.UserName; var at = u.AtUtc;
                wf.ZoneStage = u.NewStage; wf.PaymentStatus = u.NewPaymentStatus; wf.PayableAmount = u.NewPayableAmount; wf.ActionHistoryJson = u.NewHistoryJson;
                wf.UpdatedBy = by; wf.UpdatedAt = at;
                if (u.DocumentsVerified.HasValue) wf.DocumentsVerified = u.DocumentsVerified.Value;

                switch (u.RemarkKind)
                {
                    case RemarkKind.Correction: wf.CorrectionRemark = u.Remark; wf.CorrectionVersion = wf.CorrectionVersion + 1; wf.CorrectionRequestedAt = at; break;
                    case RemarkKind.Approval: wf.ApprovalRemark = u.Remark; wf.ApprovedAt = at; wf.ApprovedBy = by; break;
                    case RemarkKind.Rejection: wf.RejectionRemark = u.Remark; break;
                }
                if (u.Before.Stage == ZoneStages.JECorrectionRequested || u.Before.Stage == ZoneStages.OSCorrectionRequested)
                {
                    if (u.NewStage == ZoneStages.JEPending || u.NewStage == ZoneStages.OSPending) { wf.CorrectionResubmittedAt = at; wf.ApplicantReply = u.Remark; }
                }
                if (u.PaymentReference != null) { wf.ReceiptNumber = u.PaymentReference; wf.ReceiptDate = u.PaymentDate; }
                if (u.ConfirmPayment) { wf.PaymentMode = u.PaymentMode; wf.ReceivedBy = by; wf.PaymentConfirmedAt = at; }

                if (u.PaymentProof != null)
                {
                    if (u.ReplaceActivePaymentProof)
                    {
                        p.SoftDeleteActiveZoneDocuments(wf.Id, u.PaymentProof.DocumentType, at, ReplacedProofDeletedBy);
                        p.SaveChanges(); // same transaction: the soft delete must reach the database before the INSERT below
                    }
                    p.AddZoneDocument(new DemandApplicationZoneDocument
                    {
                        DemandApplicationZoneWorkflowId = wf.Id, Kind = u.PaymentProof.DocumentType,
                        FileName = u.PaymentProof.OriginalFileName, FilePath = u.PaymentProof.RelativePath,
                        ContentType = u.PaymentProof.ContentType, SizeBytes = u.PaymentProof.SizeBytes,
                        CreatedBy = by, CreatedAt = at, IsDeleted = false
                    });
                }

                app.Status = u.NewStatus; app.PaymentStatus = u.NewPaymentStatus; app.UpdatedBy = by; app.UpdatedAt = at;
                if (u.NewPayableAmount > 0 && u.NewPaymentStatus != PaymentStatuses.PendingOSAmount) app.FeeAmount = u.NewPayableAmount;
                if (u.RateEnteredByOfficer.HasValue) app.CalculatedRate = u.RateEnteredByOfficer;

                foreach (var audit in u.Audits ?? new System.Collections.Generic.List<AuditEntry>())
                    if (audit != null && audit.UserId.HasValue) p.AddAudit(AuditWriter.ToEntity(audit));
                p.SaveChanges();
                p.Commit();
                return Result.Success();
            }
            catch (DbUpdateConcurrencyException)
            {
                p.Rollback();
                return Result.Failure(new Error("CONCURRENCY_CONFLICT", "हा अर्ज दुसऱ्या वापरकर्त्याने नुकताच बदलला आहे. कृपया पान रिफ्रेश करा."));
            }
            catch (DbUpdateException)
            {
                p.Rollback();
                return Result.Failure(new Error("DB_ERROR", "बदल जतन करता आला नाही. कृपया पुन्हा प्रयत्न करा."));
            }
        }

        public Result SetCertificate(int applicationId, string relativePath, string fileName, string approvedBy)
        {
            using (var db = new SmcDbContext())
            {
                var wf = db.DemandApplicationZoneWorkflows.FirstOrDefault(w => w.DemandApplicationId == applicationId && !w.IsDeleted);
                if (wf == null || wf.ZoneStage != ZoneStages.Approved) return Result.Failure(new Error("NOT_APPROVED", "केवळ मंजूर अर्जासाठी प्रमाणपत्र नोंदवता येते."));
                wf.CertificatePath = relativePath; wf.CertificateFileName = fileName; wf.UpdatedBy = approvedBy; wf.UpdatedAt = DateTime.UtcNow;
                try { db.SaveChanges(); return Result.Success(); }
                catch (DbUpdateException) { return Result.Failure(new Error("DB_ERROR", "प्रमाणपत्र नोंदवता आले नाही.")); }
            }
        }
    }

    /// <summary>The database operations <see cref="ZoneWorkflowStore.Persist"/> needs, inside one transaction (a seam for tests).</summary>
    internal interface IZoneWorkflowPersistence
    {
        DemandApplicationZoneWorkflow FindWorkflow(int applicationId);
        DemandApplication FindApplication(int applicationId);
        bool IsPaymentReferenceUsedByOther(int applicationId, string reference);
        void ExpectRowVersion(DemandApplicationZoneWorkflow workflow, byte[] rowVersion);
        /// <summary>Marks every active zone document of this Kind for this workflow as deleted (IsDeleted = 1, DeletedAt, DeletedBy). Returns how many.</summary>
        int SoftDeleteActiveZoneDocuments(int workflowId, string kind, DateTime atUtc, string deletedBy);
        void AddZoneDocument(DemandApplicationZoneDocument row);
        void AddAudit(AuditLog row);
        void SaveChanges();
        void Commit();
        void Rollback();
    }

    internal sealed class EfZoneWorkflowPersistence : IZoneWorkflowPersistence
    {
        private readonly SmcDbContext _db; private readonly DbContextTransaction _tx;
        public EfZoneWorkflowPersistence(SmcDbContext db, DbContextTransaction tx) { _db = db; _tx = tx; }

        public DemandApplicationZoneWorkflow FindWorkflow(int applicationId) { return _db.DemandApplicationZoneWorkflows.FirstOrDefault(w => w.DemandApplicationId == applicationId && !w.IsDeleted); }
        public DemandApplication FindApplication(int applicationId) { return _db.DemandApplications.FirstOrDefault(a => a.Id == applicationId && !a.IsDeleted); }
        public bool IsPaymentReferenceUsedByOther(int applicationId, string reference)
        { return _db.DemandApplicationZoneWorkflows.AsNoTracking().Any(w => !w.IsDeleted && w.DemandApplicationId != applicationId && w.ReceiptNumber == reference); }
        public void ExpectRowVersion(DemandApplicationZoneWorkflow workflow, byte[] rowVersion) { _db.Entry(workflow).Property(w => w.RowVersion).OriginalValue = rowVersion; }
        public int SoftDeleteActiveZoneDocuments(int workflowId, string kind, DateTime atUtc, string deletedBy)
        {
            var rows = _db.DemandApplicationZoneDocuments.Where(d => d.DemandApplicationZoneWorkflowId == workflowId && d.Kind == kind && !d.IsDeleted).ToList();
            foreach (var r in rows) { r.IsDeleted = true; r.DeletedAt = atUtc; r.DeletedBy = deletedBy; }
            return rows.Count;
        }
        public void AddZoneDocument(DemandApplicationZoneDocument row) { _db.DemandApplicationZoneDocuments.Add(row); }
        public void AddAudit(AuditLog row) { _db.AuditLogs.Add(row); }
        public void SaveChanges() { _db.SaveChanges(); }
        public void Commit() { _tx.Commit(); }
        public void Rollback() { _tx.Rollback(); }
    }
}
