using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web.Http;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Business.Citizen;
using SMC.BhoomiMalmatta.Business.Workflow;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;
using SMC.BhoomiMalmatta.Web.Security;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Web.Api
{
    [RoutePrefix("api/v1/auth")]
    public class AuthController : ApiBase
    {
        [HttpPost, Route("login"), AllowAnonymous]
        public IHttpActionResult Login([FromBody] LoginRequest request)
        {
            if (request == null) return Fail(System.Net.HttpStatusCode.BadRequest, "BAD_REQUEST", "विनंती अवैध आहे.");
            if (!AppServices.Limiter.Allow("api-login:" + Ip, 30, TimeSpan.FromMinutes(10))) return TooMany();
            var r = AppServices.Auth.Login(request.Username, request.Password, Ip);
            if (r.IsFailure) return Fail(r.Error);
            AuthCookies.SignInEmployee(System.Web.HttpContext.Current, r.Value);
            return Ok(r.Value);
        }

        [HttpPost, Route("logout"), Authorize]
        public IHttpActionResult Logout()
        {
            if (Me != null && !Me.IsCitizen) AppServices.Auth.Logout(new AuthenticatedUser { Id = Me.UserId, Username = Me.Name, Role = Me.Role }, Ip);
            AuthCookies.SignOut(System.Web.HttpContext.Current);
            return Ok();
        }

        [HttpGet, Route("me"), Authorize]
        public IHttpActionResult WhoAmI()
        {
            var me = Me;
            return Ok(new { name = me.Name, fullName = me.FullName, role = me.Role, zoneNumber = me.ZoneNumber, applicationNumber = me.IsCitizen ? me.ApplicationNumber : null });
        }
    }

    [RoutePrefix("api/v1")]
    public class CatalogController : ApiBase
    {
        [HttpGet, Route("services"), AllowAnonymous] public IHttpActionResult Services() { return Ok(AppServices.Masters.GetServices()); }
        [HttpGet, Route("rates"), AllowAnonymous] public IHttpActionResult Rates() { return Ok(AppServices.Masters.GetRates()); }
        [HttpGet, Route("business-types"), AllowAnonymous] public IHttpActionResult BusinessTypes() { return Ok(AppServices.Masters.GetBusinessTypes()); }

        /// <summary>Display-only calculation for the form; the same engine runs again on submission.</summary>
        [HttpPost, Route("pricing/preview"), AllowAnonymous]
        public IHttpActionResult Preview([FromBody] PricingPreviewRequest request)
        {
            if (request == null) return Fail(System.Net.HttpStatusCode.BadRequest, "BAD_REQUEST", "विनंती अवैध आहे.");
            if (!AppServices.Limiter.Allow("preview:" + Ip, 120, TimeSpan.FromMinutes(1))) return TooMany();
            return Ok(AppServices.Submission.PreviewResponse(request));
        }
    }

    [RoutePrefix("api/v1/applications")]
    public class ApplicationsController : ApiBase
    {
        /// <summary>Returns the same applicant-safe status projection as the citizen page; the encrypted citizen ticket fixes application identity.</summary>
        [HttpGet, Route("status"), Authorize(Roles = ApiRoles.Citizen)]
        public IHttpActionResult Status()
        {
            var me = Me;
            if (me == null || !me.IsCitizen) return Fail(System.Net.HttpStatusCode.NotFound, "NOT_FOUND", "अर्ज आढळला नाही.");
            var detail = AppServices.Query.GetDetail(me.ApplicationId);
            if (detail == null || !string.Equals(detail.ApplicationNumber, me.ApplicationNumber, StringComparison.Ordinal))
                return Fail(System.Net.HttpStatusCode.NotFound, "NOT_FOUND", "अर्ज आढळला नाही.");
            return Ok(MapApplicantStatus(detail));
        }

        [HttpGet, Route(""), Authorize(Roles = ApiRoles.Officers)]
        public IHttpActionResult List([FromUri] string search = null, [FromUri] string status = null, [FromUri] string stage = null, [FromUri] string serviceType = null,
            [FromUri] int? zone = null, [FromUri] string route = null, [FromUri] string paymentStatus = null, [FromUri] DateTime? from = null, [FromUri] DateTime? to = null,
            [FromUri] int page = 1, [FromUri] int pageSize = 20)
        {
            return Ok(AppServices.Query.Search(new ApplicationFilter
            {
                Search = search, Status = status, Stage = stage, ServiceType = serviceType, Zone = zone, Route = route, PaymentStatus = paymentStatus, From = from, To = to,
                Page = page, PageSize = pageSize, ZoneScope = ZoneScope // server-decided; the browser cannot widen it
            }));
        }

        [HttpGet, Route("{id:int}"), Authorize(Roles = ApiRoles.Officers + "," + ApiRoles.Citizen)]
        public IHttpActionResult Get(int id)
        {
            if (!CanAccess(id)) return Fail(System.Net.HttpStatusCode.NotFound, "NOT_FOUND", "अर्ज आढळला नाही.");
            var d = AppServices.Query.GetDetail(id);
            if (d == null) return Fail(System.Net.HttpStatusCode.NotFound, "NOT_FOUND", "अर्ज आढळला नाही.");
            if (Me.IsCitizen) return Ok(MapApplicantStatus(d)); // applicants never receive officer data
            d.AllowedActions = d.IsLegacy ? new List<string>() : ZoneWorkflowMachine.AllowedActions(d.Stage, Me.Role, AppServices.WorkflowPolicy).Where(a => a != WorkflowActions.CitizenResubmit).ToList();
            return Ok(d);
        }

        /// <summary>Read-only detail projection for the officer application modal. Identity/PAN and file paths are never returned.</summary>
        [HttpGet, Route("{id:int}/officer-view"), Authorize(Roles = ApiRoles.Officers)]
        public IHttpActionResult OfficerView(int id)
        {
            var me = Me;
            if (me == null || me.IsCitizen || Array.IndexOf(Roles.OfficerRoles, me.Role) < 0 && me.Role != Roles.Admin && me.Role != Roles.Staff)
                return Fail(System.Net.HttpStatusCode.Forbidden, "FORBIDDEN", "प्रवेश नाकारला.");
            var detail = AppServices.Query.GetDetail(id); // query excludes soft-deleted applications
            if (detail == null) return Fail(System.Net.HttpStatusCode.NotFound, "NOT_FOUND", "अर्ज आढळला नाही.");
            if (!OfficerApplicationViewMapper.ZoneAllows(me.Role, me.ZoneNumber, detail.Zone))
                return Fail(System.Net.HttpStatusCode.NotFound, "NOT_FOUND", "अर्ज आढळला नाही.");
            var appRoot = System.Web.HttpContext.Current == null ? string.Empty : System.Web.HttpContext.Current.Request.ApplicationPath;
            return Ok(OfficerApplicationViewMapper.Build(detail, me.Role, appRoot, AppServices.WorkflowPolicy));
        }

        [HttpPost, Route(""), AllowAnonymous]
        public IHttpActionResult Submit([FromBody] SubmissionRequest r)
        {
            if (r == null) return Fail(System.Net.HttpStatusCode.BadRequest, "BAD_REQUEST", "विनंती अवैध आहे.");
            if (!AppServices.Limiter.Allow("api-submit:" + Ip, 10, TimeSpan.FromHours(1))) return TooMany();
            var docs = new List<DocumentUpload>();
            foreach (var p in r.Documents ?? new List<DocumentPayload>())
            {
                byte[] bytes;
                try { bytes = Convert.FromBase64String(p.ContentBase64 ?? string.Empty); } catch (FormatException) { return Fail(System.Net.HttpStatusCode.BadRequest, "FILE_ENCODING", "फाईल एन्कोडिंग अवैध आहे."); }
                docs.Add(new DocumentUpload { DocumentType = p.DocumentType, FileName = p.FileName, ContentType = p.ContentType, Content = bytes });
            }
            var data = new SubmissionData
            {
                ServiceType = r.ServiceType, BusinessType = r.BusinessType, OtherBusinessType = r.OtherBusinessType, ApplicantType = r.ApplicantType, ApplicantName = r.ApplicantName, Mobile = r.Mobile, Email = r.Email,
                GstNumber = r.GstNumber, IdentityNumber = r.IdentityNumber, PanNumber = r.PanNumber, PermanentAddress = r.PermanentAddress, CorrespondenceAddress = r.CorrespondenceAddress, SameAddress = r.SameAddress,
                State = SubmissionValidator.LockedState, District = SubmissionValidator.LockedDistrict, City = r.City, Taluka = r.Taluka, PinCode = r.PinCode, Zone = r.Zone,
                Prabhag = r.Prabhag, Location = r.Location, ServiceDescription = r.ServiceDescription, StartDate = r.StartDate, EndDate = r.EndDate, LengthFt = r.LengthFt, WidthFt = r.WidthFt,
                DeclarationAccepted = r.DeclarationAccepted, UploadedDocumentTypes = new List<string>(), ClientClaimedAmount = null
            };
            var result = AppServices.Submission.Submit(data, docs, Ip, CorrelationId);
            if (result.Item2 != null)
                return Content((System.Net.HttpStatusCode)422, new { code = "VALIDATION_FAILED", message = "अर्जात त्रुटी आहेत.", errors = result.Item2.Errors.Select(x => new FieldErrorDto { Field = x.Field, Message = x.Message }).ToList() });
            var o = result.Item1;
            return Content(System.Net.HttpStatusCode.Created, new SubmissionResponse
            {
                ApplicationId = o.ApplicationId, ApplicationNumber = o.ApplicationNumber, FinalAmount = o.Pricing.FinalAmount, TotalDays = o.Pricing.TotalDays,
                AreaSqFt = o.Pricing.AreaSqFt, PendingOfficerAmount = o.Pricing.IsPendingOfficerAmount, PaymentStatus = o.PaymentStatus
            });
        }

        /// <summary>Number-only applicant status lookup; successful lookup issues the citizen ticket bound to that application.</summary>
        [HttpPost, Route("status"), AllowAnonymous]
        public IHttpActionResult Status([FromBody] StatusLookupRequest r)
        {
            const string generic = "अर्ज आढळला नाही. कृपया क्रमांक तपासा.";
            if (!AppServices.Limiter.Allow("api-status:" + (Ip ?? "unknown"), 30, TimeSpan.FromMinutes(10))) return TooMany();
            var number = ApplicantNumberRules.Normalize(r == null ? null : r.ApplicationNumber);
            if (number == null) return GenericStatusFailure(generic);
            try
            {
                var applicationId = AppServices.Query.FindIdByNumber(number);
                var detail = applicationId.HasValue ? AppServices.Query.GetDetail(applicationId.Value) : null;
                if (detail == null || !string.Equals(detail.ApplicationNumber, number, StringComparison.Ordinal)) return GenericStatusFailure(generic);
                AuthCookies.SignInCitizen(System.Web.HttpContext.Current, applicationId.Value, number);
                return Ok(MapApplicantStatus(detail));
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(CorrelationId, "ApplicantStatusApi", ex);
                return GenericStatusFailure(generic);
            }
        }

        private IHttpActionResult GenericStatusFailure(string message)
        {
            Thread.Sleep(200);
            return Fail(System.Net.HttpStatusCode.NotFound, "NOT_FOUND", message);
        }

        private static CitizenStatusView MapApplicantStatus(ApplicationDetail detail)
        {
            return ApplicantStatusViewMapper.Map(detail);
        }

        [HttpGet, Route("{id:int}/history"), Authorize(Roles = ApiRoles.Officers)]
        public IHttpActionResult History(int id)
        {
            if (!CanAccess(id)) return Fail(System.Net.HttpStatusCode.NotFound, "NOT_FOUND", "अर्ज आढळला नाही.");
            var d = AppServices.Query.GetDetail(id);
            return d == null ? Fail(System.Net.HttpStatusCode.NotFound, "NOT_FOUND", "अर्ज आढळला नाही.") : (IHttpActionResult)Ok(d.History);
        }
    }

    [RoutePrefix("api/v1/applications")]
    public class WorkflowController : ApiBase
    {
        /// <summary>Executes one workflow action. Role, zone and the payable amount are decided on the server.</summary>
        [HttpPost, Route("{id:int}/actions"), Authorize(Roles = ApiRoles.Officers)]
        public IHttpActionResult Act(int id, [FromBody] WorkflowActionRequest r)
        {
            string action;
            var selectedActions = r == null ? null : r.Actions;
            if (selectedActions == null && r != null && !string.IsNullOrWhiteSpace(r.Action)) selectedActions = new[] { r.Action }; // compatibility for existing API clients
            if (r == null || r.ApplicationId != id || string.IsNullOrWhiteSpace(r.ExpectedStage) || string.IsNullOrWhiteSpace(r.ExpectedRowVersion) || !OfficerDecisionSelection.TryGetSingle(selectedActions, out action))
                return Fail(System.Net.HttpStatusCode.BadRequest, "ACTION_SELECTION_INVALID", "Select exactly one action.");
            var res = WorkflowRunner.Run(new WorkflowInput
            {
                ApplicationId = id, Action = action, ExpectedStage = r.ExpectedStage, ExpectedRowVersion = r.ExpectedRowVersion, Remarks = r.Remarks, OfficerRate = r.OfficerRate, PaymentReference = r.PaymentReference, PaymentMode = r.PaymentMode
            }, Actor());
            if (res.IsFailure) return Fail(res.Error);
            var counts = AppServices.Query.GetOfficerDashboardCounts(Me.Role, ZoneScope, Me.UserId);
            return Ok(new WorkflowResponse { Stage = res.Value.NewStage, Status = res.Value.NewStatus, PaymentStatus = res.Value.NewPaymentStatus, AlreadyApplied = res.Value.WasAlreadyApplied, Counts = counts });
        }

        [HttpPost, Route("{id:int}/resubmit"), Authorize(Roles = ApiRoles.Citizen)]
        public IHttpActionResult Resubmit(int id, [FromBody] ResubmitRequest r)
        {
            if (!CanAccess(id)) return Fail(System.Net.HttpStatusCode.NotFound, "NOT_FOUND", "अर्ज आढळला नाही.");
            var d = AppServices.Query.GetDetail(id);
            var docs = new List<DocumentUpload>();
            foreach (var p in (r == null ? null : r.Documents) ?? new List<DocumentPayload>())
            {
                byte[] bytes;
                try { bytes = Convert.FromBase64String(p.ContentBase64 ?? string.Empty); } catch (FormatException) { return Fail(System.Net.HttpStatusCode.BadRequest, "FILE_ENCODING", "फाईल एन्कोडिंग अवैध आहे."); }
                docs.Add(new DocumentUpload { FileName = p.FileName, ContentType = p.ContentType, Content = bytes });
            }
            var res = AppServices.Submission.Resubmit(id, d.Stage, r == null ? null : r.Reply, docs, Actor(), AppServices.Workflow, d.ApplicationNumber);
            return res.IsFailure ? Fail(res.Error) : (IHttpActionResult)Ok();
        }
    }
}
