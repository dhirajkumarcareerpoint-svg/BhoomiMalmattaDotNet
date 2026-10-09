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
    public partial class TendersPage : ModulePage<AllocationProcess>
    {
        protected override string[] RequiredRoles { get { return new[] { Roles.Admin }; } }

        protected override string Heading { get { return "देण्याची कार्यपद्धती"; } }
        protected override string EntityName { get { return "AllocationProcess"; } }
        protected override string SearchCaption { get { return "शोध (जाहिरात क्रमांक / बोलीदार / मोबाईल)"; } }
        protected override string[] Headers { get { return new[] { "जाहिरात / निविदा क्रमांक", "मालमत्ता", "पद्धत", "प्रसिद्धी", "अंतिम दिनांक", "राखीव किंमत (₹)", "सर्वोच्च बोली (₹)", "स्थिती" }; } }
        protected override List<ListItem> StatusOptions { get { return Opts(AllocationStatuses.All, AllocationStatuses.Label, false); } }

        protected override List<Field> BuildFields(bool isNew)
        {
            return new List<Field>
            {
                new Field("PropertyId", "मालमत्ता", FieldKind.Select) { Required = true, Options = PropertyOptions(true) },
                new Field("Method", "पद्धत", FieldKind.Select) { Required = true, Options = Opts(AllocationMethods.All, AllocationMethods.Label, true) },
                new Field("NoticeNumber", "जाहिरात / निविदा क्रमांक", FieldKind.Text) { MaxLength = 100 },
                new Field("Status", "स्थिती", FieldKind.Select) { Required = true, Options = Opts(AllocationStatuses.All, AllocationStatuses.Label, true) },
                new Field("PublishDate", "प्रसिद्धी दिनांक", FieldKind.Date) { Required = true }, new Field("LastDateToApply", "अर्ज / निविदा अंतिम दिनांक", FieldKind.Date),
                new Field("AuctionDate", "लिलाव / उघडण्याचा दिनांक", FieldKind.Date), new Field("ReserveAmount", "राखीव किंमत (₹)", FieldKind.Decimal),
                new Field("HighestBidAmount", "सर्वोच्च बोली (₹)", FieldKind.Decimal), new Field("HighestBidderName", "सर्वोच्च बोलीदार", FieldKind.Text) { MaxLength = 200 },
                new Field("HighestBidderMobile", "बोलीदार मोबाईल", FieldKind.Text) { MaxLength = 10 }, new Field("Shera", "शेरा", FieldKind.TextArea) { MaxLength = 2000 }
            };
        }

        protected override PagedResult<AllocationProcess> Query(string search, string status, int? propertyId, int page) { return AppServices.Register.SearchAllocations(search, status, propertyId, page, PageSize); }
        protected override AllocationProcess LoadEntity(int id) { return AppServices.Register.GetAllocation(id); }
        protected override int IdOf(AllocationProcess e) { return e.Id; }
        protected override string DetailTitle(AllocationProcess e) { return (e.NoticeNumber ?? "क्रमांक नाही") + " — " + PropertyLabel(e.PropertyId); }
        protected override string[] Cells(AllocationProcess e) { return new[] { e.NoticeNumber ?? "#" + e.Id, PropertyLabel(e.PropertyId), AllocationMethods.Label(e.Method), Day(e.PublishDate), Day(e.LastDateToApply), Money(e.ReserveAmount), Money(e.HighestBidAmount), AllocationStatuses.Label(e.Status) }; }

        protected override Dictionary<string, string> Values(AllocationProcess e)
        {
            return new Dictionary<string, string>
            {
                { "PropertyId", e.PropertyId.HasValue ? e.PropertyId.Value.ToString() : "" }, { "Method", e.Method }, { "NoticeNumber", e.NoticeNumber }, { "Status", e.Status }, { "PublishDate", DateText(e.PublishDate) },
                { "LastDateToApply", DateText(e.LastDateToApply) }, { "AuctionDate", DateText(e.AuctionDate) }, { "ReserveAmount", NumText(e.ReserveAmount) }, { "HighestBidAmount", NumText(e.HighestBidAmount) },
                { "HighestBidderName", e.HighestBidderName }, { "HighestBidderMobile", e.HighestBidderMobile }, { "Shera", e.Shera }
            };
        }
        protected override Dictionary<string, string> Defaults()
        {
            int pid; var q = Request.QueryString["property"];
            return new Dictionary<string, string> { { "Status", AllocationStatuses.Advertised }, { "Method", AllocationMethods.Auction }, { "PropertyId", q != null && int.TryParse(q, out pid) ? pid.ToString() : "" } };
        }

        protected override Result<int> Save(int id, Dictionary<string, string> v)
        {
            var pid = I(v, "PropertyId", "मालमत्ता");
            var a = new AllocationProcess
            {
                Id = id, PropertyId = pid, Method = S(v, "Method"), NoticeNumber = S(v, "NoticeNumber"), Status = S(v, "Status"), PublishDate = Dt(v, "PublishDate", "प्रसिद्धी दिनांक") ?? default(System.DateTime),
                LastDateToApply = Dt(v, "LastDateToApply", "अंतिम दिनांक"), AuctionDate = Dt(v, "AuctionDate", "लिलाव दिनांक"), ReserveAmount = D(v, "ReserveAmount", "राखीव किंमत"),
                HighestBidAmount = D(v, "HighestBidAmount", "सर्वोच्च बोली"), HighestBidderName = S(v, "HighestBidderName"), HighestBidderMobile = S(v, "HighestBidderMobile"), Shera = S(v, "Shera")
            };
            var errors = RegisterRules.ValidateAllocation(a); errors.AddRange(Errors);
            if (errors.Count > 0) return Result.Failure<int>(new Error("INVALID", string.Join(" ", errors)));
            return AppServices.Register.SaveAllocation(a, Actor());
        }

        protected override void BuildExtras(Control host, AllocationProcess e) { RegisterUi.Documents(host, "AllocationProcess", e.Id, CanWrite, Actor(), Url("id=" + e.Id), m => Fail(new[] { m })); }
    }
}
