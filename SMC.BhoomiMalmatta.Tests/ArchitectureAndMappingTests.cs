using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SMC.BhoomiMalmatta.Data.Safety;
using SMC.BhoomiMalmatta.Models.Constants;

namespace SMC.BhoomiMalmatta.Tests
{
    internal static class Repo
    {
        public static string Root
        {
            get
            {
                var d = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                while (d != null && !File.Exists(Path.Combine(d.FullName, "SMC.BhoomiMalmatta.sln"))) d = d.Parent;
                if (d == null) throw new InvalidOperationException("Solution root not found");
                return d.FullName;
            }
        }
        public static IEnumerable<string> Files(string pattern)
        {
            return Directory.EnumerateFiles(Root, pattern, SearchOption.AllDirectories)
                .Where(f => f.IndexOf(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) < 0 && f.IndexOf(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) < 0
                            && f.IndexOf(Path.DirectorySeparatorChar + "packages" + Path.DirectorySeparatorChar) < 0 && f.IndexOf(".scratch") < 0);
        }
    }

    [TestClass]
    public class DatabaseSafetyTests
    {
        [DataTestMethod]
        [DataRow("Server=localhost;Database=SMC_BhoomiMalmatta;Trusted_Connection=True")]
        [DataRow("Data Source=localhost;Initial Catalog=SMC_BhoomiMalmatta;Integrated Security=SSPI")]
        public void Exact_Database_Name_Is_Accepted(string cs) { Assert.IsTrue(DatabaseSafety.AssertSafe(cs).IsSuccess); }

        [DataTestMethod]
        [DataRow("Server=localhost;Database=master;Trusted_Connection=True")] [DataRow("Server=localhost;Database=SMC_BhoomiMalmatta_Copy;Trusted_Connection=True")]
        [DataRow("Server=localhost;Database=smc_bhoomimalmatta;Trusted_Connection=True")] [DataRow("Server=localhost;Trusted_Connection=True")] [DataRow("")]
        public void Any_Other_Or_Missing_Database_Is_Refused(string cs) { Assert.IsTrue(DatabaseSafety.AssertSafe(cs).IsFailure); }

        [TestMethod] public void AttachDbFilename_Is_Refused() { Assert.AreEqual("DB_ATTACH_FORBIDDEN", DatabaseSafety.AssertSafe("Server=.;Database=SMC_BhoomiMalmatta;AttachDbFilename=C:\\x.mdf").Error.Code); }
        [TestMethod] public void Redact_Hides_Passwords()
        {
            var r = DatabaseSafety.Redact("Server=s;Database=SMC_BhoomiMalmatta;User Id=u;Password=Sup3rSecret!");
            Assert.IsFalse(r.Contains("Sup3rSecret")); Assert.IsTrue(r.Contains("***"));
        }
    }

    [TestClass]
    public class ArchitectureTests
    {
        [TestMethod] public void Every_Project_Targets_NetFramework_48_And_There_Is_No_SdkStyle_Project()
        {
            var projects = Repo.Files("*.csproj").ToList();
            Assert.AreEqual(5, projects.Count);
            foreach (var p in projects)
            {
                var t = File.ReadAllText(p);
                Assert.IsTrue(t.Contains("<TargetFrameworkVersion>v4.8</TargetFrameworkVersion>"), p);
                Assert.IsFalse(t.Contains("Sdk=\"Microsoft.NET.Sdk"), p);
                Assert.IsFalse(Regex.IsMatch(t, "<TargetFramework>"), p);
            }
        }
        [TestMethod] public void No_Forbidden_Packages_Or_Technologies_Are_Referenced()
        {
            var banned = new[] { "Microsoft.AspNetCore", "EntityFrameworkCore", "Microsoft.EntityFrameworkCore", "Microsoft.AspNet.Mvc", "System.Web.Mvc", "Microsoft.AspNet.Razor", "Microsoft.AspNet.WebPages", "Blazor", "Sqlite", "Npgsql", "\"react\"", "\"vue\"", "@angular" };
            var files = Repo.Files("*.csproj").Concat(Repo.Files("packages.config")).Concat(Repo.Files("package.json")).Concat(Repo.Files("Web.config"));
            foreach (var f in files) { var t = File.ReadAllText(f); foreach (var b in banned) Assert.IsFalse(t.Contains(b), b + " in " + f); }
            Assert.AreEqual(0, Repo.Files("package.json").Count());
            Assert.AreEqual(0, Repo.Files("*.cshtml").Count());
        }
        [TestMethod] public void No_Net_Core_Or_Newer_Target_Remains()
        {
            foreach (var f in Repo.Files("*.csproj").Concat(Repo.Files("*.props"))) { var t = File.ReadAllText(f); Assert.IsFalse(Regex.IsMatch(t, @"net(5|6|7|8|9)\.0|netcoreapp|netstandard"), f); }
        }
        [TestMethod] public void Source_Never_Calls_Destructive_Or_Schema_Changing_Database_Apis()
        {
            var forbidden = new[] { "CreateDatabaseIfNotExists", "DropCreateDatabase", "MigrateDatabaseToLatestVersion", "Database.Create(", "Database.Delete(", "CreateIfNotExists", "DbMigrator", "DROP TABLE", "DROP DATABASE", "TRUNCATE TABLE", "ALTER TABLE", "CREATE TABLE", "CREATE DATABASE", "DELETE FROM" };
            foreach (var f in Repo.Files("*.cs").Where(f => f.IndexOf(Path.DirectorySeparatorChar + "Tests" + Path.DirectorySeparatorChar) < 0 && f.IndexOf("SMC.BhoomiMalmatta.Tests") < 0))
            { var t = File.ReadAllText(f); foreach (var w in forbidden) Assert.IsFalse(t.Contains(w), w + " in " + f); }
        }
        [TestMethod] public void EF_Initializer_Is_Disabled_And_There_Are_No_Migrations()
        {
            var ctx = File.ReadAllText(Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Data", "SmcDbContext.cs"));
            Assert.IsTrue(ctx.Contains("Database.SetInitializer<SmcDbContext>(null)"));
            Assert.IsFalse(Directory.Exists(Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Data", "Migrations")));
            Assert.AreEqual(0, Repo.Files("Configuration.cs").Count(f => File.ReadAllText(f).Contains("DbMigrationsConfiguration")));
            var web = File.ReadAllText(Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Web", "Web.config"));
            Assert.IsTrue(web.Contains("disableDatabaseInitialization=\"true\""));
        }
        [TestMethod] public void Config_Files_Contain_No_Secrets()
        {
            foreach (var f in Repo.Files("*.config").Concat(Repo.Files("*.json")).Concat(Repo.Files("*.pubxml")).Where(f => !f.EndsWith("database-schema.json") && !f.EndsWith("application-vocabulary.json")))
            {
                var t = File.ReadAllText(f);
                Assert.IsFalse(Regex.IsMatch(t, @"(Password|Pwd)\s*=\s*[^;""'\s<]{3,}", RegexOptions.IgnoreCase), f);
                Assert.IsFalse(Regex.IsMatch(t, @"<add key=""Sms\.(Password|Credential)"" value=""[^""]+""", RegexOptions.IgnoreCase), f);
            }
            Assert.AreEqual(0, Repo.Files("*.pfx").Count());
        }
    }

    [TestClass]
    public class MappingAccuracyTests
    {
        private static Dictionary<string, object> Load()
        {
            var text = File.ReadAllText(Path.Combine(Repo.Root, "database", "database-schema.json"));
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Deserialize<Dictionary<string, object>>(text);
        }
        private static string Plural(string table) { return Regex.Replace(table, "^(MR|TR)_(AST|CFC)_", ""); }

        [TestMethod] public void Every_Existing_Table_And_Column_Is_Mapped_Exactly()
        {
            var tables = ((System.Collections.ArrayList)Load()["tables"]).Cast<Dictionary<string, object>>().Where(t => (string)t["name"] != "__EFMigrationsHistory").ToList();
            Assert.AreEqual(28, tables.Count);
            var maps = Directory.GetFiles(Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Data", "Mappings"), "*Map.cs");
            Assert.AreEqual(28, maps.Length);
            var all = string.Join("\n", maps.Select(File.ReadAllText));
            var context = File.ReadAllText(Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Data", "SmcDbContext.cs"));
            var columnsChecked = 0;
            foreach (var t in tables)
            {
                var name = (string)t["name"];
                var map = maps.Select(File.ReadAllText).Single(m => m.Contains("ToTable(\"" + name + "\", \"dbo\")"));
                Assert.IsTrue(Regex.IsMatch(name, "^(MR|TR)_(AST|CFC)_"), name);
                Assert.IsTrue(context.Contains(" " + Plural(name) + " {"), "DbSet for " + name);
                foreach (var c in ((System.Collections.ArrayList)t["columns"]).Cast<Dictionary<string, object>>())
                {
                    var col = (string)c["name"];
                    var line = map.Split('\n').SingleOrDefault(l => l.Contains(".HasColumnName(\"" + col + "\")"));
                    Assert.IsNotNull(line, name + "." + col);
                    var type = (string)c["sqlDataType"];
                    if (type == "nvarchar" || type == "varchar")
                    {
                        var full = (string)c["sqlTypeFull"];
                        if (full.Contains("(max)")) Assert.IsTrue(line.Contains(".IsMaxLength()"), name + "." + col);
                        else Assert.IsTrue(line.Contains(".HasMaxLength(" + Convert.ToInt32(c["maxLengthChars"]) + ")"), name + "." + col + " length");
                        Assert.AreEqual(!(bool)c["isNullable"], line.Contains(".IsRequired()"), name + "." + col + " nullability");
                    }
                    if (type == "decimal") Assert.IsTrue(line.Contains(".HasPrecision(" + Convert.ToInt32(c["precision"]) + ", " + Convert.ToInt32(c["scale"]) + ")"), name + "." + col);
                    if ((bool)c["isIdentity"]) Assert.IsTrue(line.Contains("DatabaseGeneratedOption.Identity"), name + "." + col);
                    if (type == "timestamp") Assert.IsTrue(line.Contains(".IsRowVersion()"), name + "." + col);
                    columnsChecked++;
                }
                var pk = ((System.Collections.ArrayList)((Dictionary<string, object>)t["primaryKey"])["columns"]).Cast<Dictionary<string, object>>().Select(x => (string)x["column"]).ToList();
                Assert.IsTrue(map.Contains("HasKey(e => e." + pk[0] + ")"), name + " key");
            }
            Assert.AreEqual(Convert.ToInt32(((Dictionary<string, object>)Load()["summary"])["columnCount"]) - 2, columnsChecked); // minus the 2 columns of __EFMigrationsHistory
        }
        [TestMethod] public void Computed_And_RowVersion_Columns_Are_Not_Written_By_The_Application()
        {
            var land = File.ReadAllText(Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Data", "Mappings", "LandRegistrationMap.cs"));
            Assert.IsTrue(land.Contains("RecordNumber") && land.Contains("DatabaseGeneratedOption.Computed"));
            var zone = File.ReadAllText(Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Data", "Mappings", "DemandApplicationZoneWorkflowMap.cs"));
            Assert.IsTrue(zone.Contains("Property(e => e.RowVersion).HasColumnName(\"RowVersion\").IsRowVersion()"));
        }
    }
}


namespace SMC.BhoomiMalmatta.Tests
{
    [TestClass]
    public class OfficerLayoutRegressionTests
    {
        [TestMethod]
        public void Department_Master_Has_No_Aside_Hamburger_Or_Scrim_And_Menu_Links_Exist()
        {
            var web = Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Web");
            var master = File.ReadAllText(Path.Combine(web, "Site.Master"));
            Assert.IsFalse(master.Contains("<aside"), "Department layout must not render a sidebar.");
            Assert.IsFalse(master.Contains("btnMenu") || master.Contains("sideNav") || master.Contains("id=\"scrim\""));
            foreach (var page in Directory.GetFiles(Path.Combine(web, "Department"), "*.*", SearchOption.AllDirectories).Where(f => f.EndsWith(".aspx") || f.EndsWith(".ascx")))
                Assert.IsFalse(File.ReadAllText(page).Contains("<aside"), "Sidebar markup in " + page);
            foreach (var link in Regex.Matches(master, "href=\"~/Department/([^\"]+)\"").Cast<Match>())
                Assert.IsTrue(File.Exists(Path.Combine(web, "Department", link.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar))), link.Value);
        }

        [TestMethod]
        public void Officer_Page_Has_One_Page_Logout_And_No_Delete_Action()
        {
            var web = Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Web");
            var page = File.ReadAllText(Path.Combine(web, "Department", "Applications.aspx"));
            var masterCode = File.ReadAllText(Path.Combine(web, "Site.Master.cs"));
            Assert.AreEqual(1, Regex.Matches(page, "ID=\"btnLogout\"").Count);
            Assert.IsTrue(masterCode.Contains("if (officerHome) phUser.Visible = false") && masterCode.Contains("lnkLogout.Visible = !officerHome"));
            Assert.IsFalse(page.IndexOf("Delete", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.IsFalse(page.Contains("OfficerProcessedGrid"));
            Assert.IsFalse(page.Contains("माझे प्रक्रिया केलेले अर्ज"));
            Assert.IsTrue(page.Contains("अर्ज <asp:Literal ID=\"litApplicationCount\""));
        }

        [TestMethod]
        public void Officer_Date_Formatter_Uses_Devanagari_Digits_When_Enabled()
        {
            var date = new DateTime(2026, 10, 1, 16, 5, 9);
            Assert.AreEqual("१/१०/२०२६", SMC.BhoomiMalmatta.Models.Dto.OfficerDateFormatter.Format(date, false, true));
            Assert.AreEqual("1/10/2026, 4:05:09 PM", SMC.BhoomiMalmatta.Models.Dto.OfficerDateFormatter.Format(date, true, false));
        }
    }
}


namespace SMC.BhoomiMalmatta.Tests
{
    [TestClass]
    public class OfficerAsyncSubmissionTests
    {
        [TestMethod]
        public void Officer_Login_Uses_Applications_Landing_And_Only_Allows_Demand_Flow_ReturnUrls()
        {
            var auth = typeof(SMC.BhoomiMalmatta.Web.Security.AuthCookies);
            var landing = auth.GetMethod("EmployeeLandingUrl");
            Assert.IsNotNull(landing);
            Assert.AreEqual("/Department/Applications.aspx", SMC.BhoomiMalmatta.Web.Security.AuthCookies.EmployeeLandingUrl(null, Roles.JE, "/Department/Dashboard.aspx", "/Department/Applications.aspx"));
            Assert.AreEqual("/Department/Applications.aspx?view=all", SMC.BhoomiMalmatta.Web.Security.AuthCookies.EmployeeLandingUrl("/Department/Applications.aspx?view=all", Roles.OS, "/Department/Dashboard.aspx", "/Department/Applications.aspx"));
            Assert.AreEqual("/Department/ApplicationDetail.aspx?id=7", SMC.BhoomiMalmatta.Web.Security.AuthCookies.EmployeeLandingUrl("/Department/ApplicationDetail.aspx?id=7", Roles.AssistantCommissioner, "/Department/Dashboard.aspx", "/Department/Applications.aspx"));
            Assert.AreEqual("/Department/Reports.aspx", SMC.BhoomiMalmatta.Web.Security.AuthCookies.EmployeeLandingUrl("/Department/Reports.aspx", Roles.JE, "/Department/Dashboard.aspx", "/Department/Applications.aspx"));
            Assert.AreEqual("/Department/Applications.aspx", SMC.BhoomiMalmatta.Web.Security.AuthCookies.EmployeeLandingUrl("/Department/Properties.aspx", Roles.JE, "/Department/Dashboard.aspx", "/Department/Applications.aspx"));
            Assert.AreEqual("/Department/Applications.aspx", SMC.BhoomiMalmatta.Web.Security.AuthCookies.EmployeeLandingUrl("//evil.example/Department/Applications.aspx", Roles.OS, "/Department/Dashboard.aspx", "/Department/Applications.aspx"));
            var dashboard = File.ReadAllText(Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Web", "Department", "Dashboard.aspx.cs"));
            Assert.IsTrue(dashboard.Contains("Response.Redirect(\"~/Department/Applications.aspx\", false)"));
            var login = File.ReadAllText(Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Web", "Login.aspx.cs"));
            Assert.IsTrue(login.Contains("EmployeeLandingUrl(Request.QueryString[\"ReturnUrl\"]"));
        }

        [TestMethod]
        public void Officer_Login_LastLogin_Write_Does_Not_Read_The_User_Again_And_Audit_Remains()
        {
            var userStore = File.ReadAllText(Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Data", "Repositories", "UserStore.cs"));
            var auth = File.ReadAllText(Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Business", "Security", "AuthService.cs"));
            var recordLogin = MethodBody(userStore, "public void RecordLogin(");
            Assert.IsTrue(recordLogin.Contains("ExecuteSqlCommand(\"UPDATE dbo.MR_AST_Users SET LastLoginAt"));
            Assert.IsFalse(recordLogin.Contains("FirstOrDefault"));
            Assert.IsTrue(auth.Contains("_users.RecordLogin(u.Id, now)"));
            Assert.IsTrue(auth.Contains("Audit(u, AuditActions.LoginSuccess, ip, now)"));
        }

        [TestMethod]
        public void Admin_Staff_Dashboard_Contains_Only_Application_Counter_Cards_And_No_Register_Query()
        {
            var web = Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Web");
            var markup = File.ReadAllText(Path.Combine(web, "Department", "Dashboard.aspx"));
            var code = File.ReadAllText(Path.Combine(web, "Department", "Dashboard.aspx.cs"));
            var dtoType = typeof(SMC.BhoomiMalmatta.Models.Dto.DashboardCounts);
            var names = dtoType.GetProperties().Select(x => x.Name).ToArray();
            CollectionAssert.AreEquivalent(new[] { "Total", "Submitted", "UnderScrutiny", "FeePending", "ApprovalPending", "Approved", "Rejected", "JePending", "OsPending", "AcPending", "PaymentPending", "Legacy" }, names);
            foreach (var forbidden in new[] { "मालमत्ता", "वसुली व आर्थिक सारांश", "देण्याची कार्यपद्धती", "हस्तांतरण", "विविध उपक्रम", "GetRegisterDashboard", "AppServices.Register" })
            { Assert.IsFalse(markup.Contains(forbidden), forbidden); Assert.IsFalse(code.Contains(forbidden), forbidden); }
            Assert.IsTrue(code.Contains("GetDashboard(ZoneScope)"));
            Assert.IsTrue(code.IndexOf("CompleteRequest()", StringComparison.Ordinal) < code.IndexOf("GetDashboard(ZoneScope)", StringComparison.Ordinal));
            Assert.IsTrue(code.Contains("view=all&stage="));
            Assert.IsTrue(code.Contains("view=all&route="));
        }

        [TestMethod]
        public void Officer_Reports_Are_Application_Only_And_Register_Data_Routes_Are_Admin_Only()
        {
            var expectedReports = new[] { "status", "service", "zone", "month", "je-pending", "os-pending", "ac-pending", "payment-pending", "approved", "rejected" };
            foreach (var role in new[] { Roles.JE, Roles.OS, Roles.AssistantCommissioner })
                CollectionAssert.AreEquivalent(expectedReports, SMC.BhoomiMalmatta.Web.Services.ReportBuilder.CatalogForRole(role).Select(x => x.Key).ToArray());
            Assert.IsFalse(SMC.BhoomiMalmatta.Web.Services.ReportBuilder.IsApplicationReport("prop-list"));
            Assert.IsTrue(SMC.BhoomiMalmatta.Web.Services.ReportBuilder.CatalogForRole(Roles.Admin).Any(x => x.Key == "prop-list"));
            var root = Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Web");
            foreach (var page in new[] { "Properties.aspx.cs", "Calculations.aspx.cs", "Recovery.aspx.cs", "Transfers.aspx.cs", "Tenders.aspx.cs", "Schemes.aspx.cs" })
                Assert.IsTrue(File.ReadAllText(Path.Combine(root, "Department", page)).Contains("RequiredRoles { get { return new[] { Roles.Admin }; } }"), page);
            Assert.IsTrue(File.ReadAllText(Path.Combine(root, "Department", "Users.aspx.cs")).Contains("new[] { Roles.Admin, Roles.Staff }"));
            Assert.IsTrue(File.ReadAllText(Path.Combine(root, "Department", "Masters.aspx.cs")).Contains("new[] { Roles.Admin, Roles.Staff }"));
            var config = File.ReadAllText(Path.Combine(root, "Web.config"));
            foreach (var page in new[] { "Properties.aspx", "Calculations.aspx", "Recovery.aspx", "Transfers.aspx", "Tenders.aspx", "Schemes.aspx" })
                Assert.IsTrue(config.Contains("location path=\"Department/" + page + "\""), page);
            Assert.IsTrue(config.Contains("location path=\"Department/Users.aspx\"><system.web><authorization><allow roles=\"Admin,Staff\""));
            Assert.IsTrue(config.Contains("location path=\"Department/Masters.aspx\"><system.web><authorization><allow roles=\"Admin,Staff\""));
            var api = File.ReadAllText(Path.Combine(root, "Api", "MoreControllers.cs"));
            Assert.IsTrue(api.Contains("!ReportBuilder.IsApplicationReport(name)"));
            Assert.IsTrue(api.Contains("CatalogForRole(Me.Role)"));
            var download = File.ReadAllText(Path.Combine(root, "Download.ashx.cs"));
            Assert.IsTrue(download.Contains("me.Role != Roles.Admin")); // /Download.ashx?type=xferdoc|regdoc|xfercert
            var pageGuard = File.ReadAllText(Path.Combine(root, "Security", "SmcPage.cs"));
            Assert.IsTrue(pageGuard.Contains("Response.StatusCode = 403"));
            var global = File.ReadAllText(Path.Combine(root, "Global.asax.cs"));
            Assert.IsTrue(global.Contains("Application_EndRequest"));
            Assert.IsTrue(global.Contains("Response.StatusCode = 403"));
            Assert.IsTrue(global.Contains("Response.RedirectLocation = null"));
        }

        [TestMethod]
        public void Officer_Applications_Path_Uses_Only_Demand_Query_Surface()
        {
            var root = Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Web");
            var dashboard = File.ReadAllText(Path.Combine(root, "Department", "Dashboard.aspx.cs"));
            var page = File.ReadAllText(Path.Combine(root, "Department", "Applications.aspx.cs"));
            var query = File.ReadAllText(Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Data", "Repositories", "QueryService.cs"));
            Assert.IsFalse(page.Contains("AppServices.Register"));
            Assert.IsFalse(dashboard.Contains("AppServices.Register"));
            Assert.IsTrue(page.Contains("OfficerApplicationPageData.Load(AppServices.Query"));
            Assert.IsTrue(page.Contains("AppServices.Masters.GetServices()"));
            Assert.IsTrue(query.Contains("GetDashboard(int? zoneScope)"));
            foreach (var table in new[] { "Properties", "Leases", "RecoveryCases", "GalaTransfers", "Calculations", "SchemeApplications", "Complaints" })
                Assert.IsFalse(dashboard.Contains("db." + table), table + " must not be queried on the officer landing path");
        }

        [TestMethod]
        public void Officer_Page_Query_Plan_Uses_One_List_And_One_Kpi_Query_With_Counting_Fake()
        {
            var fake = new DocumentAndCertificateTests.FakeQuery();
            SMC.BhoomiMalmatta.Web.Services.OfficerApplicationPageData.Load(fake, Roles.JE, 23, 4, new SMC.BhoomiMalmatta.Models.Dto.ApplicationFilter { Page = 1, PageSize = 8 });
            Assert.AreEqual(1, fake.OfficerListCalls);
            Assert.AreEqual(1, fake.OfficerKpiCalls);
            Assert.AreEqual(0, fake.DashboardCalls);
            var page = File.ReadAllText(Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Web", "Department", "Applications.aspx.cs"));
            Assert.IsFalse(page.Contains("AppServices.Register"));
        }

        [TestMethod]
        public void Officer_Kpi_Failure_Is_Logged_Once_And_Does_Not_Discard_List()
        {
            var fake = new DocumentAndCertificateTests.FakeQuery { ThrowOfficerKpi = true };
            var failures = 0;
            var data = SMC.BhoomiMalmatta.Web.Services.OfficerApplicationPageData.Load(fake, Roles.JE, 23, 4,
                new SMC.BhoomiMalmatta.Models.Dto.ApplicationFilter { Page = 1, PageSize = 8 }, ex => failures++);
            Assert.IsNotNull(data.Applications);
            Assert.AreEqual(0, data.Counts.Total);
            Assert.AreEqual(1, failures);
            Assert.AreEqual(1, fake.OfficerListCalls);
            Assert.AreEqual(1, fake.OfficerKpiCalls);
        }

        [TestMethod]
        public void Dashboard_Api_Is_Officer_Authorized_And_Returns_Only_Application_Count_Dto()
        {
            var api = File.ReadAllText(Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Web", "Api", "MoreControllers.cs"));
            var dto = typeof(SMC.BhoomiMalmatta.Models.Dto.DashboardCounts).GetProperties().Select(x => x.Name).ToArray();
            Assert.IsTrue(api.Contains("[RoutePrefix(\"api/v1/dashboard\")]"));
            Assert.IsTrue(api.Contains("[HttpGet, Route(\"\"), Authorize(Roles = ApiRoles.Officers)]"));
            Assert.IsTrue(api.Contains("Ok(AppServices.Query.GetDashboard(ZoneScope))"));
            CollectionAssert.DoesNotContain(dto, "MonthlyRent");
            CollectionAssert.DoesNotContain(dto, "Properties");
            CollectionAssert.DoesNotContain(dto, "RecoveryOutstanding");
            CollectionAssert.DoesNotContain(dto, "TransfersOpen");
        }

        [TestMethod]
        public void Decision_Selection_Rejects_Zero_Or_Multiple_Actions_And_Accepts_One()
        {
            string action;
            Assert.IsFalse(SMC.BhoomiMalmatta.Models.Dto.OfficerDecisionSelection.TryGetSingle(new string[0], out action));
            Assert.IsFalse(SMC.BhoomiMalmatta.Models.Dto.OfficerDecisionSelection.TryGetSingle(new[] { "Accept", "Reject" }, out action));
            Assert.IsTrue(SMC.BhoomiMalmatta.Models.Dto.OfficerDecisionSelection.TryGetSingle(new[] { "JEVerifyForward" }, out action));
            Assert.AreEqual("JEVerifyForward", action);
        }

        [TestMethod]
        public void Service_Master_Cache_Uses_One_Fake_Load_Within_The_Five_Minute_Window()
        {
            var key = "SMC.Tests.ServiceMaster." + Guid.NewGuid().ToString("N"); var loads = 0;
            Func<int> loader = () => { loads++; return 7; };
            Assert.AreEqual(7, SMC.BhoomiMalmatta.Data.Repositories.ServiceMasterCache.GetOrLoad(key, loader));
            Assert.AreEqual(7, SMC.BhoomiMalmatta.Data.Repositories.ServiceMasterCache.GetOrLoad(key, loader));
            Assert.AreEqual(1, loads);
        }

        [TestMethod]
        public void List_Query_Shape_Uses_One_Paged_Materialization_And_No_History_Json()
        {
            var source = File.ReadAllText(Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Data", "Repositories", "QueryService.cs"));
            var page = MethodBody(source, "SearchOfficerApplications(");
            Assert.AreEqual(2, Regex.Matches(page, @"BuildOfficerPagedQuery\(q, page, size\)\.ToList\(\)").Count); // normal page and stale-page clamp branch
            var pageQuery = MethodBody(source, "internal static IQueryable<OfficerPageRow> BuildOfficerPagedQuery(");
            Assert.IsTrue(pageQuery.Contains("Skip((page - 1) * size).Take(size)"));
            Assert.IsTrue(source.Contains("actedIds.Contains(x.Id)"));
            Assert.IsFalse(page.Contains("ActionHistory.Parse"));
        }

        [TestMethod]
        public void Api_Action_Response_Includes_New_Status_And_Kpi_Counts_And_Grid_ViewState_Is_Disabled_When_ReadOnly()
        {
            var web = Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Web");
            var api = File.ReadAllText(Path.Combine(web, "Api", "CoreControllers.cs"));
            var dto = File.ReadAllText(Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Models", "Dto", "Dtos.cs"));
            var grid = File.ReadAllText(Path.Combine(web, "Department", "Controls", "OfficerPendingGrid.ascx"));
            Assert.IsTrue(api.Contains("ExpectedRowVersion = r.ExpectedRowVersion"));
            Assert.IsTrue(api.Contains("OfficerDecisionSelection.TryGetSingle"));
            Assert.IsTrue(api.Contains("Counts = counts"));
            Assert.IsTrue(dto.Contains("public OfficerDashboardCounts Counts { get; set; }"));
            Assert.IsTrue(grid.Contains("EnableViewState=\"false\""));
        }

        [TestMethod]
        public void Officer_Single_List_Uses_Requested_View_Filter_And_Kpi_Links()
        {
            var web = Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Web");
            var page = File.ReadAllText(Path.Combine(web, "Department", "Applications.aspx.cs"));
            var markup = File.ReadAllText(Path.Combine(web, "Department", "Applications.aspx"));
            var query = File.ReadAllText(Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Data", "Repositories", "QueryService.cs"));
            var kpis = File.ReadAllText(Path.Combine(web, "Department", "Controls", "OfficerKpiCards.ascx.cs"));
            Assert.IsTrue(page.Contains("OfficerView = officerFilters.StatusList.SelectedValue"));
            Assert.IsTrue(page.Contains("OfficerListViews.DefaultForRole(Me.Role)"));
            Assert.IsTrue(page.Contains("OfficerApplicationPageData.Load(AppServices.Query, Me.Role, Me.UserId, ZoneScope, filter, ex =>"));
            var pageData = File.ReadAllText(Path.Combine(web, "Services", "OfficerApplicationPageData.cs"));
            Assert.IsTrue(pageData.Contains("SearchOfficerApplications(role, userId, filter)"));
            Assert.IsTrue(query.Contains("public PagedResult<ApplicationListItem> SearchOfficerApplications("));
            Assert.IsTrue(query.Contains("x.UserId == userId && x.EntityName == \"DemandApplicationZoneWorkflow\""));
            Assert.IsTrue(kpis.Contains("Applications.aspx?view="));
            Assert.IsTrue(markup.Contains("OfficerPendingGrid"));
            Assert.IsFalse(markup.Contains("OfficerProcessedGrid"));
            Assert.IsFalse(markup.Contains("processedPager"));
        }

        [TestMethod]
        public void Officer_List_Default_View_Is_Mine_For_JE_OS_AC_And_All_For_Admin_Staff()
        {
            Assert.AreEqual(SMC.BhoomiMalmatta.Models.Dto.OfficerListViews.Mine, SMC.BhoomiMalmatta.Models.Dto.OfficerListViews.DefaultForRole(SMC.BhoomiMalmatta.Models.Constants.Roles.JE));
            Assert.AreEqual(SMC.BhoomiMalmatta.Models.Dto.OfficerListViews.Mine, SMC.BhoomiMalmatta.Models.Dto.OfficerListViews.DefaultForRole(SMC.BhoomiMalmatta.Models.Constants.Roles.OS));
            Assert.AreEqual(SMC.BhoomiMalmatta.Models.Dto.OfficerListViews.Mine, SMC.BhoomiMalmatta.Models.Dto.OfficerListViews.DefaultForRole(SMC.BhoomiMalmatta.Models.Constants.Roles.AssistantCommissioner));
            Assert.AreEqual(SMC.BhoomiMalmatta.Models.Dto.OfficerListViews.All, SMC.BhoomiMalmatta.Models.Dto.OfficerListViews.DefaultForRole(SMC.BhoomiMalmatta.Models.Constants.Roles.Admin));
            Assert.AreEqual(SMC.BhoomiMalmatta.Models.Dto.OfficerListViews.All, SMC.BhoomiMalmatta.Models.Dto.OfficerListViews.DefaultForRole(SMC.BhoomiMalmatta.Models.Constants.Roles.Staff));
        }

        [TestMethod]
        public void Allowed_Action_Computation_Is_Pure_And_Does_Not_Require_A_Database()
        {
            var actions = SMC.BhoomiMalmatta.Business.Workflow.ZoneWorkflowMachine.OfficerListActions(
                SMC.BhoomiMalmatta.Models.Constants.ProcessingRoutes.Zone,
                SMC.BhoomiMalmatta.Models.Constants.ZoneStages.JEPending,
                SMC.BhoomiMalmatta.Models.Constants.Roles.JE,
                SMC.BhoomiMalmatta.Models.Constants.PaymentStatuses.PendingOSAmount).ToArray();
            CollectionAssert.AreEquivalent(new[] { "JEVerifyForward", "JEReturnForCorrection", "JEReject" }, actions);
        }

        private static string MethodBody(string source, string marker)
        {
            var start = source.IndexOf(marker, StringComparison.Ordinal); Assert.IsTrue(start >= 0, marker);
            var open = source.IndexOf('{', start + marker.Length); Assert.IsTrue(open >= 0, marker);
            var depth = 0;
            for (var i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0) return source.Substring(start, i - start + 1);
            }
            Assert.Fail("Unclosed method body: " + marker); return string.Empty;
        }
    }
}
