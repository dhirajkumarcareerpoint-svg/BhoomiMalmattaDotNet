using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Filters;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Web
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            config.MapHttpAttributeRoutes();            // every route lives under /api/v1/ via [RoutePrefix]
            config.Formatters.Remove(config.Formatters.XmlFormatter);
            var json = config.Formatters.JsonFormatter;
            json.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            json.SerializerSettings.NullValueHandling = NullValueHandling.Include;
            json.SerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
            config.Formatters.JsonFormatter.SupportedMediaTypes.Clear();
            config.Formatters.JsonFormatter.SupportedMediaTypes.Add(new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"));
            config.MessageHandlers.Add(new RequireCustomHeaderHandler());
            config.Filters.Add(new ApiExceptionFilterAttribute());
            // Expose detailed error information only when the application is running with debug enabled (local development).
            config.IncludeErrorDetailPolicy = (System.Web.HttpContext.Current != null && System.Web.HttpContext.Current.IsDebuggingEnabled)
                ? IncludeErrorDetailPolicy.Always
                : IncludeErrorDetailPolicy.Never;
        }
    }

    /// <summary>
    /// Cookie-authenticated API: state-changing requests must carry the custom header X-SMC-Request. A cross-site form or image cannot send it
    /// (and CORS is not enabled), which blocks CSRF against the API.
    /// </summary>
    public sealed class RequireCustomHeaderHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var unsafeMethod = request.Method == HttpMethod.Post || request.Method == HttpMethod.Put || request.Method == HttpMethod.Delete || request.Method.Method == "PATCH";
            if (unsafeMethod && !request.Headers.Contains("X-SMC-Request"))
                return Task.FromResult(request.CreateResponse(HttpStatusCode.BadRequest, new { code = "CSRF_HEADER_MISSING", message = "Missing X-SMC-Request header." }));
            return base.SendAsync(request, cancellationToken);
        }
    }

    public sealed class ApiExceptionFilterAttribute : ExceptionFilterAttribute
    {
        public override void OnException(HttpActionExecutedContext ctx)
        {
            var id = System.Guid.NewGuid().ToString("N").Substring(0, 12);
            ErrorLogger.Log(id, ctx.Request.RequestUri.AbsolutePath, ctx.Exception);
            ctx.Response = ctx.Request.CreateResponse(HttpStatusCode.InternalServerError, new { code = "SERVER_ERROR", message = "सर्व्हर त्रुटी. संदर्भ: " + id });
        }
    }
}
