using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Models.Common;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Repositories
{
    public sealed class DemandApplicationStore : IDemandApplicationStore
    {
        public Result<int> Create(NewApplicationRecord r)
        {
            var d = r.Data;
            using (var db = new SmcDbContext())
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    var app = ToApplicationEntity(r);
                    db.DemandApplications.Add(app);
                    db.SaveChanges();

                    db.DemandApplicationZoneWorkflows.Add(new DemandApplicationZoneWorkflow
                    {
                        DemandApplicationId = app.Id, Zone = int.Parse(d.Zone), ZoneStage = ZoneStages.JEPending, PaymentStatus = PaymentStatuses.PendingOSAmount,
                        PayableAmount = r.FeeAmount ?? 0m, DocumentsVerified = false, CorrectionVersion = 0, ActionHistoryJson = r.InitialHistoryJson, CreatedBy = "Citizen", CreatedAt = r.SubmittedAtUtc, IsDeleted = false
                    });
                    foreach (var doc in r.Documents) db.DemandApplicationDocuments.Add(ToEntity(app.Id, doc, "Citizen", r.SubmittedAtUtc));
                    db.SaveChanges();
                    tx.Commit();
                    return Result.Success(app.Id);
                }
                catch (DbUpdateException ex)
                {
                    tx.Rollback();
                    var msg = ex.GetBaseException().Message ?? string.Empty;
                    return Result.Failure<int>(msg.IndexOf("ApplicationNumber", StringComparison.OrdinalIgnoreCase) >= 0
                        ? new Error("DUPLICATE_NUMBER", "अर्ज क्रमांक आधीच अस्तित्वात आहे. कृपया पुन्हा प्रयत्न करा.")
                        : new Error("DB_ERROR", "अर्ज जतन करता आला नाही. कृपया पुन्हा प्रयत्न करा."));
                }
            }
        }

        /// <summary>
        /// Maps a new submission onto TR_CFC_DemandApplications. Optional applicant fields use schema-safe values:
        /// NOT NULL ApplicantName / Mobile / Email / GstNumber / Prabhag become "" (never NULL, never placeholder text),
        /// nullable ApplicantType / IdentityNumber / PanNumber stay NULL when not given.
        /// </summary>
        internal static DemandApplication ToApplicationEntity(NewApplicationRecord r)
        {
            var d = r.Data;
            return new DemandApplication
            {
                ApplicationNumber = r.ApplicationNumber, ServiceType = d.ServiceType, BusinessType = d.BusinessType, OtherBusinessType = d.OtherBusinessType,
                ApplicantType = d.ApplicantType, ApplicantName = d.ApplicantName ?? string.Empty, Mobile = d.Mobile ?? string.Empty, Email = d.Email ?? string.Empty, IdentityNumber = d.IdentityNumber,
                PanNumber = d.PanNumber, GstNumber = d.GstNumber ?? string.Empty,
                PermanentAddress = d.PermanentAddress, CorrespondenceAddress = d.CorrespondenceAddress, SameAddress = d.SameAddress,
                State = d.State, District = d.District, City = d.City, Taluka = d.Taluka, PinCode = d.PinCode, Zone = d.Zone, Prabhag = d.Prabhag ?? string.Empty,
                Location = d.Location, ServiceDescription = d.ServiceDescription, StartDate = d.StartDate.Date, EndDate = d.EndDate.Date,
                RequiredDuration = r.Pricing.TotalDays + " दिवस", AreaSqFt = r.Pricing.AreaSqFt > 0 ? (decimal?)r.Pricing.AreaSqFt : null,
                LengthFt = d.LengthFt, WidthFt = d.WidthFt, CalculatedRate = d.ServiceType == "VariousBusinessSpace" ? r.Pricing.DailyRate : r.Pricing.Rate, FeeAmount = r.FeeAmount,
                DeclarationAccepted = d.DeclarationAccepted, PaymentStatus = r.PaymentStatus, Status = ApplicationStatuses.Submitted,
                SubmittedAt = r.SubmittedAtUtc, CreatedBy = "Citizen", CreatedAt = r.SubmittedAtUtc, IsDeleted = false,
                ApplicantAccessTokenHash = r.AccessTokenHash, ProcessingRoute = ProcessingRoutes.Zone
            };
        }

        public Result AddDocuments(int applicationId, List<StoredDocument> documents, string actor)
        {
            using (var db = new SmcDbContext())
            {
                var now = DateTime.UtcNow;
                foreach (var doc in documents) db.DemandApplicationDocuments.Add(ToEntity(applicationId, doc, actor, now));
                try { db.SaveChanges(); return Result.Success(); }
                catch (DbUpdateException) { return Result.Failure(new Error("DB_ERROR", "कागदपत्रे जतन करता आली नाहीत.")); }
            }
        }

        private static DemandApplicationDocument ToEntity(int appId, StoredDocument d, string actor, DateTime now)
        {
            return new DemandApplicationDocument
            {
                DemandApplicationId = appId, DocumentType = d.DocumentType, FileName = d.OriginalFileName, StoredFileName = d.StoredFileName, FilePath = d.RelativePath,
                ContentType = d.ContentType, FileSizeBytes = d.SizeBytes, CreatedBy = actor, CreatedAt = now, IsDeleted = false, VerificationStatus = VerificationStatuses.Unchecked
            };
        }
    }
}
