using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SMC.BhoomiMalmatta.Models.Common;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Business.Abstractions
{
    public sealed class PropertyRef { public int Id { get; set; } public string Code { get; set; } public string Name { get; set; } public string Category { get; set; } public string Holder { get; set; } public string Label { get { return Code + " — " + Name; } } }
    public sealed class LeaseRef { public int Id { get; set; } public int? PropertyId { get; set; } public string Label { get; set; } }
    public sealed class RegisterDocument { public int Id { get; set; } public string FileName { get; set; } public string Kind { get; set; } public string ContentType { get; set; } public string RelativePath { get; set; } public long Size { get; set; } public DateTime CreatedAt { get; set; } }
    /// <summary>Property-linked registers: properties, allocations, transfers, calculations, recovery and schemes. Every write is audited in TR_AST_AuditLogs.</summary>
    public interface IRegisterStore
    {
        PagedResult<Property> SearchProperties(string search, string category, string status, int page, int size);
        Property GetProperty(int id);
        List<PropertyRef> PropertyRefs();
        List<string> PropertyCategories();
        Result<int> SaveProperty(Property p, ActorContext actor);

        PagedResult<AllocationProcess> SearchAllocations(string search, string status, int? propertyId, int page, int size);
        AllocationProcess GetAllocation(int id);
        Result<int> SaveAllocation(AllocationProcess a, ActorContext actor);

        PagedResult<GalaTransfer> SearchTransfers(string search, string stage, int? propertyId, int page, int size);
        GalaTransfer GetTransfer(int id);
        GalaTransferRate GetTransferRate(string category);
        Result<int> CreateTransfer(int propertyId, string transferType, string existingHolder, string newHolder, string remark, ActorContext actor);
        Result TransitionTransfer(int id, string action, byte[] version, string paymentReference, string remark, ActorContext actor);
        List<RegisterDocument> TransferDocuments(int transferId);
        Result AddTransferDocument(int transferId, string kind, StoredDocument file, ActorContext actor);
        RegisterDocument GetTransferDocument(int documentId, out int transferId);

        PagedResult<Calculation> SearchCalculations(int? propertyId, int page, int size);
        Calculation GetCalculation(int id);
        Result<int> SaveCalculation(Calculation c, ActorContext actor);

        PagedResult<RecoveryCase> SearchRecovery(string search, string stage, int? propertyId, int page, int size);
        RecoveryCase GetRecovery(int id);
        List<LeaseRef> LeaseRefs();
        Result<int> SaveRecovery(RecoveryCase r, ActorContext actor);

        PagedResult<SchemeApplication> SearchSchemes(string search, string status, int? propertyId, int page, int size);
        SchemeApplication GetScheme(int id);
        Result<int> SaveScheme(SchemeApplication s, ActorContext actor);

        List<RegisterDocument> EntityDocuments(string entityType, int entityId);
        Result AddEntityDocument(string entityType, int entityId, StoredDocument file, ActorContext actor);
        RegisterDocument GetEntityDocument(int documentId);
        List<AuditLogItem> EntityAudit(string entityName, int entityId, int max);
    }

    /// <summary>Validation and business rules for the registers (pure functions, unit-tested).</summary>
    public static class RegisterRules
    {
        public const string MobileMessage = "मोबाइल क्रमांक १० अंकी आणि ६, ७, ८ किंवा ९ ने सुरू असावा.";
        private static readonly Regex Mobile = new Regex("^[6-9][0-9]{9}$", RegexOptions.CultureInvariant);

        public static bool IsValidMobile(string m) { return m != null && Mobile.IsMatch(m.Trim()); }
        public static string Trim(string s) { return string.IsNullOrWhiteSpace(s) ? null : s.Trim(); }
        private static void Max(List<string> e, string value, int max, string label) { if (value != null && value.Length > max) e.Add(label + " जास्तीत जास्त " + max + " अक्षरे असावे."); }

        public static List<string> ValidateProperty(Property p)
        {
            var e = new List<string>();
            if (Array.IndexOf(PropertyCategories.All, p.Category) < 0) e.Add("मालमत्ता प्रकार निवडा.");
            if (string.IsNullOrWhiteSpace(p.Name)) e.Add("मालमत्तेचे नाव / वर्णन आवश्यक आहे.");
            if (Array.IndexOf(PropertyStatuses.All, p.Status) < 0) e.Add("स्थिती निवडा.");
            if (p.AreaSqFt.HasValue && p.AreaSqFt.Value < 0) e.Add("क्षेत्रफळ ऋण असू शकत नाही.");
            if (p.MonthlyRent < 0 || p.AnnualDemand < 0) e.Add("भाडे / मागणी ऋण असू शकत नाही.");
            Max(e, p.Name, 250, "नाव"); Max(e, p.Ward, 50, "प्रभाग"); Max(e, p.Zone, 50, "झोन"); Max(e, p.Address, 500, "पत्ता"); Max(e, p.SurveyNumber, 50, "सर्वे क्रमांक");
            Max(e, p.TpNumber, 50, "TP क्रमांक"); Max(e, p.CurrentOccupant, 200, "धारक"); Max(e, p.Shera, 2000, "शेरा");
            return e;
        }

        public static List<string> ValidateAllocation(AllocationProcess a)
        {
            var e = new List<string>();
            if (!a.PropertyId.HasValue) e.Add("मालमत्ता निवडा.");
            if (Array.IndexOf(AllocationMethods.All, a.Method) < 0) e.Add("पद्धत निवडा.");
            if (Array.IndexOf(AllocationStatuses.All, a.Status) < 0) e.Add("स्थिती निवडा.");
            if (a.PublishDate == default(DateTime)) e.Add("प्रसिद्धी दिनांक आवश्यक आहे.");
            if (a.LastDateToApply.HasValue && a.LastDateToApply.Value.Date < a.PublishDate.Date) e.Add("अंतिम दिनांक प्रसिद्धी दिनांकापूर्वी असू शकत नाही.");
            if (a.AuctionDate.HasValue && a.LastDateToApply.HasValue && a.AuctionDate.Value.Date < a.LastDateToApply.Value.Date) e.Add("लिलाव / उघडण्याचा दिनांक अंतिम दिनांकापूर्वी असू शकत नाही.");
            if (a.ReserveAmount.HasValue && a.ReserveAmount.Value < 0) e.Add("राखीव किंमत ऋण असू शकत नाही.");
            if (a.HighestBidAmount.HasValue && a.HighestBidAmount.Value < 0) e.Add("सर्वोच्च बोली ऋण असू शकत नाही.");
            if (a.HighestBidAmount.HasValue && a.ReserveAmount.HasValue && a.HighestBidAmount.Value < a.ReserveAmount.Value && a.Status == AllocationStatuses.Awarded) e.Add("राखीव किमतीपेक्षा कमी बोलीवर वाटप करता येत नाही.");
            if (!string.IsNullOrWhiteSpace(a.HighestBidderMobile) && !IsValidMobile(a.HighestBidderMobile)) e.Add(MobileMessage);
            if (a.Status == AllocationStatuses.Awarded && (string.IsNullOrWhiteSpace(a.HighestBidderName) || !a.HighestBidAmount.HasValue)) e.Add("वाटपासाठी सर्वोच्च बोलीदाराचे नाव व रक्कम आवश्यक आहे.");
            Max(e, a.NoticeNumber, 100, "जाहिरात / निविदा क्रमांक"); Max(e, a.HighestBidderName, 200, "बोलीदार"); Max(e, a.Shera, 2000, "शेरा");
            return e;
        }

        public static List<string> ValidateRecovery(RecoveryCase r, RecoveryCase existing)
        {
            var e = new List<string>();
            if (!r.PropertyId.HasValue) e.Add("मालमत्ता निवडा.");
            if (Array.IndexOf(RecoveryStages.All, r.Stage) < 0) e.Add("टप्पा निवडा.");
            if (r.OutstandingAmount < 0 || r.RecoveredAmount < 0) e.Add("रक्कम ऋण असू शकत नाही.");
            if (r.MonthsOverdue < 0) e.Add("थकीत महिने ऋण असू शकत नाही.");
            if (r.RecoveredAmount > r.OutstandingAmount) e.Add("वसूल रक्कम थकबाकीपेक्षा जास्त असू शकत नाही.");
            if (r.RecoveredAmount > 0 && !r.RecoveryDate.HasValue) e.Add("वसुली दिनांक आवश्यक आहे.");
            if (r.NoticeDate.HasValue && r.RecoveryDate.HasValue && r.RecoveryDate.Value.Date < r.NoticeDate.Value.Date) e.Add("वसुली दिनांक नोटीस दिनांकापूर्वी असू शकत नाही.");
            if (r.Stage == RecoveryStages.NoticeIssued && (string.IsNullOrWhiteSpace(r.NoticeNumber) || !r.NoticeDate.HasValue)) e.Add("नोटीस क्रमांक व दिनांक आवश्यक आहे.");
            if (r.Stage == RecoveryStages.Sealed && !r.SealDate.HasValue) e.Add("सील दिनांक आवश्यक आहे.");
            if (r.Stage == RecoveryStages.ReAuction && !r.ReAuctionDate.HasValue) e.Add("पुनर्लिलाव दिनांक आवश्यक आहे.");
            if (r.Stage == RecoveryStages.Recovered && r.RecoveredAmount < r.OutstandingAmount) e.Add("‘वसूल’ करण्यासाठी संपूर्ण थकबाकी वसूल असणे आवश्यक आहे.");
            if (existing != null && RecoveryStages.IsClosed(existing.Stage)) e.Add("बंद केलेली वसुली प्रकरणे बदलता येत नाहीत.");
            if (existing != null && r.RecoveredAmount < existing.RecoveredAmount) e.Add("वसूल रक्कम कमी करता येत नाही.");
            Max(e, r.NoticeNumber, 100, "नोटीस क्रमांक"); Max(e, r.Shera, 2000, "शेरा");
            return e;
        }

        public static List<string> ValidateScheme(SchemeApplication s)
        {
            var e = new List<string>();
            if (!s.PropertyId.HasValue) e.Add("मालमत्ता निवडा.");
            if (string.IsNullOrWhiteSpace(s.SchemeType)) e.Add("उपक्रमाचे नाव आवश्यक आहे.");
            if (string.IsNullOrWhiteSpace(s.ApplicantName)) e.Add("अर्जदाराचे नाव आवश्यक आहे.");
            if (!string.IsNullOrWhiteSpace(s.ApplicantMobile) && !IsValidMobile(s.ApplicantMobile)) e.Add(MobileMessage);
            if (Array.IndexOf(SchemeStatuses.All, s.Status) < 0) e.Add("स्थिती निवडा.");
            if (s.OriginalOutstanding < 0 || s.WaivedAmount < 0) e.Add("रक्कम ऋण असू शकत नाही.");
            if (s.WaivedAmount > s.OriginalOutstanding) e.Add("माफ रक्कम मूळ थकबाकीपेक्षा जास्त असू शकत नाही.");
            if (s.DecisionDate.HasValue && s.DecisionDate.Value.Date < s.ApplicationDate.Date) e.Add("निर्णय दिनांक अर्ज दिनांकापूर्वी असू शकत नाही.");
            Max(e, s.SchemeType, 30, "उपक्रमाचे नाव"); Max(e, s.ApplicantName, 200, "अर्जदार"); Max(e, s.Shera, 2000, "शेरा");
            return e;
        }
    }

    public sealed class CalculationOutcome
    {
        public decimal Rate { get; set; }
        public int Months { get; set; }
        public decimal Previous { get; set; }
        public decimal Current { get; set; }
        public decimal Total { get; set; }
        public string Formula { get; set; }
    }

    /// <summary>Rent calculation: current demand = configured monthly rent of the property x months; total = current demand + previous outstanding.</summary>
    public static class RentCalculator
    {
        public const int MaxMonths = 120;
        public static Result<CalculationOutcome> Calculate(decimal monthlyRent, int months, decimal previousOutstanding)
        {
            if (monthlyRent <= 0) return Result.Failure<CalculationOutcome>(new Error("NO_RATE", "या मालमत्तेसाठी मासिक भाडे निश्चित केलेले नाही; गणना करता येत नाही."));
            if (months < 1 || months > MaxMonths) return Result.Failure<CalculationOutcome>(new Error("PERIOD", "कालावधी १ ते " + MaxMonths + " महिने असावा."));
            if (previousOutstanding < 0) return Result.Failure<CalculationOutcome>(new Error("PREV", "मागील थकबाकी ऋण असू शकत नाही."));
            var current = Math.Round(monthlyRent * months, 2, MidpointRounding.AwayFromZero);
            var total = current + previousOutstanding;
            return Result.Success(new CalculationOutcome
            {
                Rate = monthlyRent, Months = months, Previous = previousOutstanding, Current = current, Total = total,
                Formula = "चालू मागणी = मासिक भाडे " + monthlyRent.ToString("N2") + " × " + months + " महिने = " + current.ToString("N2") + "; एकूण = चालू मागणी + मागील थकबाकी " + previousOutstanding.ToString("N2") + " = " + total.ToString("N2")
            });
        }
    }

    public sealed class TransferStep { public string Action { get; set; } public string From { get; set; } public string To { get; set; } public string[] Roles { get; set; } public string AuditLabel { get; set; } public string ButtonLabel { get; set; } }

    public static class TransferRules
    {
        private static readonly string[] Writers = { Roles.Admin, Roles.JE, Roles.OS, Roles.AssistantCommissioner };
        public static readonly TransferStep[] Steps =
        {
            new TransferStep { Action = "submit", From = TransferStages.Draft, To = TransferStages.Submitted, Roles = Writers, AuditLabel = "Transfer Submitted", ButtonLabel = "अर्ज सादर करा" },
            new TransferStep { Action = "review", From = TransferStages.Submitted, To = TransferStages.Reviewed, Roles = new[] { Roles.Admin, Roles.JE }, AuditLabel = "Transfer Reviewed", ButtonLabel = "पुनरावलोकन पूर्ण / शुल्क मागवा" },
            new TransferStep { Action = "pay", From = TransferStages.Reviewed, To = TransferStages.PaymentVerified, Roles = new[] { Roles.Admin, Roles.OS }, AuditLabel = "Transfer Payment Verified", ButtonLabel = "शुल्क भरणा पडताळा" },
            new TransferStep { Action = "approve", From = TransferStages.PaymentVerified, To = TransferStages.Approved, Roles = new[] { Roles.Admin, Roles.AssistantCommissioner }, AuditLabel = "Transfer Approved", ButtonLabel = "अंतिम मंजुरी" },
            new TransferStep { Action = "reject", From = TransferStages.Submitted, To = TransferStages.Rejected, Roles = new[] { Roles.Admin, Roles.JE }, AuditLabel = "Transfer Rejected", ButtonLabel = "नाकारा" },
            new TransferStep { Action = "reject", From = TransferStages.Reviewed, To = TransferStages.Rejected, Roles = new[] { Roles.Admin, Roles.OS }, AuditLabel = "Transfer Rejected", ButtonLabel = "नाकारा" },
            new TransferStep { Action = "reject", From = TransferStages.PaymentVerified, To = TransferStages.Rejected, Roles = new[] { Roles.Admin, Roles.AssistantCommissioner }, AuditLabel = "Transfer Rejected", ButtonLabel = "नाकारा" },
        };

        public static TransferStep Find(string stage, string action)
        {
            foreach (var s in Steps) if (s.From == stage && s.Action == action) return s;
            return null;
        }

        public static List<TransferStep> Available(string stage, string role)
        {
            var l = new List<TransferStep>();
            foreach (var s in Steps) if (s.From == stage && Array.IndexOf(s.Roles, role) >= 0) l.Add(s);
            return l;
        }

        public static bool CanCreate(string role) { return Array.IndexOf(Writers, role) >= 0; }

        /// <summary>Fee and penalty come from the active MR_AST_GalaTransferRates row; the penalty applies only to breach transfers.</summary>
        public static Result<decimal[]> Fees(GalaTransferRate rate, bool isBreach)
        {
            if (rate == null || !rate.IsActive) return Result.Failure<decimal[]>(new Error("NO_RATE", "या प्रकारासाठी हस्तांतरण शुल्क निश्चित केलेले नाही."));
            return Result.Success(new[] { rate.TransferFee, isBreach ? rate.BreachPenalty : 0m });
        }
    }
}
