using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Business.PropertyRegister
{
    /// <summary>Indian-grouped rupees without decimals ("₹1,20,000"), independent of the server culture.</summary>
    public static class IndianRupee
    {
        public static string Format(decimal value)
        {
            var rounded = Math.Round(value, 0, MidpointRounding.AwayFromZero);
            var digits = Math.Abs(rounded).ToString("0", CultureInfo.InvariantCulture);
            var sb = new StringBuilder();
            if (digits.Length <= 3) sb.Append(digits);
            else
            {
                var head = digits.Substring(0, digits.Length - 3);
                var groups = new List<string>();
                while (head.Length > 2) { groups.Insert(0, head.Substring(head.Length - 2)); head = head.Substring(0, head.Length - 2); }
                if (head.Length > 0) groups.Insert(0, head);
                sb.Append(string.Join(",", groups)).Append(',').Append(digits.Substring(digits.Length - 3));
            }
            return (rounded < 0 ? "-₹" : "₹") + sb;
        }
    }

    /// <summary>
    /// Next PropertyCode suggestion. Prefix: the legacy alias map for MajorGaale/MiniGaale (MJ/MN), otherwise the prefix already used by existing
    /// rows of that category; none -> empty (no invented prefixes). Number: highest existing number with that prefix (soft-deleted rows included,
    /// gaps are not reused) + 1, at least 3 digits.
    /// </summary>
    public static class PropertyCodeSuggester
    {
        private static readonly Regex CodeShape = new Regex(@"^(.+)-(\d+)$", RegexOptions.CultureInvariant);

        public static string PrefixFor(string category, IEnumerable<string> codesOfCategory)
        {
            var known = PropertyCategories.Prefix(category);
            if (!string.IsNullOrEmpty(known)) return known;
            return (codesOfCategory ?? Enumerable.Empty<string>()).Select(c => CodeShape.Match((c ?? string.Empty).Trim())).Where(m => m.Success)
                .GroupBy(m => m.Groups[1].Value, StringComparer.Ordinal).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => g.Key).FirstOrDefault() ?? string.Empty;
        }

        public static string Next(string prefix, IEnumerable<string> codesWithPrefix)
        {
            if (string.IsNullOrEmpty(prefix)) return string.Empty;
            var max = 0; var width = 3;
            foreach (var code in codesWithPrefix ?? Enumerable.Empty<string>())
            {
                var m = CodeShape.Match((code ?? string.Empty).Trim());
                if (!m.Success || !string.Equals(m.Groups[1].Value, prefix, StringComparison.Ordinal)) continue;
                int n;
                if (int.TryParse(m.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out n) && n > max) max = n;
                width = Math.Max(width, m.Groups[2].Value.Length);
            }
            return prefix + "-" + (max + 1).ToString(new string('0', width), CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Field-level validation for the Properties dialog. Category/status are checked against the database option set, never a fixed list.</summary>
    public static class PropertyRules
    {
        public const string CodeTaken = "हा मालमत्ता क्रमांक आधीपासून आहे.";
        public const string CodeReservedByDeleted = "हा क्रमांक हटवलेल्या नोंदीसाठी राखीव आहे.";
        public const decimal MaxAmount = 9999999999999999.99m; // decimal(18,2)
        private static readonly Regex Number = new Regex(@"^-?\d+(\.\d+)?$", RegexOptions.CultureInvariant);

        public static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            { "Category", "मालमत्ता प्रकार" }, { "PropertyCode", "मालमत्ता क्रमांक" }, { "Name", "मालमत्तेचे नाव" }, { "Ward", "प्रभाग" }, { "Zone", "झोन" },
            { "Address", "पत्ता" }, { "AreaSqFt", "क्षेत्रफळ" }, { "MonthlyRent", "मासिक भाडे" }, { "AnnualDemand", "वार्षिक मागणी" },
            { "SurveyNumber", "सर्वे/गट क्रमांक" }, { "TpNumber", "TP क्रमांक" }, { "Status", "स्थिती" }, { "CurrentOccupant", "सध्याचा धारक/भाडेकरू" }, { "Shera", "शेरा" }
        };

        public static readonly Dictionary<string, int> MaxLengths = new Dictionary<string, int>
        {
            { "PropertyCode", 50 }, { "Name", 250 }, { "Ward", 50 }, { "Zone", 50 }, { "Address", 500 }, { "SurveyNumber", 50 }, { "TpNumber", 50 }, { "CurrentOccupant", 200 }, { "Shera", 2000 }
        };

        private static string T(string s) { return string.IsNullOrWhiteSpace(s) ? null : s.Trim(); }

        /// <summary>
        /// Trims and validates the request. <paramref name="existing"/> is null for a new record; on edit the stored category and code are kept and a
        /// stored status that is no longer offered stays valid (it is never silently changed).
        /// </summary>
        public static List<FieldError> Validate(PropertySaveRequest r, PropertyOptionSet options, Property existing, out Property normalized)
        {
            var e = new List<FieldError>();
            r = r ?? new PropertySaveRequest();
            var isNew = existing == null;
            var p = new Property
            {
                Id = isNew ? 0 : existing.Id,
                Category = isNew ? T(r.Category) : existing.Category,
                PropertyCode = isNew ? T(r.PropertyCode) : existing.PropertyCode,
                Name = T(r.Name), Ward = T(r.Ward), Zone = T(r.Zone), Address = T(r.Address), SurveyNumber = T(r.SurveyNumber), TpNumber = T(r.TpNumber),
                Status = T(r.Status), CurrentOccupant = T(r.CurrentOccupant), Shera = T(r.Shera)
            };
            normalized = p;

            if (p.Category == null) e.Add(new FieldError("Category", "मालमत्ता प्रकार निवडा."));
            else if (isNew && !options.Categories.Any(o => o.Code == p.Category)) e.Add(new FieldError("Category", "निवडलेला मालमत्ता प्रकार उपलब्ध नाही."));
            if (p.PropertyCode == null) e.Add(new FieldError("PropertyCode", "मालमत्ता क्रमांक आवश्यक आहे."));
            if (p.Name == null) e.Add(new FieldError("Name", "मालमत्तेचे नाव आवश्यक आहे."));
            if (p.Status == null) e.Add(new FieldError("Status", "स्थिती निवडा."));
            else if (!options.Statuses.Any(o => o.Code == p.Status) && (isNew || !string.Equals(existing.Status, p.Status, StringComparison.Ordinal)))
                e.Add(new FieldError("Status", "निवडलेली स्थिती उपलब्ध नाही."));

            p.AreaSqFt = Amount(r.AreaSqFt, "AreaSqFt", e, null);
            p.MonthlyRent = Amount(r.MonthlyRent, "MonthlyRent", e, 0m) ?? 0m;
            p.AnnualDemand = Amount(r.AnnualDemand, "AnnualDemand", e, 0m) ?? 0m;

            Length(e, "PropertyCode", p.PropertyCode); Length(e, "Name", p.Name); Length(e, "Ward", p.Ward); Length(e, "Zone", p.Zone); Length(e, "Address", p.Address);
            Length(e, "SurveyNumber", p.SurveyNumber); Length(e, "TpNumber", p.TpNumber); Length(e, "CurrentOccupant", p.CurrentOccupant); Length(e, "Shera", p.Shera);
            return e;
        }

        /// <summary>Unique PropertyCode, soft-deleted rows included (the unique index covers them).</summary>
        public static FieldError CodeConflict(PropertyCodeOwner owner, int currentId)
        {
            if (owner == null || owner.Id == currentId) return null;
            return new FieldError("PropertyCode", owner.IsDeleted ? CodeReservedByDeleted : CodeTaken);
        }

        private static decimal? Amount(string text, string field, List<FieldError> e, decimal? whenEmpty)
        {
            var s = T(text);
            if (s == null) return whenEmpty;
            var label = Labels[field];
            if (!Number.IsMatch(s)) { e.Add(new FieldError(field, label + " संख्या असावी.")); return whenEmpty; }
            if (s.StartsWith("-", StringComparison.Ordinal)) { e.Add(new FieldError(field, label + " ऋण असू शकत नाही.")); return whenEmpty; }
            var dot = s.IndexOf('.');
            if (dot >= 0 && s.Length - dot - 1 > 2) { e.Add(new FieldError(field, label + " मध्ये जास्तीत जास्त २ दशांश अंक असावेत.")); return whenEmpty; }
            decimal v;
            if (!decimal.TryParse(s, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out v) || v > MaxAmount) { e.Add(new FieldError(field, label + " खूप मोठी आहे.")); return whenEmpty; }
            return v;
        }

        private static void Length(List<FieldError> e, string field, string value)
        {
            int max;
            if (value != null && MaxLengths.TryGetValue(field, out max) && value.Length > max) e.Add(new FieldError(field, Labels[field] + " जास्तीत जास्त " + max + " अक्षरे असावे."));
        }
    }
}
