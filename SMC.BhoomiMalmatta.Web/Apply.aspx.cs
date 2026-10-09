using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.SessionState;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Business.Security;
using SMC.BhoomiMalmatta.Business.Pricing;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Web.Security;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Web
{
    /// <summary>
    /// Three-step demand application (सेवेची माहिती → पत्ता व जागा → कागदपत्रे व सादरीकरण; see <see cref="ApplyWizard"/>). Every step is validated
    /// on the server with the same SubmissionValidator used by the API; the applicant's name/mobile/e-mail are asked at the top of step 2 only when
    /// Apply.CollectApplicantDetails is true (otherwise a separate form supplies them); zone and location are part of step 2;
    /// State and District are fixed by the server, pricing is recomputed by the server, files are validated by signature.
    /// </summary>
    public partial class ApplyPage : SmcPage
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string UploadOwnerSessionKey = "Apply.UploadOwner";
        private const string UploadManifestSessionKey = "Apply.UploadManifest";

        private sealed class StagedDemandAttachment
        {
            public string OwnerKey { get; set; }
            public string RelativePath { get; set; }
            public string FileName { get; set; }
            public string ContentType { get; set; }
            public long SizeBytes { get; set; }
        }

        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            // Apply.CollectApplicantDetails: decided before post data is loaded, so hidden inputs/validators never take part.
            phApplicant.Visible = AppServices.CollectApplicantDetails;
            // Rebuild these master-backed lists before Web Forms loads posted values, so the
            // selected service and business type survive every wizard postback reliably.
            ddlService.Items.Clear();
            ddlService.Items.Add(new ListItem("— सेवा निवडा —", string.Empty));
            foreach (var s in AppServices.Masters.GetServices()) ddlService.Items.Add(new ListItem(s.NameMr, s.Code));
            ddlBusiness.Items.Clear();
            ddlBusiness.Items.Add(new ListItem("— व्यवसाय प्रकार निवडा —", string.Empty));
            foreach (var b in AppServices.Masters.GetBusinessTypes()) ddlBusiness.Items.Add(new ListItem(b.NameMr, b.Code));
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack) { UpdateBusinessControls(); UpdatePricingControls(false); return; }
            ddlZone.Items.Add(new ListItem("— झोन निवडा —", string.Empty));
            for (var z = 1; z <= 8; z++) ddlZone.Items.Add(new ListItem("झोन " + z, z.ToString()));
            ddlTaluka.Items.Add(new ListItem("-- निवडा --", string.Empty));
            foreach (var t in Talukas) ddlTaluka.Items.Add(new ListItem(t, t));
            UpdateBusinessControls();
            UpdatePricingControls(false);
            if (string.Equals(Request.QueryString["completed"], "1", StringComparison.Ordinal) && ShowCompletedApplication()) return;
            ShowStep(0);
        }

        protected override void OnPreRender(EventArgs e)
        {
            UpdateBusinessControls();
            UpdatePricingControls(false);
            UpdateStagedAttachmentControls();
            hidPricingService.Value = ddlService.SelectedValue;
            hidPricingServiceName.Value = ddlService.SelectedItem == null ? string.Empty : ddlService.SelectedItem.Text;
            base.OnPreRender(e);
        }

        private static readonly string[] Talukas = { "मध्य सोलापूर", "दक्षिण सोलापूर", "उत्तर सोलापूर" };

        private int Step { get { return mv.ActiveViewIndex; } }

        private void ShowStep(int i)
        {
            mv.ActiveViewIndex = i;
            var steps = new[] { st1, st2, st3 };
            for (var j = 0; j < steps.Length; j++) steps[j].Attributes["class"] = "st" + (j == i ? " on" : string.Empty) + (j < i ? " done" : string.Empty);
            barFill.Style["width"] = ApplyWizard.ProgressWidth(i);
            btnBack.Visible = i > 0; btnNext.Visible = i < ApplyWizard.DocumentsStep; btnSubmit.Visible = i == ApplyWizard.DocumentsStep;
            btnNext.ValidationGroup = ApplyWizard.ValidationGroup(i);
            lblErrors.Visible = false;
            // Move focus to the first field of the new step (not on the very first render of the page).
            if (IsPostBack) SetFocus(i == ApplyWizard.ServiceStep ? (Control)ddlService : i == ApplyWizard.AddressStep ? (phApplicant.Visible ? (Control)txtName : txtPermAddr) : fuIdentity);
        }

        private void UpdateBusinessControls()
        {
            var needsBusinessType = string.Equals(ddlService.SelectedValue, "VariousBusinessSpace", StringComparison.Ordinal);
            // Keep the panel in the HTML so the service selector can reveal it immediately;
            // applying the same state again here also covers every Web Forms postback path.
            pnlBusiness.Visible = true;
            if (needsBusinessType) pnlBusiness.Style.Remove("display");
            else pnlBusiness.Style["display"] = "none";
            rfvBusiness.Enabled = needsBusinessType;
            pnlOtherBusiness.Visible = needsBusinessType && string.Equals(ddlBusiness.SelectedValue, "Other", StringComparison.Ordinal);
        }

        private void UpdatePricingControls(bool serviceChanged)
        {
            var code = ddlService.SelectedValue;
            var areaBased = DemandServicePricingRules.IsAreaBased(code);
            var hasService = !string.IsNullOrWhiteSpace(code);
            pnlAreaPricing.Visible = areaBased;
            pnlPricingPreview.Visible = areaBased;
            pnlAnnualSummary.Visible = hasService && !areaBased;
            rfvEnd.Enabled = areaBased;
            txtEnd.ReadOnly = !areaBased;
            lblEndCaption.Text = areaBased ? "समाप्ती तारीख *" : "समाप्ती तारीख";

            if (serviceChanged)
            {
                txtLength.Text = string.Empty;
                txtWidth.Text = string.Empty;
                txtEnd.Text = string.Empty;
            }
            if (!areaBased)
            {
                var start = Day(txtStart.Text);
                txtEnd.Text = start.HasValue ? start.Value.AddYears(1).ToString("yyyy-MM-dd", Inv) : string.Empty;
            }

            annualService.InnerText = ddlService.SelectedItem == null ? string.Empty : ddlService.SelectedItem.Text;
            annualStart.InnerText = Day(txtStart.Text).HasValue ? Day(txtStart.Text).Value.ToString("dd/MM/yyyy", Inv) : "—";
            annualEnd.InnerText = Day(txtEnd.Text).HasValue ? Day(txtEnd.Text).Value.ToString("dd/MM/yyyy", Inv) : "—";
            var amount = DemandServicePricingRules.FixedAnnualAmount(code);
            annualAmount.InnerText = amount.HasValue ? "₹ " + amount.Value.ToString("N2", Inv) : "—";
        }

        protected void ddlService_SelectedIndexChanged(object sender, EventArgs e) { UpdateBusinessControls(); UpdatePricingControls(true); }
        protected void ddlBusiness_SelectedIndexChanged(object sender, EventArgs e) { UpdateBusinessControls(); }
        protected void cvDeclaration_ServerValidate(object source, ServerValidateEventArgs args) { args.IsValid = chkDeclaration.Checked; }
        protected void btnBack_Click(object sender, EventArgs e) { ShowStep(Math.Max(0, Step - 1)); }

        protected void btnSaveIdentity_Click(object sender, EventArgs e) { StageSelectedAttachments(); }
        protected void btnSaveAddress_Click(object sender, EventArgs e) { StageSelectedAttachments(); }
        protected void btnRemoveIdentity_Click(object sender, EventArgs e) { RemoveStagedAttachment(DocumentTypes.IdentityProof); }
        protected void btnRemoveAddress_Click(object sender, EventArgs e) { RemoveStagedAttachment(DocumentTypes.AddressProof); }

        private string GetUploadOwnerKey(bool create)
        {
            var key = Session[UploadOwnerSessionKey] as string;
            Guid parsed;
            if (!Guid.TryParseExact(key, "N", out parsed))
            {
                if (!create) return null;
                key = Guid.NewGuid().ToString("N");
                Session[UploadOwnerSessionKey] = key;
            }
            return key;
        }

        private Dictionary<string, StagedDemandAttachment> GetStagedAttachments(bool create)
        {
            var files = Session[UploadManifestSessionKey] as Dictionary<string, StagedDemandAttachment>;
            if (files == null && create)
            {
                files = new Dictionary<string, StagedDemandAttachment>(StringComparer.Ordinal);
                Session[UploadManifestSessionKey] = files;
            }
            return files;
        }

        private void StageSelectedAttachments()
        {
            var problems = new List<FieldError>();
            var hasAny = fuIdentity.HasFile || fuAddress.HasFile;
            if (fuIdentity.HasFile) StageAttachment(fuIdentity, DocumentTypes.IdentityProof, problems);
            if (fuAddress.HasFile) StageAttachment(fuAddress, DocumentTypes.AddressProof, problems);
            if (!hasAny) problems.Add(new FieldError("Documents", "ओळखपत्र किंवा पत्त्याचा पुरावा फाईल निवडा."));
            if (problems.Count > 0) ShowErrors(problems);
            else lblErrors.Visible = false;
        }

        /// <summary>The existing validation (ApplyDocumentSelection.Check) for one upload box.</summary>
        private static SelectedDocument CheckSelected(FileUpload control, string documentType)
        {
            return ApplyDocumentSelection.Check(documentType, control.FileName, control.PostedFile.ContentType, control.PostedFile.ContentLength, () => control.FileBytes);
        }

        private void StageAttachment(FileUpload control, string documentType, List<FieldError> problems)
        {
            var selected = CheckSelected(control, documentType);
            if (!selected.IsValid) { problems.Add(new FieldError("Documents", selected.Problem)); return; }
            SaveStaged(selected, problems);
        }

        /// <summary>
        /// "अर्ज सादर करा" without "जोडा / बदला": stages every file currently selected (same validation as the buttons; a selected file replaces the
        /// staged file of its type). Returns validation and save problems plus "required" messages for types with nothing staged and nothing selected.
        /// Files staged here stay staged if a later step of the submission fails.
        /// </summary>
        private List<FieldError> StageSelectedAtSubmit()
        {
            var selected = new List<SelectedDocument>();
            if (fuIdentity.HasFile) selected.Add(CheckSelected(fuIdentity, DocumentTypes.IdentityProof));
            if (fuAddress.HasFile) selected.Add(CheckSelected(fuAddress, DocumentTypes.AddressProof));
            var plan = ApplyDocumentSelection.Plan(StagedTypes(), selected);
            var problems = new List<FieldError>();
            foreach (var file in plan.ToStage) SaveStaged(file, problems);
            problems.AddRange(plan.Messages);
            return problems;
        }

        private void SaveStaged(SelectedDocument selected, List<FieldError> problems)
        {
            var bytes = selected.Content;
            var checkedFile = selected.File;
            var documentType = selected.DocumentType;
            var owner = GetUploadOwnerKey(true);
            var files = GetStagedAttachments(true);
            StagedDemandAttachment previous;
            files.TryGetValue(documentType, out previous);
            var relativePath = "wizard/demand/" + owner + "/" + Guid.NewGuid().ToString("N") + ".upload";
            try
            {
                AppServices.Files.Save(relativePath, bytes);
                files[documentType] = new StagedDemandAttachment
                {
                    OwnerKey = owner, RelativePath = relativePath, FileName = checkedFile.SafeDisplayName,
                    ContentType = checkedFile.ContentType, SizeBytes = bytes.LongLength
                };
                Session[UploadManifestSessionKey] = files;
                if (previous != null && string.Equals(previous.OwnerKey, owner, StringComparison.Ordinal)) DeleteStagedFile(previous);
            }
            catch (Exception)
            {
                try { AppServices.Files.Delete(relativePath); } catch (Exception) { }
                problems.Add(new FieldError("Documents", ApplyDocumentSelection.Label(documentType) + " जतन करता आली नाही. पुन्हा प्रयत्न करा."));
            }
        }

        private void RemoveStagedAttachment(string documentType)
        {
            var owner = GetUploadOwnerKey(false);
            var files = GetStagedAttachments(false);
            StagedDemandAttachment existing;
            if (files == null || !files.TryGetValue(documentType, out existing)) return;
            if (string.IsNullOrEmpty(owner) || !string.Equals(existing.OwnerKey, owner, StringComparison.Ordinal)) return;
            DeleteStagedFile(existing);
            files.Remove(documentType);
            Session[UploadManifestSessionKey] = files;
            lblErrors.Visible = false;
        }

        private void UpdateStagedAttachmentControls()
        {
            var owner = GetUploadOwnerKey(false);
            var files = GetStagedAttachments(false);
            StagedDemandAttachment identity, address;
            if (files == null) files = new Dictionary<string, StagedDemandAttachment>(StringComparer.Ordinal);
            files.TryGetValue(DocumentTypes.IdentityProof, out identity);
            files.TryGetValue(DocumentTypes.AddressProof, out address);
            if (identity != null && !string.Equals(identity.OwnerKey, owner, StringComparison.Ordinal)) identity = null;
            if (address != null && !string.Equals(address.OwnerKey, owner, StringComparison.Ordinal)) address = null;
            lblIdentityFileName.Text = identity == null ? string.Empty : "जोडलेली फाईल: " + Enc(identity.FileName);
            lblAddressFileName.Text = address == null ? string.Empty : "जोडलेली फाईल: " + Enc(address.FileName);
            btnRemoveIdentity.Visible = identity != null;
            btnRemoveAddress.Visible = address != null;
        }

        /// <summary>A staged file counts only for the upload owner of this session and only inside its own staging folder.</summary>
        private static bool IsUsableStaged(StagedDemandAttachment staged, string owner)
        {
            return staged != null && !string.IsNullOrEmpty(owner) && string.Equals(staged.OwnerKey, owner, StringComparison.Ordinal)
                && !string.IsNullOrEmpty(staged.RelativePath) && staged.RelativePath.StartsWith("wizard/demand/" + owner + "/", StringComparison.Ordinal);
        }

        private List<string> StagedTypes()
        {
            var owner = GetUploadOwnerKey(false);
            var files = GetStagedAttachments(false);
            return files == null ? new List<string>() : files.Where(x => IsUsableStaged(x.Value, owner)).Select(x => x.Key).ToList();
        }

        private List<DocumentUpload> GetStagedDocumentUploads(List<FieldError> problems)
        {
            var uploads = new List<DocumentUpload>();
            var owner = GetUploadOwnerKey(false);
            var files = GetStagedAttachments(false);
            foreach (var item in ApplyDocumentSelection.Required.Select(x => new { Type = x.Key, Label = x.Value }))
            {
                StagedDemandAttachment staged;
                if (files == null || !files.TryGetValue(item.Type, out staged) || !IsUsableStaged(staged, owner))
                { problems.Add(new FieldError("Documents", ApplyDocumentSelection.RequiredMessage(item.Label))); continue; }
                try
                {
                    var bytes = AppServices.Files.Read(staged.RelativePath);
                    if (bytes.LongLength != staged.SizeBytes) { problems.Add(new FieldError("Documents", item.Label + " फाईल उपलब्ध नाही. पुन्हा जोडा.")); continue; }
                    uploads.Add(new DocumentUpload { DocumentType = item.Type, FileName = staged.FileName, ContentType = staged.ContentType, Content = bytes });
                }
                catch (Exception) { problems.Add(new FieldError("Documents", item.Label + " फाईल उपलब्ध नाही. पुन्हा जोडा.")); }
            }
            return uploads;
        }

        private static void DeleteStagedFile(StagedDemandAttachment staged)
        {
            if (staged == null || string.IsNullOrWhiteSpace(staged.RelativePath) || AppServices.Files == null) return;
            try { AppServices.Files.Delete(staged.RelativePath); } catch (Exception) { }
        }

        internal static void ClearStagedAttachments(HttpSessionState session)
        {
            if (session == null) return;
            var files = session[UploadManifestSessionKey] as Dictionary<string, StagedDemandAttachment>;
            if (files != null) foreach (var file in files.Values) DeleteStagedFile(file);
            session.Remove(UploadManifestSessionKey);
            session.Remove(UploadOwnerSessionKey);
        }

        private static decimal? Dec(string s)
        {
            decimal v;
            return decimal.TryParse((s ?? string.Empty).Trim(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, Inv, out v) ? (decimal?)v : null;
        }

        private static DateTime? Day(string s)
        {
            DateTime d;
            return DateTime.TryParseExact((s ?? string.Empty).Trim(), "yyyy-MM-dd", Inv, DateTimeStyles.None, out d) ? (DateTime?)d : null;
        }

        /// <summary>Collects the form. Locked fields (State, District, City) are not read from the browser.</summary>
        private SubmissionData Collect(List<FieldError> parseErrors)
        {
            var areaBased = DemandServicePricingRules.IsAreaBased(ddlService.SelectedValue);
            var start = Day(txtStart.Text); var selectedEnd = Day(txtEnd.Text);
            var end = areaBased ? selectedEnd : (start.HasValue ? (DateTime?)start.Value.AddYears(1) : null);
            if (Step >= ApplyWizard.AddressStep || !string.IsNullOrWhiteSpace(txtStart.Text))
            {
                if (!start.HasValue) parseErrors.Add(new FieldError("StartDate", "सुरुवातीचा दिनांक वैध नाही."));
                if (areaBased && !end.HasValue) parseErrors.Add(new FieldError("EndDate", "समाप्तीचा दिनांक वैध नाही."));
            }
            var details = new List<string>();
            if (areaBased && ddlBusiness.SelectedItem != null && !string.IsNullOrEmpty(ddlBusiness.SelectedValue))
            {
                details.Add("व्यवसायाचा प्रकार: " + ddlBusiness.SelectedItem.Text);
                if (ddlBusiness.SelectedValue == "Other" && !string.IsNullOrWhiteSpace(txtOtherBusiness.Text))
                    details.Add("इतर व्यवसायाचा तपशील: " + txtOtherBusiness.Text.Trim());
            }
            if (!string.IsNullOrWhiteSpace(txtStall.Text)) details.Add("स्टॉल/जागेची आवश्यकता: " + txtStall.Text.Trim());
            if (!string.IsNullOrWhiteSpace(txtDescription.Text)) details.Add("इतर आवश्यक माहिती: " + txtDescription.Text.Trim());
            return new SubmissionData
            {
                ServiceType = ddlService.SelectedValue, BusinessType = areaBased ? ddlBusiness.SelectedValue : null,
                // Applicant type, Aadhaar/identity, PAN and GST are no longer asked: schema-safe values (ApplicantType = existing default,
                // nullable IdentityNumber/PanNumber = NULL, NOT NULL GstNumber is stored as "" by the store).
                OtherBusinessType = txtOtherBusiness.Text, ApplicantType = ApplyWizard.DefaultApplicantType, ApplicantName = phApplicant.Visible ? txtName.Text : null, Mobile = phApplicant.Visible ? txtMobile.Text : null, Email = phApplicant.Visible ? txtEmail.Text : null,
                GstNumber = null, IdentityNumber = null, PanNumber = null,
                PermanentAddress = txtPermAddr.Text, CorrespondenceAddress = chkSame.Checked ? txtPermAddr.Text : txtCorrAddr.Text, SameAddress = chkSame.Checked,
                State = SubmissionValidator.LockedState, District = SubmissionValidator.LockedDistrict, City = SubmissionValidator.LockedCity, Taluka = ddlTaluka.SelectedValue, PinCode = txtPin.Text,
                Zone = ddlZone.SelectedValue, Prabhag = null, Location = txtLocation.Text, ServiceDescription = string.Join(Environment.NewLine, details),
                StartDate = start ?? DateTime.Today, EndDate = end ?? (start ?? DateTime.Today), LengthFt = areaBased ? Dec(txtLength.Text) : null, WidthFt = areaBased ? Dec(txtWidth.Text) : null,
                DeclarationAccepted = chkDeclaration.Checked, UploadedDocumentTypes = new List<string>(), ClientClaimedAmount = null
            };
        }

        private static int StepOf(string field) { return ApplyWizard.StepOf(field); }

        /// <summary>Errors that block "Next" on the current step (server rules, date parsing and the pricing preview).</summary>
        private List<FieldError> NextErrors(SubmissionData data, List<FieldError> parseErrors)
        {
            // Everything except the final-step requirements (declaration, documents), as before; filtered to this step at the end.
            var errors = ApplyWizard.BlockingErrors(data, ApplyWizard.DocumentsStep, AppServices.Masters.GetCatalog(), AppServices.CollectApplicantDetails);
            errors.AddRange(parseErrors);
            if (errors.All(x => x.Field != "ServiceType" && x.Field != "StartDate" && x.Field != "EndDate"))
            {
                var p = AppServices.Submission.Preview(SubmissionValidator.Normalize(data));
                if (p.IsFailure) errors.Add(new FieldError("Pricing", p.Error.Message));
            }
            return errors.Where(x => StepOf(x.Field) <= Step).ToList();
        }

        protected void btnNext_Click(object sender, EventArgs e)
        {
            Page.Validate(ApplyWizard.ValidationGroup(Step));
            if (!Page.IsValid) return;
            var parse = new List<FieldError>();
            var data = Collect(parse);
            var current = NextErrors(data, parse);
            if (current.Count > 0) { ShowErrors(current); return; }
            if (Step == ApplyWizard.AddressStep) litReview.Text = BuildReview(SubmissionValidator.Normalize(data));
            ShowStep(Step + 1);
        }

        private string BuildReview(SubmissionData d)
        {
            var sb = new StringBuilder();
            Action<string> section = title => sb.Append("<section class=\"review-section\"><h4>").Append(Enc(title)).Append("</h4><div class=\"review-fields\">");
            Action<string, string, string> field = (k, v, css) => sb.Append("<div class=\"review-field").Append(string.IsNullOrEmpty(css) ? string.Empty : " " + css).Append("\"><span class=\"review-label\">").Append(Enc(k)).Append("</span><span class=\"review-value\">").Append(Enc(v)).Append("</span></div>");
            Action close = () => sb.Append("</div></section>");
            var svc = ddlService.SelectedItem == null ? d.ServiceType : ddlService.SelectedItem.Text;
            sb.Append("<div class=\"review-grid\">");
            section("सेवा माहिती");
            field("सेवा", svc, null);
            if (!string.IsNullOrEmpty(d.BusinessType)) field("व्यवसायाचा प्रकार", ddlBusiness.SelectedItem == null ? d.BusinessType : ddlBusiness.SelectedItem.Text, null);
            close();

            if (!string.IsNullOrWhiteSpace(d.ApplicantName) || !string.IsNullOrWhiteSpace(d.Mobile) || !string.IsNullOrWhiteSpace(d.Email))
            {
                section("अर्जदार");
                if (!string.IsNullOrWhiteSpace(d.ApplicantName)) field("पूर्ण नाव", d.ApplicantName, null);
                if (!string.IsNullOrWhiteSpace(d.Mobile)) field("मोबाईल", d.Mobile, null);
                if (!string.IsNullOrWhiteSpace(d.Email)) field("ई-मेल", d.Email, null);
                close();
            }

            section("पत्ता व जागा");
            field("पत्ता", d.PermanentAddress + ", " + d.City + ", " + d.Taluka + " - " + d.PinCode, "review-wide");
            if (!string.IsNullOrWhiteSpace(d.CorrespondenceAddress) && !string.Equals(d.CorrespondenceAddress, d.PermanentAddress, StringComparison.Ordinal))
                field("पत्रव्यवहाराचा पत्ता", d.CorrespondenceAddress, "review-wide");
            if (!string.IsNullOrWhiteSpace(d.Zone)) field("झोन", d.Zone, null);
            if (!string.IsNullOrWhiteSpace(d.Location)) field("जागेचे ठिकाण", d.Location, null);
            field("स्टॉल/जागेची आवश्यकता", txtStall.Text, null);
            var areaBased = DemandServicePricingRules.IsAreaBased(d.ServiceType);
            if (areaBased) { field("लांबी (ft)", txtLength.Text, null); field("रुंदी (ft)", txtWidth.Text, null); }
            if (!string.IsNullOrWhiteSpace(txtDescription.Text)) field("इतर आवश्यक माहिती", txtDescription.Text, "review-wide");
            close();

            section("कालावधी व शुल्क");
            var p = AppServices.Submission.Preview(d);
            field("प्रारंभ तारीख", d.StartDate.ToString("dd/MM/yyyy", Inv), null);
            field("समाप्ती तारीख", d.EndDate.ToString("dd/MM/yyyy", Inv), null);
            if (p.IsSuccess)
            {
                if (areaBased)
                {
                    field("एकूण दिवस", p.Value.TotalDays.ToString(Inv), null);
                    field("क्षेत्रफळ (चौ.फूट)", p.Value.AreaSqFt.ToString("N2", Inv), null);
                    field("दर (प्रतिदिन)", p.Value.DailyRate.HasValue ? "₹ " + p.Value.DailyRate.Value.ToString("N2", Inv) : "—", null);
                    field("एकूण रक्कम", p.Value.FinalAmount.HasValue ? "₹ " + p.Value.FinalAmount.Value.ToString("N2", Inv) : "—", "review-total");
                }
                else
                {
                    field("कालावधी", "१ वर्ष", null);
                    field("वार्षिक निश्चित रक्कम", p.Value.FinalAmount.HasValue ? "₹ " + p.Value.FinalAmount.Value.ToString("N2", Inv) : "—", "review-total");
                }
            }
            else
            {
                if (areaBased) { field("एकूण दिवस", "—", null); field("क्षेत्रफळ (चौ.फूट)", "—", null); field("दर (प्रतिदिन)", "—", null); }
                else field("कालावधी", "१ वर्ष", null);
                field(areaBased ? "एकूण रक्कम" : "वार्षिक निश्चित रक्कम", "—", "review-total");
            }
            close();
            sb.Append("</div>");
            return sb.ToString();
        }

        private void ShowErrors(IEnumerable<FieldError> errors)
        {
            lblErrors.Text = string.Join("<br />", errors.Select(x => Enc(x.Message)));
            lblErrors.Visible = true;
        }

        protected void btnSubmit_Click(object sender, EventArgs e)
        {
            Page.Validate(ApplyWizard.ValidationGroup(ApplyWizard.DocumentsStep));
            // Stage the files selected in this request first (a browser does not keep a chosen file after a postback),
            // so they survive any failure below and the "जोडलेली फाईल" line shows them.
            var selection = StageSelectedAtSubmit();
            if (!Page.IsValid) { if (selection.Count > 0) ShowErrors(selection); return; }
            if (selection.Count > 0) { ShowErrors(selection); return; }
            if (!AppServices.Limiter.Allow("submit:" + ClientIp, 10, TimeSpan.FromHours(1))) { ShowErrors(new[] { new FieldError("Rate", "खूप जास्त अर्ज प्रयत्न झाले. कृपया नंतर प्रयत्न करा.") }); return; }

            var problems = new List<FieldError>();
            var uploads = GetStagedDocumentUploads(problems);
            if (problems.Count > 0) { ShowErrors(problems); return; }

            var parse = new List<FieldError>();
            var data = Collect(parse);
            if (parse.Count > 0) { ShowErrors(parse); return; }
            var result = AppServices.Submission.Submit(data, uploads, ClientIp, CorrelationId, FileValidator.MaxApplicationAttachmentBytes);
            if (result.Item2 != null)
            {
                var first = result.Item2.Errors.Select(x => StepOf(x.Field)).DefaultIfEmpty(ApplyWizard.DocumentsStep).Min();
                ShowStep(first); ShowErrors(result.Item2.Errors);
                return;
            }
            var ok = result.Item1;
            ClearStagedAttachments(Session);
            AuthCookies.SignInCitizen(Context, ok.ApplicationId, ok.ApplicationNumber);
            Response.Redirect(ResolveUrl("~/Apply.aspx?completed=1"), false);
            Context.ApplicationInstance.CompleteRequest();
        }

        private bool ShowCompletedApplication()
        {
            var me = Me;
            if (me == null || !me.IsCitizen) return false;
            var saved = AppServices.Query.GetDetail(me.ApplicationId);
            if (saved == null || !string.Equals(saved.ApplicationNumber, me.ApplicationNumber, StringComparison.Ordinal)) return false;
            litNumber.Text = Enc(saved.ApplicationNumber);
            litAmountDone.Text = saved.FeeAmount.HasValue ? Enc("₹ " + saved.FeeAmount.Value.ToString("N2", Inv)) : "—";
            autoDownload.Attributes["data-href"] = ResolveUrl("~/Download.ashx?type=application");
            autoDownload.Attributes["data-application-id"] = saved.Id.ToString(Inv);
            pnlForm.Visible = false; pnlDone.Visible = true;
            return true;
        }

    }
}
