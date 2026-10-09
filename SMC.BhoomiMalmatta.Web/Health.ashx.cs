using System;
using System.Web;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Web
{
    /// <summary>GET /Health.ashx: 200 when the start-up guard passed and the master data can be read; otherwise 503. Exposes no connection details.</summary>
    public class HealthHandler : IHttpHandler
    {
        public bool IsReusable { get { return true; } }

        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "application/json";
            context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            if (!AppServices.IsHealthy) { Write(context, 503, "Unhealthy", AppServices.StartupError); return; }
            try
            {
                var cfg = AppServices.Masters.GetNumberConfig(); // one small read-only query
                Write(context, 200, "Healthy", cfg == null ? "database reachable; no active number configuration" : "database reachable");
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(context.Items["CorrelationId"] as string ?? "health", "Health", ex);
                Write(context, 503, "Unhealthy", "database not reachable");
            }
        }

        private static void Write(HttpContext c, int code, string status, string detail)
        {
            c.Response.StatusCode = code;
            c.Response.Write("{\"status\":\"" + status + "\",\"database\":\"SMC_BhoomiMalmatta\",\"detail\":\"" + HttpUtility.JavaScriptStringEncode(detail ?? "") + "\"}");
        }
    }
}
