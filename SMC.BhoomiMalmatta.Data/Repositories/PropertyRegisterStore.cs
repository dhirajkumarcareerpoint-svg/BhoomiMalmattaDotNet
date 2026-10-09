using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Business.PropertyRegister;
using SMC.BhoomiMalmatta.Models.Common;
using SMC.BhoomiMalmatta.Models.Dto;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Repositories
{
    /// <summary>
    /// EF6 access for the dedicated Properties page (MR_AST_Properties + MR_AST_MasterData). Inserts/updates rows of existing tables only;
    /// delete is a soft delete (one UPDATE statement); every write adds a TR_AST_AuditLogs row in the same transaction. The query builders
    /// are internal so tests can translate them to SQL without opening a database.
    /// </summary>
    public sealed class PropertyRegisterStore : IPropertyRegister, IPropertyOptionSource
    {
        /// <summary>The only statement used to delete a property: a soft delete. Never a DELETE.</summary>
        internal const string SoftDeleteSql = "UPDATE dbo.MR_AST_Properties SET IsDeleted = 1, DeletedAt = @p0, DeletedBy = @p1 WHERE Id = @p2 AND IsDeleted = 0";

        private static string Cut(string s, int max) { return s == null ? null : (s.Length > max ? s.Substring(0, max) : s); }
        private static string Who(ActorContext a) { return Cut(a == null ? null : a.UserName, 100); }

        // ------------------------------------------------------------ query builders (EF6-translatable) ------------------------------------------------------------

        internal static IQueryable<Property> Filtered(SmcDbContext db, PropertyListFilter f)
        {
            var q = db.Properties.AsNoTracking().Where(x => !x.IsDeleted);
            if (!string.IsNullOrWhiteSpace(f.Search))
            {
                var s = f.Search.Trim();
                q = q.Where(x => x.PropertyCode.Contains(s) || x.Name.Contains(s) || x.Ward.Contains(s) || x.Zone.Contains(s) || x.CurrentOccupant.Contains(s)
                                 || x.TpNumber.Contains(s) || x.SurveyNumber.Contains(s) || x.Address.Contains(s));
            }
            if (!string.IsNullOrWhiteSpace(f.Category)) { var c = f.Category.Trim(); q = q.Where(x => x.Category == c); }
            if (!string.IsNullOrWhiteSpace(f.Status)) { var st = f.Status.Trim(); q = q.Where(x => x.Status == st); }
            return q;
        }

        /// <summary>Only the displayed columns, newest first (as the reference list).</summary>
        internal static IQueryable<PropertyListRow> ListQuery(SmcDbContext db, PropertyListFilter f)
        {
            return Filtered(db, f).OrderByDescending(x => x.Id)
                .Select(x => new PropertyListRow { Id = x.Id, PropertyCode = x.PropertyCode, Name = x.Name, Category = x.Category, Ward = x.Ward, AnnualDemand = x.AnnualDemand, Status = x.Status });
        }

        internal static IQueryable<MasterOptionRow> MasterQuery(SmcDbContext db, string groupName)
        {
            return db.MasterData.AsNoTracking().Where(x => x.GroupName == groupName && x.IsActive && !x.IsDeleted).OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id)
                .Select(x => new MasterOptionRow { Key = x.Key, MarathiName = x.MarathiName, EnglishName = x.EnglishName, DisplayOrder = x.DisplayOrder });
        }

        internal static IQueryable<string> DistinctQuery(SmcDbContext db, PropertyColumn column)
        {
            var rows = db.Properties.AsNoTracking().Where(x => !x.IsDeleted);
            IQueryable<string> values;
            switch (column)
            {
                case PropertyColumn.Category: values = rows.Select(x => x.Category); break;
                case PropertyColumn.Status: values = rows.Select(x => x.Status); break;
                case PropertyColumn.Ward: values = rows.Select(x => x.Ward); break;
                default: values = rows.Select(x => x.Zone); break;
            }
            return values.Where(v => v != null && v != "").Distinct();
        }

        /// <summary>One simple query per referencing table (not deleted rows of that property); Count() is run on each.</summary>
        internal static Dictionary<string, IQueryable<int>> DependentQueries(SmcDbContext db, int id)
        {
            return new Dictionary<string, IQueryable<int>>
            {
                { "AllocationProcesses", db.AllocationProcesses.AsNoTracking().Where(x => x.PropertyId == id && !x.IsDeleted).Select(x => x.Id) },
                { "Calculations", db.Calculations.AsNoTracking().Where(x => x.PropertyId == id && !x.IsDeleted).Select(x => x.Id) },
                { "Documents", db.Documents.AsNoTracking().Where(x => x.PropertyId == id && !x.IsDeleted).Select(x => x.Id) },
                { "GalaTransfers", db.GalaTransfers.AsNoTracking().Where(x => x.PropertyId == id && !x.IsDeleted).Select(x => x.Id) },
                { "LandRegistrations", db.LandRegistrations.AsNoTracking().Where(x => x.PropertyId == id && !x.IsDeleted).Select(x => x.Id) },
                { "Leases", db.Leases.AsNoTracking().Where(x => x.PropertyId == id && !x.IsDeleted).Select(x => x.Id) },
                { "RecoveryCases", db.RecoveryCases.AsNoTracking().Where(x => x.PropertyId == id && !x.IsDeleted).Select(x => x.Id) },
                { "SchemeApplications", db.SchemeApplications.AsNoTracking().Where(x => x.PropertyId == id && !x.IsDeleted).Select(x => x.Id) },
                { "Complaints", db.Complaints.AsNoTracking().Where(x => x.PropertyId == id && !x.IsDeleted).Select(x => x.Id) },
            };
        }

        // ------------------------------------------------------------ reads ------------------------------------------------------------

        public PagedResult<PropertyListRow> Search(PropertyListFilter filter)
        {
            var f = PropertyService.Normalize(filter);
            using (var db = new SmcDbContext())
            {
                var total = Filtered(db, f).Count();
                var items = ListQuery(db, f).Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).ToList();
                return new PagedResult<PropertyListRow> { Page = f.Page, PageSize = f.PageSize, Total = total, Items = items };
            }
        }

        public Property Get(int id) { using (var db = new SmcDbContext()) return db.Properties.AsNoTracking().FirstOrDefault(x => x.Id == id && !x.IsDeleted); }

        public PropertyCodeOwner FindCodeOwner(string propertyCode)
        {
            using (var db = new SmcDbContext())
                return db.Properties.AsNoTracking().Where(x => x.PropertyCode == propertyCode).Select(x => new PropertyCodeOwner { Id = x.Id, IsDeleted = x.IsDeleted }).FirstOrDefault();
        }

        public List<string> CodesOfCategory(string category) { using (var db = new SmcDbContext()) return db.Properties.AsNoTracking().Where(x => x.Category == category).Select(x => x.PropertyCode).ToList(); }

        public List<string> CodesWithPrefix(string prefix)
        {
            var start = prefix + "-";
            using (var db = new SmcDbContext()) return db.Properties.AsNoTracking().Where(x => x.PropertyCode.StartsWith(start)).Select(x => x.PropertyCode).ToList();
        }

        public List<MasterOptionRow> MasterRows(string groupName) { using (var db = new SmcDbContext()) return MasterQuery(db, groupName).ToList(); }
        public List<string> DistinctInUse(PropertyColumn column) { using (var db = new SmcDbContext()) return DistinctQuery(db, column).ToList(); }

        public Dictionary<string, int> DependentCounts(int id)
        {
            using (var db = new SmcDbContext()) return DependentQueries(db, id).ToDictionary(x => x.Key, x => x.Value.Count());
        }

        // ------------------------------------------------------------ writes ------------------------------------------------------------

        /// <summary>The audit row of one property write: codes and field names only, never occupant / address values.</summary>
        internal static AuditEntry AuditEntryFor(ActorContext a, string action, int id, string oldValue, string newValue, DateTime atUtc)
        {
            if (a == null || !a.UserId.HasValue) throw new InvalidOperationException("Audit requires an authenticated officer.");
            return new AuditEntry { Action = action, EntityName = "Property", EntityId = id, OldValue = oldValue, NewValue = newValue, UserId = a.UserId, UserName = a.UserName, IpAddress = a.IpAddress, TimestampUtc = atUtc };
        }

        private static void Audit(SmcDbContext db, ActorContext a, string action, int id, string oldValue, string newValue)
        {
            db.AuditLogs.Add(AuditWriter.ToEntity(AuditEntryFor(a, action, id, oldValue, newValue, DateTime.UtcNow)));
        }

        internal static string CreateAuditValue(Property p) { return "Code=" + p.PropertyCode + "; Category=" + p.Category + "; Status=" + p.Status; }

        /// <summary>Copies the editable fields from <paramref name="incoming"/> onto <paramref name="current"/>; returns the names of the changed fields.</summary>
        internal static List<string> ApplyEditable(Property current, Property incoming)
        {
            var changed = new List<string>();
            foreach (var name in EditableFields)
            {
                var pi = typeof(Property).GetProperty(name);
                var before = pi.GetValue(current, null); var after = pi.GetValue(incoming, null);
                if (!Equals(before, after)) { pi.SetValue(current, after, null); changed.Add(name); }
            }
            return changed;
        }

        public Result<int> Create(Property p, ActorContext actor)
        {
            using (var db = new SmcDbContext())
            using (var tx = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                try
                {
                    var owner = db.Properties.AsNoTracking().Where(x => x.PropertyCode == p.PropertyCode).Select(x => new { x.IsDeleted }).FirstOrDefault();
                    if (owner != null) { tx.Rollback(); return Result.Failure<int>(new Error("CODE_TAKEN", owner.IsDeleted ? PropertyRules.CodeReservedByDeleted : PropertyRules.CodeTaken)); }
                    p.Id = 0; p.CreatedAt = DateTime.UtcNow; p.CreatedBy = Who(actor); p.IsDeleted = false; p.UpdatedAt = null; p.UpdatedBy = null;
                    db.Properties.Add(p);
                    db.SaveChanges();
                    Audit(db, actor, "Create", p.Id, null, CreateAuditValue(p));
                    db.SaveChanges();
                    tx.Commit();
                    return Result.Success(p.Id);
                }
                catch (DbUpdateException)
                {
                    tx.Rollback();
                    return Result.Failure<int>(new Error("CODE_TAKEN", PropertyRules.CodeTaken)); // unique index IX_MR_AST_Properties_PropertyCode
                }
            }
        }

        internal static readonly string[] EditableFields = { "Name", "Ward", "Zone", "Address", "AreaSqFt", "MonthlyRent", "AnnualDemand", "SurveyNumber", "TpNumber", "Status", "CurrentOccupant", "Shera" };

        public Result Update(Property p, string expectedVersion, ActorContext actor)
        {
            using (var db = new SmcDbContext())
            using (var tx = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                try
                {
                    var cur = db.Properties.FirstOrDefault(x => x.Id == p.Id && !x.IsDeleted);
                    if (cur == null) { tx.Rollback(); return Result.Failure(new Error("NOT_FOUND", PropertyService.NotFoundMessage)); }
                    if (!string.Equals(PropertyService.Version(cur), expectedVersion ?? string.Empty, StringComparison.Ordinal))
                    { tx.Rollback(); return Result.Failure(new Error("CONFLICT", PropertyService.ConflictMessage)); }
                    var changed = ApplyEditable(cur, p);
                    if (changed.Count == 0) { tx.Rollback(); return Result.Success(); }
                    cur.UpdatedAt = DateTime.UtcNow; cur.UpdatedBy = Who(actor);
                    // Field NAMES only: values can be personal data (occupant, address).
                    Audit(db, actor, "Update", cur.Id, "Code=" + cur.PropertyCode, "Changed=" + string.Join(",", changed));
                    db.SaveChanges();
                    tx.Commit();
                    return Result.Success();
                }
                catch (DbUpdateException)
                {
                    tx.Rollback();
                    return Result.Failure(new Error("CONFLICT", PropertyService.ConflictMessage));
                }
            }
        }

        public Result SoftDelete(int id, DateTime atUtc, ActorContext actor)
        {
            using (var db = new SmcDbContext())
            using (var tx = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                var code = db.Properties.AsNoTracking().Where(x => x.Id == id && !x.IsDeleted).Select(x => x.PropertyCode).FirstOrDefault();
                if (code == null) { tx.Rollback(); return Result.Failure(new Error("NOT_FOUND", PropertyService.NotFoundMessage)); }
                var message = PropertyService.DependentsMessage(DependentQueries(db, id).ToDictionary(x => x.Key, x => x.Value.Count()));
                if (message != null) { tx.Rollback(); return Result.Failure(new Error("DEPENDENTS", message)); }
                var rows = db.Database.ExecuteSqlCommand(SoftDeleteSql, atUtc, Who(actor), id);
                if (rows != 1) { tx.Rollback(); return Result.Failure(new Error("NOT_FOUND", PropertyService.NotFoundMessage)); }
                Audit(db, actor, "Delete", id, "Code=" + code, "IsDeleted=1");
                db.SaveChanges();
                tx.Commit();
                return Result.Success();
            }
        }
    }
}
