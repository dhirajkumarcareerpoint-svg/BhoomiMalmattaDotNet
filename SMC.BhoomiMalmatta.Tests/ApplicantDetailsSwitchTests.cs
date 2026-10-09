using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web.Http;
using System.Web.Script.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Business.Citizen;
using SMC.BhoomiMalmatta.Business.Documents;
using SMC.BhoomiMalmatta.Business.Numbering;
using SMC.BhoomiMalmatta.Business.Reports;
using SMC.BhoomiMalmatta.Business.Sms;
using SMC.BhoomiMalmatta.Business.Submission;
using SMC.BhoomiMalmatta.Business.Workflow;
using SMC.BhoomiMalmatta.Data.Repositories;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;
using SMC.BhoomiMalmatta.Models.Entities;
using SMC.BhoomiMalmatta.Tests.Support;
using SMC.BhoomiMalmatta.Web.Api;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Tests
{
    /// <summary>Apply.CollectApplicantDetails: applicant name / mobile / e-mail are optional (default) and every consumer tolerates empty values.</summary>
    [TestClass]
    public class ApplicantDetailsSwitchTests
    {
        private static string Web(string relative) { return File.ReadAllText(Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Web", relative)); }

        private static string View(string markup, int n)
        {
            var start = markup.IndexOf("<asp:View ID=\"v" + n + "\"", StringComparison.Ordinal);
            var next = markup.IndexOf("<asp:View ", start + 10, StringComparison.Ordinal);
            if (next < 0) next = markup.IndexOf("</asp:MultiView>", start, StringComparison.Ordinal);
            return markup.Substring(start, next - start);
        }

        /// <summary>What the wizard collects with the switch off: no name, mobile, e-mail, identifiers or GST.</summary>
        private static SubmissionData NoApplicant()
        {
            var d = TestData.Valid();
            d.ApplicantName = null; d.Mobile = null; d.Email = null; d.ApplicantType = ApplyWizard.DefaultApplicantType; d.GstNumber = null; d.IdentityNumber = null; d.PanNumber = null;
            return d;
        }

        private static DemandSubmissionService Service(FakeAppStore store, bool collect, INotifier notifier = null)
        {
            var clock = new FakeClock();
            return new DemandSubmissionService(new FakeMasters(), new ApplicationNumberService(new FakeNumberStore(), clock), store, new FakeFiles(), clock, notifier ?? new FakeNotifier(), collect);
        }

        [TestMethod]
        public void Switch_Off_Step_Two_Starts_With_Permanent_Address_And_Switch_Is_Configured_Once()
        {
            var v2 = View(Web("Apply.aspx"), 2);
            var ph = v2.IndexOf("<asp:PlaceHolder ID=\"phApplicant\" runat=\"server\" Visible=\"false\">", StringComparison.Ordinal);
            var phEnd = v2.IndexOf("</asp:PlaceHolder>", ph, StringComparison.Ordinal) + "</asp:PlaceHolder>".Length;
            Assert.IsTrue(ph > 0);
            // Outside the switchable block there is no applicant input, label or validator in step 2.
            var outside = v2.Substring(0, ph) + v2.Substring(phEnd);
            foreach (var id in new[] { "txtName", "txtMobile", "txtEmail", "rfvName", "rfvMobile", "revMobile", "revEmail", "lblNameCaption", "lblMobileCaption", "lblEmailCaption" })
                Assert.IsFalse(outside.Contains("ID=\"" + id + "\""), id);
            // The first control rendered after the (hidden) block is "कायमचा पत्ता".
            var firstLabel = Regex.Match(v2.Substring(phEnd), "<asp:Label ID=\"(\\w+)\"[^>]*Text=\"([^\"]+)\"");
            Assert.AreEqual("lblPermCaption", firstLabel.Groups[1].Value); Assert.AreEqual("कायमचा पत्ता *", firstLabel.Groups[2].Value);

            var code = Web("Apply.aspx.cs");
            StringAssert.Contains(code, "phApplicant.Visible = AppServices.CollectApplicantDetails;");
            Assert.IsTrue(code.IndexOf("phApplicant.Visible = AppServices.CollectApplicantDetails;", StringComparison.Ordinal) < code.IndexOf("protected void Page_Load", StringComparison.Ordinal), "set in OnInit, before post data");
            StringAssert.Contains(code, "ApplicantName = phApplicant.Visible ? txtName.Text : null, Mobile = phApplicant.Visible ? txtMobile.Text : null, Email = phApplicant.Visible ? txtEmail.Text : null,");
            StringAssert.Contains(code, "AppServices.Masters.GetCatalog(), AppServices.CollectApplicantDetails)");
            var config = Web("Web.config");
            Assert.AreEqual(1, Regex.Matches(config, "key=\"Apply.CollectApplicantDetails\"").Count);
            StringAssert.Contains(config, "<add key=\"Apply.CollectApplicantDetails\" value=\"false\" />");
            StringAssert.Contains(Web(Path.Combine("Services", "AppServices.cs")), "Applications, Files, Clock, Notifier, CollectApplicantDetails);");
        }

        [TestMethod]
        public void Switch_Decides_Whether_Name_And_Mobile_Are_Required_On_Step_Two()
        {
            var catalog = new FakeMasters().GetCatalog();
            var off = ApplyWizard.BlockingErrors(NoApplicant(), ApplyWizard.AddressStep, catalog, false);
            Assert.AreEqual(0, off.Count, string.Join(" | ", off.Select(x => x.Field)));
            var on = ApplyWizard.BlockingErrors(NoApplicant(), ApplyWizard.AddressStep, catalog, true);
            CollectionAssert.IsSubsetOf(new[] { "ApplicantName", "Mobile" }, on.Select(x => x.Field).ToArray());
            Assert.IsTrue(on.Any(x => x.Message == "अर्जदाराचे नाव आवश्यक आहे.")); Assert.IsFalse(on.Any(x => x.Field == "Email"), "e-mail stays optional");
            var markup = View(Web("Apply.aspx"), 2);
            StringAssert.Contains(markup, "ID=\"rfvName\" runat=\"server\" ControlToValidate=\"txtName\" ValidationGroup=\"g2\"");
            StringAssert.Contains(markup, "ID=\"rfvMobile\" runat=\"server\" ControlToValidate=\"txtMobile\" ValidationGroup=\"g2\"");
        }

        [TestMethod]
        public void Submission_Without_Applicant_Details_Stores_Empty_Strings_In_Every_Not_Null_Column()
        {
            var store = new FakeAppStore(); var svc = Service(store, false);
            Assert.IsFalse(svc.RequiresApplicantDetails);
            var result = svc.Submit(NoApplicant(), TestData.Docs(), "1.2.3.4", "corr");
            Assert.IsNull(result.Item2, result.Item2 == null ? null : string.Join(" | ", result.Item2.Errors.Select(x => x.Field)));
            var entity = DemandApplicationStore.ToApplicationEntity(store.Saved[0]);
            var schema = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(Repo.Root, "database", "database-schema.json")));
            var columns = ((ArrayList)((ArrayList)schema["tables"]).Cast<Dictionary<string, object>>().Single(t => (string)t["name"] == "TR_CFC_DemandApplications")["columns"]).Cast<Dictionary<string, object>>().ToList();
            var required = columns.Where(c => !(bool)c["isNullable"] && !(bool)c["isIdentity"]).Select(c => (string)c["name"]).ToList();
            Assert.AreEqual(35, required.Count);
            foreach (var column in required) Assert.IsNotNull(typeof(DemandApplication).GetProperty(column).GetValue(entity), "NOT NULL column left null: " + column);
            Assert.AreEqual(string.Empty, entity.ApplicantName); Assert.AreEqual(string.Empty, entity.Mobile); Assert.AreEqual(string.Empty, entity.Email);
            Assert.AreEqual(string.Empty, entity.GstNumber); Assert.IsNull(entity.IdentityNumber); Assert.IsNull(entity.PanNumber); Assert.AreEqual("वैयक्तिक", entity.ApplicantType);
            // Lengths fit the columns (nvarchar(200) / (10) / (200)).
            Assert.IsTrue(entity.ApplicantName.Length <= 200 && entity.Mobile.Length <= 10 && entity.Email.Length <= 200);
            // History actor falls back to "Citizen" (never empty).
            var first = ActionHistory.Parse(store.Saved[0].InitialHistoryJson).Single();
            Assert.AreEqual("Citizen", first.ActionByUserName); Assert.AreEqual(DemandSubmissionService.DefaultActorName, first.ActionByUserName);
            // The store itself never writes NULL for these even if handed nulls directly.
            var raw = new NewApplicationRecord { Data = NoApplicant(), ApplicationNumber = "X1", Pricing = store.Saved[0].Pricing, PaymentStatus = "Fee Pending" };
            var direct = DemandApplicationStore.ToApplicationEntity(raw);
            Assert.AreEqual(string.Empty, direct.ApplicantName); Assert.AreEqual(string.Empty, direct.Mobile); Assert.AreEqual(string.Empty, direct.Email);
        }

        [TestMethod]
        public void Supplied_Values_Are_Still_Format_Checked_And_Stored_When_The_Switch_Is_Off()
        {
            var store = new FakeAppStore(); var svc = Service(store, false);
            var badMobile = NoApplicant(); badMobile.Mobile = "12345";
            Assert.IsTrue(svc.Submit(badMobile, TestData.Docs(), null, null).Item2.Errors.Any(x => x.Field == "Mobile"));
            var badEmail = NoApplicant(); badEmail.Email = "a@b";
            Assert.IsTrue(svc.Submit(badEmail, TestData.Docs(), null, null).Item2.Errors.Any(x => x.Field == "Email"));
            Assert.AreEqual(0, store.Saved.Count);
            var given = NoApplicant(); given.ApplicantName = " राम "; given.Mobile = " 9876543210 "; given.Email = " Ram@Example.com ";
            Assert.IsNull(svc.Submit(given, TestData.Docs(), null, null).Item2);
            Assert.AreEqual("राम", store.Saved[0].Data.ApplicantName); Assert.AreEqual("9876543210", store.Saved[0].Data.Mobile); Assert.AreEqual("ram@example.com", store.Saved[0].Data.Email);
            // Switch on: the previous rules apply.
            var on = Service(new FakeAppStore(), true);
            var errors = on.Submit(NoApplicant(), TestData.Docs(), null, null).Item2.Errors.Select(x => x.Field).ToList();
            CollectionAssert.IsSubsetOf(new[] { "ApplicantName", "Mobile" }, errors);
        }

        [TestMethod]
        public void Empty_Mobile_Suppresses_The_Applicant_Sms_Without_Failing_And_Officer_Sms_Still_Goes_Out()
        {
            var masters = new FakeMasters { Template = new SmsTemplateRecord { EventType = "any", MessageBody = "अर्ज {ApplicationNumber} {ApplicantName}", DltTemplateId = "T1", SenderId = "MAHGOV" } };
            var gateway = new FakeGateway(); var events = new FakeSmsEvents(); var users = new FakeUsers();
            users.Users.Add(new UserRecord { Id = 1, Username = "ac", Role = Roles.AssistantCommissioner, IsActive = true, Mobile = "9000000001" });
            var sms = new SmsService(masters, gateway, events, users, new FakeClock(), true, "MAHGOV");
            var store = new FakeAppStore();
            var result = Service(store, false, sms).Submit(NoApplicant(), TestData.Docs(), null, null);
            Assert.IsNull(result.Item2); Assert.AreEqual(1, store.Saved.Count);
            Assert.AreEqual(0, gateway.Texts.Count, "no SMS attempt to an empty mobile");
            var row = events.Rows.Single(x => x.EventType == SmsEventTypes.ApplicationSubmitted);
            Assert.AreEqual(SmsStatuses.Suppressed, row.Status); Assert.AreEqual("No recipient mobile number", row.FailureReason);

            // Workflow notifications: the applicant event is suppressed, the AC role notification is sent.
            var wfStore = new FakeWorkflowStore { Current = FakeWorkflowStore.Snapshot(ZoneStages.PaymentRequired, PaymentStatuses.PaymentSubmitted) };
            wfStore.Current.ApplicantMobile = ""; wfStore.Current.ApplicantName = "";
            Assert.IsTrue(new ZoneWorkflowService(wfStore, new FakeClock(), sms).Execute(new WorkflowCommand { ApplicationId = 1, Action = WorkflowActions.OSConfirmPayment, ExpectedStage = ZoneStages.PaymentRequired }, Actors.OS()).IsSuccess);
            Assert.AreEqual(SmsStatuses.Suppressed, events.Rows.Single(x => x.EventType == SmsEventTypes.PaymentDone).Status);
            var ac = events.Rows.Single(x => x.EventType == SmsEventTypes.AssistantCommissionerNotification);
            Assert.AreEqual(SmsStatuses.Sent, ac.Status); Assert.AreEqual("9000000001", ac.Mobile); Assert.AreEqual(1, gateway.Texts.Count);
        }

        [TestMethod]
        public void Officer_And_Applicant_Views_Csv_And_Pdf_Rows_Tolerate_Empty_Applicant_Details()
        {
            var d = new ApplicationDetail { Id = 9, ApplicationNumber = "20260009", ProcessingRoute = ProcessingRoutes.Zone, Stage = ZoneStages.JEPending, Status = ApplicationStatuses.Submitted,
                PaymentStatus = PaymentStatuses.PendingOSAmount, ApplicantName = "", Mobile = "", Email = "", ApplicantType = "वैयक्तिक", GstNumber = "", StartDate = new DateTime(2026, 10, 6), EndDate = new DateTime(2026, 10, 8) };
            var officer = OfficerApplicationViewMapper.Build(d, Roles.JE, "");
            Assert.AreEqual("—", officer.ApplicantName); Assert.AreEqual("—", officer.Mobile); Assert.AreEqual("—", officer.Email);
            Assert.AreEqual("—", OfficerText.OrDash(null)); Assert.AreEqual("—", OfficerText.OrDash("  ")); Assert.AreEqual("राम", OfficerText.OrDash("राम"));
            var grid = Web(Path.Combine("Department", "Applications.aspx.cs"));
            StringAssert.Contains(grid, "e.Row.Cells[1].Text = Enc(OfficerText.OrDash(item.ApplicantName));"); StringAssert.Contains(grid, "e.Row.Cells[3].Text = Enc(OfficerText.OrDash(item.Mobile));");
            StringAssert.Contains(Web(Path.Combine("Department", "ApplicationDetail.aspx.cs")), "row(\"अर्जदाराचे पूर्ण नाव\", OfficerText.OrDash(d.ApplicantName)); row(\"मोबाईल\", OfficerText.OrDash(d.Mobile));");
            // Search still works by application number: the filter only adds Contains on name/mobile.
            StringAssert.Contains(File.ReadAllText(Path.Combine(Repo.Root, "SMC.BhoomiMalmatta.Data", "Repositories", "QueryService.cs")), "x.ApplicationNumber.Contains(s) || x.ApplicantName.Contains(s) || x.Mobile.Contains(s)");

            var applicant = CitizenStatusMapper.MapDetail(d);
            Assert.AreEqual(string.Empty, applicant.ApplicantName); Assert.AreEqual(string.Empty, applicant.Mobile); Assert.AreEqual(string.Empty, applicant.Email);
            Assert.IsFalse(new JavaScriptSerializer().Serialize(applicant).Contains("\"ApplicantName\":null"));
            var dashboard = Web(Path.Combine("Citizen", "Dashboard.aspx.cs"));
            StringAssert.Contains(dashboard, "if (!string.IsNullOrWhiteSpace(v.ApplicantName)) row(\"अर्जदाराचे नाव\"");
            StringAssert.Contains(dashboard, "if (!string.IsNullOrWhiteSpace(v.Mobile)) row(\"मोबाईल\"");
            StringAssert.Contains(dashboard, "if (!string.IsNullOrWhiteSpace(v.Email)) row(\"ई-मेल\"");

            var pdfRows = PdfGenerator.ApplicantRows(d);
            CollectionAssert.AreEqual(new[] { "अर्जदाराचा प्रकार" }, pdfRows.Select(x => x.Key).ToArray(), "name, mobile and e-mail rows are omitted when empty");
            Assert.AreEqual("—", PdfGenerator.Cell("")); Assert.AreEqual("—", PdfGenerator.Cell(null)); Assert.AreEqual("A", PdfGenerator.Cell("A"));
            var withName = PdfGenerator.ApplicantRows(new ApplicationDetail { ApplicantName = "राम", Mobile = "9876543210" });
            CollectionAssert.AreEqual(new[] { "अर्जदाराचे पूर्ण नाव", "मोबाईल" }, withName.Select(x => x.Key).ToArray());
            StringAssert.Contains(Web(Path.Combine("Services", "PdfGenerator.cs")), "if (!string.IsNullOrWhiteSpace(d.ApplicantName)) Row(c, \"अर्जदार\", d.ApplicantName);");

            var csv = CsvWriter.Write(new[] { "No", "Name", "Mobile" }, new List<string[]> { new[] { "20260009", "", null } });
            Assert.AreEqual("No,Name,Mobile\r\n20260009,,\r\n", csv);
        }

        [TestMethod]
        public void Certificate_Still_Generates_Without_Applicant_Details()
        {
            var q = new DocumentAndCertificateTests.FakeQuery { Detail = new ApplicationDetail { Id = 1, ApplicationNumber = "2026-0005", ProcessingRoute = ProcessingRoutes.Zone, Stage = ZoneStages.Approved, ApplicantName = "", Mobile = "", Email = "", ApprovedBy = "ac.user" } };
            var store = new FakeWorkflowStore(); var files = new FakeFiles(); var n = new FakeNotifier();
            var certs = new CertificateService(q, new FakeMasters(), new CertPdf(), files, store, n, new FakeAudit(), new FakeClock());
            Assert.IsTrue(certs.Issue(1, Actors.AC()).IsSuccess);
            Assert.IsTrue(files.Exists(store.CertificatePath));
            Assert.AreEqual(string.Empty, n.Sent.Single().RecipientMobile, "the SMS service turns this into a Suppressed record");
        }

        [DataTestMethod]
        [DataRow(null, "")] [DataRow("", "")] [DataRow("  ", "")] [DataRow("12", "**")] [DataRow("1234", "****")] [DataRow("98765", "******8765")] [DataRow("9876541230", "******1230")]
        public void Mobile_Masking_Handles_Empty_And_Short_Values(string input, string expected) { Assert.AreEqual(expected, CitizenStatusMapper.MaskMobile(input)); }

        [DataTestMethod]
        [DataRow(null, "")] [DataRow("", "")] [DataRow("a", "*")] [DataRow("x@", "**")] [DataRow("@b.c", "@b***")] [DataRow("a@b.c", "a***@b.c")]
        public void Email_Masking_Handles_Empty_And_Short_Values(string input, string expected) { Assert.AreEqual(expected, CitizenStatusMapper.MaskEmail(input)); }

        [TestMethod]
        public void Zone_And_Location_Are_On_Step_Two_After_The_Address_And_Step_Three_Has_Only_Review_Documents_And_Declaration()
        {
            var markup = Web("Apply.aspx"); var v2 = View(markup, 2); var v3 = View(markup, 3);
            foreach (var id in new[] { "ddlZone", "rfvZone", "txtLocation", "rfvLocation" }) { StringAssert.Contains(v2, "ID=\"" + id + "\""); Assert.IsFalse(v3.Contains("ID=\"" + id + "\""), id); }
            StringAssert.Contains(v2, "ControlToValidate=\"ddlZone\" InitialValue=\"\" ValidationGroup=\"g2\"");
            StringAssert.Contains(v2, "ControlToValidate=\"txtLocation\" ValidationGroup=\"g2\"");
            Assert.IsTrue(v2.IndexOf("ID=\"ddlZone\"", StringComparison.Ordinal) > v2.IndexOf("ID=\"txtPin\"", StringComparison.Ordinal), "below the address fields");
            foreach (var gone in new[] { "झोन", "जागेचे ठिकाण" }) Assert.IsFalse(v3.Contains(gone), gone);
            CollectionAssert.AreEquivalent(new[] { "litReview", "lblIdCaption", "fuIdentity", "btnSaveIdentity", "btnRemoveIdentity", "lblIdentityFileName", "lblAddrDocCaption", "fuAddress", "btnSaveAddress", "btnRemoveAddress", "lblAddressFileName", "chkDeclaration", "cvDeclaration" },
                Regex.Matches(v3, "ID=\"(\\w+)\" runat=\"server\"").Cast<Match>().Select(x => x.Groups[1].Value).Where(x => x != "v3").ToArray());
            var noPlace = NoApplicant(); noPlace.Zone = "9"; noPlace.Location = " ";
            var errors = ApplyWizard.BlockingErrors(noPlace, ApplyWizard.AddressStep, new FakeMasters().GetCatalog(), false).Select(x => x.Field).ToList();
            CollectionAssert.IsSubsetOf(new[] { "Zone", "Location" }, errors);
            // Review on step 3 omits empty applicant lines.
            StringAssert.Contains(Web("Apply.aspx.cs"), "if (!string.IsNullOrWhiteSpace(d.ApplicantName)) field(\"पूर्ण नाव\", d.ApplicantName, null);");
        }

        [TestMethod]
        public void Public_Api_Accepts_A_Request_Without_Applicant_Details_Only_When_The_Switch_Is_Off()
        {
            var previous = AppServices.Submission;
            try
            {
                AppServices.Submission = Service(new FakeAppStore(), false);
                var accepted = Controller().Submit(Request());
                Assert.AreEqual(HttpStatusCode.Created, Status(accepted));

                AppServices.Submission = Service(new FakeAppStore(), true);
                var rejected = Controller().Submit(Request());
                Assert.AreEqual((HttpStatusCode)422, Status(rejected));
                var content = rejected.GetType().GetProperty("Content").GetValue(rejected);
                var fields = ((IEnumerable<FieldErrorDto>)content.GetType().GetProperty("errors").GetValue(content)).Select(x => x.Field).ToList();
                CollectionAssert.IsSubsetOf(new[] { "ApplicantName", "Mobile" }, fields);
            }
            finally { AppServices.Submission = previous; }
        }

        private static ApplicationsController Controller() { return new ApplicationsController { Request = new HttpRequestMessage(), Configuration = new HttpConfiguration() }; }
        private static HttpStatusCode Status(IHttpActionResult r) { return (HttpStatusCode)r.GetType().GetProperty("StatusCode").GetValue(r); }

        private static SubmissionRequest Request()
        {
            var pdf = Convert.ToBase64String(TestData.Pdf);
            return new SubmissionRequest
            {
                ServiceType = "MajorGaala", PermanentAddress = "१२, सोलापूर", SameAddress = true, City = "Solapur", Taluka = "उत्तर सोलापूर", PinCode = "413001", Zone = "3", Location = "मार्केट यार्ड",
                ServiceDescription = "स्टॉल", StartDate = new DateTime(2026, 10, 10), EndDate = new DateTime(2026, 10, 14), DeclarationAccepted = true,
                Documents = new List<DocumentPayload>
                {
                    new DocumentPayload { DocumentType = DocumentTypes.IdentityProof, FileName = "id.pdf", ContentType = "application/pdf", ContentBase64 = pdf },
                    new DocumentPayload { DocumentType = DocumentTypes.AddressProof, FileName = "address.pdf", ContentType = "application/pdf", ContentBase64 = pdf }
                }
            };
        }
    }
}
