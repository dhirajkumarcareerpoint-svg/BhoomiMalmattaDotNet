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
    public partial class SchemesPage : ModulePage<SchemeApplication>
    {
        protected override string[] RequiredRoles { get { return new[] { Roles.Admin }; } }

        protected override string Heading { get { return "विविध उपक्रम"; } }
        protected override string EntityName { get { return "SchemeApplication"; } }
        protected override string SearchCaption { get { return "शोध (उपक्रम / अर्जदार / मोबाईल)"; } }
        protected override List<ListItem> StatusOptions { get { return Opts(SchemeStatuses.All, SchemeStatuses.Label, false); } }
        protected override string[] Headers { get { return new[] { "उपक्रम", "मालमत्ता", "अर्जदार", "अर्ज दिनांक", "मूळ थकबाकी (₹)", "माफ (₹)", "देय (₹)", "स्थिती" }; } }
        private bool CanDecide { get { return Me != null && (Me.Role == Roles.Admin || Me.Role == Roles.AssistantCommissioner); } }

        protected override List<Field> BuildFields(bool isNew)
        {
            var status = Opts(SchemeStatuses.All, SchemeStatuses.Label, true);
            var f = new List<Field>
            {
                new Field("SchemeType", "उपक्रमाचे नाव", FieldKind.Text) { Required = true, MaxLength = 30 },
                new Field("PropertyId", "मालमत्ता", FieldKind.Select) { Required = true, Options = PropertyOptions(true) },
                new Field("ApplicantName", "अर्जदार / लाभार्थी", FieldKind.Text) { Required = true, MaxLength = 200 }, new Field("ApplicantMobile", "मोबाईल", FieldKind.Text) { MaxLength = 10 },
                new Field("ApplicationDate", "अर्ज दिनांक", FieldKind.Date) { Required = true },
                new Field("Status", "स्थिती", FieldKind.Select) { Required = true, Options = status }, new Field("DecisionDate", "निर्णय दिनांक", FieldKind.Date),
                new Field("OriginalOutstanding", "मूळ थकबाकी (₹)", FieldKind.Decimal), new Field("WaivedAmount", "माफ रक्कम (₹)", FieldKind.Decimal),
                new Field("Shera", "तपशील / शेरा", FieldKind.TextArea) { MaxLength = 2000 }
            };
            if (!isNew) { f.Add(new Field("PayableAmount", "देय रक्कम (₹)", FieldKind.Text)); f.Add(new Field("ApprovedBy", "मंजूर करणारा", FieldKind.Text)); }
            return f;
        }

        protected override PagedResult<SchemeApplication> Query(string search, string status, int? propertyId, int page) { return AppServices.Register.SearchSchemes(search, status, propertyId, page, PageSize); }
        protected override SchemeApplication LoadEntity(int id) { return AppServices.Register.GetScheme(id); }
        protected override int IdOf(SchemeApplication e) { return e.Id; }
        protected override string DetailTitle(SchemeApplication e) { return e.SchemeType + " — " + e.ApplicantName; }
        protected override string[] Cells(SchemeApplication e) { return new[] { e.SchemeType, PropertyLabel(e.PropertyId), e.ApplicantName, Day(e.ApplicationDate), Money(e.OriginalOutstanding), Money(e.WaivedAmount), Money(e.PayableAmount), SchemeStatuses.Label(e.Status) }; }

        protected override Dictionary<string, string> Values(SchemeApplication e)
        {
            return new Dictionary<string, string>
            {
                { "SchemeType", e.SchemeType }, { "PropertyId", e.PropertyId.HasValue ? e.PropertyId.Value.ToString() : "" }, { "ApplicantName", e.ApplicantName }, { "ApplicantMobile", e.ApplicantMobile },
                { "ApplicationDate", DateText(e.ApplicationDate) }, { "Status", e.Status }, { "DecisionDate", DateText(e.DecisionDate) }, { "OriginalOutstanding", NumText(e.OriginalOutstanding) },
                { "WaivedAmount", NumText(e.WaivedAmount) }, { "Shera", e.Shera }, { "PayableAmount", Money(e.PayableAmount) }, { "ApprovedBy", e.ApprovedBy }
            };
        }
        protected override Dictionary<string, string> Defaults()
        {
            int pid; var q = Request.QueryString["property"];
            return new Dictionary<string, string> { { "Status", SchemeStatuses.Pending }, { "ApplicationDate", DateText(DateTime.Today) }, { "OriginalOutstanding", "0" }, { "WaivedAmount", "0" }, { "PropertyId", q != null && int.TryParse(q, out pid) ? pid.ToString() : "" } };
        }

        protected override Result<int> Save(int id, Dictionary<string, string> v)
        {
            var s = new SchemeApplication
            {
                Id = id, SchemeType = S(v, "SchemeType"), PropertyId = I(v, "PropertyId", "मालमत्ता"), ApplicantName = S(v, "ApplicantName"), ApplicantMobile = S(v, "ApplicantMobile"),
                ApplicationDate = Dt(v, "ApplicationDate", "अर्ज दिनांक") ?? default(DateTime), Status = S(v, "Status"), DecisionDate = Dt(v, "DecisionDate", "निर्णय दिनांक"),
                OriginalOutstanding = D(v, "OriginalOutstanding", "मूळ थकबाकी") ?? 0m, WaivedAmount = D(v, "WaivedAmount", "माफ रक्कम") ?? 0m, Shera = S(v, "Shera")
            };
            var errors = RegisterRules.ValidateScheme(s); errors.AddRange(Errors);
            var prev = id == 0 ? null : Entity.Status;
            if ((s.Status == SchemeStatuses.Approved || s.Status == SchemeStatuses.Rejected) && s.Status != prev)
            {
                if (!CanDecide) errors.Add("मंजुरी / नाकारणे केवळ सहाय्यक आयुक्त किंवा Admin करू शकतात.");
                if (!s.DecisionDate.HasValue) s.DecisionDate = DateTime.Today;
                s.ApprovedBy = Me.FullName;
            }
            else s.ApprovedBy = id == 0 ? null : Entity.ApprovedBy;
            if (errors.Count > 0) return Result.Failure<int>(new Error("INVALID", string.Join(" ", errors)));
            return AppServices.Register.SaveScheme(s, Actor());
        }

        protected override void BuildExtras(Control host, SchemeApplication e) { RegisterUi.Documents(host, "SchemeApplication", e.Id, CanWrite, Actor(), Url("id=" + e.Id), m => Fail(new[] { m })); }
    }
}
