using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Models.Common;
using SMC.BhoomiMalmatta.Models.Dto;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Business.PropertyRegister
{
    public sealed class PropertySaveOutcome
    {
        public PropertySaveOutcome() { Errors = new List<FieldError>(); }
        public int Id { get; set; }
        public List<FieldError> Errors { get; private set; }
        /// <summary>NOT_FOUND / CONFLICT / DB_ERROR; null when there is no record-level problem.</summary>
        public Error Error { get; set; }
        public bool Ok { get { return Error == null && Errors.Count == 0; } }
    }

    /// <summary>Use cases of the dedicated Properties page (Admin only; the role check is done by the caller).</summary>
    public sealed class PropertyService
    {
        public const int DefaultPageSize = 10;
        public const int MaxPageSize = 50;
        public const string ConflictMessage = "ही नोंद दुसऱ्या वापरकर्त्याने बदलली आहे. कृपया पुन्हा उघडा.";
        public const string NotFoundMessage = "नोंद आढळली नाही.";

        /// <summary>The nine tables with a NO ACTION foreign key to MR_AST_Properties, with the Marathi name used in the refusal message.</summary>
        public static readonly KeyValuePair<string, string>[] DependentTables =
        {
            new KeyValuePair<string, string>("AllocationProcesses", "वाटप / लिलाव प्रक्रिया"),
            new KeyValuePair<string, string>("Calculations", "आकारणी"),
            new KeyValuePair<string, string>("Documents", "कागदपत्रे"),
            new KeyValuePair<string, string>("GalaTransfers", "गाळा हस्तांतरण"),
            new KeyValuePair<string, string>("LandRegistrations", "भूमी नोंदणी"),
            new KeyValuePair<string, string>("Leases", "भाडेपट्टे"),
            new KeyValuePair<string, string>("RecoveryCases", "वसुली प्रकरणे"),
            new KeyValuePair<string, string>("SchemeApplications", "उपक्रम अर्ज"),
            new KeyValuePair<string, string>("Complaints", "तक्रारी"),
        };

        private readonly IPropertyRegister _store; private readonly PropertyMasterOptions _options; private readonly IClock _clock;
        public PropertyService(IPropertyRegister store, PropertyMasterOptions options, IClock clock) { _store = store; _options = options; _clock = clock; }

        public PropertyMasterOptions Options { get { return _options; } }

        public static PropertyListFilter Normalize(PropertyListFilter f)
        {
            f = f ?? new PropertyListFilter();
            return new PropertyListFilter
            {
                Search = string.IsNullOrWhiteSpace(f.Search) ? null : f.Search.Trim(),
                Category = string.IsNullOrWhiteSpace(f.Category) ? null : f.Category.Trim(),
                Status = string.IsNullOrWhiteSpace(f.Status) ? null : f.Status.Trim(),
                Page = Math.Max(1, f.Page),
                PageSize = f.PageSize <= 0 ? DefaultPageSize : Math.Min(MaxPageSize, f.PageSize)
            };
        }

        public PagedResult<PropertyListItemDto> List(PropertyListFilter filter)
        {
            var f = Normalize(filter);
            var page = _store.Search(f);
            var set = _options.Get();
            return new PagedResult<PropertyListItemDto>
            {
                Page = page.Page, PageSize = page.PageSize, Total = page.Total,
                Items = page.Items.Select(x => new PropertyListItemDto
                {
                    Id = x.Id, PropertyCode = x.PropertyCode, Name = x.Name, CategoryCode = x.Category, CategoryLabel = PropertyMasterOptions.LabelOf(set.Categories, x.Category),
                    Ward = x.Ward ?? string.Empty, AnnualDemandText = IndianRupee.Format(x.AnnualDemand), StatusCode = x.Status, StatusLabel = PropertyMasterOptions.LabelOf(set.Statuses, x.Status)
                }).ToList()
            };
        }

        public static string Version(Property p) { return p.UpdatedAt.HasValue ? p.UpdatedAt.Value.Ticks.ToString(CultureInfo.InvariantCulture) : string.Empty; }
        private static string Num(decimal? v) { return v.HasValue ? v.Value.ToString("0.##", CultureInfo.InvariantCulture) : string.Empty; }

        public PropertyDetailDto Detail(int id)
        {
            var p = _store.Get(id);
            if (p == null) return null;
            var set = _options.Get();
            return new PropertyDetailDto
            {
                Id = p.Id, Category = p.Category, CategoryLabel = PropertyMasterOptions.LabelOf(set.Categories, p.Category), PropertyCode = p.PropertyCode, Name = p.Name,
                Ward = p.Ward, Zone = p.Zone, Address = p.Address, AreaSqFt = Num(p.AreaSqFt), MonthlyRent = Num(p.MonthlyRent), AnnualDemand = Num(p.AnnualDemand),
                SurveyNumber = p.SurveyNumber, TpNumber = p.TpNumber, Status = p.Status, StatusLabel = PropertyMasterOptions.LabelOf(set.Statuses, p.Status),
                CurrentOccupant = p.CurrentOccupant, Shera = p.Shera, Version = Version(p)
            };
        }

        public string NextCode(string category)
        {
            if (string.IsNullOrWhiteSpace(category)) return string.Empty;
            var c = category.Trim();
            var prefix = PropertyCodeSuggester.PrefixFor(c, _store.CodesOfCategory(c));
            return prefix.Length == 0 ? string.Empty : PropertyCodeSuggester.Next(prefix, _store.CodesWithPrefix(prefix));
        }

        public PropertySaveOutcome Create(PropertySaveRequest request, ActorContext actor)
        {
            var o = new PropertySaveOutcome();
            Property p;
            o.Errors.AddRange(PropertyRules.Validate(request, _options.Get(), null, out p));
            if (p.PropertyCode != null && !o.Errors.Any(x => x.Field == "PropertyCode"))
            {
                var conflict = PropertyRules.CodeConflict(_store.FindCodeOwner(p.PropertyCode), 0);
                if (conflict != null) o.Errors.Add(conflict);
            }
            if (o.Errors.Count > 0) return o;
            var r = _store.Create(p, actor);
            if (r.IsFailure)
            {
                if (r.Error.Code == "CODE_TAKEN") o.Errors.Add(new FieldError("PropertyCode", r.Error.Message));
                else o.Error = r.Error;
                return o;
            }
            o.Id = r.Value; _options.Invalidate();
            return o;
        }

        public PropertySaveOutcome Update(int id, PropertySaveRequest request, ActorContext actor)
        {
            var o = new PropertySaveOutcome { Id = id };
            var existing = _store.Get(id);
            if (existing == null) { o.Error = new Error("NOT_FOUND", NotFoundMessage); return o; }
            if (!string.Equals(request == null ? null : (request.Version ?? string.Empty), Version(existing), StringComparison.Ordinal))
            { o.Error = new Error("CONFLICT", ConflictMessage); return o; }
            Property p;
            o.Errors.AddRange(PropertyRules.Validate(request, _options.Get(), existing, out p));
            if (o.Errors.Count > 0) return o;
            var r = _store.Update(p, Version(existing), actor);
            if (r.IsFailure) o.Error = r.Error; else _options.Invalidate();
            return o;
        }

        public static string DependentsMessage(IDictionary<string, int> counts)
        {
            var parts = DependentTables.Where(t => counts != null && counts.ContainsKey(t.Key) && counts[t.Key] > 0).Select(t => t.Value + " " + counts[t.Key]).ToList();
            return parts.Count == 0 ? null : "ही मालमत्ता हटवता येत नाही; संबंधित नोंदी आहेत: " + string.Join(", ", parts) + ".";
        }

        /// <summary>Soft delete; refused (DEPENDENTS) while any referencing table still has a not deleted row for the property.</summary>
        public Result Delete(int id, ActorContext actor)
        {
            if (_store.Get(id) == null) return Result.Failure(new Error("NOT_FOUND", NotFoundMessage));
            var message = DependentsMessage(_store.DependentCounts(id));
            if (message != null) return Result.Failure(new Error("DEPENDENTS", message));
            var r = _store.SoftDelete(id, _clock.UtcNow, actor);
            if (r.IsSuccess) _options.Invalidate();
            return r;
        }
    }
}
