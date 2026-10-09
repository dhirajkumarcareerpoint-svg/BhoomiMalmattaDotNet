using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Models.Common;
using SMC.BhoomiMalmatta.Models.Dto;

namespace SMC.BhoomiMalmatta.Business.PropertyRegister
{
    /// <summary>Which MR_AST_MasterData groups hold the property lists. Defaults: PropertyCategory / PropertyStatus; Ward/Zone groups are optional.</summary>
    public sealed class PropertyOptionGroups
    {
        public const string DefaultCategoryGroup = "PropertyCategory";
        public const string DefaultStatusGroup = "PropertyStatus";
        public string CategoryGroup { get; set; }
        public string StatusGroup { get; set; }
        /// <summary>Empty = wards come from the values stored in MR_AST_Properties.</summary>
        public string WardGroup { get; set; }
        /// <summary>Empty = zones come from the values stored in MR_AST_Properties.</summary>
        public string ZoneGroup { get; set; }

        public static PropertyOptionGroups Default() { return new PropertyOptionGroups { CategoryGroup = DefaultCategoryGroup, StatusGroup = DefaultStatusGroup }; }
    }

    /// <summary>A loaded option set plus where each list came from and any read problem.</summary>
    public sealed class PropertyOptionSet
    {
        public PropertyOptionSet() { Categories = new List<PropertyOption>(); Statuses = new List<PropertyOption>(); Wards = new List<PropertyOption>(); Zones = new List<PropertyOption>(); Warnings = new List<string>(); }
        public List<PropertyOption> Categories { get; private set; }
        public List<PropertyOption> Statuses { get; private set; }
        public List<PropertyOption> Wards { get; private set; }
        public List<PropertyOption> Zones { get; private set; }
        public List<string> Warnings { get; private set; }
        public string WardSource { get; set; }
        public string ZoneSource { get; set; }

        public PropertyOptionsDto ToDto()
        {
            var d = new PropertyOptionsDto();
            d.Categories.AddRange(Categories); d.Statuses.AddRange(Statuses); d.Wards.AddRange(Wards); d.Zones.AddRange(Zones); d.Warnings.AddRange(Warnings);
            return d;
        }
    }

    /// <summary>
    /// The ONE provider of property categories, statuses, wards and zones (no hard-coded option list anywhere). Categories/statuses = the
    /// MR_AST_MasterData group rows (active, not deleted, DisplayOrder) UNION the values in use in MR_AST_Properties (a value only in use shows its
    /// raw code). Wards/zones = their master group when configured, otherwise the stored values (natural order); Zone1..Zone8 are added only when
    /// every stored zone already has that format. Cached for 5 minutes; a failed read gives an empty part plus a warning (never a fallback list).
    /// </summary>
    public sealed class PropertyMasterOptions
    {
        public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
        public const string MasterReadWarning = "मालमत्ता मास्टर माहिती (प्रकार / स्थिती) वाचता आली नाही; फक्त वापरातील मूल्ये दाखवली आहेत.";
        public const string InUseReadWarning = "मालमत्तांमधील वापरातील मूल्ये वाचता आली नाहीत.";
        private static readonly Regex ZoneFormat = new Regex("^Zone[1-8]$", RegexOptions.CultureInvariant);

        private readonly IPropertyOptionSource _source; private readonly IClock _clock; private readonly PropertyOptionGroups _groups;
        private readonly object _gate = new object();
        private PropertyOptionSet _cached; private DateTime _cachedAtUtc;

        public PropertyMasterOptions(IPropertyOptionSource source, IClock clock, PropertyOptionGroups groups = null)
        { _source = source; _clock = clock; _groups = groups ?? PropertyOptionGroups.Default(); }

        public PropertyOptionGroups Groups { get { return _groups; } }

        /// <summary>The cached option set (loaded at most once per 5 minutes; a set with read problems is not cached).</summary>
        public PropertyOptionSet Get()
        {
            lock (_gate)
            {
                var now = _clock.UtcNow;
                if (_cached != null && now - _cachedAtUtc < CacheDuration) return _cached;
                var loaded = Load();
                if (loaded.Warnings.Count == 0) { _cached = loaded; _cachedAtUtc = now; }
                else _cached = null;
                return loaded;
            }
        }

        /// <summary>Forget the cache (after a save that may have added a new in-use value).</summary>
        public void Invalidate() { lock (_gate) { _cached = null; } }

        public string CategoryLabel(string code) { return LabelOf(Get().Categories, code); }
        public string StatusLabel(string code) { return LabelOf(Get().Statuses, code); }
        public bool IsCategory(string code) { return Contains(Get().Categories, code); }
        public bool IsStatus(string code) { return Contains(Get().Statuses, code); }

        public static string LabelOf(IEnumerable<PropertyOption> options, string code)
        {
            if (code == null) return string.Empty;
            var o = options.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.Ordinal));
            return o == null || string.IsNullOrWhiteSpace(o.Label) ? code : o.Label;
        }

        private static bool Contains(IEnumerable<PropertyOption> options, string code) { return code != null && options.Any(x => string.Equals(x.Code, code, StringComparison.Ordinal)); }

        private PropertyOptionSet Load()
        {
            var set = new PropertyOptionSet();
            var masterFailed = false; var inUseFailed = false;
            Func<string, List<MasterOptionRow>> master = g =>
            {
                if (string.IsNullOrWhiteSpace(g)) return new List<MasterOptionRow>();
                try { return _source.MasterRows(g) ?? new List<MasterOptionRow>(); } catch (Exception) { masterFailed = true; return new List<MasterOptionRow>(); }
            };
            Func<PropertyColumn, List<string>> inUse = c =>
            {
                try { return (_source.DistinctInUse(c) ?? new List<string>()).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct(StringComparer.Ordinal).ToList(); }
                catch (Exception) { inUseFailed = true; return new List<string>(); }
            };

            set.Categories.AddRange(Union(master(_groups.CategoryGroup), inUse(PropertyColumn.Category)));
            set.Statuses.AddRange(Union(master(_groups.StatusGroup), inUse(PropertyColumn.Status)));

            var wardMaster = master(_groups.WardGroup);
            var storedWards = inUse(PropertyColumn.Ward);
            if (wardMaster.Count > 0) { set.Wards.AddRange(Union(wardMaster, storedWards)); set.WardSource = "MR_AST_MasterData group " + _groups.WardGroup + " + values in use"; }
            else { set.Wards.AddRange(storedWards.OrderBy(x => x, NaturalComparer.Instance).Select(x => new PropertyOption { Code = x, Label = x })); set.WardSource = "values stored in MR_AST_Properties"; }

            var zoneMaster = master(_groups.ZoneGroup);
            var storedZones = inUse(PropertyColumn.Zone);
            if (zoneMaster.Count > 0) { set.Zones.AddRange(Union(zoneMaster, storedZones)); set.ZoneSource = "MR_AST_MasterData group " + _groups.ZoneGroup + " + values in use"; }
            else
            {
                var zones = new List<string>(storedZones);
                var zoneFormat = zones.Count > 0 && zones.All(z => ZoneFormat.IsMatch(z));
                if (zoneFormat) foreach (var n in Enumerable.Range(1, 8)) if (!zones.Contains("Zone" + n)) zones.Add("Zone" + n);
                set.Zones.AddRange(zones.OrderBy(x => x, NaturalComparer.Instance).Select(x => new PropertyOption { Code = x, Label = x }));
                set.ZoneSource = "values stored in MR_AST_Properties" + (zoneFormat ? " + Zone1..Zone8" : string.Empty);
            }

            if (masterFailed) set.Warnings.Add(MasterReadWarning);
            if (inUseFailed) set.Warnings.Add(InUseReadWarning);
            return set;
        }

        private static IEnumerable<PropertyOption> Union(List<MasterOptionRow> master, List<string> inUse)
        {
            var result = new List<PropertyOption>();
            foreach (var row in master.OrderBy(x => x.DisplayOrder))
            {
                if (string.IsNullOrWhiteSpace(row.Key) || result.Any(x => x.Code == row.Key)) continue;
                var label = !string.IsNullOrWhiteSpace(row.MarathiName) ? row.MarathiName.Trim() : !string.IsNullOrWhiteSpace(row.EnglishName) ? row.EnglishName.Trim() : row.Key;
                result.Add(new PropertyOption { Code = row.Key, Label = label });
            }
            foreach (var value in inUse.OrderBy(x => x, NaturalComparer.Instance))
                if (!result.Any(x => x.Code == value)) result.Add(new PropertyOption { Code = value, Label = value });
            return result;
        }
    }

    /// <summary>Natural order: "Prabhag 2" before "Prabhag 11"; otherwise ordinal, case-insensitive.</summary>
    public sealed class NaturalComparer : IComparer<string>
    {
        public static readonly NaturalComparer Instance = new NaturalComparer();
        private static readonly Regex Parts = new Regex(@"\d+|\D+", RegexOptions.CultureInvariant);

        public int Compare(string x, string y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return -1;
            if (y == null) return 1;
            var a = Parts.Matches(x); var b = Parts.Matches(y);
            for (var i = 0; i < Math.Min(a.Count, b.Count); i++)
            {
                var p = a[i].Value; var q = b[i].Value;
                int c;
                if (char.IsDigit(p[0]) && char.IsDigit(q[0]))
                {
                    var pt = p.TrimStart('0'); var qt = q.TrimStart('0');
                    c = pt.Length.CompareTo(qt.Length);
                    if (c == 0) c = string.CompareOrdinal(pt, qt);
                }
                else c = string.Compare(p, q, StringComparison.OrdinalIgnoreCase);
                if (c != 0) return c;
            }
            var len = a.Count.CompareTo(b.Count);
            return len != 0 ? len : string.CompareOrdinal(x, y);
        }
    }
}
