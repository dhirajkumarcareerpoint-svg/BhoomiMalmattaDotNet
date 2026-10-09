using System;
using System.Collections.Generic;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Business.Security;
using SMC.BhoomiMalmatta.Models.Common;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;

namespace SMC.BhoomiMalmatta.Business.Documents
{
    public sealed class DocumentFile { public byte[] Content { get; set; } public string FileName { get; set; } public string ContentType { get; set; } }

    /// <summary>Authorized document download. Physical paths never leave the server.</summary>
    public sealed class DocumentService
    {
        private readonly IQueryService _q; private readonly IFileStorage _files; private readonly IAuditWriter _audit; private readonly IClock _clock;
        public DocumentService(IQueryService q, IFileStorage files, IAuditWriter audit, IClock clock) { _q = q; _files = files; _audit = audit; _clock = clock; }

        /// <param name="citizenApplicationId">The application the citizen proved access to (token), or null for officers.</param>
        public Result<DocumentFile> Open(int documentId, string role, int? userId, string userName, string ip, int? citizenApplicationId)
        {
            var doc = _q.GetDocument(documentId);
            if (doc == null) return Result.Failure<DocumentFile>(new Error("NOT_FOUND", "कागदपत्र आढळले नाही."));
            // Documents in TR_CFC_DemandApplicationDocuments are applicant-submitted: citizen-visible to that applicant only.
            var citizenOk = citizenApplicationId.HasValue && citizenApplicationId.Value == doc.ApplicationId;
            if (!DocumentAccessPolicy.CanDownload(role, DocumentVisibility.CitizenVisible, citizenOk))
                return Result.Failure<DocumentFile>(new Error("FORBIDDEN", "हे कागदपत्र पाहण्याची परवानगी नाही."));
            byte[] data;
            try { data = _files.Read(doc.RelativePath); }
            catch (Exception) { return Result.Failure<DocumentFile>(new Error("FILE_MISSING", "कागदपत्र फाईल उपलब्ध नाही.")); }
            if (userId.HasValue)
            {
                try { _audit.Write(new AuditEntry { Action = "Document Downloaded", EntityName = "DemandApplicationDocument", EntityId = documentId, UserId = userId, UserName = userName, IpAddress = ip, TimestampUtc = _clock.UtcNow }); }
                catch (Exception) { }
            }
            return Result.Success(new DocumentFile { Content = data, FileName = FileValidator.SanitizeName(doc.FileName), ContentType = doc.ContentType });
        }
    }

    /// <summary>
    /// Generates the final certificate / permission letter (मंजुरी पत्र) PDF after approval and records it in the existing CertificatePath/CertificateFileName
    /// columns. If generation failed after the approval was committed, the first download generates it (an approved application always yields a letter).
    /// </summary>
    public sealed class CertificateService : ICertificateIssuer
    {
        private readonly IQueryService _q; private readonly IMasterDataReader _masters; private readonly IPdfGenerator _pdf; private readonly IFileStorage _files;
        private readonly IZoneWorkflowStore _store; private readonly INotifier _notifier; private readonly IAuditWriter _audit; private readonly IClock _clock;
        public CertificateService(IQueryService q, IMasterDataReader masters, IPdfGenerator pdf, IFileStorage files, IZoneWorkflowStore store, INotifier notifier, IAuditWriter audit, IClock clock)
        { _q = q; _masters = masters; _pdf = pdf; _files = files; _store = store; _notifier = notifier; _audit = audit; _clock = clock; }

        /// <summary>Relative storage path (under the configured storage root, outside the web root) built only from a sanitized application number.</summary>
        public static string RelativePathFor(string applicationNumber)
        {
            var safe = FileValidator.SanitizeName((applicationNumber ?? string.Empty).Replace("/", "_").Replace("\\", "_"));
            if (string.IsNullOrWhiteSpace(safe) || safe.Contains("..")) safe = "application";
            return "certificates/" + safe + "/certificate.pdf";
        }

        public Result Issue(int applicationId, ActorContext actor)
        {
            var d = _q.GetDetail(applicationId);
            if (d == null) return Result.Failure(new Error("NOT_FOUND", "अर्ज आढळला नाही."));
            var issued = IssueFor(d, actor);
            return issued.IsFailure ? Result.Failure(issued.Error) : Result.Success();
        }

        /// <summary>
        /// Authorized certificate download. Citizens only with the ticket of the same application; officer roles otherwise (zone scoping is done by the caller).
        /// Generates the letter on first request when the approved application has none yet (or the stored file is missing).
        /// </summary>
        public Result<DocumentFile> OpenForDownload(int applicationId, string role, int? citizenApplicationId, ActorContext actor)
        {
            var allowed = role == Roles.Citizen ? citizenApplicationId.HasValue && citizenApplicationId.Value == applicationId : Array.IndexOf(Roles.OfficerRoles, role) >= 0;
            if (!allowed) return Result.Failure<DocumentFile>(new Error("FORBIDDEN", "हे प्रमाणपत्र पाहण्याची परवानगी नाही."));
            var d = _q.GetDetail(applicationId);
            if (d == null) return Result.Failure<DocumentFile>(new Error("NOT_FOUND", "अर्ज आढळला नाही."));
            if (!d.IsLegacy && d.Stage != ZoneStages.Approved) return Result.Failure<DocumentFile>(new Error("NOT_APPROVED", "केवळ मंजूर अर्जासाठी प्रमाणपत्र उपलब्ध आहे."));
            var bytes = d.HasCertificate ? TryRead(d.CertificatePath) : null;
            if (bytes == null && !d.IsLegacy)
            {
                var issued = IssueFor(d, actor);
                if (issued.IsFailure) return Result.Failure<DocumentFile>(issued.Error);
                bytes = TryRead(issued.Value);
            }
            if (bytes == null) return Result.Failure<DocumentFile>(new Error("FILE_MISSING", "प्रमाणपत्र फाईल उपलब्ध नाही."));
            return Result.Success(new DocumentFile { Content = bytes, ContentType = "application/pdf", FileName = "Certificate-" + FileValidator.SanitizeName(d.ApplicationNumber ?? "application") + ".pdf" });
        }

        private byte[] TryRead(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return null;
            try { return _files.Read(relativePath); } catch (Exception) { return null; }
        }

        private Result<string> IssueFor(ApplicationDetail d, ActorContext actor)
        {
            if (d.IsLegacy || d.Stage != ZoneStages.Approved) return Result.Failure<string>(new Error("NOT_APPROVED", "केवळ मंजूर अर्जासाठी प्रमाणपत्र तयार करता येते."));
            var approvedBy = string.IsNullOrWhiteSpace(d.ApprovedBy) ? actor.UserName : d.ApprovedBy;
            var bytes = _pdf.Certificate(d, _masters.GetPermitConfig(), _masters.GetPermitTerms(), approvedBy);
            var rel = RelativePathFor(d.ApplicationNumber);
            _files.Save(rel, bytes);
            var saved = _store.SetCertificate(d.Id, rel, "Certificate-" + FileValidator.SanitizeName(d.ApplicationNumber ?? "application") + ".pdf", actor.UserName ?? approvedBy);
            if (saved.IsFailure) return Result.Failure<string>(saved.Error);
            var applicationId = d.Id;
            if (actor.UserId.HasValue)
            {
                try { _audit.Write(new AuditEntry { Action = AuditActions.CertificateGenerated, EntityName = "DemandApplicationZoneWorkflow", EntityId = applicationId, UserId = actor.UserId, UserName = actor.UserName, IpAddress = actor.IpAddress, TimestampUtc = _clock.UtcNow }); }
                catch (Exception) { }
            }
            try
            {
                _notifier.Notify(new NotificationRequest { EventType = SmsEventTypes.CertificateAvailable, ApplicationNumber = d.ApplicationNumber, RecipientMobile = d.Mobile,
                    Variables = new Dictionary<string, string> { { "ApplicationNumber", d.ApplicationNumber }, { "ApplicantName", d.ApplicantName } } });
            }
            catch (Exception) { }
            return Result.Success(rel);
        }
    }
}
