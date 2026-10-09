using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using System.Web.Http.Controllers;
using SMC.BhoomiMalmatta.Business.PropertyRegister;
using SMC.BhoomiMalmatta.Models.Common;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Web.Api
{
    /// <summary>
    /// Admin-only Web API authorization: an authenticated user without the Admin role gets 403 (not 401, which forms authentication would turn
    /// into a login redirect); an anonymous request gets 401 without a redirect.
    /// </summary>
    public sealed class AdminOnlyApiAttribute : AuthorizeAttribute
    {
        public AdminOnlyApiAttribute() { Roles = SMC.BhoomiMalmatta.Models.Constants.Roles.Admin; }

        protected override void HandleUnauthorizedRequest(HttpActionContext actionContext)
        {
            var principal = actionContext.ControllerContext.RequestContext.Principal;
            if (principal != null && principal.Identity != null && principal.Identity.IsAuthenticated)
            {
                actionContext.Response = actionContext.Request.CreateResponse(HttpStatusCode.Forbidden, new ApiError { Code = "FORBIDDEN", Message = "प्रवेश नाकारला." });
                return;
            }
            if (HttpContext.Current != null) HttpContext.Current.Response.SuppressFormsAuthenticationRedirect = true;
            base.HandleUnauthorizedRequest(actionContext);
        }
    }

    /// <summary>Dedicated API of Department/Properties.aspx. DTOs only; Admin only (attribute + re-checked in every action); writes are audited.</summary>
    [RoutePrefix("api/v1/properties"), AdminOnlyApi]
    public class PropertiesController : ApiBase
    {
        private bool IsAdmin { get { return Me != null && !Me.IsCitizen && Me.Role == Roles.Admin; } }
        private IHttpActionResult Forbidden() { return Fail(HttpStatusCode.Forbidden, "FORBIDDEN", "प्रवेश नाकारला."); }
        private static string Root { get { return HttpContext.Current == null ? string.Empty : (HttpContext.Current.Request.ApplicationPath ?? string.Empty).TrimEnd('/'); } }

        [HttpGet, Route("")]
        public IHttpActionResult List([FromUri] string search = null, [FromUri] string category = null, [FromUri] string status = null, [FromUri] int page = 1, [FromUri] int pageSize = PropertyService.DefaultPageSize)
        {
            if (!IsAdmin) return Forbidden();
            return Ok(AppServices.Properties.List(new PropertyListFilter { Search = search, Category = category, Status = status, Page = page, PageSize = pageSize }));
        }

        [HttpGet, Route("options")]
        public IHttpActionResult Options()
        {
            if (!IsAdmin) return Forbidden();
            return Ok(OptionsDto(AppServices.Properties.Options.Get()));
        }

        /// <summary>The options DTO plus the default status (the legacy "vacant" code when the database offers it, otherwise the first status).</summary>
        public static PropertyOptionsDto OptionsDto(PropertyOptionSet set)
        {
            var dto = set.ToDto();
            dto.DefaultStatus = dto.Statuses.Any(x => x.Code == PropertyStatuses.Vacant) ? PropertyStatuses.Vacant : dto.Statuses.Select(x => x.Code).FirstOrDefault();
            return dto;
        }

        [HttpGet, Route("next-code")]
        public IHttpActionResult NextCode([FromUri] string category = null)
        {
            if (!IsAdmin) return Forbidden();
            return Ok(new { code = AppServices.Properties.NextCode(category) });
        }

        [HttpGet, Route("{id:int}")]
        public IHttpActionResult Get(int id)
        {
            if (!IsAdmin) return Forbidden();
            var d = AppServices.Properties.Detail(id);
            if (d == null) return Fail(HttpStatusCode.NotFound, "NOT_FOUND", PropertyService.NotFoundMessage);
            d.RelatedLinks.AddRange(PropertyRelatedLinks.For(id, Root));
            if (AppServices.Register != null)
                d.Documents.AddRange(AppServices.Register.EntityDocuments("Property", id).Select(x => new PropertyDocumentDto
                {
                    Id = x.Id, FileName = x.FileName, SizeText = (x.Size / 1024) + " KB", DateText = x.CreatedAt.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture),
                    Url = Root + "/Download.ashx?type=regdoc&id=" + x.Id
                }));
            d.DocumentsUrl = Root + "/Department/Properties.aspx?documents=" + id;
            return Ok(d);
        }

        [HttpPost, Route("")]
        public IHttpActionResult Create([FromBody] PropertySaveRequest request)
        {
            if (!IsAdmin) return Forbidden();
            var o = AppServices.Properties.Create(request, Actor());
            return o.Ok ? Content(HttpStatusCode.Created, new { id = o.Id }) : Outcome(o);
        }

        [HttpPut, Route("{id:int}")]
        public IHttpActionResult Update(int id, [FromBody] PropertySaveRequest request)
        {
            if (!IsAdmin) return Forbidden();
            var o = AppServices.Properties.Update(id, request, Actor());
            return o.Ok ? Ok(new { id }) : Outcome(o);
        }

        [HttpDelete, Route("{id:int}")]
        public IHttpActionResult Delete(int id)
        {
            if (!IsAdmin) return Forbidden();
            var r = AppServices.Properties.Delete(id, Actor());
            if (r.IsSuccess) return Ok(new { id });
            return Fail(r.Error.Code == "NOT_FOUND" ? HttpStatusCode.NotFound : r.Error.Code == "DEPENDENTS" || r.Error.Code == "CONFLICT" ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest, r.Error.Code, r.Error.Message);
        }

        private IHttpActionResult Outcome(PropertySaveOutcome o)
        {
            if (o.Errors.Count > 0)
                return Content((HttpStatusCode)422, new { code = "VALIDATION_FAILED", message = "माहितीत त्रुटी आहेत.", errors = o.Errors.Select(x => new PropertyFieldErrorDto { Field = x.Field, Message = x.Message }).ToList() });
            var e = o.Error ?? new Error("DB_ERROR", "नोंद जतन करता आली नाही.");
            return Fail(e.Code == "NOT_FOUND" ? HttpStatusCode.NotFound : e.Code == "CONFLICT" ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest, e.Code, e.Message);
        }
    }

    /// <summary>The existing "संबंधित नोंदी" tiles of the property detail view (same targets, icons and labels as before).</summary>
    public static class PropertyRelatedLinks
    {
        private static readonly string[][] Tiles =
        {
            new[] { "Tenders.aspx", "🏷", "देण्याची कार्यपद्धती" }, new[] { "Transfers.aspx", "🔁", "हस्तांतरण" }, new[] { "Calculations.aspx", "🧮", "Calculation" },
            new[] { "Recovery.aspx", "💰", "वसुली प्रक्रिया" }, new[] { "Schemes.aspx", "🎯", "विविध उपक्रम" }
        };

        public static List<PropertyLinkDto> For(int propertyId, string appRoot)
        {
            return Tiles.Select(t => new PropertyLinkDto { Href = (appRoot ?? string.Empty) + "/Department/" + t[0] + "?property=" + propertyId, Icon = t[1], Label = t[2] }).ToList();
        }
    }
}
