using System;
using System.Collections.Generic;
using System.Web.UI;
using System.Web.UI.WebControls;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Models.Common;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;
using SMC.BhoomiMalmatta.Models.Entities;
using SMC.BhoomiMalmatta.Web.Security;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Web.Department
{
    public partial class TransfersPage : ModulePage<GalaTransfer>
    {
        protected override string[] RequiredRoles { get { return new[] { Roles.Admin }; } }

        protected override string Heading { get { return "हस्तांतरण"; } }
        protected override string EntityName { get { return "GalaTransfer"; } }
        protected override string SearchCaption { get { return "शोध (धारक / भरणा संदर्भ)"; } }
        protected override string StatusCaption { get { return "टप्पा"; } }
        protected override List<ListItem> StatusOptions { get { return Opts(TransferStages.All, TransferStages.Label, false); } }
        protected override bool CanEdit(GalaTransfer e) { return false; }
        protected override string[] Headers { get { return new[] { "क्र.", "मालमत्ता", "पूर्वीचा धारक", "नवीन धारक", "प्रकार", "शुल्क (₹)", "दंड (₹)", "टप्पा" }; } }

        protected override List<Field> BuildFields(bool isNew)
        {
            if (isNew)
                return new List<Field>
                {
                    new Field("PropertyId", "मालमत्ता / गाळा", FieldKind.Select) { Required = true, Options = PropertyOptions(true) },
                    new Field("TransferType", "हस्तांतरण प्रकार", FieldKind.Select) { Required = true, Options = Opts(TransferTypes.All, TransferTypes.Label, true) },
                    new Field("ExistingHolder", "सध्याचा धारक (मालमत्तेवर नोंद नसल्यास आवश्यक)", FieldKind.Text) { MaxLength = 200 },
                    new Field("NewHolder", "नवीन धारक", FieldKind.Text) { Required = true, MaxLength = 200 },
                    new Field("Shera", "शेरा", FieldKind.TextArea) { MaxLength = 2000 }
                };
            return new List<Field>
            {
                new Field("Property", "मालमत्ता", FieldKind.Text), new Field("ExistingHolder", "पूर्वीचा धारक", FieldKind.Text), new Field("NewHolder", "नवीन धारक", FieldKind.Text),
                new Field("TransferType", "हस्तांतरण प्रकार", FieldKind.Text), new Field("TransferFee", "हस्तांतरण शुल्क (₹)", FieldKind.Text), new Field("Penalty", "दंड (₹)", FieldKind.Text),
                new Field("Stage", "टप्पा", FieldKind.Text), new Field("PaymentReference", "भरणा संदर्भ (UTR / पावती)", FieldKind.Text), new Field("PaymentVerifiedAt", "भरणा पडताळणी दिनांक", FieldKind.Text),
                new Field("ApprovedBy", "मंजूर करणारा", FieldKind.Text), new Field("ApprovedAt", "मंजुरी दिनांक", FieldKind.Text), new Field("DecisionRemark", "निर्णय शेरा", FieldKind.Text), new Field("Shera", "शेरा", FieldKind.Text)
            };
        }

        protected override PagedResult<GalaTransfer> Query(string search, string status, int? propertyId, int page) { return AppServices.Register.SearchTransfers(search, status, propertyId, page, PageSize); }
        protected override GalaTransfer LoadEntity(int id) { return AppServices.Register.GetTransfer(id); }
        protected override int IdOf(GalaTransfer e) { return e.Id; }
        protected override string DetailTitle(GalaTransfer e) { return "हस्तांतरण #" + e.Id + " — " + TransferStages.Label(e.Stage); }
        protected override string[] Cells(GalaTransfer e) { return new[] { "#" + e.Id, PropertyLabel(e.PropertyId), e.ExistingHolder, e.NewHolder, TransferTypes.Label(e.TransferType), Money(e.TransferFee), Money(e.Penalty), TransferStages.Label(e.Stage) }; }

        protected override Dictionary<string, string> Values(GalaTransfer e)
        {
            return new Dictionary<string, string>
            {
                { "Property", PropertyLabel(e.PropertyId) }, { "ExistingHolder", e.ExistingHolder }, { "NewHolder", e.NewHolder }, { "TransferType", TransferTypes.Label(e.TransferType) }, { "TransferFee", Money(e.TransferFee) },
                { "Penalty", Money(e.Penalty) }, { "Stage", TransferStages.Label(e.Stage) }, { "PaymentReference", e.PaymentReference }, { "PaymentVerifiedAt", e.PaymentVerifiedAt.HasValue ? e.PaymentVerifiedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : null },
                { "ApprovedBy", e.ApprovedBy }, { "ApprovedAt", e.ApprovedAt.HasValue ? e.ApprovedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : null }, { "DecisionRemark", e.DecisionRemark }, { "Shera", e.Shera }
            };
        }
        protected override Dictionary<string, string> Defaults()
        {
            int pid; var q = Request.QueryString["property"];
            return new Dictionary<string, string> { { "TransferType", TransferTypes.Regular }, { "PropertyId", q != null && int.TryParse(q, out pid) ? pid.ToString() : "" } };
        }

        protected override Result<int> Save(int id, Dictionary<string, string> v)
        {
            if (!TransferRules.CanCreate(Me.Role)) return Result.Failure<int>(new Error("FORBIDDEN", "हस्तांतरण विनंती नोंदवण्याची परवानगी नाही."));
            var pid = I(v, "PropertyId", "मालमत्ता");
            var type = S(v, "TransferType");
            if (Errors.Count > 0) return Result.Failure<int>(new Error("INVALID", string.Join(" ", Errors)));
            if (Array.IndexOf(TransferTypes.All, type) < 0) return Result.Failure<int>(new Error("INVALID", "हस्तांतरण प्रकार निवडा."));
            return AppServices.Register.CreateTransfer(pid.Value, type, S(v, "ExistingHolder"), S(v, "NewHolder"), S(v, "Shera"), Actor());
        }

        protected override void BuildExtras(Control host, GalaTransfer e)
        {
            var steps = TransferRules.Available(e.Stage, Me.Role);
            var cert = e.Stage == TransferStages.Approved;
            Lit(host, "<div class=\"card mt\"><h3 style=\"margin-top:0\">कार्यप्रवाह</h3><p class=\"muted\">विनंती → JE पुनरावलोकन → शुल्क भरणा पडताळणी (OS) → अंतिम मंजुरी (AC) → धारक अद्ययावत → प्रमाणपत्र. सध्याचा टप्पा: <b>" + Enc(TransferStages.Label(e.Stage)) + "</b></p>");
            if (cert) Lit(host, "<div class=\"btns\"><a class=\"btn\" style=\"text-align:center;text-decoration:none\" href=\"" + Enc(ResolveUrl("~/Download.ashx") + "?type=xfercert&id=" + e.Id) + "\">हस्तांतरण प्रमाणपत्र डाउनलोड करा</a></div>");
            if (steps.Count > 0)
            {
                var ver = new HiddenField { ID = "hfVer" }; host.Controls.Add(ver);
                if (!IsPostBack) ver.Value = e.Version == null ? "" : Convert.ToBase64String(e.Version);
                TextBox pay = null, remark = null;
                var needPay = steps.Exists(s => s.Action == "pay");
                Lit(host, "<div class=\"two\">");
                if (needPay) { Lit(host, "<div><label class=\"lb\" for=\"txtPay\">भरणा संदर्भ (UTR / पावती क्रमांक) *</label>"); pay = new TextBox { ID = "txtPay", CssClass = "in", MaxLength = 100 }; host.Controls.Add(pay); Lit(host, "</div>"); }
                Lit(host, "<div><label class=\"lb\" for=\"txtRemark\">शेरा (नाकारण्यासाठी आवश्यक)</label>"); remark = new TextBox { ID = "txtRemark", CssClass = "in", TextMode = TextBoxMode.MultiLine, Rows = 2, MaxLength = 2000 }; host.Controls.Add(remark); Lit(host, "</div></div><div class=\"btns\">");
                foreach (var step in steps)
                {
                    var s = step;
                    var b = new Button { ID = "btn_" + s.Action, Text = s.ButtonLabel, CssClass = s.Action == "reject" ? "btn o" : "btn", CausesValidation = false };
                    b.Click += (o, a) => Act(s.Action, ver.Value, pay == null ? null : pay.Text, remark.Text);
                    host.Controls.Add(b);
                }
                Lit(host, "</div>");
            }
            else if (!TransferStages.IsFinal(e.Stage)) Lit(host, "<p class=\"muted\">या टप्प्यावरील कृती आपल्या भूमिकेसाठी उपलब्ध नाही.</p>");
            Lit(host, "</div>");
            RegisterUi.Documents(host, RegisterUi.TransferScope, e.Id, CanWrite && !TransferStages.IsFinal(e.Stage), Actor(), Url("id=" + e.Id), m => Fail(new[] { m }));
        }

        private void Act(string action, string version, string payRef, string remark)
        {
            byte[] ver = null;
            if (!string.IsNullOrEmpty(version)) { try { ver = Convert.FromBase64String(version); } catch (FormatException) { Fail(new[] { "अवैध विनंती." }); return; } }
            Result r;
            try { r = AppServices.Register.TransitionTransfer(EntityId, action, ver, payRef, remark, Actor()); }
            catch (Exception ex) { ErrorLogger.Log(CorrelationId ?? "n/a", Request.Path, ex); Fail(new[] { "कृती पूर्ण करता आली नाही. संदर्भ: " + (CorrelationId ?? "n/a") }); return; }
            if (r.IsFailure) { Fail(new[] { r.Error.Message }); return; }
            Response.Redirect(Url("id=" + EntityId + "&saved=1"), false);
            Context.ApplicationInstance.CompleteRequest();
        }
    }
}
