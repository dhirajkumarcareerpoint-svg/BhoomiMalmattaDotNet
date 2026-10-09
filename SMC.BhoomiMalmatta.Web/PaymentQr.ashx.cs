using System;
using System.IO;
using System.Web;
using SMC.BhoomiMalmatta.Business.Citizen;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Web
{
    /// <summary>Streams only the single QR file configured by the server. The request cannot select a path.</summary>
    public sealed class PaymentQrHandler : IHttpHandler
    {
        public bool IsReusable { get { return true; } }
        public void ProcessRequest(HttpContext context)
        {
            context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            context.Response.Cache.SetNoStore();
            context.Response.Cache.SetExpires(DateTime.UtcNow.AddDays(-1));
            var configured = AppServices.Setting("Payment.QrImagePath", "");
            if (string.IsNullOrWhiteSpace(configured)) { context.Response.StatusCode = 404; return; }
            var root = context.Server.MapPath("~/");
            var asset = PaymentQrAsset.Load(configured, root, File.Exists, File.OpenRead);
            if (asset.StatusCode != 200) { context.Response.StatusCode = 404; return; }
            context.Response.ContentType = asset.ContentType;
            if (string.Equals(context.Request.QueryString["download"], "1", StringComparison.Ordinal))
                context.Response.AddHeader("Content-Disposition", "attachment; filename=payment-qr" + asset.FileExtension);
            context.Response.BinaryWrite(asset.Content);
        }
    }
}
