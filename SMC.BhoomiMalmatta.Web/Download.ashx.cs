using System;
using System.Web;
using System.Web.SessionState;
using SMC.BhoomiMalmatta.Business.Security;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Web.Security;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Web
{
    /// <summary>
    /// The only door to uploaded files and generated PDFs. Authorization: a citizen ticket (own application only) or an officer ticket
    /// (zone-scoped when the officer has a zone). Physical paths are never exposed; files are always sent as attachments.
    /// </summary>
    public class DownloadHandler : IHttpHandler
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext ctx)
        {
            var p = ctx.User as SmcPrincipal;
            if (p == null) { Deny(ctx, 401); return; }
            var me = p.Me;
            var type = (ctx.Request.QueryString["type"] ?? string.Empty).ToLowerInvariant();
            int id;
            int.TryParse(ctx.Request.QueryString["id"], out id);
            if (type == "xferdoc" || type == "regdoc" || type == "xfercert")
            {
                if (me.IsCitizen || me.Role != Roles.Admin) { Deny(ctx, 403); return; }
                ServeRegister(ctx, type, id, me);
                return;
            }
            int appId;
            if (me.IsCitizen) appId = me.ApplicationId;
            else if (!int.TryParse(ctx.Request.QueryString["appId"], out appId) && type != "doc" && type != "zdoc") { Deny(ctx, 400); return; }

            if (type == "doc")
            {
                var rec = AppServices.Query.GetDocument(id);
                if (rec == null) { Deny(ctx, 404); return; }
                if (!ZoneAllowed(me, rec.ApplicationId)) { Deny(ctx, 403); return; }
                var r = AppServices.Documents.Open(id, me.Role, me.IsCitizen ? (int?)null : me.UserId, me.Name, ctx.Request.UserHostAddress, me.IsCitizen ? (int?)me.ApplicationId : null);
                if (r.IsFailure) { Deny(ctx, r.Error.Code == "NOT_FOUND" || r.Error.Code == "FILE_MISSING" ? 404 : 403); return; }
                var inline = ctx.Request.QueryString["inline"] == "1" && (r.Value.ContentType == "application/pdf" || r.Value.ContentType == "image/jpeg" || r.Value.ContentType == "image/png");
                Send(ctx, r.Value.Content, r.Value.ContentType, r.Value.FileName, inline);
                return;
            }

            if (type == "zdoc")
            {
                var rec = AppServices.Query.GetZoneDocument(id);
                if (rec == null || (!me.IsCitizen && (me.Role == Roles.Staff || Array.IndexOf(Roles.OfficerRoles, me.Role) < 0)) || !ZoneAllowed(me, rec.ApplicationId)) { Deny(ctx, 404); return; }
                try
                {
                    var bytes = AppServices.Files.Read(rec.RelativePath);
                    if (!me.IsCitizen && me.UserId > 0)
                        AppServices.Audit.Write(new SMC.BhoomiMalmatta.Business.Abstractions.AuditEntry { Action = "Payment Proof Viewed", EntityName = "DemandApplicationZoneDocument", EntityId = id, UserId = me.UserId, UserName = me.Name, IpAddress = ctx.Request.UserHostAddress, TimestampUtc = DateTime.UtcNow });
                    var inline = ctx.Request.QueryString["inline"] == "1" && (rec.ContentType == "application/pdf" || rec.ContentType == "image/jpeg" || rec.ContentType == "image/png");
                    Send(ctx, bytes, rec.ContentType, rec.FileName, inline);
                }
                catch (Exception) { Deny(ctx, 404); }
                return;
            }

            if (!ZoneAllowed(me, appId)) { Deny(ctx, 403); return; }
            var d = AppServices.Query.GetDetail(appId);
            if (d == null) { Deny(ctx, 404); return; }
            var cfg = AppServices.Masters.GetPermitConfig();
            var no = FileValidator.SanitizeName(d.ApplicationNumber);
            switch (type)
            {
                case "ack": Send(ctx, AppServices.Pdf.Acknowledgement(d, cfg), "application/pdf", "Acknowledgement-" + no + ".pdf"); break;
                case "application": Send(ctx, AppServices.Pdf.SubmittedApplication(d, cfg), "application/pdf", "Application-" + no + ".pdf"); break;
                case "receipt":
                    if (d.PaymentStatus != PaymentStatuses.PaymentDone) { Deny(ctx, 404); return; }
                    Send(ctx, AppServices.Pdf.Receipt(d, cfg), "application/pdf", "Receipt-" + no + ".pdf"); break;
                case "cert":
                    // Approved → always downloadable: a letter that failed to generate at approval time is generated now.
                    var correlation = ctx.Items["CorrelationId"] as string;
                    var actor = new SMC.BhoomiMalmatta.Business.Abstractions.ActorContext
                    {
                        UserId = me.IsCitizen ? (int?)null : me.UserId, UserName = me.IsCitizen ? "citizen" : me.Name, Role = me.Role, ZoneNumber = me.ZoneNumber,
                        ApplicationId = me.IsCitizen ? (int?)me.ApplicationId : null, IpAddress = ctx.Request.UserHostAddress, CorrelationId = correlation
                    };
                    SMC.BhoomiMalmatta.Models.Common.Result<SMC.BhoomiMalmatta.Business.Documents.DocumentFile> cert;
                    try { cert = AppServices.Certificates.OpenForDownload(appId, me.Role, me.IsCitizen ? (int?)me.ApplicationId : null, actor); }
                    catch (Exception ex) { ErrorLogger.Log(correlation, "CertificateDownload", ex); Deny(ctx, 404); return; }
                    if (cert.IsFailure)
                    {
                        if (cert.Error.Code != "NOT_APPROVED" && cert.Error.Code != "FORBIDDEN") ErrorLogger.Log(correlation, "CertificateDownload", new InvalidOperationException(cert.Error.Code));
                        Deny(ctx, cert.Error.Code == "FORBIDDEN" ? 403 : 404); return;
                    }
                    Send(ctx, cert.Value.Content, "application/pdf", "Certificate-" + no + ".pdf"); break;
                default: Deny(ctx, 400); break;
            }
        }

        /// <summary>Register documents and the transfer certificate: officers only; every access is audited.</summary>
        private static void ServeRegister(HttpContext ctx, string type, int id, SmcIdentity me)
        {
            byte[] bytes; string name, mime;
            if (type == "xfercert")
            {
                var t = AppServices.Register.GetTransfer(id);
                if (t == null || t.Stage != TransferStages.Approved) { Deny(ctx, 404); return; }
                var p = AppServices.Register.GetProperty(t.PropertyId);
                if (p == null) { Deny(ctx, 404); return; }
                bytes = PdfGenerator.TransferCertificate(t, p, AppServices.Masters.GetPermitConfig());
                name = "TransferCertificate-" + p.PropertyCode + ".pdf"; mime = "application/pdf";
            }
            else
            {
                int transferId;
                var d = type == "xferdoc" ? AppServices.Register.GetTransferDocument(id, out transferId) : AppServices.Register.GetEntityDocument(id);
                if (d == null) { Deny(ctx, 404); return; }
                try { bytes = AppServices.Files.Read(d.RelativePath); } catch (Exception) { Deny(ctx, 404); return; }
                name = d.FileName; mime = d.ContentType;
                try { AppServices.Audit.Write(new SMC.BhoomiMalmatta.Business.Abstractions.AuditEntry { Action = "Document Downloaded", EntityName = type == "xferdoc" ? "GalaTransferDocument" : "Document", EntityId = id, UserId = me.UserId, UserName = me.Name, IpAddress = ctx.Request.UserHostAddress, TimestampUtc = DateTime.UtcNow }); }
                catch (Exception ex) { ErrorLogger.Log("download", "audit", ex); }
            }
            Send(ctx, bytes, mime, name);
        }

        /// <summary>Officers with a ZoneNumber may only open applications of their zone (Admin: all). Citizens: only their own application.</summary>
        private static bool ZoneAllowed(SmcIdentity me, int applicationId)
        {
            if (me.IsCitizen) return CitizenTicketAccess.Allows(me.ApplicationId, applicationId);
            if (!me.ZoneNumber.HasValue || me.Role == Roles.Admin) return true;
            var d = AppServices.Query.GetDetail(applicationId);
            return d != null && d.Zone == me.ZoneNumber.Value.ToString();
        }

        private static void Send(HttpContext ctx, byte[] content, string contentType, string fileName)
        { Send(ctx, content, contentType, fileName, false); }

        private static void Send(HttpContext ctx, byte[] content, string contentType, string fileName, bool inline)
        {
            var safe = FileValidator.SanitizeName(fileName);
            var ascii = System.Text.RegularExpressions.Regex.Replace(safe, @"[^\x20-\x7E]", "_").Replace("\"", "_");
            ctx.Response.Clear();
            ctx.Response.ContentType = contentType;
            ctx.Response.AddHeader("Content-Disposition", (inline ? "inline" : "attachment") + "; filename=\"" + ascii + "\"; filename*=UTF-8''" + Uri.EscapeDataString(safe));
            ctx.Response.AddHeader("X-Content-Type-Options", "nosniff");
            ctx.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            ctx.Response.Cache.SetNoStore();
            ctx.Response.BinaryWrite(content);
        }

        private static void Deny(HttpContext ctx, int code)
        {
            ctx.Response.StatusCode = code; ctx.Response.TrySkipIisCustomErrors = true;
            ctx.Response.ContentType = "text/plain; charset=utf-8";
            ctx.Response.Write(code == 404 ? "Not found" : code == 401 ? "Sign in required" : code == 400 ? "Bad request" : "Forbidden");
        }
    }
}
