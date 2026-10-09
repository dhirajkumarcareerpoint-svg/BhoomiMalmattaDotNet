using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SMC.BhoomiMalmatta.Business.Reports;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;

namespace SMC.BhoomiMalmatta.Web.Services
{
    public sealed class ReportTable
    {
        public string Title { get; set; }
        public string[] Headers { get; set; }
        public List<string[]> Rows { get; set; }
        public string ToCsv() { return CsvWriter.Write(Headers, Rows); }
    }

    /// <summary>One implementation for the Reports page, the CSV export and the API.</summary>
    public static class ReportBuilder
    {
        private static readonly KeyValuePair<string, string>[] ApplicationCatalog =
        {
            new KeyValuePair<string, string>("status", "स्थितीनुसार अर्ज"), new KeyValuePair<string, string>("service", "सेवेनुसार अर्ज"),
            new KeyValuePair<string, string>("zone", "झोननुसार अर्ज"), new KeyValuePair<string, string>("month", "दिनांक कालावधीनुसार (महिना)"),
            new KeyValuePair<string, string>("je-pending", "JE कडे प्रलंबित"), new KeyValuePair<string, string>("os-pending", "OS कडे प्रलंबित"),
            new KeyValuePair<string, string>("ac-pending", "सहाय्यक आयुक्तांकडे प्रलंबित"), new KeyValuePair<string, string>("payment-pending", "शुल्क भरणा प्रलंबित"),
            new KeyValuePair<string, string>("approved", "मंजूर अर्ज"), new KeyValuePair<string, string>("rejected", "नाकारलेले अर्ज"),
        };
        private static readonly KeyValuePair<string, string>[] RegisterCatalog =
        {
            new KeyValuePair<string, string>("prop-category", "मालमत्ता: प्रकार / स्थितीनुसार सारांश"), new KeyValuePair<string, string>("prop-list", "मालमत्ता यादी"),
            new KeyValuePair<string, string>("alloc-list", "देण्याची कार्यपद्धती यादी"), new KeyValuePair<string, string>("transfer-list", "हस्तांतरण अहवाल"),
            new KeyValuePair<string, string>("calc-list", "Calculation यादी"), new KeyValuePair<string, string>("recovery-summary", "वसुली / महसूल: टप्प्यानुसार सारांश"),
            new KeyValuePair<string, string>("recovery-list", "वसुली प्रकरणे यादी"), new KeyValuePair<string, string>("scheme-list", "विविध उपक्रम यादी"),
            new KeyValuePair<string, string>("audit-list", "Audit इतिहास अहवाल"),
        };
        public static readonly KeyValuePair<string, string>[] Catalog = ApplicationCatalog.Concat(RegisterCatalog).ToArray();

        public static KeyValuePair<string, string>[] CatalogForRole(string role)
        { return role == Roles.Admin ? Catalog : ApplicationCatalog; }

        public static bool IsApplicationReport(string name)
        { return ApplicationCatalog.Any(x => x.Key == name); }
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int Cap = 5000;

        public static ReportTable Build(string name, int? zoneScope, DateTime? from, DateTime? to, string role)
        {
            var catalog = CatalogForRole(role);
            var title = catalog.Where(c => c.Key == name).Select(c => c.Value).FirstOrDefault();
            if (title == null) return null;
            var q = AppServices.Query;
            switch (name)
            {
                case "status": return Aggregate(title, "स्थिती", q.ReportByStatus(zoneScope, from, to), role == Roles.Admin);
                case "service": return Aggregate(title, "सेवा", q.ReportByService(zoneScope, from, to), role == Roles.Admin);
                case "zone": return Aggregate(title, "झोन", q.ReportByZone(zoneScope, from, to), role == Roles.Admin);
                case "month":
                    return Aggregate(title, "महिना", q.ReportByMonth(zoneScope, from ?? DateTime.Today.AddMonths(-12), to ?? DateTime.Today), role == Roles.Admin);
            }
            var reg = role == Roles.Admin ? RegisterReport(name, title, from, to) : null;
            if (reg != null) return reg;
            var f = new ApplicationFilter { ZoneScope = zoneScope, From = from, To = to, PageSize = 100 };
            switch (name)
            {
                case "je-pending": f.Stage = ZoneStages.JEPending; break;
                case "os-pending": f.Stage = ZoneStages.OSPending; break;
                case "ac-pending": f.Stage = ZoneStages.AssistantCommissionerApprovalPending; break;
                case "payment-pending": f.Stage = ZoneStages.PaymentRequired; break;
                case "approved": f.Status = ApplicationStatuses.Approved; break;
                case "rejected": f.Status = ApplicationStatuses.Rejected; break;
            }
            var rows = new List<string[]>();
            for (var page = 1; rows.Count < Cap; page++)
            {
                f.Page = page;
                var r = q.Search(f);
                rows.AddRange(r.Items.Select(x => new[] { x.ApplicationNumber, x.ApplicantName, x.ServiceType, x.Zone, x.Status, x.Stage, x.PaymentStatus,
                    x.FeeAmount.HasValue ? x.FeeAmount.Value.ToString("0.00", Inv) : "", x.SubmittedAt.HasValue ? x.SubmittedAt.Value.ToString("yyyy-MM-dd", Inv) : "", x.ProcessingRoute }));
                if (rows.Count >= r.Total || r.Items.Count == 0) break;
            }
            var admin = role == Roles.Admin;
            if (!admin) rows = rows.Select(x => x.Take(7).Concat(x.Skip(8)).ToArray()).ToList(); // omit fee amount from officer reports
            return new ReportTable { Title = title, Headers = admin ? new[] { "अर्ज क्रमांक", "अर्जदार", "सेवा", "झोन", "स्थिती", "स्तर", "शुल्क स्थिती", "रक्कम", "दिनांक", "मार्ग" } : new[] { "अर्ज क्रमांक", "अर्जदार", "सेवा", "झोन", "स्थिती", "स्तर", "शुल्क स्थिती", "दिनांक", "मार्ग" }, Rows = rows };
        }

        private static List<T> AllPages<T>(Func<int, SMC.BhoomiMalmatta.Models.Dto.PagedResult<T>> fetch)
        {
            var all = new List<T>();
            for (var page = 1; all.Count < Cap; page++)
            {
                var r = fetch(page);
                all.AddRange(r.Items);
                if (all.Count >= r.Total || r.Items.Count == 0) break;
            }
            return all;
        }

        private static bool InRange(DateTime created, DateTime? from, DateTime? to)
        {
            return (!from.HasValue || created >= from.Value.Date) && (!to.HasValue || created < to.Value.Date.AddDays(1));
        }

        private static string Dec(decimal? v) { return v.HasValue ? v.Value.ToString("0.00", Inv) : ""; }
        private static string Day(DateTime? d) { return d.HasValue ? d.Value.ToString("yyyy-MM-dd", Inv) : ""; }

        /// <summary>Reports over the property-linked registers; the date filter applies to the record creation date. Returns null for non-register report names.</summary>
        private static ReportTable RegisterReport(string name, string title, DateTime? from, DateTime? to)
        {
            var reg = AppServices.Register;
            var props = reg.PropertyRefs().ToDictionary(p => p.Id, p => p.Code);
            Func<int?, string> pc = id => id.HasValue && props.ContainsKey(id.Value) ? props[id.Value] : "";
            switch (name)
            {
                case "prop-category":
                {
                    var rows = AllPages(p => reg.SearchProperties(null, null, null, p, 200)).Where(x => InRange(x.CreatedAt, from, to));
                    return new ReportTable { Title = title, Headers = new[] { "प्रकार", "स्थिती", "संख्या", "मासिक भाडे (₹)", "वार्षिक मागणी (₹)" },
                        Rows = rows.GroupBy(x => x.Category + "|" + x.Status).OrderBy(g => g.Key).Select(g => new[] { PropertyLabels.Category(g.First().Category), PropertyLabels.Status(g.First().Status), g.Count().ToString(), g.Sum(x => x.MonthlyRent).ToString("0.00", Inv), g.Sum(x => x.AnnualDemand).ToString("0.00", Inv) }).ToList() };
                }
                case "prop-list":
                    return new ReportTable { Title = title, Headers = new[] { "क्रमांक", "प्रकार", "नाव", "प्रभाग", "झोन", "TP क्रमांक", "धारक", "स्थिती", "मासिक भाडे (₹)", "वार्षिक मागणी (₹)" },
                        Rows = AllPages(p => reg.SearchProperties(null, null, null, p, 200)).Where(x => InRange(x.CreatedAt, from, to)).Select(x => new[] { x.PropertyCode, PropertyLabels.Category(x.Category), x.Name, x.Ward, x.Zone, x.TpNumber, x.CurrentOccupant, PropertyLabels.Status(x.Status), Dec(x.MonthlyRent), Dec(x.AnnualDemand) }).ToList() };
                case "alloc-list":
                    return new ReportTable { Title = title, Headers = new[] { "जाहिरात क्रमांक", "मालमत्ता", "पद्धत", "प्रसिद्धी", "अंतिम दिनांक", "कार्यक्रम दिनांक", "राखीव (₹)", "सर्वोच्च बोली (₹)", "बोलीदार", "मोबाईल", "स्थिती" },
                        Rows = AllPages(p => reg.SearchAllocations(null, null, null, p, 200)).Where(x => InRange(x.CreatedAt, from, to)).Select(x => new[] { x.NoticeNumber, pc(x.PropertyId), x.Method, Day(x.PublishDate), Day(x.LastDateToApply), Day(x.AuctionDate), Dec(x.ReserveAmount), Dec(x.HighestBidAmount), x.HighestBidderName, x.HighestBidderMobile, x.Status }).ToList() };
                case "transfer-list":
                    return new ReportTable { Title = title, Headers = new[] { "क्र.", "मालमत्ता", "पूर्वीचा धारक", "नवीन धारक", "प्रकार", "शुल्क (₹)", "दंड (₹)", "भरणा संदर्भ", "टप्पा", "मंजुरी दिनांक" },
                        Rows = AllPages(p => reg.SearchTransfers(null, null, null, p, 200)).Where(x => InRange(x.CreatedAt, from, to)).Select(x => new[] { x.Id.ToString(), pc(x.PropertyId), x.ExistingHolder, x.NewHolder, x.TransferType, Dec(x.TransferFee), Dec(x.Penalty), x.PaymentReference, x.Stage, Day(x.ApprovedAt) }).ToList() };
                case "calc-list":
                    return new ReportTable { Title = title, Headers = new[] { "क्र.", "मालमत्ता", "मासिक भाडे (₹)", "महिने", "मागील थकबाकी (₹)", "चालू मागणी (₹)", "एकूण (₹)", "दिनांक" },
                        Rows = AllPages(p => reg.SearchCalculations(null, p, 200)).Where(x => InRange(x.CreatedAt, from, to)).Select(x => new[] { x.Id.ToString(), pc(x.PropertyId), Dec(x.Rate), x.PeriodMonths.ToString(), Dec(x.PreviousOutstanding), Dec(x.CurrentDemand), Dec(x.TotalAmount), Day(x.CalculationDate) }).ToList() };
                case "recovery-summary":
                {
                    var rows = AllPages(p => reg.SearchRecovery(null, null, null, p, 200)).Where(x => InRange(x.CreatedAt, from, to));
                    return new ReportTable { Title = title, Headers = new[] { "टप्पा", "प्रकरणे", "थकबाकी (₹)", "वसूल (₹)", "शिल्लक (₹)" },
                        Rows = rows.GroupBy(x => x.Stage).OrderBy(g => g.Key).Select(g => new[] { g.Key, g.Count().ToString(), g.Sum(x => x.OutstandingAmount).ToString("0.00", Inv), g.Sum(x => x.RecoveredAmount).ToString("0.00", Inv), g.Sum(x => x.OutstandingAmount - x.RecoveredAmount).ToString("0.00", Inv) }).ToList() };
                }
                case "recovery-list":
                    return new ReportTable { Title = title, Headers = new[] { "क्र.", "मालमत्ता", "थकीत महिने", "थकबाकी (₹)", "वसूल (₹)", "शिल्लक (₹)", "टप्पा", "नोटीस क्रमांक", "नोटीस दिनांक", "वसुली दिनांक" },
                        Rows = AllPages(p => reg.SearchRecovery(null, null, null, p, 200)).Where(x => InRange(x.CreatedAt, from, to)).Select(x => new[] { x.Id.ToString(), pc(x.PropertyId), x.MonthsOverdue.ToString(), Dec(x.OutstandingAmount), Dec(x.RecoveredAmount), Dec(x.OutstandingAmount - x.RecoveredAmount), x.Stage, x.NoticeNumber, Day(x.NoticeDate), Day(x.RecoveryDate) }).ToList() };
                case "scheme-list":
                    return new ReportTable { Title = title, Headers = new[] { "उपक्रम", "मालमत्ता", "अर्जदार", "मोबाईल", "अर्ज दिनांक", "मूळ थकबाकी (₹)", "माफ (₹)", "देय (₹)", "स्थिती", "निर्णय दिनांक" },
                        Rows = AllPages(p => reg.SearchSchemes(null, null, null, p, 200)).Where(x => InRange(x.CreatedAt, from, to)).Select(x => new[] { x.SchemeType, pc(x.PropertyId), x.ApplicantName, x.ApplicantMobile, Day(x.ApplicationDate), Dec(x.OriginalOutstanding), Dec(x.WaivedAmount), Dec(x.PayableAmount), x.Status, Day(x.DecisionDate) }).ToList() };
                case "audit-list":
                    return new ReportTable { Title = title, Headers = new[] { "दिनांक / वेळ", "वापरकर्ता", "कृती", "घटक", "ID", "जुने मूल्य", "नवे मूल्य" },
                        Rows = AllPages(p => AppServices.Query.SearchAudit(from, to, null, null, p, 200)).Select(x => new[] { x.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", Inv), x.UserName, x.Action, x.EntityName, x.EntityId.ToString(), x.OldValue, x.NewValue }).ToList() };
            }
            return null;
        }

        private static ReportTable Aggregate(string title, string labelHeader, List<ReportRow> rows, bool includeAmount = true)
        {
            return new ReportTable { Title = title, Headers = includeAmount ? new[] { labelHeader, "अर्ज संख्या", "रक्कम (₹)" } : new[] { labelHeader, "अर्ज संख्या" },
                Rows = includeAmount ? rows.Select(r => new[] { r.Label, r.Count.ToString(), r.Amount.ToString("0.00", Inv) }).ToList() : rows.Select(r => new[] { r.Label, r.Count.ToString() }).ToList() };
        }
    }
}
