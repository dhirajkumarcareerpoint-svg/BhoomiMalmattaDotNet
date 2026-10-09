using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Business.Numbering;
using SMC.BhoomiMalmatta.Business.Submission;
using SMC.BhoomiMalmatta.Data.Repositories;
using SMC.BhoomiMalmatta.Models.Entities;
using SMC.BhoomiMalmatta.Tests.Support;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Tests
{
    /// <summary>The citizen "मागणी अर्ज" wizard has three steps; the applicant block (name, mobile, optional e-mail) opens step 2.</summary>
    [TestClass]
    public class ApplyWizardTests
    {
        private static string Web(string relative) { return File.ReadAllText(Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Web", relative)); }
        private static string Markup { get { return Web("Apply.aspx"); } }
        private static string Code { get { return Web("Apply.aspx.cs"); } }

        /// <summary>Markup between the opening tag of View vN and the next View (or the end of the MultiView).</summary>
        private static string ViewMarkup(string markup, int n)
        {
            var start = markup.IndexOf("<asp:View ID=\"v" + n + "\"", StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, "View v" + n + " missing");
            var next = markup.IndexOf("<asp:View ", start + 10, StringComparison.Ordinal);
            if (next < 0) next = markup.IndexOf("</asp:MultiView>", start, StringComparison.Ordinal);
            return markup.Substring(start, next - start);
        }

        /// <summary>Wizard input without e-mail, Aadhaar/identity, PAN, GST: exactly what the page now collects.</summary>
        private static SubmissionData WizardData()
        {
            var d = TestData.Valid();
            d.Email = ""; d.ApplicantType = ApplyWizard.DefaultApplicantType; d.GstNumber = null; d.IdentityNumber = null; d.PanNumber = null;
            return d;
        }

        [TestMethod]
        public void Markup_Has_Exactly_Three_Steps_With_Headings_And_Circles_In_Order()
        {
            var m = Markup;
            Assert.AreEqual(3, Regex.Matches(m, "<asp:View ID=\"v\\d\"").Count);
            Assert.AreEqual(3, ApplyWizard.Count);
            var headings = Regex.Matches(m, "<h3>(.*?)</h3>").Cast<Match>().Select(x => x.Groups[1].Value).ToArray();
            CollectionAssert.AreEqual(new[] { "१. सेवेची माहिती", "२. पत्ता व जागा", "३. कागदपत्रे व सादरीकरण" }, headings);
            var circles = Regex.Matches(m, "<div class=\"st[^\"]*\" id=\"st(\\d)\" runat=\"server\"><span>(\\d)</span>([^<]+)</div>").Cast<Match>().ToList();
            CollectionAssert.AreEqual(new[] { "1", "2", "3" }, circles.Select(x => x.Groups[2].Value).ToArray());
            CollectionAssert.AreEqual(ApplyWizard.Titles, circles.Select(x => x.Groups[3].Value).ToArray());
            StringAssert.Contains(m, "style=\"width:33.33%\"");
            foreach (var gone in new[] { "अर्जदाराची माहिती", "<span>4</span>", "id=\"st4\"", "ID=\"v4\"", "४.", "g4" }) Assert.IsFalse(m.Contains(gone), gone);
            var code = Code;
            StringAssert.Contains(code, "new[] { st1, st2, st3 }");
            foreach (var gone in new[] { "st4", "\"g4\"", "* 25", "Four-step", "i < 3", "i == 3", "Step == 2", "Step >= 2" }) Assert.IsFalse(code.Contains(gone), gone);
            var designer = Web("Apply.aspx.designer.cs");
            foreach (var gone in new[] { " st4;", " v4;", " ddlApplicantType;", " txtIdentity;", " txtPan;", " txtGst;" }) Assert.IsFalse(designer.Contains(gone), gone);
        }

        [TestMethod]
        public void Applicant_Block_Opens_Step_Two_Behind_The_Switch_And_Runs_With_The_Address_Validation_Group()
        {
            var v2 = ViewMarkup(Markup, 2);
            StringAssert.Contains(v2, "<legend>अर्जदार</legend>");
            // The block is wrapped in a hidden PlaceHolder (rendered only when Apply.CollectApplicantDetails=true).
            var ph = v2.IndexOf("<asp:PlaceHolder ID=\"phApplicant\" runat=\"server\" Visible=\"false\">", StringComparison.Ordinal);
            var phEnd = v2.IndexOf("</asp:PlaceHolder>", ph, StringComparison.Ordinal);
            Assert.IsTrue(ph > 0 && phEnd > ph);
            foreach (var id in new[] { "txtName", "txtMobile", "txtEmail", "rfvName", "rfvMobile", "revMobile", "revEmail" })
            { var at = v2.IndexOf("ID=\"" + id + "\"", StringComparison.Ordinal); Assert.IsTrue(at > ph && at < phEnd, id + " inside the switchable block"); }
            var name = v2.IndexOf("ID=\"txtName\"", StringComparison.Ordinal); var mobile = v2.IndexOf("ID=\"txtMobile\"", StringComparison.Ordinal);
            var email = v2.IndexOf("ID=\"txtEmail\"", StringComparison.Ordinal); var address = v2.IndexOf("ID=\"txtPermAddr\"", StringComparison.Ordinal);
            Assert.IsTrue(name > 0 && name < mobile && mobile < email && email < address, "name, mobile, e-mail come first in step 2");
            StringAssert.Contains(v2, "Text=\"अर्जदाराचे पूर्ण नाव *\""); StringAssert.Contains(v2, "Text=\"मोबाईल क्रमांक *\""); StringAssert.Contains(v2, "Text=\"ई-मेल आयडी\"");
            StringAssert.Contains(v2, "ValidationExpression=\"^\\s*[6-9][0-9]{9}\\s*$\""); StringAssert.Contains(v2, "ErrorMessage=\"अर्जदाराचे नाव आवश्यक आहे.\"");
            StringAssert.Contains(v2, "ErrorMessage=\"कृपया वैध ई-मेल पत्ता भरा.\"");
            // Every client validator of step 2 (applicant + address + dates/area) is in g2; step 3 validators are in g3.
            CollectionAssert.AreEquivalent(new[] { "g2" }, Regex.Matches(v2, "ValidationGroup=\"(g\\d)\"").Cast<Match>().Select(x => x.Groups[1].Value).Distinct().ToArray());
            CollectionAssert.AreEquivalent(new[] { "g3" }, Regex.Matches(ViewMarkup(Markup, 3), "ValidationGroup=\"(g\\d)\"").Cast<Match>().Select(x => x.Groups[1].Value).Distinct().ToArray());
            CollectionAssert.AreEquivalent(new[] { "g1" }, Regex.Matches(ViewMarkup(Markup, 1), "ValidationGroup=\"(g\\d)\"").Cast<Match>().Select(x => x.Groups[1].Value).Distinct().ToArray());
            StringAssert.Contains(Code, "SetFocus(i == ApplyWizard.ServiceStep ? (Control)ddlService : i == ApplyWizard.AddressStep ? (phApplicant.Visible ? (Control)txtName : txtPermAddr) : fuIdentity)");
        }

        [TestMethod]
        public void Pan_Identity_Gst_And_Applicant_Type_Are_Never_Rendered_Or_Requested()
        {
            var m = Markup; var code = Code;
            foreach (var gone in new[] { "txtIdentity", "txtPan", "txtGst", "ddlApplicantType", "PAN", "आधार", "GST", "अर्जदाराचा प्रकार" }) Assert.IsFalse(m.Contains(gone), "markup: " + gone);
            foreach (var gone in new[] { "txtIdentity", "txtPan", "txtGst", "ddlApplicantType", "\"PAN", "आधार/ओळखपत्र क्रमांक", "GST क्रमांक" }) Assert.IsFalse(code.Contains(gone), "code-behind: " + gone);
            StringAssert.Contains(code, "GstNumber = null, IdentityNumber = null, PanNumber = null,");
            StringAssert.Contains(code, "ApplicantType = ApplyWizard.DefaultApplicantType");
            Assert.AreEqual("वैयक्तिक", ApplyWizard.DefaultApplicantType);
        }

        [DataTestMethod]
        [DataRow(0, "g1")] [DataRow(1, "g2")] [DataRow(2, "g3")]
        public void Step_Index_Maps_To_Validation_Group_And_Progress(int step, string group)
        {
            Assert.AreEqual(group, ApplyWizard.ValidationGroup(step));
            Assert.AreEqual(new[] { "33.33%", "66.67%", "100%" }[step], ApplyWizard.ProgressWidth(step));
        }

        [DataTestMethod]
        [DataRow("ServiceType", 0)] [DataRow("BusinessType", 0)] [DataRow("OtherBusinessType", 0)]
        [DataRow("ApplicantName", 1)] [DataRow("Mobile", 1)] [DataRow("Email", 1)] [DataRow("PermanentAddress", 1)] [DataRow("CorrespondenceAddress", 1)]
        [DataRow("Taluka", 1)] [DataRow("PinCode", 1)] [DataRow("StartDate", 1)] [DataRow("EndDate", 1)] [DataRow("LengthFt", 1)] [DataRow("WidthFt", 1)] [DataRow("Pricing", 1)]
        [DataRow("Zone", 1)] [DataRow("Location", 1)] [DataRow("DeclarationAccepted", 2)] [DataRow("Documents", 2)]
        public void Every_Validation_Field_Belongs_To_Step_1_To_3(string field, int step)
        {
            Assert.AreEqual(step, ApplyWizard.StepOf(field));
            Assert.IsTrue(ApplyWizard.StepOf(field) >= 0 && ApplyWizard.StepOf(field) < ApplyWizard.Count);
        }

        [DataTestMethod]
        [DataRow("ApplicantName", "  ")] [DataRow("Mobile", "12345")] [DataRow("Mobile", "5876543210")] [DataRow("Mobile", "")]
        [DataRow("PermanentAddress", "")] [DataRow("Email", "not-an-email")] [DataRow("PinCode", "41")]
        public void Next_On_Step_Two_Is_Blocked_By_Invalid_Applicant_Or_Address(string field, string value)
        {
            var d = WizardData();
            typeof(SubmissionData).GetProperty(field).SetValue(d, value);
            var errors = ApplyWizard.BlockingErrors(d, ApplyWizard.AddressStep, new FakeMasters().GetCatalog());
            Assert.IsTrue(errors.Any(x => x.Field == field), string.Join(" | ", errors.Select(x => x.Field)));
        }

        [TestMethod]
        public void Next_On_Step_Two_Is_Allowed_When_Valid_And_Step_Three_Fields_Do_Not_Block_It()
        {
            var d = WizardData(); d.DeclarationAccepted = false; d.UploadedDocumentTypes = new List<string>();
            var catalog = new FakeMasters().GetCatalog();
            var step2 = ApplyWizard.BlockingErrors(d, ApplyWizard.AddressStep, catalog);
            Assert.AreEqual(0, step2.Count, string.Join(" | ", step2.Select(x => x.Field + ": " + x.Message)));
            // Zone and location now belong to step 2 and block its "Next".
            var noPlace = WizardData(); noPlace.Zone = ""; noPlace.Location = "";
            var blocked = ApplyWizard.BlockingErrors(noPlace, ApplyWizard.AddressStep, catalog);
            Assert.IsTrue(blocked.Any(x => x.Field == "Zone")); Assert.IsTrue(blocked.Any(x => x.Field == "Location"));
            // Step 1 is never blocked by applicant or address problems.
            var bad = WizardData(); bad.ApplicantName = ""; bad.Mobile = "1";
            Assert.AreEqual(0, ApplyWizard.BlockingErrors(bad, ApplyWizard.ServiceStep, catalog).Count);
        }

        [TestMethod]
        public void Wizard_Submission_Without_Optional_Identifiers_Satisfies_Every_Not_Null_Column()
        {
            var store = new FakeAppStore(); var clock = new FakeClock();
            var svc = new DemandSubmissionService(new FakeMasters(), new ApplicationNumberService(new FakeNumberStore(), clock), store, new FakeFiles(), clock, new FakeNotifier());
            var data = WizardData();
            Assert.AreEqual(0, SubmissionValidator.Validate(SubmissionValidator.Normalize(data), new FakeMasters().GetCatalog()).Count);
            var result = svc.Submit(data, TestData.Docs(), "1.2.3.4", "corr");
            Assert.IsNull(result.Item2, result.Item2 == null ? null : string.Join(" | ", result.Item2.Errors.Select(x => x.Field)));

            var entity = DemandApplicationStore.ToApplicationEntity(store.Saved[0]);
            var schema = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(Repo.Root, "database", "database-schema.json")));
            var table = ((ArrayList)schema["tables"]).Cast<Dictionary<string, object>>().Single(t => (string)t["name"] == "TR_CFC_DemandApplications");
            var required = ((ArrayList)table["columns"]).Cast<Dictionary<string, object>>().Where(c => !(bool)c["isNullable"] && !(bool)c["isIdentity"]).Select(c => (string)c["name"]).ToList();
            Assert.AreEqual(35, required.Count, "NOT NULL, non-identity columns of TR_CFC_DemandApplications in the schema JSON");
            foreach (var column in required)
            {
                var property = typeof(DemandApplication).GetProperty(column);
                Assert.IsNotNull(property, "entity property for " + column);
                Assert.IsNotNull(property.GetValue(entity), "NOT NULL column left null: " + column);
            }
            // Schema-safe values for the fields the wizard no longer asks.
            Assert.AreEqual(string.Empty, entity.GstNumber); Assert.AreEqual(string.Empty, entity.Email);
            Assert.IsNull(entity.IdentityNumber); Assert.IsNull(entity.PanNumber); Assert.AreEqual("वैयक्तिक", entity.ApplicantType);
            Assert.AreEqual("राम पाटील", entity.ApplicantName); Assert.AreEqual("9876543210", entity.Mobile);
            var nullable = ((ArrayList)table["columns"]).Cast<Dictionary<string, object>>().Where(c => (bool)c["isNullable"]).Select(c => (string)c["name"]).ToList();
            CollectionAssert.IsSubsetOf(new[] { "ApplicantType", "IdentityNumber", "PanNumber" }, nullable, "NULL is only used for nullable columns");
            CollectionAssert.IsSubsetOf(new[] { "Email", "GstNumber", "ApplicantName", "Mobile" }, required);
        }

        [TestMethod]
        public void Server_Still_Rejects_Missing_Name_And_Invalid_Mobile_And_Validates_Email_Only_When_Present()
        {
            var clock = new FakeClock(); var store = new FakeAppStore();
            var svc = new DemandSubmissionService(new FakeMasters(), new ApplicationNumberService(new FakeNumberStore(), clock), store, new FakeFiles(), clock, new FakeNotifier());
            var noName = WizardData(); noName.ApplicantName = " ";
            var r1 = svc.Submit(noName, TestData.Docs(), null, null);
            Assert.IsTrue(r1.Item2.Errors.Any(x => x.Field == "ApplicantName" && x.Message == "अर्जदाराचे नाव आवश्यक आहे."));
            var badMobile = WizardData(); badMobile.Mobile = "98765";
            Assert.IsTrue(svc.Submit(badMobile, TestData.Docs(), null, null).Item2.Errors.Any(x => x.Field == "Mobile" && x.Message.Contains("६, ७, ८ किंवा ९")));
            var badEmail = WizardData(); badEmail.Email = "a@b";
            Assert.IsTrue(svc.Submit(badEmail, TestData.Docs(), null, null).Item2.Errors.Any(x => x.Field == "Email"));
            Assert.AreEqual(0, store.Saved.Count);
            var withEmail = WizardData(); withEmail.Email = " Ram@Example.com ";
            Assert.IsNull(svc.Submit(withEmail, TestData.Docs(), null, null).Item2);
            Assert.AreEqual("ram@example.com", store.Saved[0].Data.Email);
            Assert.IsNull(svc.Submit(WizardData(), TestData.Docs(), null, null).Item2);
            Assert.AreEqual(string.Empty, store.Saved[1].Data.Email);
        }

        [TestMethod]
        public void Review_And_Pdf_No_Longer_Show_Empty_Identity_Pan_Or_Gst_Rows()
        {
            var code = Code;
            StringAssert.Contains(code, "section(\"अर्जदार\");");
            var newRow = PdfGenerator.ApplicantRows(new SMC.BhoomiMalmatta.Models.Dto.ApplicationDetail { ApplicantType = "वैयक्तिक", ApplicantName = "A", Mobile = "9876543210", Email = "", GstNumber = "", IdentityNumber = null, PanNumber = null });
            Assert.IsFalse(newRow.Any(x => x.Key.Contains("PAN") || x.Key.Contains("आधार") || x.Key.Contains("GST")));
            var oldRow = PdfGenerator.ApplicantRows(new SMC.BhoomiMalmatta.Models.Dto.ApplicationDetail { ApplicantName = "A", Mobile = "9876543210", GstNumber = "27ABCDE1234F1Z5", IdentityNumber = "123456789012", PanNumber = "ABCDE1234F" });
            Assert.AreEqual(3, oldRow.Count(x => x.Key.Contains("PAN") || x.Key.Contains("आधार") || x.Key.Contains("GST")), "older applications still print their stored values");
        }
    }
}
