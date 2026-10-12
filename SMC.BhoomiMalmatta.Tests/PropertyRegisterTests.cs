using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Business.PropertyRegister;
using SMC.BhoomiMalmatta.Data;
using SMC.BhoomiMalmatta.Data.Repositories;
using SMC.BhoomiMalmatta.Models.Common;
using SMC.BhoomiMalmatta.Models.Dto;
using SMC.BhoomiMalmatta.Models.Entities;
using SMC.BhoomiMalmatta.Tests.Support;

namespace SMC.BhoomiMalmatta.Tests
{
    /// <summary>In-memory MR_AST_MasterData groups + MR_AST_Properties values for the options provider.</summary>
    internal sealed class FakePropertyOptionSource : IPropertyOptionSource
    {
        public Dictionary<string, List<MasterOptionRow>> Groups = new Dictionary<string, List<MasterOptionRow>>();
        public Dictionary<PropertyColumn, List<string>> InUse = new Dictionary<PropertyColumn, List<string>>();
        public bool ThrowMaster, ThrowInUse; public int MasterCalls, InUseCalls;

        public List<MasterOptionRow> MasterRows(string groupName)
        {
            MasterCalls++;
            if (ThrowMaster) throw new InvalidOperationException("master down");
            List<MasterOptionRow> rows; return Groups.TryGetValue(groupName, out rows) ? rows.OrderBy(x => x.DisplayOrder).ToList() : new List<MasterOptionRow>();
        }

        public List<MasterOptionRow> MasterRowsWithPrefix(string groupPrefix)
        {
            MasterCalls++;
            if (ThrowMaster) throw new InvalidOperationException("master down");
            if (string.IsNullOrWhiteSpace(groupPrefix)) return new List<MasterOptionRow>();
            return Groups.Where(g => g.Key.StartsWith(groupPrefix, StringComparison.Ordinal)).OrderBy(g => g.Key, StringComparer.Ordinal)
                .SelectMany(g => g.Value.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id).Select(x => new MasterOptionRow
                {
                    Id = x.Id, GroupName = g.Key, IsActive = x.IsActive, IsDeleted = x.IsDeleted, Key = x.Key, MarathiName = x.MarathiName, EnglishName = x.EnglishName, DisplayOrder = x.DisplayOrder
                })).ToList();
        }

        public List<string> DistinctInUse(PropertyColumn column)
        {
            InUseCalls++;
            if (ThrowInUse) throw new InvalidOperationException("properties down");
            List<string> v; return InUse.TryGetValue(column, out v) ? v.ToList() : new List<string>();
        }

        /// <summary>The 9 categories and 2 statuses of database/proposed-changes/property-master-data.sql.</summary>
        public static FakePropertyOptionSource FromSqlScript()
        {
            var s = new FakePropertyOptionSource();
            foreach (var r in PropertyRegisterTests.ScriptRows())
            {
                if (!s.Groups.ContainsKey(r[0])) s.Groups[r[0]] = new List<MasterOptionRow>();
                s.Groups[r[0]].Add(new MasterOptionRow { Key = r[1], MarathiName = r[2], EnglishName = r[3], DisplayOrder = int.Parse(r[4]) });
            }
            return s;
        }
    }

    /// <summary>In-memory MR_AST_Properties with the audit rows the real store writes in the same transaction.</summary>
    internal sealed class FakePropertyStore : IPropertyRegister
    {
        public List<Property> Rows = new List<Property>();
        public List<AuditEntry> Audits = new List<AuditEntry>();
        public Dictionary<int, Dictionary<string, int>> Dependents = new Dictionary<int, Dictionary<string, int>>();
        public PropertyListFilter LastFilter;

        public PagedResult<PropertyListRow> Search(PropertyListFilter f)
        {
            LastFilter = f;
            var q = Rows.Where(x => !x.IsDeleted);
            if (f.Search != null) q = q.Where(x => new[] { x.PropertyCode, x.Name, x.Ward, x.Zone, x.CurrentOccupant, x.TpNumber, x.SurveyNumber, x.Address }.Any(v => v != null && v.Contains(f.Search)));
            if (f.Category != null) q = q.Where(x => x.Category == f.Category);
            if (f.Status != null) q = q.Where(x => x.Status == f.Status);
            var all = q.OrderByDescending(x => x.Id).ToList();
            return new PagedResult<PropertyListRow>
            {
                Page = f.Page, PageSize = f.PageSize, Total = all.Count,
                Items = all.Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).Select(x => new PropertyListRow { Id = x.Id, PropertyCode = x.PropertyCode, Name = x.Name, Category = x.Category, Ward = x.Ward, AnnualDemand = x.AnnualDemand, Status = x.Status }).ToList()
            };
        }

        public Property Get(int id) { var p = Rows.FirstOrDefault(x => x.Id == id && !x.IsDeleted); return p == null ? null : Copy(p); }
        public PropertyCodeOwner FindCodeOwner(string code) { var p = Rows.FirstOrDefault(x => x.PropertyCode == code); return p == null ? null : new PropertyCodeOwner { Id = p.Id, IsDeleted = p.IsDeleted }; }
        public List<string> CodesOfCategory(string c) { return Rows.Where(x => x.Category == c).Select(x => x.PropertyCode).ToList(); }
        public List<string> CodesWithPrefix(string prefix) { return Rows.Where(x => x.PropertyCode.StartsWith(prefix + "-", StringComparison.Ordinal)).Select(x => x.PropertyCode).ToList(); }

        public Result<int> Create(Property p, ActorContext actor)
        {
            p.Id = Rows.Count == 0 ? 1 : Rows.Max(x => x.Id) + 1; p.CreatedBy = actor.UserName;
            Rows.Add(p);
            Audits.Add(PropertyRegisterStore.AuditEntryFor(actor, "Create", p.Id, null, PropertyRegisterStore.CreateAuditValue(p), DateTime.UtcNow));
            return Result.Success(p.Id);
        }

        public Result Update(Property p, string expectedVersion, ActorContext actor)
        {
            var cur = Rows.First(x => x.Id == p.Id);
            var changed = PropertyRegisterStore.ApplyEditable(cur, p);
            if (changed.Count == 0) return Result.Success();
            cur.UpdatedAt = (cur.UpdatedAt ?? new DateTime(2026, 1, 1)).AddMinutes(1); cur.UpdatedBy = actor.UserName;
            Audits.Add(PropertyRegisterStore.AuditEntryFor(actor, "Update", cur.Id, "Code=" + cur.PropertyCode, "Changed=" + string.Join(",", changed), DateTime.UtcNow));
            return Result.Success();
        }

        public Dictionary<string, int> DependentCounts(int id) { Dictionary<string, int> d; return Dependents.TryGetValue(id, out d) ? d : new Dictionary<string, int>(); }

        public Result SoftDelete(int id, DateTime atUtc, ActorContext actor)
        {
            var p = Rows.First(x => x.Id == id);
            p.IsDeleted = true; p.DeletedAt = atUtc; p.DeletedBy = actor.UserName;
            Audits.Add(PropertyRegisterStore.AuditEntryFor(actor, "Delete", id, "Code=" + p.PropertyCode, "IsDeleted=1", atUtc));
            return Result.Success();
        }

        private static Property Copy(Property p) { return (Property)typeof(object).GetMethod("MemberwiseClone", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(p, null); }
    }

    [TestClass]
    public class PropertyRegisterTests
    {
        private static readonly ActorContext Admin = new ActorContext { UserId = 1, UserName = "admin", Role = "Admin", IpAddress = "127.0.0.1" };

        internal static string ScriptText() { return File.ReadAllText(Path.Combine(Repo.Root, "database", "proposed-changes", "property-master-data.sql")); }

        /// <summary>(GroupName, Key, MarathiName, EnglishName, DisplayOrder) tuples of the VALUES list of the SQL file.</summary>
        internal static List<string[]> ScriptRows()
        {
            return Regex.Matches(ScriptText(), @"\(N'(Property\w+)',\s*N'([^']+)',\s*N'([^']+)',\s*N'([^']+)',\s*(\d+)\)")
                .Cast<Match>().Select(m => new[] { m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value.Trim(), m.Groups[4].Value.Trim(), m.Groups[5].Value }).ToList();
        }

        private static PropertyMasterOptions Options(FakePropertyOptionSource src, FakeClock clock = null) { return new PropertyMasterOptions(src, clock ?? new FakeClock()); }

        private static PropertyService Service(out FakePropertyStore store, FakePropertyOptionSource src = null)
        {
            store = new FakePropertyStore();
            return new PropertyService(store, Options(src ?? FakePropertyOptionSource.FromSqlScript()), new FakeClock());
        }

        private static PropertySaveRequest Valid(string category = "MajorGaale", string code = "MJ-010")
        {
            return new PropertySaveRequest { Category = category, PropertyCode = code, Name = "गाळा क्र. 10", Ward = "Prabhag 2", Zone = "Zone1", MonthlyRent = "1200", AnnualDemand = "14400", Status = "Rikamy" };
        }

        private static Property Row(int id, string code, string category = "MajorGaale", string status = "Rikamy", bool deleted = false)
        {
            return new Property { Id = id, PropertyCode = code, Name = "Name " + id, Category = category, Status = status, Ward = "Prabhag " + id, Zone = "Zone" + (id % 8 + 1), AnnualDemand = 1000m * id, IsDeleted = deleted };
        }

        // ------------------------------------------------------------ labels and rupees ------------------------------------------------------------

        [TestMethod]
        public void Category_And_Status_Labels_Come_From_Master_Rows_And_Unknown_Codes_Show_Raw()
        {
            var o = Options(FakePropertyOptionSource.FromSqlScript());
            Assert.AreEqual("Major गाळे", o.CategoryLabel("MajorGaale"));
            Assert.AreEqual("इतर भाडेतत्वावरील मनपा मालमत्ता", o.CategoryLabel("OtherMunicipalProperty"));
            Assert.AreEqual("रिक्त", o.StatusLabel("Rikamy"));
            Assert.AreEqual("भाडेतत्वावर दिलेली", o.StatusLabel("Bhadyane"));
            Assert.AreEqual("SomethingNew", o.CategoryLabel("SomethingNew"));
            Assert.AreEqual("Closed", o.StatusLabel("Closed"));
            Assert.AreEqual(string.Empty, o.StatusLabel(null));
        }

        [TestMethod]
        public void Label_Falls_Back_To_English_Then_Key()
        {
            var src = new FakePropertyOptionSource();
            src.Groups["PropertyCategory"] = new List<MasterOptionRow> { new MasterOptionRow { Key = "A", MarathiName = " ", EnglishName = "Alpha", DisplayOrder = 1 }, new MasterOptionRow { Key = "B", MarathiName = "", EnglishName = "", DisplayOrder = 2 } };
            var o = Options(src);
            Assert.AreEqual("Alpha", o.CategoryLabel("A"));
            Assert.AreEqual("B", o.CategoryLabel("B"));
        }

        [TestMethod]
        public void Rupees_Use_Indian_Grouping()
        {
            Assert.AreEqual("₹0", IndianRupee.Format(0m));
            Assert.AreEqual("₹1,200", IndianRupee.Format(1200m));
            Assert.AreEqual("₹1,20,000", IndianRupee.Format(120000m));
            Assert.AreEqual("₹12,34,568", IndianRupee.Format(1234567.5m));
            Assert.AreEqual("₹1,00,00,000", IndianRupee.Format(10000000m));
            Assert.AreEqual("₹999", IndianRupee.Format(999m));
        }

        // ------------------------------------------------------------ next code ------------------------------------------------------------

        [TestMethod]
        public void Next_Code_Uses_Highest_Number_Ignores_Gaps_And_Counts_Soft_Deleted_Rows()
        {
            FakePropertyStore store; var svc = Service(out store);
            store.Rows.AddRange(new[] { Row(1, "MJ-001"), Row(2, "MJ-002"), Row(3, "MJ-007", deleted: true), Row(4, "MN-020", "MiniGaale") });
            Assert.AreEqual("MJ-008", svc.NextCode("MajorGaale"));
            Assert.AreEqual("MN-021", svc.NextCode("MiniGaale"));
            Assert.AreEqual("MJ-001", PropertyCodeSuggester.Next("MJ", new string[0]));
            Assert.AreEqual("MJ-1000", PropertyCodeSuggester.Next("MJ", new[] { "MJ-999" }));
            Assert.AreEqual("MJ-0013", PropertyCodeSuggester.Next("MJ", new[] { "MJ-0012", "MJX-500", "MJ-ABC" }));
        }

        [TestMethod]
        public void Next_Code_For_Category_Without_Prefix_Is_Empty_Or_Learned_From_Existing_Codes()
        {
            FakePropertyStore store; var svc = Service(out store);
            Assert.AreEqual(string.Empty, svc.NextCode("SamajMandir"));
            Assert.AreEqual(string.Empty, svc.NextCode(""));
            store.Rows.AddRange(new[] { Row(1, "SM-004", "SamajMandir"), Row(2, "SM-009", "SamajMandir", deleted: true), Row(3, "X12", "SamajMandir") });
            Assert.AreEqual("SM-010", svc.NextCode("SamajMandir"));
        }

        // ------------------------------------------------------------ unique code and validation ------------------------------------------------------------

        [TestMethod]
        public void Unique_Code_Rule_Includes_Soft_Deleted_Rows_With_Two_Messages()
        {
            FakePropertyStore store; var svc = Service(out store);
            store.Rows.Add(Row(1, "MJ-001")); store.Rows.Add(Row(2, "MJ-002", deleted: true));
            var taken = svc.Create(Valid(code: "MJ-001"), Admin);
            Assert.AreEqual(PropertyRules.CodeTaken, taken.Errors.Single(x => x.Field == "PropertyCode").Message);
            Assert.AreEqual("हा मालमत्ता क्रमांक आधीपासून आहे.", PropertyRules.CodeTaken);
            var reserved = svc.Create(Valid(code: "MJ-002"), Admin);
            Assert.AreEqual(PropertyRules.CodeReservedByDeleted, reserved.Errors.Single(x => x.Field == "PropertyCode").Message);
            Assert.AreEqual("हा क्रमांक हटवलेल्या नोंदीसाठी राखीव आहे.", PropertyRules.CodeReservedByDeleted);
            Assert.IsNull(PropertyRules.CodeConflict(new PropertyCodeOwner { Id = 5 }, 5));
            Assert.AreEqual(2, store.Rows.Count);
        }

        [TestMethod]
        public void Validation_Gives_Field_Level_Marathi_Messages()
        {
            var set = Options(FakePropertyOptionSource.FromSqlScript()).Get();
            Property p;
            var e = PropertyRules.Validate(new PropertySaveRequest { AreaSqFt = "abc", MonthlyRent = "-5", AnnualDemand = "10.123", Name = new string('x', 251), Shera = new string('s', 2001) }, set, null, out p);
            var m = e.ToDictionary(x => x.Field, x => x.Message);
            Assert.AreEqual("मालमत्ता प्रकार निवडा.", m["Category"]);
            Assert.AreEqual("मालमत्ता क्रमांक आवश्यक आहे.", m["PropertyCode"]);
            Assert.AreEqual("स्थिती निवडा.", m["Status"]);
            Assert.AreEqual("क्षेत्रफळ संख्या असावी.", m["AreaSqFt"]);
            Assert.AreEqual("मासिक भाडे ऋण असू शकत नाही.", m["MonthlyRent"]);
            Assert.AreEqual("वार्षिक मागणी मध्ये जास्तीत जास्त २ दशांश अंक असावेत.", m["AnnualDemand"]);
            Assert.AreEqual("मालमत्तेचे नाव जास्तीत जास्त 250 अक्षरे असावे.", m["Name"]);
            Assert.AreEqual("शेरा जास्तीत जास्त 2000 अक्षरे असावे.", m["Shera"]);
            e = PropertyRules.Validate(new PropertySaveRequest { Category = "MajorGaale", PropertyCode = "MJ-1", Name = "n", Status = "Rikamy", AnnualDemand = "99999999999999999" }, set, null, out p);
            Assert.AreEqual("वार्षिक मागणी खूप मोठी आहे.", e.Single().Message);
            e = PropertyRules.Validate(new PropertySaveRequest { Category = " MajorGaale ", PropertyCode = " MJ-1 ", Name = " n ", Status = "Rikamy", AreaSqFt = "12.5", MonthlyRent = "", AnnualDemand = "0" }, set, null, out p);
            Assert.AreEqual(0, e.Count);
            Assert.AreEqual("MJ-1", p.PropertyCode); Assert.AreEqual("n", p.Name); Assert.AreEqual(12.5m, p.AreaSqFt); Assert.AreEqual(0m, p.MonthlyRent);
        }

        [TestMethod]
        public void Validation_Accepts_Every_Database_Category_And_Rejects_Unknown()
        {
            var set = Options(FakePropertyOptionSource.FromSqlScript()).Get();
            Assert.AreEqual(9, set.Categories.Count);
            foreach (var c in set.Categories)
            {
                Property p;
                Assert.AreEqual(0, PropertyRules.Validate(Valid(c.Code, "X-1"), set, null, out p).Count, c.Code);
            }
            Property q;
            var e = PropertyRules.Validate(Valid("Unknown", "X-1"), set, null, out q);
            Assert.AreEqual("निवडलेला मालमत्ता प्रकार उपलब्ध नाही.", e.Single(x => x.Field == "Category").Message);
            var s = Valid(); s.Status = "Closed";
            Assert.AreEqual("निवडलेली स्थिती उपलब्ध नाही.", PropertyRules.Validate(s, set, null, out q).Single().Message);
        }

        [TestMethod]
        public void Legacy_Category_And_Status_Are_Kept_On_Edit()
        {
            var set = Options(FakePropertyOptionSource.FromSqlScript()).Get();
            var existing = new Property { Id = 7, Category = "OldCategory", PropertyCode = "OC-1", Status = "OldStatus" };
            var r = Valid("MajorGaale", "CHANGED"); r.Status = "OldStatus";
            Property p;
            Assert.AreEqual(0, PropertyRules.Validate(r, set, existing, out p).Count);
            Assert.AreEqual("OldCategory", p.Category); Assert.AreEqual("OC-1", p.PropertyCode); Assert.AreEqual("OldStatus", p.Status);
            r.Status = "AnotherUnknown";
            Assert.AreEqual("Status", PropertyRules.Validate(r, set, existing, out p).Single().Field);
        }

        // ------------------------------------------------------------ list ------------------------------------------------------------

        [TestMethod]
        public void List_Filters_Pages_And_Maps_Labels()
        {
            FakePropertyStore store; var svc = Service(out store);
            for (var i = 1; i <= 23; i++) store.Rows.Add(Row(i, "MJ-" + i.ToString("000"), i % 2 == 0 ? "MiniGaale" : "MajorGaale", i % 3 == 0 ? "Bhadyane" : "Rikamy", i == 23));
            var first = svc.List(new PropertyListFilter { Page = 1 });
            Assert.AreEqual(22, first.Total); Assert.AreEqual(10, first.Items.Count); Assert.AreEqual(3, first.TotalPages);
            Assert.AreEqual(22, first.Items[0].Id);
            var third = svc.List(new PropertyListFilter { Page = 3, PageSize = 10 });
            Assert.AreEqual(2, third.Items.Count);
            var mini = svc.List(new PropertyListFilter { Category = "MiniGaale", Status = "Bhadyane" });
            Assert.IsTrue(mini.Items.All(x => x.CategoryCode == "MiniGaale" && x.StatusCode == "Bhadyane" && x.CategoryLabel == "Mini गाळे" && x.StatusLabel == "भाडेतत्वावर दिलेली"));
            Assert.AreEqual(3, mini.Total); // 6, 12, 18
            var zone = svc.List(new PropertyListFilter { Search = " Zone4 " });
            Assert.AreEqual("Zone4", store.LastFilter.Search);
            Assert.IsTrue(zone.Total > 0 && zone.Items.All(x => store.Rows.First(r => r.Id == x.Id).Zone == "Zone4"));
            Assert.AreEqual("₹10,000", svc.List(new PropertyListFilter { Search = "MJ-010" }).Items.Single().AnnualDemandText);
            Assert.AreEqual(50, PropertyService.Normalize(new PropertyListFilter { PageSize = 5000 }).PageSize);
            Assert.AreEqual(1, PropertyService.Normalize(new PropertyListFilter { Page = -4 }).Page);
            Assert.AreEqual(10, PropertyService.Normalize(null).PageSize);
        }

        // ------------------------------------------------------------ save, conflict, delete, audit ------------------------------------------------------------

        [TestMethod]
        public void Create_And_Update_Write_Audit_Rows_Without_Personal_Data()
        {
            FakePropertyStore store; var svc = Service(out store);
            var r = Valid(); r.CurrentOccupant = "Ramesh Patil"; r.Address = "Main road 12";
            var created = svc.Create(r, Admin);
            Assert.IsTrue(created.Ok);
            var detail = svc.Detail(created.Id);
            Assert.AreEqual("", detail.Version);
            var edit = Valid(); edit.Name = "Changed"; edit.CurrentOccupant = "Suresh"; edit.Version = detail.Version;
            Assert.IsTrue(svc.Update(created.Id, edit, Admin).Ok);
            Assert.AreEqual(2, store.Audits.Count);
            Assert.AreEqual("Create", store.Audits[0].Action); Assert.AreEqual("Property", store.Audits[0].EntityName); Assert.AreEqual(1, store.Audits[0].UserId);
            Assert.AreEqual("Code=MJ-010; Category=MajorGaale; Status=Rikamy", store.Audits[0].NewValue);
            Assert.AreEqual("Update", store.Audits[1].Action);
            Assert.AreEqual("Changed=Name,Address,CurrentOccupant", store.Audits[1].NewValue);
            foreach (var a in store.Audits)
                foreach (var personal in new[] { "Ramesh", "Suresh", "Main road" })
                    Assert.IsFalse((a.OldValue + a.NewValue).Contains(personal), personal);
            Assert.ThrowsException<InvalidOperationException>(() => PropertyRegisterStore.AuditEntryFor(new ActorContext(), "Create", 1, null, "x", DateTime.UtcNow));
        }

        [TestMethod]
        public void Update_With_Stale_Version_Is_A_Conflict()
        {
            FakePropertyStore store; var svc = Service(out store);
            var row = Row(1, "MJ-001"); row.UpdatedAt = new DateTime(2026, 5, 1, 10, 0, 0); store.Rows.Add(row);
            var stale = Valid(); stale.Version = new DateTime(2026, 4, 1).Ticks.ToString();
            var o = svc.Update(1, stale, Admin);
            Assert.AreEqual("CONFLICT", o.Error.Code);
            Assert.AreEqual("ही नोंद दुसऱ्या वापरकर्त्याने बदलली आहे. कृपया पुन्हा उघडा.", o.Error.Message);
            stale.Version = PropertyService.Version(row);
            Assert.IsTrue(svc.Update(1, stale, Admin).Ok);
            Assert.AreEqual("NOT_FOUND", svc.Update(99, stale, Admin).Error.Code);
        }

        [TestMethod]
        public void Soft_Delete_Sets_Fields_And_Keeps_The_Row()
        {
            FakePropertyStore store; var svc = Service(out store);
            store.Rows.Add(Row(1, "MJ-001"));
            Assert.IsTrue(svc.Delete(1, Admin).IsSuccess);
            var row = store.Rows.Single();
            Assert.IsTrue(row.IsDeleted); Assert.AreEqual(new FakeClock().UtcNow, row.DeletedAt); Assert.AreEqual("admin", row.DeletedBy);
            Assert.AreEqual("Delete", store.Audits.Single().Action);
            Assert.AreEqual("NOT_FOUND", svc.Delete(1, Admin).Error.Code);
            Assert.AreEqual(0, svc.List(new PropertyListFilter()).Total);
        }

        [TestMethod]
        public void Delete_Is_Refused_For_Each_Of_The_Nine_Dependent_Tables()
        {
            var tables = new[] { "AllocationProcesses", "Calculations", "Documents", "GalaTransfers", "LandRegistrations", "Leases", "RecoveryCases", "SchemeApplications", "Complaints" };
            CollectionAssert.AreEqual(tables, PropertyService.DependentTables.Select(x => x.Key).ToArray());
            foreach (var t in tables)
            {
                FakePropertyStore store; var svc = Service(out store);
                store.Rows.Add(Row(1, "MJ-001"));
                store.Dependents[1] = new Dictionary<string, int> { { t, 2 } };
                var r = svc.Delete(1, Admin);
                Assert.AreEqual("DEPENDENTS", r.Error.Code, t);
                var name = PropertyService.DependentTables.Single(x => x.Key == t).Value;
                StringAssert.Contains(r.Error.Message, name + " 2", t);
                Assert.IsFalse(store.Rows.Single().IsDeleted, t);
                Assert.AreEqual(0, store.Audits.Count, t);
            }
        }

        // ------------------------------------------------------------ options provider ------------------------------------------------------------

        [TestMethod]
        public void Provider_Orders_Master_Rows_Then_Values_In_Use()
        {
            var src = new FakePropertyOptionSource();
            src.Groups["PropertyCategory"] = new List<MasterOptionRow> { new MasterOptionRow { Key = "B", MarathiName = "बी", DisplayOrder = 2 }, new MasterOptionRow { Key = "A", MarathiName = "ए", DisplayOrder = 1 } };
            src.InUse[PropertyColumn.Category] = new List<string> { "Z", "B", "C", " " };
            src.InUse[PropertyColumn.Ward] = new List<string> { "Prabhag 11", "Prabhag 2", "Prabhag 1" };
            var set = Options(src).Get();
            CollectionAssert.AreEqual(new[] { "A", "B", "C", "Z" }, set.Categories.Select(x => x.Code).ToArray());
            CollectionAssert.AreEqual(new[] { "ए", "बी", "C", "Z" }, set.Categories.Select(x => x.Label).ToArray());
            CollectionAssert.AreEqual(new[] { "Prabhag 1", "Prabhag 2", "Prabhag 11" }, set.Wards.Select(x => x.Code).ToArray());
            Assert.AreEqual(0, set.Warnings.Count);
        }

        [TestMethod]
        public void Provider_Adds_Zone1_To_Zone8_Only_When_Stored_Zones_Use_That_Format()
        {
            var src = new FakePropertyOptionSource();
            src.InUse[PropertyColumn.Zone] = new List<string> { "Zone3", "Zone1" };
            CollectionAssert.AreEqual(Enumerable.Range(1, 8).Select(i => "Zone" + i).ToArray(), Options(src).Get().Zones.Select(x => x.Code).ToArray());
            src.InUse[PropertyColumn.Zone] = new List<string> { "Zone3", "North" };
            CollectionAssert.AreEqual(new[] { "North", "Zone3" }, Options(src).Get().Zones.Select(x => x.Code).ToArray());
            src.InUse[PropertyColumn.Zone] = new List<string>();
            Assert.AreEqual(0, Options(src).Get().Zones.Count);
        }

        [TestMethod]
        public void Provider_Uses_Ward_And_Zone_Master_Groups_When_Configured()
        {
            var src = new FakePropertyOptionSource();
            src.Groups["Wards"] = new List<MasterOptionRow> { new MasterOptionRow { Key = "W1", MarathiName = "प्रभाग १", DisplayOrder = 1 } };
            src.InUse[PropertyColumn.Ward] = new List<string> { "W9" };
            var o = new PropertyMasterOptions(src, new FakeClock(), new PropertyOptionGroups { CategoryGroup = "PropertyCategory", StatusGroup = "PropertyStatus", WardGroup = "Wards" });
            CollectionAssert.AreEqual(new[] { "W1", "W9" }, o.Get().Wards.Select(x => x.Code).ToArray());
        }

        [TestMethod]
        public void Provider_Caches_For_Five_Minutes()
        {
            var src = FakePropertyOptionSource.FromSqlScript(); var clock = new FakeClock();
            var o = Options(src, clock);
            o.Get(); var calls = src.MasterCalls;
            clock.UtcNow = clock.UtcNow.AddMinutes(4).AddSeconds(59); o.Get();
            Assert.AreEqual(calls, src.MasterCalls);
            clock.UtcNow = clock.UtcNow.AddSeconds(1); o.Get();
            Assert.IsTrue(src.MasterCalls > calls);
            calls = src.MasterCalls; o.Invalidate(); o.Get();
            Assert.IsTrue(src.MasterCalls > calls);
            Assert.AreEqual(TimeSpan.FromMinutes(5), PropertyMasterOptions.CacheDuration);
        }

        [TestMethod]
        public void Provider_Failure_Gives_Empty_Lists_Plus_Warning_Never_A_Fallback_List()
        {
            var src = new FakePropertyOptionSource { ThrowMaster = true, ThrowInUse = true };
            var o = Options(src);
            var set = o.Get();
            Assert.AreEqual(0, set.Categories.Count + set.Statuses.Count + set.Wards.Count + set.Zones.Count);
            CollectionAssert.AreEqual(new[] { PropertyMasterOptions.MasterReadWarning, PropertyMasterOptions.InUseReadWarning }, set.Warnings);
            var calls = src.MasterCalls; o.Get();
            Assert.IsTrue(src.MasterCalls > calls, "a failed read must not be cached");
            src.ThrowInUse = false; src.InUse[PropertyColumn.Category] = new List<string> { "MajorGaale" };
            set = o.Get();
            CollectionAssert.AreEqual(new[] { "MajorGaale" }, set.Categories.Select(x => x.Code).ToArray());
            CollectionAssert.AreEqual(new[] { PropertyMasterOptions.MasterReadWarning }, set.Warnings);
        }

        // ------------------------------------------------------------ offline SQL translation ------------------------------------------------------------

        private static SmcDbContext OfflineContext(out SqlConnection connection)
        {
            var builder = new DbModelBuilder();
            SmcDbContext.ConfigureModel(builder);
            var compiled = builder.Build(new DbProviderInfo("System.Data.SqlClient", "2012")).Compile();
            connection = new SqlConnection("Data Source=(local);Initial Catalog=SMC_BhoomiMalmatta;Integrated Security=True;Connect Timeout=1");
            return new SmcDbContext(connection, compiled);
        }

        [TestMethod]
        public void List_Options_And_Dependent_Queries_Translate_To_Sql_Without_Opening_Database()
        {
            SqlConnection connection;
            using (var db = OfflineContext(out connection))
            using (connection)
            {
                var list = PropertyRegisterStore.ListQuery(db, new PropertyListFilter { Search = "Zone4", Category = "MajorGaale", Status = "Rikamy", Page = 2, PageSize = 10 }).Skip(10).Take(10).ToString();
                foreach (var col in new[] { "PropertyCode", "Name", "Ward", "Zone", "CurrentOccupant", "TpNumber", "SurveyNumber", "Address" })
                    Assert.IsTrue(Regex.IsMatch(list, @"\[" + col + @"\] LIKE"), col + ": " + list);
                StringAssert.Contains(list, "[IsDeleted]");
                StringAssert.Contains(list, "ORDER BY");
                StringAssert.Contains(list, "OFFSET");
                Assert.IsFalse(list.Contains("[Shera]"), "list must select only the displayed columns");
                Assert.IsFalse(list.Contains("[MonthlyRent]"), "list must select only the displayed columns");

                var master = PropertyRegisterStore.MasterQuery(db, "PropertyCategory").ToString();
                StringAssert.Contains(master, "MR_AST_MasterData");
                StringAssert.Contains(master, "[IsActive]");
                StringAssert.Contains(master, "[IsDeleted]");
                StringAssert.Contains(master, "[DisplayOrder]");
                foreach (PropertyColumn c in Enum.GetValues(typeof(PropertyColumn)))
                    StringAssert.Contains(PropertyRegisterStore.DistinctQuery(db, c).ToString(), "DISTINCT");

                var deps = PropertyRegisterStore.DependentQueries(db, 5);
                CollectionAssert.AreEqual(PropertyService.DependentTables.Select(x => x.Key).ToArray(), deps.Keys.ToArray());
                foreach (var d in deps)
                {
                    var sql = d.Value.ToString();
                    StringAssert.Contains(sql, "[PropertyId]", d.Key);
                    StringAssert.Contains(sql, "[IsDeleted]", d.Key);
                }
                StringAssert.Contains(deps["Complaints"].ToString(), "TR_CFC_Complaints");
            }
        }

        [TestMethod]
        public void Master_Prefix_Query_Translates_To_Like_With_Filters_Order_And_Only_Needed_Columns()
        {
            SqlConnection connection;
            using (var db = OfflineContext(out connection))
            using (connection)
            {
                var sql = PropertyRegisterStore.MasterPrefixQuery(db, "Prabhag_").ToString();
                StringAssert.Contains(sql, "MR_AST_MasterData");
                Assert.IsTrue(Regex.IsMatch(sql, @"\[GroupName\] LIKE"), sql);
                StringAssert.Contains(sql, "[IsActive]");
                StringAssert.Contains(sql, "[IsDeleted]");
                Assert.IsTrue(Regex.IsMatch(sql, @"ORDER BY .*\[GroupName\].*\[DisplayOrder\].*\[Id\]", RegexOptions.Singleline), sql);
                foreach (var col in new[] { "[Description]", "[Shera]", "[CreatedBy]", "[CreatedAt]", "[UpdatedAt]", "[DeletedAt]" })
                    Assert.IsFalse(sql.Contains(col), col + " must not be selected: " + sql);
            }
            Assert.AreEqual(0, new PropertyRegisterStore().MasterRowsWithPrefix("  ").Count, "a blank prefix must not match every group");
        }

        [TestMethod]
        public void Fake_Prefix_Source_Returns_Matching_Groups_In_Order()
        {
            var src = new FakePropertyOptionSource();
            src.Groups["Prabhag_Zone2"] = new List<MasterOptionRow> { new MasterOptionRow { Key = "P21", DisplayOrder = 2 }, new MasterOptionRow { Key = "P20", DisplayOrder = 1 } };
            src.Groups["Prabhag_Zone1"] = new List<MasterOptionRow> { new MasterOptionRow { Key = "P10", DisplayOrder = 1 } };
            src.Groups["PropertyCategory"] = new List<MasterOptionRow> { new MasterOptionRow { Key = "MajorGaale", DisplayOrder = 1 } };
            var rows = src.MasterRowsWithPrefix("Prabhag_");
            CollectionAssert.AreEqual(new[] { "P10", "P20", "P21" }, rows.Select(x => x.Key).ToArray());
            CollectionAssert.AreEqual(new[] { "Prabhag_Zone1", "Prabhag_Zone2", "Prabhag_Zone2" }, rows.Select(x => x.GroupName).ToArray());
            Assert.AreEqual(0, src.MasterRowsWithPrefix("").Count);
        }

        [TestMethod]
        public void Soft_Delete_Is_An_Update_Never_A_Delete()
        {
            var sql = PropertyRegisterStore.SoftDeleteSql;
            StringAssert.StartsWith(sql, "UPDATE dbo.MR_AST_Properties SET IsDeleted = 1, DeletedAt = @p0, DeletedBy = @p1");
            Assert.IsFalse(Regex.IsMatch(sql, @"\bDELETE\b", RegexOptions.IgnoreCase));
            var store = File.ReadAllText(Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Data", "Repositories", "PropertyRegisterStore.cs"));
            Assert.IsFalse(Regex.IsMatch(store, @"DELETE\s+FROM", RegexOptions.IgnoreCase));
            Assert.IsFalse(store.Contains(".Remove("));
            Assert.IsFalse(store.Contains("RemoveRange("));
        }

        // ------------------------------------------------------------ SQL file ------------------------------------------------------------

        [TestMethod]
        public void Master_Data_Script_Is_Idempotent_Data_Only_With_The_Expected_Rows()
        {
            var text = ScriptText();
            var code = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            code = Regex.Replace(code, @"--[^\n]*", string.Empty);
            foreach (var forbidden in new[] { @"\bCREATE\b", @"\bALTER\b", @"\bDROP\b", @"\bTRUNCATE\b", @"\bDELETE\b", @"\bUPDATE\b", @"\bMERGE\b", @"DB_NAME\(" })
                Assert.IsFalse(Regex.IsMatch(code, forbidden, RegexOptions.IgnoreCase), forbidden);
            StringAssert.Contains(code, "WHERE NOT EXISTS (SELECT 1 FROM dbo.MR_AST_MasterData AS m WHERE m.GroupName = r.GroupName AND m.[Key] = r.[Key])");
            StringAssert.Contains(code, "N'master-data-script', SYSUTCDATETIME(), 0");
            StringAssert.Contains(code, "INSERT INTO dbo.MR_AST_MasterData (GroupName, [Key], MarathiName, EnglishName, IsActive, DisplayOrder, CreatedBy, CreatedAt, IsDeleted)");
            StringAssert.Contains(text, "PURPOSE"); StringAssert.Contains(text, "ORDER / HOW TO RUN"); StringAssert.Contains(text, "ROLLBACK");

            var expected = new[]
            {
                "PropertyCategory|MajorGaale|Major गाळे|Major Gaale|1", "PropertyCategory|MiniGaale|Mini गाळे|Mini Gaale|2",
                "PropertyCategory|LandFee|Land Fee (भुई भाडे)|Land Fee|3", "PropertyCategory|SamajMandir|समाज मंदिर|Samaj Mandir|4",
                "PropertyCategory|Abhyasika|अभ्यासिका|Abhyasika|5", "PropertyCategory|Gaale256|256 गाळे|256 Gaale|6", "PropertyCategory|TP3_23|TP-3/23|TP-3/23|7",
                "PropertyCategory|AdhikrutKhoke|अधिकृत खोके|Authorised kiosks|8", "PropertyCategory|OtherMunicipalProperty|इतर भाडेतत्वावरील मनपा मालमत्ता|Other municipal property|9",
                "PropertyStatus|Rikamy|रिक्त|Vacant|1", "PropertyStatus|Bhadyane|भाडेतत्वावर दिलेली|Leased|2"
            };
            CollectionAssert.AreEqual(expected, ScriptRows().Select(r => string.Join("|", r)).ToArray());
        }
    }
}
