using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Results;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SMC.BhoomiMalmatta.Business.PropertyRegister;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;
using SMC.BhoomiMalmatta.Web;
using SMC.BhoomiMalmatta.Web.Api;
using SMC.BhoomiMalmatta.Web.Department;
using SMC.BhoomiMalmatta.Web.Security;
using SMC.BhoomiMalmatta.Tests.Support;

namespace SMC.BhoomiMalmatta.Tests
{
    [TestClass]
    public class PropertiesPageTests
    {
        private static readonly string[] NonAdminRoles = { Roles.JE, Roles.OS, Roles.AssistantCommissioner, Roles.Staff };
        private static string Web(string relative) { return File.ReadAllText(Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Web", relative)); }
        private static string Markup { get { return Web(Path.Combine("Department", "Properties.aspx")); } }
        private static string Script { get { return Web(Path.Combine("Scripts", "properties.js")); } }
        private static string CodeBehind { get { return Web(Path.Combine("Department", "Properties.aspx.cs")); } }

        // ------------------------------------------------------------ access ------------------------------------------------------------

        [TestMethod]
        public void Page_Is_Admin_Only()
        {
            var page = new PropertiesPage();
            var roles = (string[])typeof(PropertiesPage).GetProperty("RequiredRoles", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(page, null);
            CollectionAssert.AreEqual(new[] { Roles.Admin }, roles);
            foreach (var r in NonAdminRoles) CollectionAssert.DoesNotContain(roles, r);
            Assert.IsTrue(Web("Web.config").Contains("<location path=\"Department/Properties.aspx\"><system.web><authorization><allow roles=\"Admin\" /><deny users=\"*\" />"));
            Assert.IsTrue(Web("Global.asax.cs").Contains("path.Equals(\"~/Department/Properties.aspx\""));
            Assert.IsTrue(typeof(SmcPage).IsAssignableFrom(typeof(PropertiesPage)));
        }

        private static HttpActionContext ActionContext(string role, string action)
        {
            var config = new HttpConfiguration();
            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/v1/properties");
            request.Properties[System.Web.Http.Hosting.HttpPropertyKeys.HttpConfigurationKey] = config;
            var cd = new HttpControllerDescriptor(config, "Properties", typeof(PropertiesController));
            var ad = new ReflectedHttpActionDescriptor(cd, typeof(PropertiesController).GetMethod(action));
            var principal = role == null ? (System.Security.Principal.IPrincipal)new System.Security.Principal.GenericPrincipal(new System.Security.Principal.GenericIdentity(""), new string[0])
                : new SmcPrincipal(new SmcIdentity { Role = role, UserId = 7, Name = "officer" });
            var cc = new HttpControllerContext { Configuration = config, Request = request, ControllerDescriptor = cd, RequestContext = new HttpRequestContext { Principal = principal } };
            return new HttpActionContext(cc, ad);
        }

        [TestMethod]
        public void Every_Api_Action_Returns_403_For_Non_Admin_Roles_Through_The_Attribute()
        {
            Assert.IsTrue(typeof(PropertiesController).GetCustomAttributes(typeof(AdminOnlyApiAttribute), true).Any());
            var actions = new[] { "List", "Options", "NextCode", "Get", "Create", "Update", "Delete" };
            foreach (var role in NonAdminRoles)
                foreach (var a in actions)
                {
                    var ctx = ActionContext(role, a);
                    new AdminOnlyApiAttribute().OnAuthorization(ctx);
                    Assert.IsNotNull(ctx.Response, role + " " + a);
                    Assert.AreEqual(HttpStatusCode.Forbidden, ctx.Response.StatusCode, role + " " + a);
                }
            var admin = ActionContext(Roles.Admin, "List");
            new AdminOnlyApiAttribute().OnAuthorization(admin);
            Assert.IsNull(admin.Response);
            var anonymous = ActionContext(null, "List");
            new AdminOnlyApiAttribute().OnAuthorization(anonymous);
            Assert.AreEqual(HttpStatusCode.Unauthorized, anonymous.Response.StatusCode);
        }

        [TestMethod]
        public void Every_Api_Action_Rechecks_Admin_And_Returns_403()
        {
            foreach (var role in NonAdminRoles)
            {
                var c = new PropertiesController { Request = new HttpRequestMessage(), Configuration = new HttpConfiguration(), User = new SmcPrincipal(new SmcIdentity { Role = role, UserId = 7, Name = "officer" }) };
                var results = new IHttpActionResult[] { c.List(), c.Options(), c.NextCode("MajorGaale"), c.Get(1), c.Create(new PropertySaveRequest()), c.Update(1, new PropertySaveRequest()), c.Delete(1) };
                foreach (var r in results)
                {
                    var n = r as NegotiatedContentResult<ApiError>;
                    Assert.IsNotNull(n, role);
                    Assert.AreEqual(HttpStatusCode.Forbidden, n.StatusCode, role);
                }
            }
            var api = Web(Path.Combine("Api", "PropertiesController.cs"));
            Assert.AreEqual(7, Regex.Matches(api, @"if \(!IsAdmin\) return Forbidden\(\);").Count);
            Assert.IsTrue(Web(Path.Combine("App_Start", "WebApiConfig.cs")).Contains("RequireCustomHeaderHandler"), "writes need the X-SMC-Request header");
        }

        [TestMethod]
        public void Options_Dto_Default_Status_Comes_From_The_Database_List()
        {
            var o = new PropertyMasterOptions(FakePropertyOptionSource.FromSqlScript(), new FakeClock());
            var dto = PropertiesController.OptionsDto(o.Get());
            Assert.AreEqual("Rikamy", dto.DefaultStatus);
            Assert.AreEqual(9, dto.Categories.Count); Assert.AreEqual(2, dto.Statuses.Count);
            var empty = PropertiesController.OptionsDto(new PropertyMasterOptions(new FakePropertyOptionSource(), new FakeClock()).Get());
            Assert.IsNull(empty.DefaultStatus);
        }

        // ------------------------------------------------------------ markup and script ------------------------------------------------------------

        [TestMethod]
        public void Markup_Js_And_Code_Behind_Contain_No_Category_Or_Status_Literals()
        {
            var keys = PropertyRegisterTests.ScriptRows().Select(r => r[1]).Concat(new[] { "Vacant", "Leased", "Major Gaale", "Mini Gaale" }).ToList();
            var statusLabels = PropertyRegisterTests.ScriptRows().Where(r => r[0] == "PropertyStatus").Select(r => r[2]).ToList();
            foreach (var source in new[] { Markup, Script, CodeBehind })
            {
                foreach (var k in keys) Assert.IsFalse(Regex.IsMatch(source, @"\b" + Regex.Escape(k) + @"\b"), k);
                foreach (var l in statusLabels) Assert.IsFalse(source.Contains(l), l);
                Assert.IsFalse(source.Contains("PropertyCategories") || source.Contains("PropertyStatuses"));
            }
            foreach (Match m in Regex.Matches(Markup, "<option[^>]*>")) Assert.AreEqual("<option value=\"\">", m.Value);
            StringAssert.Contains(Script, "/options");
            StringAssert.Contains(Script, "defaultStatus");
        }

        [TestMethod]
        public void Table_Headers_Are_In_Reference_Order()
        {
            var thead = Regex.Match(Markup, "<thead>(.*?)</thead>", RegexOptions.Singleline).Groups[1].Value;
            var headers = Regex.Matches(thead, "<th[^>]*>(.*?)</th>").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
            CollectionAssert.AreEqual(new[] { "मालमत्ता क्र.", "नाव", "मालमत्ता प्रकार", "प्रभाग", "वार्षिक मागणी", "स्थिती", "क्रिया" }, headers);
            StringAssert.Contains(Markup, "placeholder=\"शोधा (नाव, क्रमांक, इ.)\"");
            StringAssert.Contains(Markup, "मालमत्ता प्रकार - सर्व");
            StringAssert.Contains(Markup, "स्थिती - सर्व");
            StringAssert.Contains(Markup, "+ नवीन नोंद");
            StringAssert.Contains(Markup, "Major/Mini गाळे, Land Fee, समाज मंदिर, अभ्यासिका, 256 गाळे, TP-3/23, अधिकृत खोके, इतर मनपा मालमत्ता");
            StringAssert.Contains(Markup, "EnableViewState=\"false\"");
            StringAssert.Contains(Script, "कोणतीही नोंद आढळली नाही.");
            StringAssert.Contains(Script, "300");
            var css = Web(Path.Combine("Content", "css", "app.css"));
            StringAssert.Contains(css, ".property-main{--prop-align:right}");
            StringAssert.Contains(css, ".prop-pill[data-status=Bhadyane]{background:#16a34a}");
            StringAssert.Contains(css, ".prop-pill[data-status=Rikamy]{background:#a64d85}");
        }

        [TestMethod]
        public void Dialog_Fields_Are_In_Reference_Order()
        {
            var ids = Regex.Matches(Markup, "id=\"pf_(\\w+)\"").Cast<Match>().Select(m => m.Groups[1].Value).Where(x => !x.EndsWith("New")).ToArray();
            CollectionAssert.AreEqual(new[] { "Category", "PropertyCode", "Name", "Ward", "Zone", "Address", "AreaSqFt", "MonthlyRent", "AnnualDemand", "SurveyNumber", "TpNumber", "Status", "CurrentOccupant", "Shera" }, ids);
            StringAssert.Contains(Markup, "<textarea id=\"pf_Shera\" name=\"Shera\" class=\"prop-in\" rows=\"4\"");
            StringAssert.Contains(Markup, "बंद करा</button>");
            StringAssert.Contains(Markup, "जतन करा (Save)");
            StringAssert.Contains(Markup, "माहिती");
            StringAssert.Contains(Script, "+ नवीन मूल्य टाका");
            StringAssert.Contains(Script, "next-code?category=");
            StringAssert.Contains(Script, "codeTouched");
            StringAssert.Contains(Script, "'X-SMC-Request'");
            StringAssert.Contains(Script, "res.status === 422");
            StringAssert.Contains(Script, "नोंद जतन केली.");
        }

        [TestMethod]
        public void Dialog_Is_Accessible_Closes_On_Esc_And_Returns_Focus()
        {
            StringAssert.Contains(Markup, "role=\"dialog\" aria-modal=\"true\" aria-labelledby=\"propDialogTitle\"");
            StringAssert.Contains(Markup, "role=\"alertdialog\" aria-modal=\"true\"");
            StringAssert.Contains(Markup, "aria-label=\"बंद करा\"");
            StringAssert.Contains(Markup, "ही मालमत्ता हटवायची का?");
            StringAssert.Contains(Script, "e.key === 'Escape'");
            StringAssert.Contains(Script, "e.key !== 'Tab'");
            StringAssert.Contains(Script, "o.focus()");
            StringAssert.Contains(Script, "बदल जतन केलेले नाहीत. बंद करायचे?");
            StringAssert.Contains(Script, "if (e.target === this) closeDialog(false)");
            Assert.IsFalse(Markup.Contains("<form"), "no nested form inside the Web Forms form");
            Assert.IsFalse(Regex.IsMatch(Script + Markup, @"https?://"), "no external resources");
        }

        // ------------------------------------------------------------ header and name ------------------------------------------------------------

        [TestMethod]
        public void Properties_Page_Has_No_Dark_Header_And_Exactly_One_Logout()
        {
            Assert.IsTrue(SiteMaster.IsBarePage("~/Department/Properties.aspx"));
            Assert.IsFalse(SiteMaster.IsBarePage("~/Department/Calculations.aspx"));
            Assert.IsFalse(SiteMaster.IsBarePage("~/Department/Applications.aspx"));
            var master = Web("Site.Master");
            var header = Regex.Match(master, "<asp:PlaceHolder ID=\"phSiteHeader\" runat=\"server\"><header class=\"grad\">.*?</header></asp:PlaceHolder>", RegexOptions.Singleline);
            Assert.IsTrue(header.Success);
            StringAssert.Contains(header.Value, "ID=\"lnkLogout\"");
            var nav = Regex.Match(master, "<nav class=\"admin-links\".*?</nav>", RegexOptions.Singleline).Value;
            foreach (var link in new[] { "मागणी अर्ज", "Audit log", "Reports", "Users", "Master data", "ID=\"lnkNavLogout\"", "litNavUser" }) StringAssert.Contains(nav, link);
            Assert.AreEqual(2, Regex.Matches(master, "बाहेर पडा").Count);
            var cs = Web("Site.Master.cs");
            StringAssert.Contains(cs, "phSiteHeader.Visible = !barePage;");
            StringAssert.Contains(cs, "phNavUser.Visible = barePage && me != null && phAdminLinks.Visible;");
            StringAssert.Contains(cs, "if (officerHome) phUser.Visible = false");
            StringAssert.Contains(cs, "lnkLogout.Visible = !officerHome");
        }

        [TestMethod]
        public void Name_And_Role_Are_Printed_Once()
        {
            Assert.AreEqual("Ramesh (Admin)", DisplayName.WithRole("Ramesh", "Admin"));
            Assert.AreEqual("Ramesh (Admin)", DisplayName.WithRole("Ramesh (Admin)", "Admin"));
            Assert.AreEqual("Ramesh (admin)", DisplayName.WithRole("Ramesh (admin)", "Admin"));
            Assert.AreEqual("System Admin (Admin)", DisplayName.WithRole(" System Admin ", "Admin"));
            Assert.AreEqual("Ramesh", DisplayName.WithRole("Ramesh", null));
            Assert.AreEqual("(JE)", DisplayName.WithRole(null, "JE"));
            StringAssert.Contains(Web("Site.Master.cs"), "DisplayName.WithRole(me.FullName, me.Role)");
            StringAssert.Contains(Web(Path.Combine("Department", "Dashboard.aspx.cs")), "DisplayName.WithRole(Me.FullName, Me.Role)");
        }

        [TestMethod]
        public void Reports_And_Pdf_Use_Database_Labels()
        {
            StringAssert.Contains(Web(Path.Combine("Services", "ReportBuilder.cs")), "PropertyLabels.Category(x.Category)");
            StringAssert.Contains(Web(Path.Combine("Services", "ReportBuilder.cs")), "PropertyLabels.Status(x.Status)");
            StringAssert.Contains(Web(Path.Combine("Services", "PdfGenerator.cs")), "PropertyLabels.Category(p.Category)");
        }

        [TestMethod]
        public void Other_Register_Pages_Still_Use_ModulePage()
        {
            foreach (var page in new[] { "Calculations", "Recovery", "Transfers", "Tenders", "Schemes" })
                Assert.IsTrue(Regex.IsMatch(Web(Path.Combine("Department", page + ".aspx.cs")), @":\s*ModulePage<"), page);
        }
    }
}
