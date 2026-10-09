using System;
using System.Linq;
using System.Web.Http;
using SMC.BhoomiMalmatta.Business.Security;
using SMC.BhoomiMalmatta.Business.Workflow;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Web.Api
{
    [RoutePrefix("api/v1/documents")]
    public class DocumentsController : ApiBase
    {
        [HttpGet, Route("{id:int}"), Authorize(Roles = ApiRoles.Officers + "," + ApiRoles.Citizen)]
        public IHttpActionResult Download(int id)
        {
            var rec = AppServices.Query.GetDocument(id);
            if (rec == null || !CanAccess(rec.ApplicationId)) return Fail(System.Net.HttpStatusCode.NotFound, "NOT_FOUND", "कागदपत्र आढळले नाही.");
            var me = Me;
            var r = AppServices.Documents.Open(id, me.Role, me.IsCitizen ? (int?)null : me.UserId, me.Name, Ip, me.IsCitizen ? (int?)me.ApplicationId : null);
            return r.IsFailure ? Fail(r.Error) : FileResult(r.Value.Content, r.Value.ContentType, r.Value.FileName);
        }
    }

    [RoutePrefix("api/v1/applications")]
    public class PaymentsController : ApiBase
    {
        [HttpGet, Route("{id:int}/payment"), Authorize(Roles = ApiRoles.Officers + "," + ApiRoles.Citizen)]
        public IHttpActionResult Get(int id)
        {
            if (!CanAccess(id)) return Fail(System.Net.HttpStatusCode.NotFound, "NOT_FOUND", "अर्ज आढळला नाही.");
            var d = AppServices.Query.GetDetail(id);
            if (d == null) return Fail(System.Net.HttpStatusCode.NotFound, "NOT_FOUND", "अर्ज आढळला नाही.");
            return Ok(new PaymentInfo { PaymentStatus = d.PaymentStatus, PayableAmount = d.PayableAmount, Reference = Me.IsCitizen ? null : d.PaymentReference });
        }

        /// <summary>Manual payment confirmation by OS: validates the UTR/reference and is idempotent (same reference twice = no-op; a different one = 409).</summary>
        [HttpPost, Route("{id:int}/payment/confirm"), Authorize(Roles = Roles.OS)]
        public IHttpActionResult Confirm(int id, [FromBody] PaymentConfirmRequest r)
        {
            if (r == null) return Fail(System.Net.HttpStatusCode.BadRequest, "BAD_REQUEST", "विनंती अवैध आहे.");
            if (!CanAccess(id)) return Fail(System.Net.HttpStatusCode.NotFound, "NOT_FOUND", "अर्ज आढळला नाही.");
            var d = AppServices.Query.GetDetail(id);
            if (d == null) return Fail(System.Net.HttpStatusCode.NotFound, "NOT_FOUND", "अर्ज आढळला नाही.");
            if (r.ExpectedStage != d.Stage || r.ExpectedRowVersion != d.RowVersion) return Fail(System.Net.HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT", "अर्जाची स्थिती बदलली आहे. पुन्हा उघडा.");
            return RunPayment(id, WorkflowActions.OSConfirmPayment, d, r.ExpectedStage, r.ExpectedRowVersion, null, r.PaymentMode);
        }

        [HttpPost, Route("{id:int}/payment/verify"), Authorize(Roles = Roles.OS)]
        public IHttpActionResult Verify(int id, [FromBody] PaymentVerificationRequest r)
        {
            if (r == null) return Fail(System.Net.HttpStatusCode.BadRequest, "BAD_REQUEST", "विनंती अवैध आहे.");
            if (!CanAccess(id)) return Fail(System.Net.HttpStatusCode.NotFound, "NOT_FOUND", "अर्ज आढळला नाही.");
            var d = AppServices.Query.GetDetail(id);
            if (d == null) return Fail(System.Net.HttpStatusCode.NotFound, "NOT_FOUND", "अर्ज आढळला नाही.");
            if (r.ExpectedStage != d.Stage || r.ExpectedRowVersion != d.RowVersion) return Fail(System.Net.HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT", "अर्जाची स्थिती बदलली आहे. पुन्हा उघडा.");
            return RunPayment(id, WorkflowActions.OSConfirmPayment, d, r.ExpectedStage, r.ExpectedRowVersion, null, r.PaymentMode);
        }

        [HttpPost, Route("{id:int}/payment/reject"), Authorize(Roles = Roles.OS)]
        public IHttpActionResult Reject(int id, [FromBody] PaymentRejectionRequest r)
        {
            if (r == null) return Fail(System.Net.HttpStatusCode.BadRequest, "BAD_REQUEST", "विनंती अवैध आहे.");
            if (!CanAccess(id)) return Fail(System.Net.HttpStatusCode.NotFound, "NOT_FOUND", "अर्ज आढळला नाही.");
            var d = AppServices.Query.GetDetail(id);
            if (d == null) return Fail(System.Net.HttpStatusCode.NotFound, "NOT_FOUND", "अर्ज आढळला नाही.");
            if (r.ExpectedStage != d.Stage || r.ExpectedRowVersion != d.RowVersion) return Fail(System.Net.HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT", "अर्जाची स्थिती बदलली आहे. पुन्हा उघडा.");
            return RunPayment(id, WorkflowActions.OSRejectPayment, d, r.ExpectedStage, r.ExpectedRowVersion, r.Remarks, null);
        }

        [HttpPost, Route("{id:int}/payment/submit"), Authorize(Roles = ApiRoles.Citizen)]
        public IHttpActionResult Submit(int id, [FromBody] ApplicantPaymentSubmitRequest r)
        {
            if (r == null || !r.PaymentDate.HasValue || string.IsNullOrWhiteSpace(r.ContentBase64)) return Fail(System.Net.HttpStatusCode.BadRequest, "BAD_REQUEST", "UTR, दिनांक आणि भरणा पुरावा आवश्यक आहे.");
            if (!CanAccess(id)) return Fail(System.Net.HttpStatusCode.NotFound, "NOT_FOUND", "अर्ज आढळला नाही.");
            var d = AppServices.Query.GetDetail(id);
            if (d == null) return Fail(System.Net.HttpStatusCode.NotFound, "NOT_FOUND", "अर्ज आढळला नाही.");
            if (r.ExpectedStage != d.Stage || r.ExpectedRowVersion != d.RowVersion) return Fail(System.Net.HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT", "अर्जाची स्थिती बदलली आहे. पुन्हा उघडा.");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(r.ContentBase64); } catch (FormatException) { return Fail(System.Net.HttpStatusCode.BadRequest, "FILE_ENCODING", "फाईल एन्कोडिंग अवैध आहे."); }
            if (bytes.Length > 5 * 1024 * 1024) return Fail(System.Net.HttpStatusCode.BadRequest, "FILE_TOO_LARGE", "भरणा पुरावा ५ MB पेक्षा मोठा असू शकत नाही.");
            var actor = new SMC.BhoomiMalmatta.Business.Abstractions.ActorContext { Role = Roles.Citizen, UserName = "citizen", ApplicationId = Me.ApplicationId, IpAddress = Ip, CorrelationId = CorrelationId };
            return RunPayment(id, WorkflowActions.CitizenSubmitPayment, d, r.ExpectedStage, r.ExpectedRowVersion, null, null,
                r.PaymentReference, r.PaymentDate, new SMC.BhoomiMalmatta.Business.Abstractions.DocumentUpload { FileName = r.FileName, ContentType = r.ContentType, Content = bytes }, actor);
        }

        private IHttpActionResult RunPayment(int id, string action, ApplicationDetail d, string stage, string rowVersion, string remarks, string mode,
            string reference = null, DateTime? paymentDate = null, SMC.BhoomiMalmatta.Business.Abstractions.DocumentUpload proof = null, SMC.BhoomiMalmatta.Business.Abstractions.ActorContext actor = null)
        {
            var res = WorkflowRunner.Run(new WorkflowInput { ApplicationId = id, Action = action, ExpectedStage = stage, ExpectedRowVersion = rowVersion, Remarks = remarks, PaymentMode = mode, PaymentReference = reference, PaymentDate = paymentDate, PaymentProof = proof }, actor ?? Actor());
            if (res.IsFailure) return Fail(res.Error);
            return Ok(new WorkflowResponse { Stage = res.Value.NewStage, Status = res.Value.NewStatus, PaymentStatus = res.Value.NewPaymentStatus, AlreadyApplied = res.Value.WasAlreadyApplied });
        }

    }

    [RoutePrefix("api/v1/applications")]
    public class CertificatesController : ApiBase
    {
        [HttpPost, Route("{id:int}/certificate/regenerate"), Authorize(Roles = ApiRoles.AdminOrAc)]
        public IHttpActionResult Regenerate(int id)
        {
            if (!CanAccess(id)) return Fail(System.Net.HttpStatusCode.NotFound, "NOT_FOUND", "अर्ज आढळला नाही.");
            var r = AppServices.Certificates.Issue(id, Actor());
            return r.IsFailure ? Fail(r.Error) : (IHttpActionResult)Ok();
        }
    }

    [RoutePrefix("api/v1/dashboard")]
    public class DashboardController : ApiBase
    {
        [HttpGet, Route(""), Authorize(Roles = ApiRoles.Officers)]
        public IHttpActionResult Get() { return Ok(AppServices.Query.GetDashboard(ZoneScope)); }
    }

    [RoutePrefix("api/v1/audit-logs")]
    public class AuditController : ApiBase
    {
        [HttpGet, Route(""), Authorize(Roles = ApiRoles.AdminOrAc)]
        public IHttpActionResult List([FromUri] DateTime? from = null, [FromUri] DateTime? to = null, [FromUri] string action = null, [FromUri] string user = null, [FromUri] int page = 1, [FromUri] int pageSize = 25)
        { return Ok(AppServices.Query.SearchAudit(from, to, action, user, page, pageSize)); }
    }

    [RoutePrefix("api/v1/reports")]
    public class ReportsController : ApiBase
    {
        [HttpGet, Route(""), Authorize(Roles = ApiRoles.Officers)]
        public IHttpActionResult Catalog() { return Ok(ReportBuilder.CatalogForRole(Me.Role).Select(c => new { name = c.Key, title = c.Value })); }

        [HttpGet, Route("{name}"), Authorize(Roles = ApiRoles.Officers)]
        public IHttpActionResult Get(string name, [FromUri] DateTime? from = null, [FromUri] DateTime? to = null)
        {
            if (Me.Role != Roles.Admin && !ReportBuilder.IsApplicationReport(name)) return StatusCode(System.Net.HttpStatusCode.Forbidden);
            var t = ReportBuilder.Build(name, ZoneScope, from, to, Me.Role);
            return t == null ? Fail(System.Net.HttpStatusCode.NotFound, "NOT_FOUND", "अहवाल आढळला नाही.") : (IHttpActionResult)Ok(new ReportDto { Title = t.Title, Headers = t.Headers, Rows = t.Rows });
        }

        [HttpGet, Route("{name}/csv"), Authorize(Roles = ApiRoles.Officers)]
        public IHttpActionResult Csv(string name, [FromUri] DateTime? from = null, [FromUri] DateTime? to = null)
        {
            if (Me.Role != Roles.Admin && !ReportBuilder.IsApplicationReport(name)) return StatusCode(System.Net.HttpStatusCode.Forbidden);
            var t = ReportBuilder.Build(name, ZoneScope, from, to, Me.Role);
            if (t == null) return Fail(System.Net.HttpStatusCode.NotFound, "NOT_FOUND", "अहवाल आढळला नाही.");
            var bytes = new System.Text.UTF8Encoding(true).GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(t.ToCsv())).ToArray();
            return FileResult(bytes, "text/csv", "report-" + FileValidator.SanitizeName(name) + ".csv");
        }
    }

    [RoutePrefix("api/v1/users")]
    public class UsersController : ApiBase
    {
        [HttpGet, Route(""), Authorize(Roles = Roles.Admin)]
        public IHttpActionResult List() { return Ok(AppServices.Query.GetUsers()); }
    }

    [RoutePrefix("api/v1/health")]
    public class HealthController : ApiBase
    {
        [HttpGet, Route(""), AllowAnonymous]
        public IHttpActionResult Get()
        {
            if (!AppServices.IsHealthy) return Content(System.Net.HttpStatusCode.ServiceUnavailable, new { status = "Unhealthy", detail = AppServices.StartupError });
            try
            {
                AppServices.Masters.GetNumberConfig();
                if (User != null && User.IsInRole(Roles.Admin))
                {
                    var configured = AppServices.Setting("Payment.QrImagePath", "");
                    var root = System.Web.Hosting.HostingEnvironment.MapPath("~/");
                    var paymentQr = ApplicantQrAvailability.DiagnosticStatus(configured, root, System.IO.File.Exists);
                    return Ok(new { status = "Healthy", database = "SMC_BhoomiMalmatta", paymentQr = paymentQr });
                }
                return Ok(new { status = "Healthy", database = "SMC_BhoomiMalmatta" });
            }
            catch (Exception) { return Content(System.Net.HttpStatusCode.ServiceUnavailable, new { status = "Unhealthy", detail = "database not reachable" }); }
        }
    }
}
