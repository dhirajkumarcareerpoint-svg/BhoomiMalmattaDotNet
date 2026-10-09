using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Web.Http;
using System.Web.Http.Results;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Models.Common;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;
using SMC.BhoomiMalmatta.Web.Security;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Web.Api
{
    public static class ApiRoles
    {
        public const string Officers = "Admin,JE,OS,AssistantCommissioner,Staff";
        public const string AdminOrAc = "Admin,AssistantCommissioner";
        public const string Citizen = "Citizen";
    }

    /// <summary>Common helpers. Role, zone and user always come from the encrypted ticket on the server, never from the request.</summary>
    public abstract class ApiBase : ApiController
    {
        protected SmcIdentity Me { get { var p = User as SmcPrincipal; return p == null ? null : p.Me; } }
        protected string Ip { get { return System.Web.HttpContext.Current == null ? null : System.Web.HttpContext.Current.Request.UserHostAddress; } }
        protected string CorrelationId { get { return System.Web.HttpContext.Current == null ? null : System.Web.HttpContext.Current.Items["CorrelationId"] as string; } }
        protected int? ZoneScope { get { return Me == null || Me.IsCitizen || Me.Role == Roles.Admin ? null : Me.ZoneNumber; } }

        protected ActorContext Actor()
        {
            var me = Me;
            if (me == null || me.IsCitizen) return new ActorContext { UserName = "citizen", Role = Roles.Citizen, IpAddress = Ip, CorrelationId = CorrelationId };
            return new ActorContext { UserId = me.UserId, UserName = me.Name, Role = me.Role, ZoneNumber = me.ZoneNumber, IpAddress = Ip, CorrelationId = CorrelationId };
        }

        protected static HttpStatusCode StatusFor(string code)
        {
            switch (code)
            {
                case "NOT_FOUND": case "FILE_MISSING": return HttpStatusCode.NotFound;
                case "FORBIDDEN": case "WORKFLOW_FORBIDDEN": case "ZONE_FORBIDDEN": case "LEGACY_READONLY": return HttpStatusCode.Forbidden;
                case "CONCURRENCY_CONFLICT": case "STALE_STAGE": case "PAYMENT_DUPLICATE": case "WORKFLOW_FINALIZED": case "WORKFLOW_INVALID_TRANSITION": return HttpStatusCode.Conflict;
                case "LOGIN_INVALID": return HttpStatusCode.Unauthorized;
                case "LOGIN_LOCKED": return (HttpStatusCode)429;
                default: return HttpStatusCode.BadRequest;
            }
        }

        protected IHttpActionResult Fail(Error e) { return Content(StatusFor(e.Code), new ApiError { Code = e.Code, Message = e.Message }); }
        protected IHttpActionResult Fail(HttpStatusCode status, string code, string message) { return Content(status, new ApiError { Code = code, Message = message }); }
        protected IHttpActionResult TooMany() { return Fail((HttpStatusCode)429, "RATE_LIMITED", "खूप जास्त विनंत्या. कृपया थोड्या वेळाने प्रयत्न करा."); }

        protected IHttpActionResult FileResult(byte[] bytes, string contentType, string fileName)
        {
            var ascii = Regex.Replace(fileName ?? "file", "[^\\x20-\\x7E]", "_").Replace("\"", "_");
            var resp = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
            resp.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            resp.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = ascii };
            resp.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true, NoCache = true };
            return ResponseMessage(resp);
        }

        /// <summary>Citizens: only their own application. Officers: only applications in their zone (Admin / no zone: all).</summary>
        protected bool CanAccess(int applicationId)
        {
            var me = Me;
            if (me == null) return false;
            if (me.IsCitizen) return CitizenTicketAccess.Allows(me.ApplicationId, applicationId);
            if (!ZoneScope.HasValue) return true;
            var d = AppServices.Query.GetDetail(applicationId);
            return d != null && d.Zone == ZoneScope.Value.ToString();
        }
    }
}
