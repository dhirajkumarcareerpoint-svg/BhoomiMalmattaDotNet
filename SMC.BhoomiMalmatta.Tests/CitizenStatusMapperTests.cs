using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SMC.BhoomiMalmatta.Business.Citizen;
using SMC.BhoomiMalmatta.Web.Api;
using SMC.BhoomiMalmatta.Web.Security;
using System.Web.Http;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Tests
{
    [TestClass]
    public class CitizenStatusMapperTests
    {
        private static ApplicationDetail Detail(string stage, string payment = PaymentStatuses.NotRequired)
        {
            return new ApplicationDetail { Id = 42, ApplicationNumber = "20260042", ProcessingRoute = ProcessingRoutes.Zone, ServiceType = "SVC", ServiceName = "à¤®à¤°à¤¾à¤ à¥€ à¤¸à¥‡à¤µà¤¾", ApplicantName = "Applicant",
                Stage = stage, Status = "Submitted", PaymentStatus = payment, PayableAmount = 250m, FeeAmount = 250m, SubmittedAt = DateTime.UtcNow.AddDays(-2),
                StartDate = new DateTime(2026, 10, 6), EndDate = new DateTime(2027, 10, 6), TotalDays = 365 };
        }

        private static HistoryItem H(string action, string fromRole, string remark = null, int seq = 1)
        { return new HistoryItem { Sequence = seq, Action = action, FromRole = fromRole, Remarks = remark, AtUtc = new DateTime(2026, 10, 6, 10, 0, 0, DateTimeKind.Utc) }; }

        [TestMethod]
        public void StageTable_CoversInitialCorrectionResubmissionAndForwardedStates()
        {
            var initial = CitizenStatusMapper.MapDetail(Detail(ZoneStages.JEPending));
            Assert.AreEqual(ApplicantText.Pending, initial.Levels[0].Status); Assert.AreEqual("Active", initial.Levels[0].State);
            Assert.AreEqual(ApplicantText.Waiting, initial.Levels[1].Status); Assert.AreEqual(ApplicantText.Waiting, initial.Levels[2].Status);
            Assert.IsFalse(initial.ShowCorrectionForm);

            var jeCorrection = Detail(ZoneStages.JECorrectionRequested); jeCorrection.History.Add(H(AuditActions.JEReturnedForCorrection, Roles.JE, "Fix address"));
            var correction = CitizenStatusMapper.MapDetail(jeCorrection);
            Assert.AreEqual(ApplicantText.CorrectionRequired, correction.Levels[0].Status); Assert.AreEqual("Fix address", correction.Levels[0].Remarks);
            Assert.AreEqual(ApplicantText.CorrectionAction, correction.Levels[0].ActionRequired); Assert.IsTrue(correction.ShowCorrectionForm);
            var correctionEvent = correction.Timeline.Single(x => x.Event == ApplicantText.CorrectionEvent);
            Assert.AreEqual(ApplicantText.Level1, correctionEvent.Level); Assert.AreEqual("Fix address", correctionEvent.Remark); Assert.AreEqual(10, correctionEvent.AtUtc.Hour);

            var jeResubmit = Detail(ZoneStages.JEPending); jeResubmit.History.Add(H(AuditActions.CitizenResubmitted, Roles.Citizen, "Updated address"));
            Assert.AreEqual(ApplicantText.ResubmittedJe, CitizenStatusMapper.MapDetail(jeResubmit).Levels[0].Status);

            var osPending = Detail(ZoneStages.OSPending); osPending.History.Add(H(AuditActions.JEVerified, Roles.JE));
            var os = CitizenStatusMapper.MapDetail(osPending);
            Assert.AreEqual(ApplicantText.Approved, os.Levels[0].Status); Assert.AreEqual(ApplicantText.Pending, os.Levels[1].Status);
            Assert.AreEqual("OS Review", os.Levels[0].NextStage); Assert.AreEqual(ApplicantText.Waiting, os.Levels[2].Status);

            var osCorrection = Detail(ZoneStages.OSCorrectionRequested); osCorrection.History.Add(H(AuditActions.OSReturnedForCorrection, Roles.OS, "Attach proof"));
            var osFix = CitizenStatusMapper.MapDetail(osCorrection);
            Assert.IsTrue(osFix.ShowCorrectionForm); Assert.AreEqual("Attach proof", osFix.Levels[1].Remarks);

            var osResubmit = Detail(ZoneStages.OSPending); osResubmit.History.Add(H(AuditActions.CitizenResubmitted, Roles.Citizen));
            Assert.AreEqual(ApplicantText.ResubmittedOs, CitizenStatusMapper.MapDetail(osResubmit).Levels[1].Status);
        }

        [TestMethod]
        public void PaymentStates_ShowFormOnlyWhenPaymentIsRequiredOrRejected()
        {
            var requested = Detail(ZoneStages.PaymentRequired, PaymentStatuses.PaymentRequired);
            requested.History.Add(H(AuditActions.PaymentRequestSent, Roles.OS));
            var g = CitizenStatusMapper.MapDetail(requested);
            Assert.AreEqual(ApplicantText.PaymentRequired, g.Levels[1].Status); Assert.IsTrue(g.Payment.ShowForm); Assert.IsFalse(g.Payment.IsRecord);

            var submitted = Detail(ZoneStages.PaymentRequired, PaymentStatuses.PaymentSubmitted);
            submitted.PaymentReference = "ABC12345"; submitted.PaymentDate = new DateTime(2026, 10, 6); submitted.History.Add(H(AuditActions.PaymentSubmittedByApplicant, Roles.Citizen));
            submitted.Documents.Add(new DocumentItem { IsZoneDocument = true, DocumentType = "PaymentProof", FileName = "receipt.pdf", CreatedAt = DateTime.UtcNow });
            var h = CitizenStatusMapper.MapDetail(submitted);
            Assert.AreEqual(ApplicantText.PaymentSubmitted, h.Levels[1].Status); Assert.IsFalse(h.Payment.ShowForm); Assert.IsTrue(h.Payment.IsRecord);
            Assert.AreEqual(ApplicantText.PaymentSubmittedLabel, h.PaymentStatus);
            Assert.AreEqual("ABC12345", h.Payment.Reference); Assert.AreEqual("receipt.pdf", h.Payment.ProofFileName); Assert.IsNotNull(h.Payment.SubmittedAtUtc);

            // Legacy-state row (OSPending + PaymentDone): no "Awaiting forwarding by OS" line any more.
            var paid = CitizenStatusMapper.MapDetail(Detail(ZoneStages.OSPending, PaymentStatuses.PaymentDone));
            Assert.AreEqual(ApplicantText.PaymentDone, paid.Levels[1].Status); Assert.IsNull(paid.Levels[1].Remarks);
            Assert.AreEqual(ApplicantText.AcApproval, paid.Levels[1].NextStage); Assert.AreEqual("Waiting", paid.Levels[2].State);
        }

        [TestMethod]
        public void AcApprovalReturnAndCertificateStates_AreProjectedSafely()
        {
            var ac = Detail(ZoneStages.AssistantCommissionerApprovalPending, PaymentStatuses.PaymentDone);
            var j = CitizenStatusMapper.MapDetail(ac);
            Assert.AreEqual(ApplicantText.Pending, j.Levels[2].Status); Assert.AreEqual("Active", j.Levels[2].State);

            var returned = Detail(ZoneStages.OSPending, PaymentStatuses.PaymentDone);
            returned.History.Add(H(AuditActions.ACReturned, Roles.AssistantCommissioner, "private internal note"));
            var k = CitizenStatusMapper.MapDetail(returned);
            Assert.AreEqual(ApplicantText.ReturnedToOs, k.Levels[2].Status); Assert.IsNull(k.Levels[2].Remarks); Assert.AreEqual("Active", k.Levels[1].State);
            Assert.IsFalse(k.Timeline.Any(x => x.Remark == "private internal note")); Assert.IsFalse(k.Timeline.Any(x => x.Event == "AC Returned"));

            var noCert = CitizenStatusMapper.MapDetail(Detail(ZoneStages.Approved, PaymentStatuses.PaymentDone));
            Assert.IsTrue(noCert.Certificate.IsApproved); Assert.IsFalse(noCert.Certificate.HasDownload); Assert.AreEqual(ApplicantText.CertificatePreparing, noCert.Certificate.Text);
            var cert = Detail(ZoneStages.Approved, PaymentStatuses.PaymentDone); cert.CertificatePath = "certificates/permit.pdf";
            Assert.IsTrue(CitizenStatusMapper.MapDetail(cert).Certificate.HasDownload);

            var rejected = Detail(ZoneStages.Rejected); rejected.History.Add(H(AuditActions.OSRejected, Roles.OS, "Not eligible"));
            var m = CitizenStatusMapper.MapDetail(rejected);
            Assert.AreEqual(ApplicantText.Rejected, m.Levels[1].Status); Assert.AreEqual("Not eligible", m.Levels[1].Remarks);
            Assert.AreEqual("Waiting", m.Levels[2].Status); Assert.IsFalse(m.Payment.ShowForm); Assert.IsFalse(m.ShowCorrectionForm);
        }

        [TestMethod]
        public void LegacyDetail_RemainsReadOnlyWithoutStepperOrHistory()
        {
            var legacy = Detail(ZoneStages.JEPending); legacy.ProcessingRoute = ProcessingRoutes.Legacy; legacy.History.Add(H(AuditActions.JERejected, Roles.JE, "internal"));
            var mapped = CitizenStatusMapper.MapDetail(legacy);
            Assert.IsTrue(mapped.IsLegacy); Assert.AreEqual(0, mapped.Levels.Count); Assert.AreEqual(0, mapped.Timeline.Count);
            Assert.IsFalse(mapped.ShowCorrectionForm);
        }

        [TestMethod]
        public void ApplicantDto_ContainsNoInternalIdentityOrSecurityFields()
        {
            var names = typeof(CitizenStatusView).GetProperties().Select(x => x.Name).ToArray();
            Assert.IsFalse(names.Any(n => n.IndexOf("Officer", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("UserId", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Ip", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Hash", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Correlation", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Pan", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Identity", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Path", StringComparison.OrdinalIgnoreCase) >= 0));
            Assert.IsFalse(typeof(ApplicantDocumentItem).GetProperties().Any(p => p.Name.IndexOf("Path", StringComparison.OrdinalIgnoreCase) >= 0 || p.Name.IndexOf("Stored", StringComparison.OrdinalIgnoreCase) >= 0));
            var detail = Detail(ZoneStages.JEPending); detail.Mobile = "9876541230"; detail.Email = "applicant@example.org"; detail.History.Add(H(AuditActions.JEVerified, Roles.JE));
            var mapped = CitizenStatusMapper.MapDetail(detail);
            Assert.IsFalse(mapped.Timeline.Any(x => x.Event == "JE Verified"));
            Assert.AreEqual("******1230", mapped.Mobile); Assert.AreEqual("ap***@example.org", mapped.Email);
            Assert.IsFalse(string.Join("|", typeof(CitizenStatusView).GetProperties().Select(p => p.Name)).Contains("AccessCode"));
        }

        [TestMethod]
        public void QrResolution_HidesBlankMissingAndTraversalPaths_AndSupportsVirtualOrPhysicalFiles()
        {
            var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "smc"));
            Func<string, bool> present = p => p.EndsWith("Content" + Path.DirectorySeparatorChar + "payment" + Path.DirectorySeparatorChar + "qr.png", StringComparison.OrdinalIgnoreCase) || p == Path.GetFullPath(Path.Combine(Path.GetTempPath(), "configured-qr.jpg"));
            Assert.IsNull(ApplicantQrPath.Resolve("", root, present));
            Assert.IsNull(ApplicantQrPath.Resolve("~/Content/payment/missing.png", root, p => false));
            Assert.IsNull(ApplicantQrPath.Resolve("~/../outside.png", root, present));
            var virtualQr = ApplicantQrPath.Resolve("~/Content/payment/qr.png", root, present);
            Assert.IsNotNull(virtualQr); Assert.IsTrue(virtualQr.IsVirtual); Assert.AreEqual("~/Content/payment/qr.png", virtualQr.Value);
            var physical = ApplicantQrPath.Resolve(Path.Combine(Path.GetTempPath(), "configured-qr.jpg"), root, present);
            Assert.IsNotNull(physical); Assert.IsFalse(physical.IsVirtual);
        }

        [TestMethod]
        public void PaymentQr_UsesExistingConfiguredAssetAndSniffsActualImageFormat()
        {
            var project = ProjectFile("SMC.BhoomiMalmatta.Web", "");
            var image = Path.Combine(project, "Content", "payment", "qr.png");
            Assert.IsTrue(File.Exists(image), "Expected existing QR file at Content/payment/qr.png.");
            using (var imageStream = File.OpenRead(image))
                Assert.AreEqual("image/jpeg", PaymentQrImageType.Detect(".png", imageStream), "The file has a .png name but contains JPEG/JFIF bytes; the handler must return the actual MIME type.");

            var projectFile = File.ReadAllText(Path.Combine(project, "SMC.BhoomiMalmatta.Web.csproj"));
            StringAssert.Contains(projectFile, "<Content Include=\"Content\\payment\\qr.png\" />");
            Assert.AreEqual(1, projectFile.Split(new[] { "Content\\payment\\qr.png" }, StringSplitOptions.None).Length - 1);
            var config = File.ReadAllText(Path.Combine(project, "Web.config"));
            StringAssert.Contains(config, "key=\"Payment.QrImagePath\" value=\"~/Content/payment/qr.png\"");
            Assert.AreEqual(1, config.Split(new[] { "key=\"Payment.QrImagePath\"" }, StringSplitOptions.None).Length - 1);
            Assert.AreEqual("configured+file found", ApplicantQrAvailability.DiagnosticStatus("~/Content/payment/qr.png", project, File.Exists));

            var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            using (var stream = new MemoryStream(png)) Assert.AreEqual("image/png", PaymentQrImageType.Detect(".png", stream));
            using (var stream = new MemoryStream(png)) Assert.IsNull(PaymentQrImageType.Detect(".txt", stream));
            Assert.AreEqual("not configured", ApplicantQrAvailability.DiagnosticStatus("", project, File.Exists));
            Assert.AreEqual("configured+file missing", ApplicantQrAvailability.DiagnosticStatus("~/Content/payment/missing.png", project, File.Exists));

            var tempRoot = Path.Combine(Path.GetTempPath(), "smc-qr-" + Guid.NewGuid().ToString("N"));
            var tempVirtual = Path.Combine(tempRoot, "Content", "payment", "qr.png");
            var tempPhysical = Path.Combine(tempRoot, "physical.png");
            var unsupported = Path.Combine(tempRoot, "qr.config");
            Directory.CreateDirectory(Path.GetDirectoryName(tempVirtual));
            var validPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/nGkAAAAASUVORK5CYII=");
            try
            {
                File.WriteAllBytes(tempVirtual, validPng); File.WriteAllBytes(tempPhysical, validPng); File.WriteAllBytes(unsupported, validPng);
                var virtualResponse = PaymentQrAsset.Load("~/Content/payment/qr.png", tempRoot, File.Exists, File.OpenRead);
                Assert.AreEqual(200, virtualResponse.StatusCode); Assert.AreEqual("image/png", virtualResponse.ContentType); CollectionAssert.AreEqual(validPng, virtualResponse.Content);
                var physicalResponse = PaymentQrAsset.Load(tempPhysical, tempRoot, File.Exists, File.OpenRead);
                Assert.AreEqual(200, physicalResponse.StatusCode); Assert.AreEqual("image/png", physicalResponse.ContentType);
                Assert.AreEqual(404, PaymentQrAsset.Load("", tempRoot, File.Exists, File.OpenRead).StatusCode);
                Assert.AreEqual(404, PaymentQrAsset.Load("~/Content/payment/missing.png", tempRoot, File.Exists, File.OpenRead).StatusCode);
                Assert.AreEqual(404, PaymentQrAsset.Load(unsupported, tempRoot, File.Exists, File.OpenRead).StatusCode);
                Assert.AreEqual("configured+file found", ApplicantQrAvailability.DiagnosticStatus("~/Content/payment/qr.png", tempRoot, File.Exists));
                Assert.AreEqual("configured+file missing", ApplicantQrAvailability.DiagnosticStatus("~/Content/payment/missing.png", tempRoot, File.Exists));
            }
            finally { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); }

            var health = File.ReadAllText(Path.Combine(project, "Api", "MoreControllers.cs"));
            StringAssert.Contains(health, "User.IsInRole(Roles.Admin)");
            StringAssert.Contains(health, "paymentQr = paymentQr");
            var handler = File.ReadAllText(Path.Combine(project, "PaymentQr.ashx.cs"));
            StringAssert.Contains(handler, "SetNoStore"); StringAssert.Contains(handler, "PaymentQrAsset.Load");
            StringAssert.Contains(handler, "Request.QueryString[\"download\"]");
        }

        [TestMethod]
        public void ApplicantDateFormatter_UsesConfiguredDigitStyleForDateAndTime()
        {
            var date = new DateTime(2026, 10, 6, 13, 5, 0);
            Assert.AreEqual("06/10/2026 13:05", ApplicantDateFormatter.Format(date, "dd/MM/yyyy HH:mm", false));
            Assert.AreEqual("\u0966\u096c/\u0967\u0966/\u0968\u0966\u0968\u096c \u0967\u0969:\u0966\u096b", ApplicantDateFormatter.Format(date, "dd/MM/yyyy HH:mm", true));
        }

        [TestMethod]
        public void ServiceDisplay_UsesExistingServiceAndBusinessMasterNamesAndSafeFallbacks()
        {
            var services = new System.Collections.Generic.Dictionary<string, ServiceItem>(StringComparer.OrdinalIgnoreCase)
            {
                { "MiniGaala", new ServiceItem { Code = "MiniGaala", Name = "Mini Gaala", NameMr = "\u092e\u093f\u0928\u0940 \u0917\u093e\u0933\u0947" } },
                { "MajorGaala", new ServiceItem { Code = "MajorGaala", Name = "Major Gaala", NameMr = "\u092e\u0947\u091c\u0930 \u0917\u093e\u0933\u0947" } },
                { "VariousBusinessSpace", new ServiceItem { Code = "VariousBusinessSpace", Name = "Various business space", NameMr = "\u0935\u093f\u0935\u093f\u0927 \u0935\u094d\u092f\u0935\u0938\u093e\u092f\u093e\u0938\u093e\u0920\u0940 \u091c\u093e\u0917\u093e \u092e\u093e\u0917\u0923\u0940" } }
            };
            var types = new System.Collections.Generic.Dictionary<string, BusinessTypeItem>(StringComparer.OrdinalIgnoreCase)
            {
                { "GanpatiIdolStall", new BusinessTypeItem { Code = "GanpatiIdolStall", Name = "Ganesh idol Stall", NameMr = "\u0917\u0923\u092a\u0924\u0940 \u092e\u0942\u0930\u094d\u0924\u0940 \u0938\u094d\u091f\u0949\u0932" } },
                { "DiwaliFaralStall", new BusinessTypeItem { Code = "DiwaliFaralStall", Name = "Diwali Faral Stall", NameMr = "\u0926\u093f\u0935\u093e\u0933\u0940 \u092b\u0930\u093e\u0933 \u0938\u094d\u091f\u0949\u0932" } },
                { "DiwaliFirecrackerStall", new BusinessTypeItem { Code = "DiwaliFirecrackerStall", Name = "Diwali Firecracker Stall", NameMr = "\u0926\u093f\u0935\u093e\u0933\u0940 \u092b\u091f\u093e\u0915\u0947 \u0938\u094d\u091f\u0949\u0932" } },
                { "Other", new BusinessTypeItem { Code = "Other", Name = "Other", NameMr = "Other" } }
            };
            Assert.AreEqual("\u092e\u093f\u0928\u0940 \u0917\u093e\u0933\u0947", ServiceDisplay.Resolve("MiniGaala", null, null, services, types).ServiceName);
            Assert.AreEqual("\u092e\u0947\u091c\u0930 \u0917\u093e\u0933\u0947", ServiceDisplay.Resolve("MajorGaala", null, null, services, types).ServiceName);
            Assert.AreEqual("\u0917\u0923\u092a\u0924\u0940 \u092e\u0942\u0930\u094d\u0924\u0940 \u0938\u094d\u091f\u0949\u0932", ServiceDisplay.Resolve("VariousBusinessSpace", "GanpatiIdolStall", null, services, types).SubServiceName);
            Assert.AreEqual("\u0926\u093f\u0935\u093e\u0933\u0940 \u092b\u0930\u093e\u0933 \u0938\u094d\u091f\u0949\u0932", ServiceDisplay.Resolve("VariousBusinessSpace", "DiwaliFaralStall", null, services, types).SubServiceName);
            Assert.AreEqual("\u0926\u093f\u0935\u093e\u0933\u0940 \u092b\u091f\u093e\u0915\u0947 \u0938\u094d\u091f\u0949\u0932", ServiceDisplay.Resolve("VariousBusinessSpace", "DiwaliFirecrackerStall", null, services, types).SubServiceName);
            Assert.AreEqual("custom activity", ServiceDisplay.Resolve("VariousBusinessSpace", "Other", " custom activity ", services, types).SubServiceName);
            Assert.AreEqual("UnknownCode", ServiceDisplay.Resolve("UnknownCode", null, null, services, types).ServiceName);
            Assert.IsNull(ServiceDisplay.Resolve("VariousBusinessSpace", null, null, services, types).SubServiceName);
            var detail = Detail(ZoneStages.JEPending); detail.ServiceType = "VariousBusinessSpace"; detail.BusinessType = "GanpatiIdolStall";
            var applicantView = ApplicantStatusViewMapper.Map(detail, services, types);
            Assert.AreEqual("\u0917\u0923\u092a\u0924\u0940 \u092e\u0942\u0930\u094d\u0924\u0940 \u0938\u094d\u091f\u0949\u0932", applicantView.SubServiceName);
            Assert.AreEqual(applicantView.SubServiceName, applicantView.BusinessType);
        }

        [TestMethod]
        public void ApplicantStatusAndPdfSource_UseSubServiceQrStateAndMarathiDateFixes()
        {
            Assert.IsTrue(typeof(CitizenStatusView).GetProperty("SubServiceName") != null);
            var page = File.ReadAllText(ProjectFile("SMC.BhoomiMalmatta.Web", Path.Combine("Citizen", "Dashboard.aspx.cs")));
            StringAssert.Contains(page, "pnlPayment.Visible && (d.PaymentStatus == PaymentStatuses.PaymentRequired || d.PaymentStatus == PaymentStatuses.PaymentRejected)");
            StringAssert.Contains(page, "litPaymentQr.Text = qr.ToString()"); StringAssert.Contains(page, "ApplicantQrAvailability.IsAvailable");
            // The page builds the attribute inside a C# string literal, so the source text contains the escaped quote (onerror=\"...).
            StringAssert.Contains(page, "onerror=\\\"this.style.display='none';this.nextElementSibling.hidden=false;");
            StringAssert.Contains(page, "PaymentQrUnavailable"); StringAssert.Contains(page, "PaymentInstructionReference");
            StringAssert.Contains(page, "ApplicantDateFormatter.Format(d.StartDate"); StringAssert.Contains(page, "ApplicantDateFormatter.Format(d.PaymentDate.Value");
            Assert.IsFalse(page.Contains("ApplicantText.CurrentStage +"));
            var pdf = File.ReadAllText(ProjectFile("SMC.BhoomiMalmatta.Web", Path.Combine("Services", "PdfGenerator.cs")));
            StringAssert.Contains(pdf, "ServiceDisplay.Get(d.ServiceType, d.BusinessType, d.OtherBusinessType)");
            StringAssert.Contains(pdf, "Row(c, \"उप-सेवा\", serviceDisplay.SubServiceName)");
            var grid = File.ReadAllText(ProjectFile("SMC.BhoomiMalmatta.Web", Path.Combine("Department", "Applications.aspx.cs")));
            StringAssert.Contains(grid, "ServiceDisplay.Get(item.ServiceType, item.BusinessType, item.OtherBusinessType)");
        }

        [TestMethod]
        public void NumberOnlyLookupAndApplicantTicketPolicies_AreEnforced()
        {
            Assert.AreEqual("20260042", ApplicantNumberRules.Normalize(" 20260042 "));
            Assert.AreEqual("AB12cd", ApplicantNumberRules.Normalize(" AB12cd "));
            Assert.IsNull(ApplicantNumberRules.Normalize("2026-0042")); Assert.IsNull(ApplicantNumberRules.Normalize("2026 0042"));
            Assert.AreEqual(1, typeof(StatusLookupRequest).GetProperties().Length);
            Assert.AreEqual("ApplicationNumber", typeof(StatusLookupRequest).GetProperties().Single().Name);
            Assert.IsTrue(CitizenTicketAccess.Allows(42, 42)); Assert.IsFalse(CitizenTicketAccess.Allows(42, 43)); Assert.IsFalse(CitizenTicketAccess.Allows(0, 42));
            Assert.AreEqual("******1230", CitizenStatusMapper.MaskMobile("9876541230"));
            Assert.AreEqual("ap***@example.org", CitizenStatusMapper.MaskEmail("applicant@example.org"));

            var statusFile = ProjectFile("SMC.BhoomiMalmatta.Web", "Status.aspx");
            var statusMarkup = File.ReadAllText(statusFile);
            StringAssert.Contains(statusMarkup, "\u0938\u094d\u0925\u093f\u0924\u0940 \u092a\u0939\u093e");
            StringAssert.Contains(statusMarkup, "\u0905\u0930\u094d\u091c \u0915\u094d\u0930\u092e\u093e\u0902\u0915 \u0935\u093e\u092a\u0930\u0942\u0928 \u0938\u094d\u0925\u093f\u0924\u0940 \u092a\u0939\u093e.");
            var statusPageSource = File.ReadAllText(ProjectFile("SMC.BhoomiMalmatta.Web", "Status.aspx.cs"));
            StringAssert.Contains(statusPageSource, "FailureDelayMilliseconds = 200");
            StringAssert.Contains(statusPageSource, "LookupLimit = 30");
            StringAssert.Contains(statusPageSource, @"ErrorLogger.Log(CorrelationId, ""ApplicantStatusLookup"", ex)");
            Assert.IsFalse(statusPageSource.Contains("ErrorLogger.Log(number"));
            var apiSource = File.ReadAllText(ProjectFile("SMC.BhoomiMalmatta.Web", Path.Combine("Api", "CoreControllers.cs")));
            StringAssert.Contains(apiSource, "कृपया क्रमांक तपासा."); StringAssert.Contains(apiSource, "api-status:");
            Assert.IsFalse(statusMarkup.Contains("txtAccessCode")); Assert.IsFalse(statusMarkup.Contains("\u092a\u094d\u0930\u0935\u0947\u0936 \u0915\u094b\u0921"));
            var applyMarkup = File.ReadAllText(ProjectFile("SMC.BhoomiMalmatta.Web", "Apply.aspx"));
            StringAssert.Contains(applyMarkup, "\u0906\u092a\u0932\u093e \u0905\u0930\u094d\u091c \u0915\u094d\u0930\u092e\u093e\u0902\u0915 \u091c\u0924\u0928 \u0915\u0930\u093e"); Assert.IsFalse(applyMarkup.Contains("\u092a\u094d\u0930\u0935\u0947\u0936 \u0915\u094b\u0921"));
            var pdfSource = File.ReadAllText(ProjectFile("SMC.BhoomiMalmatta.Web", Path.Combine("Services", "PdfGenerator.cs")));
            StringAssert.Contains(pdfSource, "\u0905\u0930\u094d\u091c\u093e\u091a\u0940 \u0938\u094d\u0925\u093f\u0924\u0940 \u0924\u092a\u093e\u0938\u0923\u094d\u092f\u093e\u0938\u093e\u0920\u0940 \u0905\u0930\u094d\u091c \u0915\u094d\u0930\u092e\u093e\u0902\u0915 \u0935\u093e\u092a\u0930\u093e."); Assert.IsFalse(pdfSource.Contains("\u092a\u094d\u0930\u0935\u0947\u0936 \u0915\u094b\u0921"));

            var limiter = new RateLimiter();
            for (var i = 0; i < 30; i++) Assert.IsTrue(limiter.Allow("status-ip", 30, TimeSpan.FromMinutes(10)));
            Assert.IsFalse(limiter.Allow("status-ip", 30, TimeSpan.FromMinutes(10)));

            AssertCitizenAuthorize(typeof(PaymentsController).GetMethod("Submit"));
            AssertCitizenAuthorize(typeof(WorkflowController).GetMethod("Resubmit"));
            var status = typeof(ApplicationsController).GetMethods().Single(m => m.Name == "Status" && m.GetParameters().Length == 1);
            Assert.IsNotNull(status); Assert.IsTrue(status.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Any());
            AssertCitizenAuthorize(typeof(ApplicationsController).GetMethod("Get", new[] { typeof(int) }));
            var dashboardMarkup = File.ReadAllText(ProjectFile("SMC.BhoomiMalmatta.Web", Path.Combine("Citizen", "Dashboard.aspx.cs")));
            Assert.IsFalse(dashboardMarkup.Contains("api/v1/applications/"));
            StringAssert.Contains(dashboardMarkup, "Download.ashx?type=");
        }

        private static void AssertCitizenAuthorize(MethodInfo method)
        {
            Assert.IsNotNull(method);
            var authorize = method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().SingleOrDefault();
            Assert.IsNotNull(authorize); StringAssert.Contains(authorize.Roles, "Citizen");
        }

        private static string ProjectFile(string project, string relative)
        {
            var root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", ".."));
            return Path.Combine(root, project, relative);
        }
    }
}


