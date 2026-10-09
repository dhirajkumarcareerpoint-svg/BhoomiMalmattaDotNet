namespace SMC.BhoomiMalmatta.Models.Constants
{
    /// <summary>Values marked (verified) exist in the live data; the others are new values for free-text columns that have no check constraint.</summary>
    /// <summary>
    /// LEGACY ALIAS MAP ONLY - not an option list. The stored codes of the two original categories and their PropertyCode prefixes (MJ / MN).
    /// Every dropdown, label and validation of the Properties page comes from PropertyMasterOptions (MR_AST_MasterData + values in use).
    /// <see cref="All"/> is kept only for the legacy RegisterRules.ValidateProperty and its existing tests.
    /// </summary>
    public static class PropertyCategories
    {
        public const string Major = "MajorGaale";   // (verified) code prefix MJ
        public const string Mini = "MiniGaale";     // (verified) code prefix MN
        public static readonly string[] All = { Major, Mini };
        public static string Prefix(string v) { return v == Major ? "MJ" : v == Mini ? "MN" : null; }
    }

    /// <summary>LEGACY ALIAS MAP ONLY - the two verified stored status codes (labels come from PropertyMasterOptions).</summary>
    public static class PropertyStatuses
    {
        public const string Rented = "Bhadyane";    // (verified)
        public const string Vacant = "Rikamy";      // (verified)
        public static readonly string[] All = { Rented, Vacant };
    }

    public static class AllocationMethods
    {
        public const string Auction = "SarvajanikLilaw"; // (verified)
        public const string Tender = "Nividha";
        public const string Allocation = "Vatap";
        public static readonly string[] All = { Auction, Tender, Allocation };
        public static string Label(string v) { return v == Auction ? "सार्वजनिक लिलाव" : v == Tender ? "निविदा" : v == Allocation ? "वाटप" : v; }
    }

    public static class AllocationStatuses
    {
        public const string Advertised = "JahirNamaPrasiddh"; // (verified)
        public const string BidReceived = "BidReceived";
        public const string Awarded = "Awarded";
        public const string Cancelled = "Cancelled";
        public static readonly string[] All = { Advertised, BidReceived, Awarded, Cancelled };
        public static string Label(string v) { return v == Advertised ? "जाहिरनामा प्रसिद्ध" : v == BidReceived ? "बोली प्राप्त" : v == Awarded ? "मंजूर / वाटप" : v == Cancelled ? "रद्द" : v; }
    }

    public static class TransferTypes
    {
        public const string Regular = "Regular"; // (verified)
        public const string Breach = "Breach";
        public static readonly string[] All = { Regular, Breach };
        public static string Label(string v) { return v == Regular ? "नियमित हस्तांतरण" : v == Breach ? "उल्लंघनासह हस्तांतरण" : v; }
    }

    public static class TransferStages
    {
        public const string Draft = "Draft";                 // (verified)
        public const string Submitted = "Submitted";         // request filed, JE review pending
        public const string Reviewed = "Reviewed";           // JE accepted, payment pending
        public const string PaymentVerified = "PaymentVerified"; // OS verified payment, AC approval pending
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public static readonly string[] All = { Draft, Submitted, Reviewed, PaymentVerified, Approved, Rejected };
        public static bool IsFinal(string s) { return s == Approved || s == Rejected; }
        public static string Label(string v)
        {
            switch (v)
            {
                case Draft: return "मसुदा";
                case Submitted: return "पुनरावलोकन प्रलंबित (JE)";
                case Reviewed: return "शुल्क भरणा प्रलंबित";
                case PaymentVerified: return "अंतिम मंजुरी प्रलंबित (AC)";
                case Approved: return "मंजूर";
                case Rejected: return "नाकारले";
                default: return v;
            }
        }
    }

    public static class RecoveryStages
    {
        public const string Demand = "Demand";
        public const string NoticeIssued = "NoticeIssued";
        public const string Sealed = "Sealed";
        public const string ReAuction = "ReAuction";
        public const string Recovered = "Recovered";
        public const string Closed = "Closed";
        public static readonly string[] All = { Demand, NoticeIssued, Sealed, ReAuction, Recovered, Closed };
        public static bool IsClosed(string s) { return s == Recovered || s == Closed; }
        public static string Label(string v)
        {
            switch (v)
            {
                case Demand: return "मागणी";
                case NoticeIssued: return "नोटीस बजावली";
                case Sealed: return "सील केले";
                case ReAuction: return "पुनर्लिलाव";
                case Recovered: return "वसूल";
                case Closed: return "बंद";
                default: return v;
            }
        }
    }

    public static class SchemeStatuses
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public static readonly string[] All = { Pending, Approved, Rejected };
        public static string Label(string v) { return v == Pending ? "प्रलंबित" : v == Approved ? "मंजूर" : v == Rejected ? "नाकारले" : v; }
    }

    public static class CalculationStatuses { public const string Calculated = "Calculated"; }
}
