using System;
using System.Collections.Generic;
using System.Linq;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Business.Numbering;
using SMC.BhoomiMalmatta.Business.Pricing;
using SMC.BhoomiMalmatta.Business.Security;
using SMC.BhoomiMalmatta.Business.Workflow;
using SMC.BhoomiMalmatta.Models.Common;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;

namespace SMC.BhoomiMalmatta.Business.Submission
{
    public sealed class SubmissionOutcome
    {
        public int ApplicationId { get; set; }
        public string ApplicationNumber { get; set; }
        public PricingResult Pricing { get; set; }
        public string PaymentStatus { get; set; }
    }
    public sealed class SubmissionFailure
    {
        public SubmissionFailure(IEnumerable<FieldError> errors) { Errors = errors.ToList(); }
        public List<FieldError> Errors { get; private set; }
    }

    public sealed class DemandSubmissionService
    {
        private readonly IMasterDataReader _masters; private readonly ApplicationNumberService _numbers; private readonly IDemandApplicationStore _store;
        private readonly IFileStorage _files; private readonly IClock _clock; private readonly INotifier _notifier; private readonly bool _requireApplicantDetails;
        /// <summary>History actor name used when the applicant name is not collected.</summary>
        public const string DefaultActorName = "Citizen";
        public DemandSubmissionService(IMasterDataReader masters, ApplicationNumberService numbers, IDemandApplicationStore store, IFileStorage files, IClock clock, INotifier notifier = null)
            : this(masters, numbers, store, files, clock, notifier, true) { }
        /// <param name="requireApplicantDetails">Apply.CollectApplicantDetails (applies to the wizard and the public API alike).</param>
        public DemandSubmissionService(IMasterDataReader masters, ApplicationNumberService numbers, IDemandApplicationStore store, IFileStorage files, IClock clock, INotifier notifier, bool requireApplicantDetails)
        { _masters = masters; _numbers = numbers; _store = store; _files = files; _clock = clock; _notifier = notifier ?? new NullNotifier(); _requireApplicantDetails = requireApplicantDetails; }

        public bool RequiresApplicantDetails { get { return _requireApplicantDetails; } }

        /// <summary>Server-side pricing preview used by the form; identical maths to the final submission.</summary>
        public Result<PricingResult> Preview(SubmissionData d, decimal? officerRate = null)
        {
            var serviceCode = (d.ServiceType ?? string.Empty).Trim();
            if (!_masters.GetCatalog().ServiceCodes.Contains(serviceCode))
                return Result.Failure<PricingResult>(new Error("SERVICE_NOT_CONFIGURED", "कृपया वैध सेवा निवडा."));
            RateInfo rate;
            if (DemandServicePricingRules.IsAreaBased(serviceCode))
                rate = new RateInfo { ServiceCode = "VariousBusinessSpace", Rate = 1m, Unit = "sq.ft." };
            else
                // These requested annual prices deliberately override conflicting master rates without changing master data.
                rate = new RateInfo { ServiceCode = serviceCode, Rate = DemandServicePricingRules.FixedAnnualAmount(serviceCode), Unit = "fixed" };
            var effectiveEndDate = DemandServicePricingRules.EffectiveEndDate(serviceCode, d.StartDate, d.EndDate);
            var calculated = DemandPricingCalculator.Calculate(new PricingRequest
            {
                ServiceCode = rate.ServiceCode, RateUnit = rate.Unit, MasterRate = rate.Rate, StartDate = d.StartDate, EndDate = effectiveEndDate,
                LengthFt = d.LengthFt, WidthFt = d.WidthFt, OfficerEnteredRate = officerRate
            });
            if (calculated.IsSuccess && !calculated.Value.FinalAmount.HasValue)
                return Result.Failure<PricingResult>(new Error("RATE_REQUIRED", "या सेवेसाठी मंजूर दर उपलब्ध नाही. दर निश्चित होईपर्यंत अर्ज सादर करता येणार नाही."));
            return calculated;
        }

        public PricingPreviewResponse PreviewResponse(PricingPreviewRequest r)
        {
            var d = new SubmissionData { ServiceType = r.ServiceType, StartDate = r.StartDate, EndDate = r.EndDate, LengthFt = r.LengthFt, WidthFt = r.WidthFt };
            var effectiveEndDate = DemandServicePricingRules.EffectiveEndDate(r.ServiceType, r.StartDate, r.EndDate);
            var p = Preview(d);
            var isFormAreaCharge = DemandServicePricingRules.IsAreaBased(d.ServiceType);
            var endDateText = effectiveEndDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            if (p.IsFailure) return new PricingPreviewResponse { Error = p.Error.Message, EffectiveEndDate = endDateText, Unit = isFormAreaCharge ? "प्रतिदिन" : "वार्षिक" };
            return new PricingPreviewResponse { TotalDays = p.Value.TotalDays, AreaSqFt = p.Value.AreaSqFt, Rate = isFormAreaCharge ? p.Value.DailyRate : p.Value.Rate, FinalAmount = p.Value.FinalAmount, PendingOfficerAmount = p.Value.IsPendingOfficerAmount, EffectiveEndDate = endDateText, Unit = isFormAreaCharge ? "प्रतिदिन" : "वार्षिक" };
        }

        /// <summary>Validates and prices on the server, stores files, then creates application + workflow + documents in one transaction.</summary>
        public Tuple<SubmissionOutcome, SubmissionFailure> Submit(SubmissionData raw, IList<DocumentUpload> uploads, string ip, string correlationId)
        { return Submit(raw, uploads, ip, correlationId, FileValidator.MaxBytes); }

        /// <summary>Application-form variant with its configured per-attachment size limit.</summary>
        public Tuple<SubmissionOutcome, SubmissionFailure> Submit(SubmissionData raw, IList<DocumentUpload> uploads, string ip, string correlationId, long maxUploadBytes)
        {
            Func<string, string, Tuple<SubmissionOutcome, SubmissionFailure>> fail = (f, m) => Tuple.Create<SubmissionOutcome, SubmissionFailure>(null, new SubmissionFailure(new[] { new FieldError(f, m) }));
            uploads = uploads ?? new List<DocumentUpload>();
            var d = SubmissionValidator.Normalize(raw);
            d.UploadedDocumentTypes = uploads.Select(u => u.DocumentType).Distinct().ToList();
            var errors = SubmissionValidator.Validate(d, _masters.GetCatalog(), _requireApplicantDetails);

            var checks = new List<Tuple<DocumentUpload, FileCheck>>();
            foreach (var u in uploads)
            {
                var chk = FileValidator.Validate(u.FileName, u.ContentType, u.Content, maxUploadBytes);
                if (chk.IsFailure) errors.Add(new FieldError("Documents", u.DocumentType + ": " + chk.Error.Message));
                else checks.Add(Tuple.Create(u, chk.Value));
            }
            if (errors.Count > 0) return Tuple.Create<SubmissionOutcome, SubmissionFailure>(null, new SubmissionFailure(errors));

            var price = Preview(d);
            if (price.IsFailure) return fail("Pricing", price.Error.Message);
            var p = price.Value;
            // The annual amount is always replaced by the server rule; the area preview claim is checked for stale input.
            if (DemandServicePricingRules.IsAreaBased(d.ServiceType) && d.ClientClaimedAmount.HasValue && p.FinalAmount.HasValue && d.ClientClaimedAmount.Value != p.FinalAmount.Value)
                return fail("Amount", "रक्कम जुळत नाही; कृपया पान रिफ्रेश करून पुन्हा प्रयत्न करा.");

            var number = _numbers.Next();
            if (number.IsFailure) return fail("ApplicationNumber", number.Error.Message);

            var now = _clock.UtcNow;
            var token = AccessToken.NewToken();
            var pay = p.IsPendingOfficerAmount ? PaymentStatuses.PendingOSAmount : PaymentStatuses.FeePending;
            var first = new HistoryEntry
            {
                ApplicationId = 0, ApplicationNumber = number.Value, FromRole = Roles.Citizen, ToRole = Roles.JE, Action = AuditActions.ApplicationSubmitted,
                PreviousStatus = string.Empty, NewStatus = ApplicationStatuses.Submitted, PreviousStage = string.Empty, NewStage = ZoneStages.JEPending,
                ActionByUserName = string.IsNullOrWhiteSpace(d.ApplicantName) ? DefaultActorName : d.ApplicantName, ActionAtUtc = now.ToString("O"), IpAddress = ip, CorrelationId = correlationId
            };

            var docs = new List<StoredDocument>(); var savedPaths = new List<string>();
            try
            {
                foreach (var t in checks)
                {
                    var rel = "documents/" + number.Value.Replace("/", "_") + "/" + t.Item2.StoredName;
                    _files.Save(rel, t.Item1.Content); savedPaths.Add(rel);
                    docs.Add(new StoredDocument { DocumentType = t.Item1.DocumentType, OriginalFileName = t.Item2.SafeDisplayName, StoredFileName = t.Item2.StoredName, RelativePath = rel, ContentType = t.Item2.ContentType, SizeBytes = t.Item1.Content.LongLength });
                }
                var created = _store.Create(new NewApplicationRecord
                {
                    Data = d, ApplicationNumber = number.Value, AccessTokenHash = AccessToken.Hash(token), Pricing = p, FeeAmount = p.FinalAmount, PaymentStatus = pay,
                    InitialHistoryJson = ActionHistory.Append("[]", first), SubmittedAtUtc = now, Documents = docs
                });
                if (created.IsFailure) { Cleanup(savedPaths); return fail("Save", created.Error.Message); }
                try
                {
                    // An empty mobile is passed through: the SMS service records "Suppressed / No recipient mobile number" and sends nothing.
                    _notifier.Notify(new NotificationRequest
                    {
                        EventType = SmsEventTypes.ApplicationSubmitted, ApplicationNumber = number.Value, RecipientMobile = d.Mobile,
                        Variables = new Dictionary<string, string> { { "ApplicationNumber", number.Value }, { "ApplicantName", d.ApplicantName } }
                    });
                }
                catch (Exception) { /* SMS failure never cancels the submission */ }
                return Tuple.Create(new SubmissionOutcome { ApplicationId = created.Value, ApplicationNumber = number.Value, Pricing = p, PaymentStatus = pay }, (SubmissionFailure)null);
            }
            catch (Exception)
            {
                Cleanup(savedPaths);
                throw;
            }
        }

        /// <summary>Citizen resubmits after a correction request: validates + stores the new documents, then runs the CitizenResubmit workflow action.</summary>
        public Result Resubmit(int applicationId, string expectedStage, string reply, IList<DocumentUpload> uploads, ActorContext citizen, ZoneWorkflowService workflow, string applicationNumber)
        {
            if (string.IsNullOrWhiteSpace(reply) && (uploads == null || uploads.Count == 0))
                return Result.Failure(new Error("REPLY_REQUIRED", "कृपया उत्तर लिहा किंवा दुरुस्त कागदपत्र जोडा."));
            var docs = new List<StoredDocument>(); var saved = new List<string>();
            foreach (var u in uploads ?? new List<DocumentUpload>())
            {
                var chk = FileValidator.Validate(u.FileName, u.ContentType, u.Content);
                if (chk.IsFailure) return Result.Failure(chk.Error);
                var rel = "documents/" + applicationNumber.Replace("/", "_") + "/" + chk.Value.StoredName;
                _files.Save(rel, u.Content); saved.Add(rel);
                var type = expectedStage == ZoneStages.OSCorrectionRequested ? DocumentTypes.Level2Correction : DocumentTypes.Level1Correction;
                docs.Add(new StoredDocument { DocumentType = type, OriginalFileName = chk.Value.SafeDisplayName, StoredFileName = chk.Value.StoredName, RelativePath = rel, ContentType = chk.Value.ContentType, SizeBytes = u.Content.LongLength });
            }
            var wf = workflow.Execute(new WorkflowCommand { ApplicationId = applicationId, Action = WorkflowActions.CitizenResubmit, ExpectedStage = expectedStage, Remarks = reply }, citizen);
            if (wf.IsFailure) { Cleanup(saved); return Result.Failure(wf.Error); }
            if (docs.Count > 0) _store.AddDocuments(applicationId, docs, "Citizen");
            return Result.Success();
        }

        private void Cleanup(IEnumerable<string> paths) { foreach (var p in paths) { try { _files.Delete(p); } catch (Exception) { } } }
    }
}
