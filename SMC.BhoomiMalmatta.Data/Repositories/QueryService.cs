using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq.Expressions;
using System.Linq;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Business.Pricing;
using SMC.BhoomiMalmatta.Business.Workflow;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;
using SMC.BhoomiMalmatta.Data.Repositories;

namespace SMC.BhoomiMalmatta.Data.Repositories
{
    /// <summary>Read-only queries for lists, details, dashboard, reports, audit and documents. Never exposes entities or password hashes.</summary>
    public sealed class QueryService : IQueryService
    {
        internal sealed class AppRow
        {
            public int Id { get; set; }
            public string ApplicationNumber { get; set; }
            public string ServiceType { get; set; }
            public string ServiceName { get; set; }
            public string BusinessType { get; set; }
            public string OtherBusinessType { get; set; }
            public string ApplicantName { get; set; }
            public string Mobile { get; set; }
            public string Zone { get; set; }
            public string Status { get; set; }
            public string PaymentStatus { get; set; }
            public decimal? FeeAmount { get; set; }
            public DateTime? SubmittedAt { get; set; }
            public string Route { get; set; }
            public int? ZoneNo { get; set; }
            public string Stage { get; set; }
            public byte[] RowVersion { get; set; }
            public decimal? PayableAmount { get; set; }
        }

        internal sealed class OfficerKpiFlags
        {
            public int Total { get; set; }
            public int Pending { get; set; }
            public int Approved { get; set; }
            public int Rejected { get; set; }
        }

        internal sealed class OfficerPageRow
        {
            public AppRow Row { get; set; }
            public int Total { get; set; }
        }

        private sealed class ReplaceParameterVisitor : ExpressionVisitor
        {
            private readonly ParameterExpression _oldParameter;
            private readonly ParameterExpression _newParameter;
            public ReplaceParameterVisitor(ParameterExpression oldParameter, ParameterExpression newParameter)
            { _oldParameter = oldParameter; _newParameter = newParameter; }
            protected override Expression VisitParameter(ParameterExpression node)
            { return node == _oldParameter ? _newParameter : base.VisitParameter(node); }
        }

        internal static IQueryable<AppRow> Base(SmcDbContext db)
        {
            return from a in db.DemandApplications.AsNoTracking()
                   where !a.IsDeleted
                   join z in db.DemandApplicationZoneWorkflows.AsNoTracking().Where(x => !x.IsDeleted) on a.Id equals z.DemandApplicationId into zj
                   from z in zj.DefaultIfEmpty()
                   join l in db.DemandApplicationWorkflows.AsNoTracking().Where(x => !x.IsDeleted) on a.Id equals l.DemandApplicationId into lj
                   from l in lj.DefaultIfEmpty()
                   select new AppRow
                   {
                       Id = a.Id, ApplicationNumber = a.ApplicationNumber, ServiceType = a.ServiceType, ServiceName = a.ServiceType, BusinessType = a.BusinessType, OtherBusinessType = a.OtherBusinessType, ApplicantName = a.ApplicantName, Mobile = a.Mobile, Zone = a.Zone,
                       Status = a.Status, PaymentStatus = a.PaymentStatus, FeeAmount = a.FeeAmount, SubmittedAt = a.SubmittedAt ?? a.CreatedAt, Route = a.ProcessingRoute,
                       ZoneNo = (int?)z.Zone, Stage = z != null ? z.ZoneStage : (l != null ? l.Stage : null),
                       RowVersion = z != null ? z.RowVersion : null, PayableAmount = z != null ? z.PayableAmount : (l != null ? l.PayableAmount : (decimal?)null)
                   };
        }

        internal static IQueryable<AppRow> Apply(IQueryable<AppRow> q, ApplicationFilter f)
        {
            f.Normalize();
            if (f.ZoneScope.HasValue) { var zs = f.ZoneScope.Value; q = q.Where(x => x.ZoneNo == zs); }
            if (!string.IsNullOrWhiteSpace(f.Search)) { var s = f.Search.Trim(); q = q.Where(x => x.ApplicationNumber.Contains(s) || x.ApplicantName.Contains(s) || x.Mobile.Contains(s)); }
            if (!string.IsNullOrWhiteSpace(f.Status)) { var v = f.Status; q = q.Where(x => x.Status == v); }
            if (!string.IsNullOrWhiteSpace(f.Stage)) { var v = f.Stage; q = q.Where(x => x.Stage == v); }
            if (!string.IsNullOrWhiteSpace(f.ServiceType)) { var v = f.ServiceType; q = q.Where(x => x.ServiceType == v); }
            if (!string.IsNullOrWhiteSpace(f.PaymentStatus)) { var v = f.PaymentStatus; q = q.Where(x => x.PaymentStatus == v); }
            if (!string.IsNullOrWhiteSpace(f.Route)) { var v = f.Route; q = q.Where(x => x.Route == v); }
            if (f.Zone.HasValue) { var z = f.Zone.Value; q = q.Where(x => x.ZoneNo == z); }
            if (f.From.HasValue) { var d = f.From.Value.Date; q = q.Where(x => x.SubmittedAt >= d); }
            if (f.To.HasValue) { var d = f.To.Value.Date.AddDays(1); q = q.Where(x => x.SubmittedAt < d); }
            return q;
        }

        public PublicApplicationStatus GetPublicStatus(string applicationNumber)
        {
            if (string.IsNullOrWhiteSpace(applicationNumber)) return null;
            AppRow row;
            using (var db = new SmcDbContext())
                row = Base(db).FirstOrDefault(x => x.ApplicationNumber == applicationNumber.Trim());
            if (row == null) return null;

            string stageLabel;
            string message;
            switch (row.Stage ?? string.Empty)
            {
                case ZoneStages.JEPending: stageLabel = "तपासणी प्रलंबित"; message = "आपला अर्ज प्राप्त झाला असून तपासणीसाठी पाठवला आहे."; break;
                case ZoneStages.JECorrectionRequested:
                case ZoneStages.OSCorrectionRequested: stageLabel = "अर्जदाराकडून दुरुस्ती आवश्यक"; message = "आपल्या अर्जात दुरुस्ती आवश्यक आहे. सुरक्षित अर्ज प्रवेश करून सूचना पहा."; break;
                case ZoneStages.OSPending: stageLabel = "कार्यालयीन तपासणी सुरू"; message = "आपला अर्ज कार्यालयीन तपासणीमध्ये आहे."; break;
                case ZoneStages.PaymentRequired: stageLabel = "शुल्क भरणा आवश्यक"; message = "आपल्या अर्जासाठी शुल्क भरणा आवश्यक आहे."; break;
                case ZoneStages.AssistantCommissionerApprovalPending: stageLabel = "मंजुरी प्रलंबित"; message = "आपला अर्ज मंजुरीसाठी पाठवला आहे."; break;
                case ZoneStages.Approved: stageLabel = "मंजूर"; message = "आपला अर्ज मंजूर झाला आहे."; break;
                case ZoneStages.Rejected: stageLabel = "नामंजूर"; message = "आपल्या अर्जावर निर्णय घेण्यात आला आहे."; break;
                default: stageLabel = LegacyStageLabel(row.Status); message = LegacyStatusMessage(row.Status); break;
            }
            return new PublicApplicationStatus { ApplicationNumber = row.ApplicationNumber, Service = row.ServiceType, CurrentStage = stageLabel, SubmittedAt = row.SubmittedAt, StatusMessage = message };
        }

        private static string LegacyStageLabel(string status)
        {
            switch (status)
            {
                case ApplicationStatuses.Approved: return "मंजूर";
                case ApplicationStatuses.Rejected: return "नामंजूर";
                case ApplicationStatuses.FeePending: return "शुल्क भरणा आवश्यक";
                case ApplicationStatuses.ApprovalPending: return "मंजुरी प्रलंबित";
                case ApplicationStatuses.UnderScrutiny: return "तपासणी सुरू";
                case ApplicationStatuses.Submitted: return "सादर केले";
                default: return "प्रक्रिया सुरू";
            }
        }

        private static string LegacyStatusMessage(string status)
        {
            switch (status)
            {
                case ApplicationStatuses.Approved: return "आपला अर्ज मंजूर झाला आहे.";
                case ApplicationStatuses.Rejected: return "आपल्या अर्जावर निर्णय घेण्यात आला आहे.";
                case ApplicationStatuses.FeePending: return "आपल्या अर्जासाठी शुल्क भरणा आवश्यक आहे.";
                case ApplicationStatuses.ApprovalPending: return "आपला अर्ज मंजुरीसाठी पाठवला आहे.";
                case ApplicationStatuses.UnderScrutiny: return "आपला अर्ज तपासणीमध्ये आहे.";
                default: return "आपला अर्ज प्राप्त झाला आहे आणि प्रक्रिया सुरू आहे.";
            }
        }

        public PagedResult<ApplicationListItem> Search(ApplicationFilter filter)
        {
            var page = Math.Max(1, filter.Page); var size = Math.Min(100, Math.Max(1, filter.PageSize));
            using (var db = new SmcDbContext())
            {
                var q = Apply(Base(db), filter);
                var rows = q.OrderByDescending(x => x.Id).Select(x => new { Row = x, Total = q.Count() })
                    .Skip((page - 1) * size).Take(size).ToList();
                var total = rows.Count == 0 ? 0 : rows[0].Total;
                if (rows.Count == 0 && page > 1)
                {
                    total = q.Count(); page = Math.Min(page, Math.Max(1, (total + size - 1) / size));
                    if (total > 0) rows = q.OrderByDescending(x => x.Id).Select(x => new { Row = x, Total = q.Count() })
                        .Skip((page - 1) * size).Take(size).ToList();
                }
                var serviceMap = MasterDataRepository.GetServiceMap();
                return new PagedResult<ApplicationListItem>
                {
                    Page = page, PageSize = size, Total = total,
                    Items = rows.Select(x => ToListItem(x.Row, serviceMap)).ToList()
                };
            }
        }

        public ApplicationDetail GetDetail(int applicationId)
        {
            using (var db = new SmcDbContext())
            {
                var rows = (from application in db.DemandApplications.AsNoTracking()
                            join zoneWorkflow in db.DemandApplicationZoneWorkflows.AsNoTracking().Where(x => !x.IsDeleted) on application.Id equals zoneWorkflow.DemandApplicationId into zoneRows
                            from zoneWorkflow in zoneRows.DefaultIfEmpty()
                            join legacyWorkflow in db.DemandApplicationWorkflows.AsNoTracking().Where(x => !x.IsDeleted) on application.Id equals legacyWorkflow.DemandApplicationId into legacyRows
                            from legacyWorkflow in legacyRows.DefaultIfEmpty()
                            join document in db.DemandApplicationDocuments.AsNoTracking().Where(x => !x.IsDeleted) on application.Id equals document.DemandApplicationId into documentRows
                            from document in documentRows.DefaultIfEmpty()
                            join zoneDocument in db.DemandApplicationZoneDocuments.AsNoTracking().Where(x => !x.IsDeleted) on zoneWorkflow.Id equals zoneDocument.DemandApplicationZoneWorkflowId into zoneDocumentRows
                            from zoneDocument in zoneDocumentRows.DefaultIfEmpty()
                            join audit in db.AuditLogs.AsNoTracking().Where(x => x.EntityName == "DemandApplication" || x.EntityName == "DemandApplicationWorkflow")
                                on (application.ProcessingRoute == ProcessingRoutes.Zone ? -1 : application.Id) equals audit.EntityId into auditRows
                            from audit in auditRows.DefaultIfEmpty()
                            where application.Id == applicationId && !application.IsDeleted
                            select new { Application = application, ZoneWorkflow = zoneWorkflow, LegacyWorkflow = legacyWorkflow, Document = document, ZoneDocument = zoneDocument, Audit = audit }).ToList();
                if (rows.Count == 0) return null;
                var a = rows[0].Application;
                ServiceItem serviceItem;
                var serviceMap = MasterDataRepository.GetServiceMap();
                var serviceName = serviceMap.TryGetValue(a.ServiceType ?? string.Empty, out serviceItem) ? serviceItem.NameMr : null;
                var d = new ApplicationDetail
                {
                    Id = a.Id, ApplicationNumber = a.ApplicationNumber, ProcessingRoute = a.ProcessingRoute, ServiceType = a.ServiceType, ServiceName = string.IsNullOrEmpty(serviceName) ? a.ServiceType : serviceName, BusinessType = a.BusinessType, OtherBusinessType = a.OtherBusinessType, ApplicantType = a.ApplicantType, ApplicantName = a.ApplicantName,
                    Mobile = a.Mobile, Email = a.Email, GstNumber = a.GstNumber, IdentityNumber = a.IdentityNumber, PanNumber = a.PanNumber, PermanentAddress = a.PermanentAddress, CorrespondenceAddress = a.CorrespondenceAddress, State = a.State,
                    District = a.District, City = a.City, Taluka = a.Taluka, PinCode = a.PinCode, Zone = a.Zone, Prabhag = a.Prabhag, Location = a.Location, ServiceDescription = a.ServiceDescription,
                    AvailableSpace = a.AvailableSpace, SpaceRequirement = a.SpaceRequirement, RequiredDuration = a.RequiredDuration, RequiredTime = a.RequiredTime,
                    ElectricityRequired = a.ElectricityRequired, WaterRequired = a.WaterRequired, OtherFacilities = a.OtherFacilities, WasteManagement = a.WasteManagement, OtherInformation = a.OtherInformation,
                    StartDate = a.StartDate, EndDate = a.EndDate, TotalDays = DemandPricingCalculator.TotalDays(a.StartDate, a.EndDate), LengthFt = a.LengthFt, WidthFt = a.WidthFt, AreaSqFt = a.AreaSqFt,
                    CalculatedRate = a.CalculatedRate, FeeAmount = a.FeeAmount, Status = a.Status, PaymentStatus = a.PaymentStatus, SubmittedAt = a.SubmittedAt ?? a.CreatedAt
                };
                var zoneWorkflowId = 0;
                if (a.ProcessingRoute == ProcessingRoutes.Zone)
                {
                    var z = rows[0].ZoneWorkflow;
                    if (z != null)
                    {
                        zoneWorkflowId = z.Id;
                        d.Stage = z.ZoneStage; d.PaymentStatus = z.PaymentStatus; d.PayableAmount = z.PayableAmount; d.PaymentReference = z.ReceiptNumber; d.PaymentDate = z.ReceiptDate; d.PaymentConfirmedAt = z.PaymentConfirmedAt; d.PaymentVerifier = z.ReceivedBy; d.CertificatePath = z.CertificatePath; d.ApprovedBy = z.ApprovedBy;
                        d.RowVersion = z.RowVersion == null ? null : Convert.ToBase64String(z.RowVersion);
                        d.History = ActionHistory.Parse(z.ActionHistoryJson).Select(h => new HistoryItem
                        {
                            Sequence = h.Sequence, AtUtc = h.AtUtc, Action = h.Action, FromRole = h.FromRole, ToRole = h.ToRole, PreviousStatus = h.PreviousStatus, NewStatus = h.NewStatus, Remarks = h.Remarks, By = h.ActionByUserName,
                            PaymentReference = h.PaymentReference, PaymentDate = SMC.BhoomiMalmatta.Business.Citizen.CitizenStatusMapper.ParsePaymentDate(h.PaymentDate)
                        }).ToList();
                    }
                }
                else
                {
                    var l = rows[0].LegacyWorkflow;
                    if (l != null) { d.Stage = l.Stage; d.PaymentStatus = l.PaymentStatus; d.PayableAmount = l.PayableAmount; d.PaymentReference = l.Utr; d.CertificatePath = l.CertificateFilePath; }
                    // Legacy applications have no JSON history; show the existing audit trail instead (read-only).
                    var seq = 1;
                    d.History = rows.Where(x => x.Audit != null).Select(x => x.Audit).GroupBy(x => x.Id).Select(x => x.First()).OrderBy(x => x.Timestamp)
                        .Select(x => new HistoryItem { Sequence = seq++, AtUtc = x.Timestamp, Action = x.Action, NewStatus = x.NewValue, By = x.UserName }).ToList();
                }
                var mainDocuments = rows.Where(x => x.Document != null).Select(x => x.Document).GroupBy(x => x.Id).Select(x => x.First()).OrderBy(x => x.Id)
                    .Select(x => new DocumentItem { Id = x.Id, DocumentType = x.DocumentType, FileName = x.FileName, ContentType = x.ContentType, SizeBytes = x.FileSizeBytes, VerificationStatus = x.VerificationStatus, RequestRemark = x.RequestRemark, CreatedAt = x.CreatedAt }).ToList();
                d.Documents = mainDocuments;
                if (a.ProcessingRoute == ProcessingRoutes.Zone && zoneWorkflowId > 0)
                    d.Documents.AddRange(rows.Where(x => x.ZoneDocument != null).Select(x => x.ZoneDocument).GroupBy(x => x.Id).Select(x => x.First()).OrderBy(x => x.Id)
                        .Select(x => new DocumentItem { Id = x.Id, DocumentType = x.Kind, FileName = x.FileName, ContentType = x.ContentType, SizeBytes = x.SizeBytes, CreatedAt = x.CreatedAt, IsZoneDocument = true }).ToList());
                return d;
            }
        }

        public int? FindIdByNumber(string applicationNumber)
        {
            using (var db = new SmcDbContext())
            {
                var id = db.DemandApplications.AsNoTracking().Where(a => a.ApplicationNumber == applicationNumber && !a.IsDeleted).Select(a => (int?)a.Id).FirstOrDefault();
                return id;
            }
        }

        public string GetRegisteredMobile(int applicationId)
        {
            using (var db = new SmcDbContext())
                return db.DemandApplications.AsNoTracking().Where(a => a.Id == applicationId && !a.IsDeleted).Select(a => a.Mobile).FirstOrDefault();
        }

        public string GetAccessTokenHash(int applicationId)
        {
            using (var db = new SmcDbContext()) return db.DemandApplications.AsNoTracking().Where(a => a.Id == applicationId).Select(a => a.ApplicantAccessTokenHash).FirstOrDefault();
        }

        public DashboardCounts GetDashboard(int? zoneScope)
        {
            using (var db = new SmcDbContext())
            {
                var q = Apply(Base(db), new ApplicationFilter { ZoneScope = zoneScope });
                return q.GroupBy(x => 1).Select(g => new DashboardCounts
                {
                    Total = g.Count(), Submitted = g.Count(x => x.Status == ApplicationStatuses.Submitted),
                    UnderScrutiny = g.Count(x => x.Status == ApplicationStatuses.UnderScrutiny),
                    FeePending = g.Count(x => x.Status == ApplicationStatuses.FeePending),
                    ApprovalPending = g.Count(x => x.Status == ApplicationStatuses.ApprovalPending),
                    Approved = g.Count(x => x.Status == ApplicationStatuses.Approved), Rejected = g.Count(x => x.Status == ApplicationStatuses.Rejected),
                    JePending = g.Count(x => x.Stage == ZoneStages.JEPending), OsPending = g.Count(x => x.Stage == ZoneStages.OSPending),
                    AcPending = g.Count(x => x.Stage == ZoneStages.AssistantCommissionerApprovalPending),
                    PaymentPending = g.Count(x => x.Stage == ZoneStages.PaymentRequired), Legacy = g.Count(x => x.Route != ProcessingRoutes.Zone)
                }).FirstOrDefault() ?? new DashboardCounts();
            }
        }

        private static string[] StagesFor(string role)
        {
            if (role == Roles.JE) return new[] { ZoneStages.JEPending };
            if (role == Roles.OS) return new[] { ZoneStages.OSPending, ZoneStages.PaymentRequired };
            if (role == Roles.AssistantCommissioner) return new[] { ZoneStages.AssistantCommissionerApprovalPending };
            return new string[0];
        }

        public OfficerDashboardCounts GetOfficerDashboardCounts(string role, int? zoneScope, int userId)
        {
            using (var db = new SmcDbContext())
            {
                var q = Apply(Base(db), new ApplicationFilter { ZoneScope = zoneScope });
                var pending = OfficerPendingPredicate(role);

                // Compose the role-specific predicate before EF sees the projection. The SQL has only
                // per-row integer flags followed by SUMs: no conditional Count or nested IQueryable.
                var flags = BuildOfficerKpiFlags(q, pending);
                var counts = BuildOfficerKpiAggregateQuery(flags).FirstOrDefault();

                // Materialize the small, distinct ID projection separately. Chunk Contains to keep
                // SQL Server below its parameter limit while retaining exact results for large histories.
                var processedIds = BuildOfficerProcessedIdQuery(db, userId, zoneScope).ToList();
                var processed = 0;
                for (var offset = 0; offset < processedIds.Count; offset += 1000)
                {
                    var batch = processedIds.Skip(offset).Take(1000).ToList();
                    processed += BuildOfficerProcessedCountQuery(q, batch).FirstOrDefault();
                }
                if (counts == null) counts = new OfficerDashboardCounts();
                counts.Processed = processed;
                return counts;
            }
        }

        /// <summary>
        /// "Pending with me" for the officer list (default view), the KPI "pending" counter and SearchPendingForRole. Only actionable rows:
        /// OS sees OSPending plus PaymentRequired rows whose payment the applicant has submitted (awaiting verification) — not unpaid ones;
        /// AC sees AC-pending rows whose payment is done or not required (the only rows ACApprove accepts). Plain comparisons only (EF6-translatable).
        /// </summary>
        internal static Expression<Func<AppRow, bool>> OfficerPendingPredicate(string role)
        {
            if (role == Roles.JE) return x => x.Route == ProcessingRoutes.Zone && x.Stage == ZoneStages.JEPending;
            if (role == Roles.OS) return x => x.Route == ProcessingRoutes.Zone &&
                (x.Stage == ZoneStages.OSPending || (x.Stage == ZoneStages.PaymentRequired && x.PaymentStatus == PaymentStatuses.PaymentSubmitted));
            if (role == Roles.AssistantCommissioner) return x => x.Route == ProcessingRoutes.Zone && x.Stage == ZoneStages.AssistantCommissionerApprovalPending &&
                (x.PaymentStatus == PaymentStatuses.PaymentDone || x.PaymentStatus == PaymentStatuses.NotRequired);
            var allStages = new[] { ZoneStages.JEPending, ZoneStages.OSPending, ZoneStages.PaymentRequired, ZoneStages.AssistantCommissionerApprovalPending };
            return x => x.Route == ProcessingRoutes.Zone && allStages.Contains(x.Stage);
        }

        private static bool IsWorkflowRole(string role) { return role == Roles.JE || role == Roles.OS || role == Roles.AssistantCommissioner; }

        /// <summary>The officer's own pending rows; roles without workflow actions have none.</summary>
        internal static IQueryable<AppRow> WherePendingFor(IQueryable<AppRow> q, string role)
        {
            if (IsWorkflowRole(role)) return q.Where(OfficerPendingPredicate(role));
            var none = StagesFor(role);
            return q.Where(x => none.Contains(x.Stage));
        }

        internal static IQueryable<OfficerDashboardCounts> BuildOfficerKpiAggregateQuery(IQueryable<OfficerKpiFlags> flags)
        {
            return flags.GroupBy(x => 1).Select(g => new OfficerDashboardCounts
                {
                    Total = g.Sum(x => x.Total), Pending = g.Sum(x => x.Pending),
                    Approved = g.Sum(x => x.Approved), Rejected = g.Sum(x => x.Rejected)
                });
        }

        internal static IQueryable<int> BuildOfficerProcessedIdQuery(SmcDbContext db, int userId, int? zoneScope)
        {
            var query = from l in db.AuditLogs.AsNoTracking()
                        join a in db.DemandApplications.AsNoTracking().Where(x => !x.IsDeleted) on l.EntityId equals a.Id
                        join z in db.DemandApplicationZoneWorkflows.AsNoTracking().Where(x => !x.IsDeleted) on a.Id equals z.DemandApplicationId
                        where l.UserId == userId && l.EntityName == "DemandApplicationZoneWorkflow"
                        select new { l.EntityId, z.Zone };
            if (zoneScope.HasValue) { var zone = zoneScope.Value; query = query.Where(x => x.Zone == zone); }
            return query.Select(x => x.EntityId).Distinct();
        }

        internal static IQueryable<int> BuildOfficerProcessedCountQuery(IQueryable<AppRow> source, IList<int> ids)
        {
            return source.Where(x => ids.Contains(x.Id)).Select(x => x.Id).Distinct()
                .GroupBy(x => 1).Select(g => g.Count());
        }

        internal static IQueryable<OfficerKpiFlags> BuildOfficerKpiFlags(IQueryable<AppRow> source, Expression<Func<AppRow, bool>> pending)
        {
            var p = Expression.Parameter(typeof(AppRow), "x");
            var pendingBody = new ReplaceParameterVisitor(pending.Parameters[0], p).Visit(pending.Body);
            Expression<Func<AppRow, bool>> approved = x => x.Status == ApplicationStatuses.Approved;
            Expression<Func<AppRow, bool>> rejected = x => x.Status == ApplicationStatuses.Rejected;
            var approvedBody = new ReplaceParameterVisitor(approved.Parameters[0], p).Visit(approved.Body);
            var rejectedBody = new ReplaceParameterVisitor(rejected.Parameters[0], p).Visit(rejected.Body);
            var init = Expression.MemberInit(Expression.New(typeof(OfficerKpiFlags)),
                Expression.Bind(typeof(OfficerKpiFlags).GetProperty("Total"), Expression.Constant(1)),
                Expression.Bind(typeof(OfficerKpiFlags).GetProperty("Pending"), Expression.Condition(pendingBody, Expression.Constant(1), Expression.Constant(0))),
                Expression.Bind(typeof(OfficerKpiFlags).GetProperty("Approved"), Expression.Condition(approvedBody, Expression.Constant(1), Expression.Constant(0))),
                Expression.Bind(typeof(OfficerKpiFlags).GetProperty("Rejected"), Expression.Condition(rejectedBody, Expression.Constant(1), Expression.Constant(0))));
            return source.Select(Expression.Lambda<Func<AppRow, OfficerKpiFlags>>(init, p));
        }

        public PagedResult<ApplicationListItem> SearchPendingForRole(string role, ApplicationFilter filter)
        {
            filter = filter ?? new ApplicationFilter();
            var page = Math.Max(1, filter.Page); var size = Math.Min(8, Math.Max(1, filter.PageSize));
            using (var db = new SmcDbContext())
            {
                var q = WherePendingFor(Apply(Base(db), filter), role);
                var rows = q.OrderByDescending(x => x.Id).Select(x => new { Row = x, Total = q.Count() }).Skip((page - 1) * size).Take(size).ToList();
                var total = rows.Count == 0 ? 0 : rows[0].Total;
                if (rows.Count == 0 && page > 1)
                {
                    total = q.Count(); page = Math.Min(page, Math.Max(1, (total + size - 1) / size));
                    if (total > 0) rows = q.OrderByDescending(x => x.Id).Select(x => new { Row = x, Total = q.Count() }).Skip((page - 1) * size).Take(size).ToList();
                }
                var serviceMap = MasterDataRepository.GetServiceMap();
                return new PagedResult<ApplicationListItem> { Page = page, PageSize = size, Total = total, Items = rows.Select(x => ToListItem(x.Row, serviceMap)).ToList() };
            }
        }

        /// <summary>Single server-paged list for officers. The browser-selected view never changes the signed-in user's zone scope.</summary>
        public PagedResult<ApplicationListItem> SearchOfficerApplications(string role, int userId, ApplicationFilter filter)
        {
            filter = filter ?? new ApplicationFilter();
            var page = Math.Max(1, filter.Page); var size = Math.Min(8, Math.Max(1, filter.PageSize));
            using (var db = new SmcDbContext())
            {
                var q = BuildOfficerApplicationsQuery(db, role, userId, filter);
                var rows = BuildOfficerPagedQuery(q, page, size).ToList();
                var total = rows.Count == 0 ? 0 : rows[0].Total;
                if (rows.Count == 0 && page > 1)
                {
                    total = q.Count(); page = Math.Min(page, Math.Max(1, (total + size - 1) / size));
                    if (total > 0) rows = BuildOfficerPagedQuery(q, page, size).ToList();
                }
                var serviceMap = MasterDataRepository.GetServiceMap();
                return new PagedResult<ApplicationListItem>
                {
                    Page = page, PageSize = size, Total = total,
                    Items = rows.Select(x => ToListItem(x.Row, serviceMap)).ToList()
                };
            }
        }

        internal static IQueryable<AppRow> BuildOfficerApplicationsQuery(SmcDbContext db, string role, int userId, ApplicationFilter filter)
        {
            filter = filter ?? new ApplicationFilter();
            var view = string.IsNullOrWhiteSpace(filter.OfficerView) ? OfficerListViews.Mine : filter.OfficerView;
            filter.Status = null;
            var q = Apply(Base(db), filter);
            if (view == OfficerListViews.Mine)
                q = WherePendingFor(q, role); // same predicate as the KPI "pending" counter
            else if (view == OfficerListViews.Processed)
            {
                var actedIds = db.AuditLogs.AsNoTracking()
                    .Where(x => x.UserId == userId && x.EntityName == "DemandApplicationZoneWorkflow")
                    .Select(x => x.EntityId).Distinct();
                q = q.Where(x => x.Route == ProcessingRoutes.Zone && actedIds.Contains(x.Id));
            }
            else if (view == OfficerListViews.Approved)
                q = q.Where(x => x.Status == ApplicationStatuses.Approved || x.Stage == ZoneStages.Approved);
            else if (view == OfficerListViews.Rejected)
                q = q.Where(x => x.Status == ApplicationStatuses.Rejected || x.Stage == ZoneStages.Rejected);
            else if (view == OfficerListViews.Correction)
                q = q.Where(x => x.Stage == ZoneStages.JECorrectionRequested || x.Stage == ZoneStages.OSCorrectionRequested);
            return q;
        }

        internal static IQueryable<OfficerPageRow> BuildOfficerPagedQuery(IQueryable<AppRow> q, int page, int size)
        {
            return q.OrderByDescending(x => x.Id).Select(x => new OfficerPageRow { Row = x, Total = q.Count() })
                .Skip((page - 1) * size).Take(size);
        }

        private static ApplicationListItem ToListItem(AppRow x, IDictionary<string, ServiceItem> serviceMap)
        { ServiceItem service; var serviceName = serviceMap != null && serviceMap.TryGetValue(x.ServiceType ?? string.Empty, out service) ? service.NameMr : x.ServiceType; return new ApplicationListItem { Id = x.Id, ApplicationNumber = x.ApplicationNumber, ServiceType = x.ServiceType, ServiceName = serviceName, BusinessType = x.BusinessType, OtherBusinessType = x.OtherBusinessType, ApplicantName = x.ApplicantName, Mobile = x.Mobile, Zone = x.ZoneNo.HasValue ? x.ZoneNo.Value.ToString() : x.Zone, Status = x.Status, Stage = x.Stage, PaymentStatus = x.PaymentStatus, FeeAmount = x.FeeAmount, SubmittedAt = x.SubmittedAt, ProcessingRoute = x.Route, RowVersion = x.RowVersion == null ? null : Convert.ToBase64String(x.RowVersion), PayableAmount = x.PayableAmount }; }

        private List<AppRow> Rows(int? zoneScope, DateTime? from, DateTime? to)
        {
            using (var db = new SmcDbContext()) return Apply(Base(db), new ApplicationFilter { ZoneScope = zoneScope, From = from, To = to }).ToList();
        }
        private static List<ReportRow> Group(IEnumerable<AppRow> rows, Func<AppRow, string> key)
        {
            return rows.GroupBy(key).Select(g => new ReportRow { Label = g.Key ?? "-", Count = g.Count(), Amount = g.Sum(x => x.FeeAmount ?? 0m) }).OrderByDescending(r => r.Count).ThenBy(r => r.Label).ToList();
        }
        public List<ReportRow> ReportByStatus(int? zoneScope, DateTime? from, DateTime? to) { return Group(Rows(zoneScope, from, to), x => x.Status); }
        public List<ReportRow> ReportByService(int? zoneScope, DateTime? from, DateTime? to) { return Group(Rows(zoneScope, from, to), x => x.ServiceType); }
        public List<ReportRow> ReportByZone(int? zoneScope, DateTime? from, DateTime? to) { return Group(Rows(zoneScope, from, to), x => x.ZoneNo.HasValue ? "झोन " + x.ZoneNo.Value : "Legacy / झोन नाही"); }
        public List<ReportRow> ReportByMonth(int? zoneScope, DateTime from, DateTime to)
        {
            return Rows(zoneScope, from, to).Where(x => x.SubmittedAt.HasValue).GroupBy(x => x.SubmittedAt.Value.Year * 100 + x.SubmittedAt.Value.Month).OrderBy(g => g.Key)
                .Select(g => new ReportRow { Label = (g.Key / 100) + "-" + (g.Key % 100).ToString("00"), Count = g.Count(), Amount = g.Sum(x => x.FeeAmount ?? 0m) }).ToList();
        }

        public PagedResult<AuditLogItem> SearchAudit(DateTime? from, DateTime? to, string action, string userName, int page, int pageSize)
        {
            page = Math.Max(1, page); pageSize = Math.Min(200, Math.Max(1, pageSize));
            using (var db = new SmcDbContext())
            {
                var q = db.AuditLogs.AsNoTracking().AsQueryable();
                if (from.HasValue) { var f = from.Value.Date; q = q.Where(x => x.Timestamp >= f); }
                if (to.HasValue) { var t = to.Value.Date.AddDays(1); q = q.Where(x => x.Timestamp < t); }
                if (!string.IsNullOrWhiteSpace(action)) { var a = action.Trim(); q = q.Where(x => x.Action.Contains(a)); }
                if (!string.IsNullOrWhiteSpace(userName)) { var u = userName.Trim(); q = q.Where(x => x.UserName.Contains(u)); }
                var total = q.Count();
                var items = q.OrderByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToList()
                    .Select(x => new AuditLogItem { Id = x.Id, Timestamp = x.Timestamp, UserName = x.UserName, Action = x.Action, EntityName = x.EntityName, EntityId = x.EntityId, OldValue = x.OldValue, NewValue = x.NewValue, IpAddress = x.IpAddress }).ToList();
                return new PagedResult<AuditLogItem> { Page = page, PageSize = pageSize, Total = total, Items = items };
            }
        }

        public List<UserListItem> GetUsers()
        {
            using (var db = new SmcDbContext())
                return db.Users.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.Username)
                    .Select(x => new UserListItem { Id = x.Id, Username = x.Username, FullName = x.FullName, Designation = x.Designation, Role = x.Role, ZoneNumber = x.ZoneNumber, IsActive = x.IsActive, LastLoginAt = x.LastLoginAt }).ToList();
        }

        public DocumentRecord GetDocument(int documentId)
        {
            using (var db = new SmcDbContext())
            {
                var q = from d in db.DemandApplicationDocuments.AsNoTracking()
                        where d.Id == documentId && !d.IsDeleted
                        join a in db.DemandApplications.AsNoTracking() on d.DemandApplicationId equals a.Id
                        select new DocumentRecord { Id = d.Id, ApplicationId = a.Id, ApplicationNumber = a.ApplicationNumber, DocumentType = d.DocumentType, FileName = d.FileName, ContentType = d.ContentType, RelativePath = d.FilePath, SizeBytes = d.FileSizeBytes };
                return q.FirstOrDefault();
            }
        }

        public DocumentRecord GetZoneDocument(int documentId)
        {
            using (var db = new SmcDbContext())
            {
                var q = from d in db.DemandApplicationZoneDocuments.AsNoTracking()
                        where d.Id == documentId && !d.IsDeleted && d.Kind == "PaymentProof"
                        join w in db.DemandApplicationZoneWorkflows.AsNoTracking() on d.DemandApplicationZoneWorkflowId equals w.Id
                        join a in db.DemandApplications.AsNoTracking() on w.DemandApplicationId equals a.Id
                        where !w.IsDeleted && !a.IsDeleted
                        select new DocumentRecord { Id = d.Id, ApplicationId = a.Id, ApplicationNumber = a.ApplicationNumber, DocumentType = d.Kind, FileName = d.FileName, ContentType = d.ContentType, RelativePath = d.FilePath, SizeBytes = d.SizeBytes };
                return q.FirstOrDefault();
            }
        }
    }
}
