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
    public partial class RecoveryPage : ModulePage<RecoveryCase>
    {
        protected override string[] RequiredRoles { get { return new[] { Roles.Admin }; } }

        protected override string Heading { get { return "वसुली प्रक्रिया"; } }
        protected override string EntityName { get { return "RecoveryCase"; } }
        protected override string SearchCaption { get { return "शोध (नोटीस क्रमांक)"; } }
        protected override string StatusCaption { get { return "टप्पा"; } }
        protected override List<ListItem> StatusOptions { get { return Opts(RecoveryStages.All, RecoveryStages.Label, false); } }
        protected override bool CanEdit(RecoveryCase e) { return !RecoveryStages.IsClosed(e.Stage); }
        protected override string[] Headers { get { return new[] { "प्रकरण", "मालमत्ता", "थकीत महिने", "थकबाकी (₹)", "वसूल (₹)", "शिल्लक (₹)", "टप्पा", "नोटीस क्रमांक" }; } }

        protected override List<Field> BuildFields(bool isNew)
        {
            var leases = new List<ListItem> { new ListItem("-- नाही --", string.Empty) };
            foreach (var l in AppServices.Register.LeaseRefs()) leases.Add(new ListItem(l.Label, l.Id.ToString()));
            return new List<Field>
            {
                new Field("PropertyId", "मालमत्ता", FieldKind.Select) { Required = true, LockedOnEdit = true, Options = PropertyOptions(true) },
                new Field("LeaseId", "भाडेकरार", FieldKind.Select) { LockedOnEdit = true, Options = leases },
                new Field("MonthsOverdue", "थकीत महिने", FieldKind.Int), new Field("OutstandingAmount", "थकबाकी / मागणी (₹)", FieldKind.Decimal) { Required = true },
                new Field("Stage", "टप्पा / कृती", FieldKind.Select) { Required = true, Options = Opts(RecoveryStages.All, RecoveryStages.Label, true) },
                new Field("NoticeNumber", "नोटीस क्रमांक", FieldKind.Text) { MaxLength = 100 }, new Field("NoticeDate", "नोटीस दिनांक", FieldKind.Date),
                new Field("RecoveredAmount", "वसूल रक्कम (₹)", FieldKind.Decimal), new Field("RecoveryDate", "वसुली दिनांक", FieldKind.Date),
                new Field("SealDate", "सील दिनांक", FieldKind.Date), new Field("ReAuctionDate", "पुनर्लिलाव दिनांक", FieldKind.Date)
            };
        }

        protected override PagedResult<RecoveryCase> Query(string search, string status, int? propertyId, int page) { return AppServices.Register.SearchRecovery(search, status, propertyId, page, PageSize); }
        protected override RecoveryCase LoadEntity(int id) { return AppServices.Register.GetRecovery(id); }
        protected override int IdOf(RecoveryCase e) { return e.Id; }
        protected override string DetailTitle(RecoveryCase e) { return "प्रकरण #" + e.Id + " — " + PropertyLabel(e.PropertyId) + " — शिल्लक ₹ " + (e.OutstandingAmount - e.RecoveredAmount).ToString("N2"); }
        protected override string[] Cells(RecoveryCase e) { return new[] { "#" + e.Id, PropertyLabel(e.PropertyId), e.MonthsOverdue.ToString(), Money(e.OutstandingAmount), Money(e.RecoveredAmount), Money(e.OutstandingAmount - e.RecoveredAmount), RecoveryStages.Label(e.Stage), e.NoticeNumber ?? "-" }; }

        protected override Dictionary<string, string> Values(RecoveryCase e)
        {
            return new Dictionary<string, string>
            {
                { "PropertyId", e.PropertyId.HasValue ? e.PropertyId.Value.ToString() : "" }, { "LeaseId", e.LeaseId.HasValue ? e.LeaseId.Value.ToString() : "" }, { "MonthsOverdue", e.MonthsOverdue.ToString() },
                { "OutstandingAmount", NumText(e.OutstandingAmount) }, { "Stage", e.Stage }, { "NoticeNumber", e.NoticeNumber }, { "NoticeDate", DateText(e.NoticeDate) }, { "RecoveredAmount", NumText(e.RecoveredAmount) },
                { "RecoveryDate", DateText(e.RecoveryDate) }, { "SealDate", DateText(e.SealDate) }, { "ReAuctionDate", DateText(e.ReAuctionDate) }
            };
        }
        protected override Dictionary<string, string> Defaults()
        {
            int pid; var q = Request.QueryString["property"];
            return new Dictionary<string, string> { { "Stage", RecoveryStages.Demand }, { "MonthsOverdue", "0" }, { "RecoveredAmount", "0" }, { "PropertyId", q != null && int.TryParse(q, out pid) ? pid.ToString() : "" } };
        }

        protected override Result<int> Save(int id, Dictionary<string, string> v)
        {
            var r = new RecoveryCase
            {
                Id = id, PropertyId = id == 0 ? I(v, "PropertyId", "मालमत्ता") : Entity.PropertyId, LeaseId = id == 0 ? I(v, "LeaseId", "भाडेकरार") : Entity.LeaseId,
                MonthsOverdue = I(v, "MonthsOverdue", "थकीत महिने") ?? 0, OutstandingAmount = D(v, "OutstandingAmount", "थकबाकी") ?? 0m, Stage = S(v, "Stage"), NoticeNumber = S(v, "NoticeNumber"),
                NoticeDate = Dt(v, "NoticeDate", "नोटीस दिनांक"), RecoveredAmount = D(v, "RecoveredAmount", "वसूल रक्कम") ?? 0m, RecoveryDate = Dt(v, "RecoveryDate", "वसुली दिनांक"),
                SealDate = Dt(v, "SealDate", "सील दिनांक"), ReAuctionDate = Dt(v, "ReAuctionDate", "पुनर्लिलाव दिनांक")
            };
            var errors = RegisterRules.ValidateRecovery(r, id == 0 ? null : Entity); errors.AddRange(Errors);
            if (errors.Count > 0) return Result.Failure<int>(new Error("INVALID", string.Join(" ", errors)));
            return AppServices.Register.SaveRecovery(r, Actor());
        }

        protected override void BuildExtras(Control host, RecoveryCase e)
        {
            if (RecoveryStages.IsClosed(e.Stage)) Lit(host, "<div class=\"notice info\" role=\"status\">हे प्रकरण बंद आहे; पुढील बदल करता येत नाहीत. इतिहास खाली दिसतो.</div>");
            RegisterUi.Documents(host, "RecoveryCase", e.Id, CanWrite && !RecoveryStages.IsClosed(e.Stage), Actor(), Url("id=" + e.Id), m => Fail(new[] { m }));
        }
    }
}
