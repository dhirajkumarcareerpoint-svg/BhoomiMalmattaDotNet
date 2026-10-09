using System;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Business.Security;

namespace SMC.BhoomiMalmatta.Web.Services
{
    /// <summary>Document list and upload box shared by the register pages. Files go through the same signature/size validation as application documents.</summary>
    public static class RegisterUi
    {
        public const string TransferScope = "transfer";
        public static readonly string[] TransferKinds = { "IdentityProof", "TransferDeed", "NOC", "Other" };

        public static void Documents(Control host, string scope, int entityId, bool canUpload, ActorContext actor, string redirectUrl, Action<string> fail)
        {
            var isTransfer = scope == TransferScope;
            var docs = isTransfer ? AppServices.Register.TransferDocuments(entityId) : AppServices.Register.EntityDocuments(scope, entityId);
            var sb = new StringBuilder("<div class=\"card mt\"><h3 style=\"margin-top:0\">कागदपत्रे</h3>");
            if (docs.Count == 0) sb.Append("<p class=\"muted\">कागदपत्रे जोडलेली नाहीत.</p>");
            else
            {
                sb.Append("<div class=\"tscroll\"><table class=\"tbl\"><thead><tr><th>फाईल</th><th>प्रकार</th><th>आकार</th><th>दिनांक</th></tr></thead><tbody>");
                foreach (var d in docs)
                {
                    var url = VirtualPathUtility.ToAbsolute("~/Download.ashx") + "?type=" + (isTransfer ? "xferdoc" : "regdoc") + "&id=" + d.Id;
                    sb.Append("<tr><td><a href=\"").Append(HttpUtility.HtmlAttributeEncode(url)).Append("\">").Append(HttpUtility.HtmlEncode(d.FileName)).Append("</a></td><td>")
                      .Append(HttpUtility.HtmlEncode(isTransfer ? d.Kind : "-")).Append("</td><td>").Append((d.Size / 1024) + " KB").Append("</td><td>").Append(d.CreatedAt.ToString("dd/MM/yyyy")).Append("</td></tr>");
                }
                sb.Append("</tbody></table></div>");
            }
            host.Controls.Add(new LiteralControl(sb.ToString()));
            if (canUpload)
            {
                host.Controls.Add(new LiteralControl("<div class=\"two\"><div><label class=\"lb\" for=\"fuDoc\">नवीन कागदपत्र (PDF / JPG / PNG, कमाल ५ MB)</label>"));
                var fu = new FileUpload { ID = "fuDoc", CssClass = "in" }; host.Controls.Add(fu);
                host.Controls.Add(new LiteralControl("</div>"));
                DropDownList kind = null;
                if (isTransfer)
                {
                    host.Controls.Add(new LiteralControl("<div><label class=\"lb\" for=\"ddlDocKind\">प्रकार</label>"));
                    kind = new DropDownList { ID = "ddlDocKind", CssClass = "in" };
                    foreach (var k in TransferKinds) kind.Items.Add(k);
                    host.Controls.Add(kind); host.Controls.Add(new LiteralControl("</div>"));
                }
                host.Controls.Add(new LiteralControl("</div><div class=\"btns\">"));
                var btn = new Button { ID = "btnUpload", Text = "कागदपत्र जोडा", CssClass = "btn o", CausesValidation = false };
                btn.Click += (s, a) =>
                {
                    if (!fu.HasFile) { fail("कृपया फाईल निवडा."); return; }
                    var bytes = fu.FileBytes;
                    var check = FileValidator.Validate(fu.FileName, fu.PostedFile.ContentType, bytes);
                    if (check.IsFailure) { fail(check.Error.Message); return; }
                    var rel = "registers/" + scope + "/" + entityId + "/" + check.Value.StoredName;
                    var stored = new StoredDocument { DocumentType = isTransfer ? kind.SelectedValue : scope, OriginalFileName = check.Value.SafeDisplayName, StoredFileName = check.Value.StoredName, RelativePath = rel, ContentType = check.Value.ContentType, SizeBytes = bytes.LongLength };
                    AppServices.Files.Save(rel, bytes);
                    var r = isTransfer ? AppServices.Register.AddTransferDocument(entityId, kind.SelectedValue, stored, actor) : AppServices.Register.AddEntityDocument(scope, entityId, stored, actor);
                    if (r.IsFailure) { AppServices.Files.Delete(rel); fail(r.Error.Message); return; }
                    HttpContext.Current.Response.Redirect(redirectUrl, false);
                    HttpContext.Current.ApplicationInstance.CompleteRequest();
                };
                host.Controls.Add(btn);
                host.Controls.Add(new LiteralControl("</div>"));
            }
            host.Controls.Add(new LiteralControl("</div>"));
        }
    }
}
