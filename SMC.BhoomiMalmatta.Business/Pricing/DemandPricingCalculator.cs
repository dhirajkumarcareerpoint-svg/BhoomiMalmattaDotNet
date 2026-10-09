using System;
using SMC.BhoomiMalmatta.Models.Common;

namespace SMC.BhoomiMalmatta.Business.Pricing
{
    /// <summary>Explicit rules for the stable service codes used by the demand application master.</summary>
    public static class DemandServicePricingRules
    {
        public const string VariousBusinessSpace = "VariousBusinessSpace";
        public const string MiniGaala = "MiniGaala";
        public const string MajorGaala = "MajorGaala";

        public static bool IsAreaBased(string serviceCode)
        { return string.Equals((serviceCode ?? string.Empty).Trim(), VariousBusinessSpace, StringComparison.Ordinal); }

        public static decimal? FixedAnnualAmount(string serviceCode)
        {
            var code = (serviceCode ?? string.Empty).Trim();
            if (IsAreaBased(code)) return null;
            if (string.Equals(code, MajorGaala, StringComparison.Ordinal)) return 50000m;
            return 20000m;
        }

        public static DateTime EffectiveEndDate(string serviceCode, DateTime startDate, DateTime selectedEndDate)
        { return IsAreaBased(serviceCode) ? selectedEndDate.Date : startDate.Date.AddYears(1); }
    }

    public enum PricingMode { Fixed, PerAreaPerDay, PerDay }

    public sealed class PricingRequest
    {
        public string ServiceCode { get; set; }
        public string RateUnit { get; set; }
        public decimal? MasterRate { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal? LengthFt { get; set; }
        public decimal? WidthFt { get; set; }
        public decimal? OfficerEnteredRate { get; set; }
    }

    public sealed class PricingResult
    {
        public PricingMode Mode { get; set; }
        public int TotalDays { get; set; }
        public decimal AreaSqFt { get; set; }
        public decimal? Rate { get; set; }
        public decimal? DailyRate { get; set; }
        public decimal? FinalAmount { get; set; }
        public bool IsPendingOfficerAmount { get; set; }
    }

    /// <summary>
    /// Single source of truth for fee calculation. Units come from MR_CFC_DemandServiceRates.Unit ("fixed", "sq.ft." in the live data).
    /// The server always recomputes; client amounts are never trusted.
    /// </summary>
    public static class DemandPricingCalculator
    {
        public static bool TryParseUnit(string unit, out PricingMode mode)
        {
            var u = (unit ?? string.Empty).Trim().ToLowerInvariant().Replace(" ", "").Replace(".", "");
            switch (u)
            {
                case "fixed": mode = PricingMode.Fixed; return true;
                case "sqft": case "sqfeet": case "area": case "persqft": mode = PricingMode.PerAreaPerDay; return true;
                case "day": case "daily": case "perday": mode = PricingMode.PerDay; return true;
                default: mode = PricingMode.Fixed; return false;
            }
        }

        /// <summary>Inclusive: both the start and the end date are counted.</summary>
        public static int TotalDays(DateTime start, DateTime end) { return (end.Date - start.Date).Days + 1; }

        public static Result<PricingResult> Calculate(PricingRequest r)
        {
            if (r.EndDate.Date < r.StartDate.Date)
                return Result.Failure<PricingResult>(new Error("DATE_RANGE", "समाप्ती दिनांक सुरुवातीच्या दिनांकापेक्षा आधीचा असू शकत नाही."));
            PricingMode mode;
            if (!TryParseUnit(r.RateUnit, out mode))
                return Result.Failure<PricingResult>(new Error("UNIT_UNKNOWN", "सेवेचे दर एकक (unit) ओळखता आले नाही."));

            var days = TotalDays(r.StartDate, r.EndDate);
            decimal area = r.LengthFt.HasValue && r.LengthFt.Value > 0 && r.WidthFt.HasValue && r.WidthFt.Value > 0
                ? Round(r.LengthFt.Value * r.WidthFt.Value) : 0m;
            if (mode == PricingMode.PerAreaPerDay)
            {
                if (area <= 0m)
                    return Result.Failure<PricingResult>(new Error("AREA_REQUIRED", "लांबी व रुंदी शून्यापेक्षा जास्त असणे आवश्यक आहे."));
            }

            // Rate precedence: approved master rate first; officer-entered rate only when the master rate is NULL.
            decimal? rate = null;
            if (r.MasterRate.HasValue && r.MasterRate.Value > 0) rate = r.MasterRate;
            else if (r.OfficerEnteredRate.HasValue && r.OfficerEnteredRate.Value > 0) rate = r.OfficerEnteredRate;

            if (!rate.HasValue)
                return Result.Success(new PricingResult { Mode = mode, TotalDays = days, AreaSqFt = area, Rate = null, FinalAmount = null, IsPendingOfficerAmount = true });

            decimal dailyRate;
            decimal amount;
            switch (mode)
            {
                case PricingMode.Fixed: dailyRate = rate.Value; amount = rate.Value; break;
                case PricingMode.PerDay: dailyRate = rate.Value; amount = rate.Value * days; break;
                default: dailyRate = area * rate.Value; amount = dailyRate * days; break;
            }
            return Result.Success(new PricingResult { Mode = mode, TotalDays = days, AreaSqFt = area, Rate = rate, DailyRate = Round(dailyRate), FinalAmount = Round(amount), IsPendingOfficerAmount = false });
        }

        private static decimal Round(decimal v) { return Math.Round(v, 2, MidpointRounding.AwayFromZero); }
    }
}
