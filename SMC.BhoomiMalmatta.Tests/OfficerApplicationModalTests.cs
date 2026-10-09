using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SMC.BhoomiMalmatta.Business.Citizen;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;
using SMC.BhoomiMalmatta.Web.Api;
using SMC.BhoomiMalmatta.Web.Services;
using System.Web.Http;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http.Results;
using SMC.BhoomiMalmatta.Web.Security;

namespace SMC.BhoomiMalmatta.Tests
{
    [TestClass]
    public class OfficerApplicationModalTests
    {
        [TestMethod]
        public void OfficerModalMapper_ReturnsSafeSectionsDocumentsAndOrderedNamedHistory()
        {
            var d = new ApplicationDetail
            {
                Id = 2128, ApplicationNumber = "20262128", ProcessingRoute = ProcessingRoutes.Zone,
                ServiceType = "VariousBusinessSpace", ServiceName = "\u091c\u093e\u0917\u093e \u092e\u093e\u0917\u0923\u0940", ApplicantName = "Applicant <script>", Mobile = "9876541230", Email = "applicant@example.org",
                Stage = ZoneStages.JEPending, Status = ApplicationStatuses.UnderScrutiny, PaymentStatus = PaymentStatuses.PaymentRequired,
                PayableAmount = 300m, FeeAmount = 300m, LengthFt = 10, WidthFt = 10, AreaSqFt = 100, CalculatedRate = 1,
                StartDate = new DateTime(2026, 10, 6), EndDate = new DateTime(2026, 10, 8), TotalDays = 3,
                Zone = "2", Location = "à¤®à¥à¤–à¥à¤¯ à¤°à¤¸à¥à¤¤à¤¾", SpaceRequirement = "à¤¸à¥à¤Ÿà¥‰à¤²", AvailableSpace = "à¤ªà¥‚à¤°à¥à¤µ à¤¬à¤¾à¤œà¥‚", RequiredDuration = "à¥© à¤¦à¤¿à¤µà¤¸",
                Documents = { new DocumentItem { Id = 9, DocumentType = "PaymentProof", FileName = "proof.pdf", ContentType = "application/pdf", IsZoneDocument = true, VerificationStatus = VerificationStatuses.Unchecked, CreatedAt = new DateTime(2026, 10, 7) } },
                History = {
                    new HistoryItem { Sequence = 2, AtUtc = new DateTime(2026, 10, 7), Action = AuditActions.JEVerified, FromRole = Roles.JE, By = "Officer Name", Remarks = "forwarded" },
                    new HistoryItem { Sequence = 1, AtUtc = new DateTime(2026, 10, 6), Action = AuditActions.ApplicationSubmitted, FromRole = Roles.Citizen, By = "Citizen", Remarks = "submitted" }
                }
            };
            var view = OfficerApplicationViewMapper.Build(d, Roles.JE, "/smc");
            Assert.AreEqual("20262128", view.ApplicationNumber); Assert.AreEqual("Applicant <script>", view.ApplicantName);
            Assert.IsTrue(view.Fields.Any(x => x.Label == "\u0938\u0947\u0935\u093e" && x.Value == "\u091c\u093e\u0917\u093e \u092e\u093e\u0917\u0923\u0940"));
            Assert.IsTrue(view.Fields.Any(x => x.Label == "\u090f\u0915\u0942\u0923 \u0915\u094d\u0937\u0947\u0924\u094d\u0930\u092b\u0933 (sq ft)" && x.Value == "100"));
            Assert.AreEqual(2, view.History.Count); Assert.AreEqual("\u0905\u0930\u094d\u091c \u0938\u093e\u0926\u0930 \u0915\u0947\u0932\u093e", view.History[0].Action);
            Assert.AreEqual("Officer Name", view.History[1].OfficerName); Assert.AreEqual("forwarded", view.History[1].Remark);
            Assert.IsFalse(string.IsNullOrWhiteSpace(view.History[1].DateLabel));
            Assert.AreEqual("\u092d\u0930\u0923\u093e \u092a\u0941\u0930\u093e\u0935\u093e", view.Documents[0].Type); Assert.IsTrue(view.Documents[0].PreviewUrl.StartsWith("/smc/Download.ashx?type=zdoc&id=9&appId=2128&inline=1", StringComparison.Ordinal));
            Assert.IsTrue(view.Documents[0].DownloadUrl.StartsWith("/smc/Download.ashx?type=zdoc&id=9&appId=2128", StringComparison.Ordinal));
            Assert.IsFalse(view.CanUploadDocuments); Assert.IsFalse(view.CanVerifyDocuments);
            var propertyNames = typeof(OfficerApplicationView).GetProperties().Select(x => x.Name).Concat(typeof(OfficerDocumentView).GetProperties().Select(x => x.Name)).ToArray();
            Assert.IsFalse(propertyNames.Any(x => new[] { "Pan", "Identity", "Path", "Hash", "Token", "Password", "Ip", "Correlation" }.Any(k => x.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)));
            Assert.IsFalse(typeof(CitizenTimelineItem).GetProperties().Any(x => x.Name.IndexOf("Officer", StringComparison.OrdinalIgnoreCase) >= 0 || x.Name == "By"));
        }

        [TestMethod]
        public void LegacyAndAdminStaffAreReadOnlyAndMissingSitePhotoHasMessage()
        {
            var legacy = new ApplicationDetail { Id = 1, ProcessingRoute = ProcessingRoutes.Legacy, Status = ApplicationStatuses.Approved, PaymentStatus = PaymentStatuses.NotRequired };
            var legacyView = OfficerApplicationViewMapper.Build(legacy, Roles.JE, "");
            Assert.IsTrue(legacyView.IsLegacy); Assert.IsTrue(legacyView.ReadOnly); Assert.AreEqual(0, legacyView.AllowedActionLabels.Count);
            StringAssert.Contains(legacyView.SitePhotoMessage, "\u0909\u092a\u0932\u092c\u094d\u0927 \u0928\u093e\u0939\u0940"); Assert.IsNull(legacyView.SitePhotoUrl);
            Assert.IsTrue(OfficerApplicationViewMapper.ZoneAllows(Roles.JE, 2, "2"));
            Assert.IsFalse(OfficerApplicationViewMapper.ZoneAllows(Roles.JE, 1, "2"));
            Assert.IsTrue(OfficerApplicationViewMapper.ZoneAllows(Roles.OS, null, "2"));
            Assert.IsFalse(OfficerApplicationViewMapper.ZoneAllows(Roles.Staff, 1, "2"));
            foreach (var role in new[] { Roles.Admin, Roles.Staff })
            {
                var view = OfficerApplicationViewMapper.Build(new ApplicationDetail { ProcessingRoute = ProcessingRoutes.Zone, Stage = ZoneStages.JEPending, PaymentStatus = PaymentStatuses.NotRequired }, role, "");
                Assert.IsTrue(view.ReadOnly); Assert.AreEqual(0, view.AllowedActionLabels.Count);
            }
        }

        [TestMethod]
        public void OfficerDetailEndpointIsReadOnlyAuthorizedAndLinksNeverResolveUnderControls()
        {
            var endpoint = typeof(ApplicationsController).GetMethods().Single(x => x.Name == "OfficerView");
            Assert.IsTrue(endpoint.GetCustomAttributes(typeof(HttpGetAttribute), true).Any());
            var route = endpoint.GetCustomAttributes(typeof(RouteAttribute), true).Cast<RouteAttribute>().Single();
            Assert.AreEqual("{id:int}/officer-view", route.Template);
            var authorize = endpoint.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single();
            StringAssert.Contains(authorize.Roles, "JE"); StringAssert.Contains(authorize.Roles, "Staff");
            var root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", ".."));
            var web = Path.Combine(root, "SMC.BhoomiMalmatta.Web");
            var relativeLink = new Regex("(?:href|NavigateUrl)\\s*=\\s*['\\\"]ApplicationDetail\\.aspx", RegexOptions.IgnoreCase);
            foreach (var file in Directory.GetFiles(web, "*.*", SearchOption.AllDirectories).Where(f => f.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".ascx", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
                Assert.IsFalse(relativeLink.IsMatch(File.ReadAllText(file)), "Relative ApplicationDetail link found in " + file.Substring(web.Length));
            var markup = File.ReadAllText(Path.Combine(web, "Department", "Applications.aspx"));
            StringAssert.Contains(markup, "aria-modal=\"true\""); StringAssert.Contains(markup, "e.key === 'Escape'");
        }

        [TestMethod]
        public void OfficerDetailEndpointReturnsDtoForZoneAndNotFoundAcrossZones()
        {
            var previousQuery = AppServices.Query;
            var previousContext = HttpContext.Current;
            var fake = new DocumentAndCertificateTests.FakeQuery
            {
                Detail = new ApplicationDetail { Id = 2128, ApplicationNumber = "20262128", ProcessingRoute = ProcessingRoutes.Zone, Zone = "2", Stage = ZoneStages.JEPending, PaymentStatus = PaymentStatuses.NotRequired }
            };
            try
            {
                AppServices.Query = fake;
                HttpContext.Current = new HttpContext(new HttpRequest("", "http://localhost/", ""), new HttpResponse(new StringWriter()));
                var eligible = ControllerFor(Roles.JE, 2).OfficerView(2128) as OkNegotiatedContentResult<OfficerApplicationView>;
                Assert.IsNotNull(eligible); Assert.AreEqual("20262128", eligible.Content.ApplicationNumber);
                var otherZone = ControllerFor(Roles.JE, 3).OfficerView(2128) as NegotiatedContentResult<ApiError>;
                Assert.IsNotNull(otherZone); Assert.AreEqual(HttpStatusCode.NotFound, otherZone.StatusCode);
            }
            finally { AppServices.Query = previousQuery; HttpContext.Current = previousContext; }
        }

        private static ApplicationsController ControllerFor(string role, int? zone)
        {
            var controller = new ApplicationsController { Request = new HttpRequestMessage(), Configuration = new HttpConfiguration(), User = new SmcPrincipal(new SmcIdentity { Role = role, UserId = 7, ZoneNumber = zone, Name = "officer" }) };
            return controller;
        }
    }
}

