using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Models.Common;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Web.Security
{
    public enum FieldKind { Text, TextArea, Decimal, Int, Date, Select }

    public sealed class Field
    {
        public Field(string key, string label, FieldKind kind) { Key = key; Label = label; Kind = kind; Options = new List<ListItem>(); }
        public string Key { get; private set; }
        public string Label { get; private set; }
        public FieldKind Kind { get; private set; }
        public bool Required { get; set; }
        public int MaxLength { get; set; }
        public bool LockedOnEdit { get; set; }
        public List<ListItem> Options { get; set; }
    }

    /// <summary>
    /// Shared list / detail / create-edit page for the Department registers. Controls are created in code with fixed IDs (so view state and
    /// postbacks work), every write re-checks the role on the server, and a one-time form token stops repeated submits.
    /// </summary>
    public abstract class ModulePage<T> : DeptPage where T : class
    {
        protected const int PageSize = 20;
        private Panel _root;
        private PlaceHolder _detail;
        private readonly Dictionary<string, Control> _inputs = new Dictionary<string, Control>();
        private readonly List<string> _errors = new List<string>();
        private HiddenField _token;
        private TextBox _txtSearch; private DropDownList _ddlStatus, _ddlProperty, _ddlExtra;
        private Button _btnSave, _btnPrev, _btnNext;
        private Literal _litError, _litList, _litPager;
        private IList<PropertyRef> _props;

        protected T Entity;
        protected bool IsNew;
        protected bool IsDetail;
        protected int EntityId;
        protected Panel Root { get { return _root; } }

        // ---- configuration supplied by each module ----
        protected abstract string Heading { get; }
        protected abstract string EntityName { get; }
        protected abstract string[] Headers { get; }
        protected abstract List<Field> BuildFields(bool isNew);
        protected abstract PagedResult<T> Query(string search, string status, int? propertyId, int page);
        protected abstract T LoadEntity(int id);
        protected abstract int IdOf(T e);
        protected abstract string[] Cells(T e);
        protected abstract Dictionary<string, string> Values(T e);
        protected abstract Dictionary<string, string> Defaults();
        protected abstract Result<int> Save(int id, Dictionary<string, string> v);
        protected virtual string[] WriteRoles { get { return new[] { Roles.Admin, Roles.JE, Roles.OS, Roles.AssistantCommissioner }; } }
        protected virtual List<ListItem> StatusOptions { get { return null; } }
        protected virtual string StatusCaption { get { return "स्थिती"; } }
        protected virtual List<ListItem> ExtraOptions { get { return null; } }
        protected virtual string ExtraCaption { get { return "प्रकार"; } }
        protected string ExtraValue { get { return _ddlExtra == null ? null : _ddlExtra.SelectedValue; } }
        protected virtual bool PropertyFilter { get { return true; } }
        protected virtual string SearchCaption { get { return "शोध"; } }
        protected virtual bool CanEdit(T e) { return true; }
        protected virtual bool AllowCreate { get { return true; } }
        protected virtual string DetailTitle(T e) { return Heading; }
        protected virtual void BuildExtras(Control host, T e) { }
        protected virtual string PageUrl { get { return Request.AppRelativeCurrentExecutionFilePath; } }

        protected bool CanWrite { get { return Me != null && Array.IndexOf(WriteRoles, Me.Role) >= 0; } }
        protected string Url(string query) { return ResolveUrl(PageUrl) + (string.IsNullOrEmpty(query) ? "" : "?" + query); }

        protected List<ListItem> PropertyOptions(bool withBlank)
        {
            var l = new List<ListItem>();
            if (withBlank) l.Add(new ListItem("-- निवडा --", string.Empty));
            foreach (var p in Props()) l.Add(new ListItem(p.Label, p.Id.ToString()));
            return l;
        }
        protected IList<PropertyRef> Props() { return _props ?? (_props = AppServices.Register.PropertyRefs()); }
        protected static List<ListItem> Opts(IEnumerable<string> values, Func<string, string> label, bool withBlank)
        {
            var l = new List<ListItem>();
            if (withBlank) l.Add(new ListItem("-- निवडा --", string.Empty));
            foreach (var v in values) l.Add(new ListItem(label(v), v));
            return l;
        }

        // ---- value helpers (record parse errors) ----
        protected string S(Dictionary<string, string> v, string key) { string s; return v.TryGetValue(key, out s) && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null; }
        protected decimal? D(Dictionary<string, string> v, string key, string label)
        {
            var s = S(v, key); if (s == null) return null;
            decimal d; if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out d) && d >= 0 && d < 1000000000000m) return Math.Round(d, 2);
            _errors.Add(label + " वैध रक्कम / संख्या असावी."); return null;
        }
        protected int? I(Dictionary<string, string> v, string key, string label)
        {
            var s = S(v, key); if (s == null) return null;
            int i; if (int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out i)) return i;
            _errors.Add(label + " पूर्ण संख्या असावी."); return null;
        }
        protected DateTime? Dt(Dictionary<string, string> v, string key, string label)
        {
            var s = S(v, key); if (s == null) return null;
            DateTime d; if (DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) && d.Year >= 1900 && d.Year <= 2200) return d;
            _errors.Add(label + " वैध दिनांक असावा."); return null;
        }
        protected void AddError(string m) { _errors.Add(m); }
        protected List<string> Errors { get { return _errors; } }
        protected static string DateText(DateTime? d) { return d.HasValue ? d.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty; }
        protected static string NumText(decimal? d) { return d.HasValue ? d.Value.ToString("0.##", CultureInfo.InvariantCulture) : string.Empty; }
        protected static string Money(decimal? d) { return d.HasValue ? d.Value.ToString("N2") : "-"; }
        protected static string Day(DateTime? d) { return d.HasValue ? d.Value.ToString("dd/MM/yyyy") : "-"; }
        protected string PropertyLabel(int? id) { var p = id.HasValue ? Props().FirstOrDefault(x => x.Id == id.Value) : null; return p == null ? "-" : p.Label; }

        protected static void Lit(Control host, string html) { host.Controls.Add(new LiteralControl(html)); }

        // ---- lifecycle ----
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            var holder = Master == null ? null : Master.FindControl("cphMain");
            if (holder == null) throw new InvalidOperationException("Master content placeholder missing.");
            _root = new Panel { ID = "pnlModule" };
            holder.Controls.Add(_root);
            Lit(_root, "<h2>" + Enc(Heading) + "</h2>");

            var idText = Request.QueryString["id"];
            IsNew = Request.QueryString["new"] == "1";
            if (!string.IsNullOrEmpty(idText))
            {
                int id; if (!int.TryParse(idText, NumberStyles.None, CultureInfo.InvariantCulture, out id)) { Response.Redirect("~/NotFound.aspx", true); return; }
                EntityId = id; Entity = LoadEntity(id);
                if (Entity == null) { Response.Redirect("~/NotFound.aspx", true); return; }
                IsDetail = true;
            }
            if (IsNew && (!CanWrite || !AllowCreate)) { Response.Redirect("~/AccessDenied.aspx", true); return; }

            _litError = new Literal { ID = "litError" };
            _root.Controls.Add(_litError);
            if (IsDetail || IsNew) BuildDetail(); else BuildList();
        }

        private void BuildList()
        {
            Lit(_root, "<div class=\"card\"><div class=\"filters\">");
            Lit(_root, "<div><label class=\"lb\" for=\"" + "txtSearch" + "\">" + Enc(SearchCaption) + "</label>");
            _txtSearch = new TextBox { ID = "txtSearch", CssClass = "in", MaxLength = 100 }; _root.Controls.Add(_txtSearch); Lit(_root, "</div>");
            var so = StatusOptions;
            if (so != null)
            {
                Lit(_root, "<div><label class=\"lb\" for=\"ddlStatus\">" + Enc(StatusCaption) + "</label>");
                _ddlStatus = new DropDownList { ID = "ddlStatus", CssClass = "in" }; _ddlStatus.Items.Add(new ListItem("सर्व", string.Empty)); foreach (var i in so) _ddlStatus.Items.Add(i);
                _root.Controls.Add(_ddlStatus); Lit(_root, "</div>");
            }
            var xo = ExtraOptions;
            if (xo != null)
            {
                Lit(_root, "<div><label class=\"lb\" for=\"ddlExtra\">" + Enc(ExtraCaption) + "</label>");
                _ddlExtra = new DropDownList { ID = "ddlExtra", CssClass = "in" }; _ddlExtra.Items.Add(new ListItem("सर्व", string.Empty)); foreach (var i in xo) _ddlExtra.Items.Add(i);
                _root.Controls.Add(_ddlExtra); Lit(_root, "</div>");
            }
            if (PropertyFilter)
            {
                Lit(_root, "<div><label class=\"lb\" for=\"ddlProperty\">मालमत्ता</label>");
                _ddlProperty = new DropDownList { ID = "ddlProperty", CssClass = "in" }; _ddlProperty.Items.Add(new ListItem("सर्व", string.Empty)); foreach (var i in PropertyOptions(false)) _ddlProperty.Items.Add(i);
                _root.Controls.Add(_ddlProperty); Lit(_root, "</div>");
            }
            Lit(_root, "</div><div class=\"btns\">");
            var go = new Button { ID = "btnFilter", Text = "शोधा", CssClass = "btn", CausesValidation = false }; go.Click += (s, a) => { PageNo = 1; };
            var clear = new Button { ID = "btnClear", Text = "साफ करा", CssClass = "btn o", CausesValidation = false }; clear.Click += (s, a) => Response.Redirect(Url(null));
            _root.Controls.Add(go); _root.Controls.Add(clear);
            if (CanWrite && AllowCreate) Lit(_root, "<a class=\"btn\" style=\"text-align:center;text-decoration:none\" href=\"" + Enc(Url("new=1")) + "\">+ नवीन नोंद</a>");
            Lit(_root, "</div></div><div class=\"card mt\">");
            _litList = new Literal { ID = "litList" }; _root.Controls.Add(_litList);
            Lit(_root, "<div class=\"pager\">");
            _btnPrev = new Button { ID = "btnPrev", Text = "← मागील", CssClass = "btn o", CausesValidation = false }; _btnPrev.Click += (s, a) => { PageNo = Math.Max(1, PageNo - 1); };
            _litPager = new Literal { ID = "litPager" };
            _btnNext = new Button { ID = "btnNext", Text = "पुढील →", CssClass = "btn o", CausesValidation = false }; _btnNext.Click += (s, a) => { PageNo = PageNo + 1; };
            _root.Controls.Add(_btnPrev); _root.Controls.Add(_litPager); _root.Controls.Add(_btnNext);
            Lit(_root, "</div></div>");
            if (!IsPostBack)
            {
                var q = Request.QueryString;
                if (_ddlStatus != null && q["status"] != null && _ddlStatus.Items.FindByValue(q["status"]) != null) _ddlStatus.SelectedValue = q["status"];
                if (_ddlExtra != null && q["type"] != null && _ddlExtra.Items.FindByValue(q["type"]) != null) _ddlExtra.SelectedValue = q["type"];
                if (_ddlProperty != null && q["property"] != null && _ddlProperty.Items.FindByValue(q["property"]) != null) _ddlProperty.SelectedValue = q["property"];
                if (_txtSearch != null && q["q"] != null) _txtSearch.Text = q["q"].Length > 100 ? q["q"].Substring(0, 100) : q["q"];
            }
        }

        private int PageNo { get { return ViewState["pg"] == null ? 1 : (int)ViewState["pg"]; } set { ViewState["pg"] = value; } }

        private void BuildDetail()
        {
            var canEdit = CanWrite && (IsNew || CanEdit(Entity));
            var fields = BuildFields(IsNew);
            Lit(_root, "<p><a href=\"" + Enc(Url(null)) + "\">← यादीकडे परत</a></p>");
            if (IsDetail) Lit(_root, "<div class=\"card\"><h3 style=\"margin-top:0\">" + Enc(DetailTitle(Entity)) + "</h3>");
            else Lit(_root, "<div class=\"card\"><h3 style=\"margin-top:0\">नवीन नोंद</h3>");
            if (canEdit)
            {
                _token = new HiddenField { ID = "hfToken" }; _root.Controls.Add(_token);
                Lit(_root, "<div class=\"two\">");
                foreach (var f in fields)
                {
                    if (f.LockedOnEdit && IsDetail) { Lit(_root, "<div><span class=\"lb\">" + Enc(f.Label) + "</span><div class=\"in\" style=\"background:#f6f1f8\">" + Enc(DisplayOf(f)) + "</div></div>"); continue; }
                    Lit(_root, f.Kind == FieldKind.TextArea ? "<div style=\"grid-column:1/-1\">" : "<div>");
                    Lit(_root, "<label class=\"lb\" for=\"f_" + f.Key + "\">" + Enc(f.Label) + (f.Required ? " *" : "") + "</label>");
                    Control c = MakeInput(f); _inputs[f.Key] = c; _root.Controls.Add(c);
                    Lit(_root, "</div>");
                }
                Lit(_root, "</div><div class=\"btns\">");
                _btnSave = new Button { ID = "btnSave", Text = "जतन करा", CssClass = "btn", CausesValidation = false, UseSubmitBehavior = true };
                _btnSave.Click += OnSave; _root.Controls.Add(_btnSave);
                Lit(_root, "</div></div>");
                if (!IsPostBack)
                {
                    _token.Value = Guid.NewGuid().ToString("N");
                    var vals = IsNew ? Defaults() : Values(Entity);
                    foreach (var f in fields) { Control c; string v; if (_inputs.TryGetValue(f.Key, out c) && vals.TryGetValue(f.Key, out v)) SetInput(c, v); }
                }
            }
            else
            {
                var vals = Values(Entity);
                var sb = new StringBuilder("<dl class=\"kv\">");
                foreach (var f in fields) sb.Append("<dt>").Append(Enc(f.Label)).Append("</dt><dd>").Append(Enc(DisplayOf(f, vals))).Append("</dd>");
                sb.Append("</dl>");
                if (!CanWrite) sb.Append("<p class=\"muted mt\">ही नोंद केवळ वाचनीय आहे.</p>");
                Lit(_root, sb + "</div>");
            }
            if (IsDetail)
            {
                _detail = new PlaceHolder { ID = "phExtras" }; _root.Controls.Add(_detail);
                BuildExtras(_detail, Entity);
                RenderAudit();
            }
        }

        private string DisplayOf(Field f) { return DisplayOf(f, Values(Entity)); }
        private static string DisplayOf(Field f, Dictionary<string, string> vals)
        {
            string v; if (!vals.TryGetValue(f.Key, out v) || string.IsNullOrEmpty(v)) return "-";
            if (f.Kind == FieldKind.Select) { var o = f.Options.FirstOrDefault(x => x.Value == v); return o == null ? v : o.Text; }
            if (f.Kind == FieldKind.Date) { DateTime d; if (DateTime.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return d.ToString("dd/MM/yyyy"); }
            return v;
        }

        private static Control MakeInput(Field f)
        {
            var id = "f_" + f.Key;
            if (f.Kind == FieldKind.Select)
            {
                var d = new DropDownList { ID = id, CssClass = "in" };
                foreach (var o in f.Options) d.Items.Add(o);
                return d;
            }
            var t = new TextBox { ID = id, CssClass = "in" };
            if (f.MaxLength > 0) t.MaxLength = f.MaxLength;
            if (f.Kind == FieldKind.TextArea) { t.TextMode = TextBoxMode.MultiLine; t.Rows = 3; }
            else if (f.Kind == FieldKind.Date) t.TextMode = TextBoxMode.Date;
            else if (f.Kind == FieldKind.Decimal) { t.Attributes["inputmode"] = "decimal"; t.MaxLength = 15; }
            else if (f.Kind == FieldKind.Int) { t.Attributes["inputmode"] = "numeric"; t.MaxLength = 6; }
            return t;
        }

        private static void SetInput(Control c, string v)
        {
            var t = c as TextBox; if (t != null) { t.Text = v; return; }
            var d = c as DropDownList; if (d != null && d.Items.FindByValue(v ?? "") != null) d.SelectedValue = v ?? "";
        }

        private void OnSave(object sender, EventArgs e)
        {
            if (!CanWrite) { Response.Redirect("~/AccessDenied.aspx", true); return; }
            if (IsDetail && !CanEdit(Entity)) { Fail(new[] { "ही नोंद आता बदलता येत नाही." }); return; }
            var used = Session["used-tokens"] as HashSet<string>;
            if (used == null) { used = new HashSet<string>(); Session["used-tokens"] = used; }
            if (string.IsNullOrEmpty(_token.Value) || used.Contains(_token.Value)) { Fail(new[] { "हा फॉर्म आधीच सादर झाला आहे. कृपया यादी तपासा." }); return; }
            var v = new Dictionary<string, string>();
            foreach (var kv in _inputs)
            {
                var t = kv.Value as TextBox; var d = kv.Value as DropDownList;
                v[kv.Key] = t != null ? t.Text : d != null ? d.SelectedValue : null;
            }
            var fields = BuildFields(IsNew);
            _errors.Clear();
            foreach (var f in fields)
            {
                if (!_inputs.ContainsKey(f.Key)) continue;
                var s = S(v, f.Key);
                if (f.Required && s == null) _errors.Add(f.Label + " आवश्यक आहे.");
                if (f.Kind == FieldKind.Select && s != null && !f.Options.Any(o => o.Value == s)) _errors.Add(f.Label + ": अवैध निवड.");
            }
            if (_errors.Count > 0) { Fail(_errors); return; }
            Result<int> r;
            try { r = Save(IsNew ? 0 : EntityId, v); }
            catch (Exception ex) { ErrorLogger.Log(CorrelationId ?? "n/a", Request.Path, ex); Fail(new[] { "नोंद जतन करता आली नाही. कृपया पुन्हा प्रयत्न करा. संदर्भ: " + (CorrelationId ?? "n/a") }); return; }
            if (r.IsFailure) { Fail(new[] { r.Error.Message }); return; }
            used.Add(_token.Value);
            Response.Redirect(Url("id=" + r.Value + "&saved=1"), false);
            Context.ApplicationInstance.CompleteRequest();
        }

        protected void Fail(IEnumerable<string> messages)
        {
            var sb = new StringBuilder("<div class=\"notice bad\" role=\"alert\"><ul style=\"margin:0;padding-left:18px\">");
            foreach (var m in messages) sb.Append("<li>").Append(Enc(m)).Append("</li>");
            _litError.Text = sb.Append("</ul></div>").ToString();
        }
        protected void Succeed(string message) { _litError.Text = "<div class=\"notice ok\" role=\"status\">" + Enc(message) + "</div>"; }

        private void RenderAudit()
        {
            var host = new PlaceHolder { ID = "phAudit" }; _root.Controls.Add(host);
            Lit(host, "<div class=\"card mt\"><h3 style=\"margin-top:0\">बदल इतिहास (Audit)</h3>");
            var rows = AppServices.Register.EntityAudit(EntityName, EntityId, 50);
            if (rows.Count == 0) { Lit(host, "<p class=\"muted\">या नोंदीसाठी audit नोंदी उपलब्ध नाहीत.</p></div>"); return; }
            var sb = new StringBuilder("<div class=\"tscroll\"><table class=\"tbl\"><thead><tr><th>दिनांक / वेळ</th><th>वापरकर्ता</th><th>कृती</th><th>जुने मूल्य</th><th>नवे मूल्य</th></tr></thead><tbody>");
            foreach (var a in rows)
                sb.Append("<tr><td>").Append(a.Timestamp.ToString("dd/MM/yyyy HH:mm")).Append("</td><td>").Append(Enc(a.UserName)).Append("</td><td>").Append(Enc(a.Action)).Append("</td><td>").Append(Enc(a.OldValue)).Append("</td><td>").Append(Enc(a.NewValue)).Append("</td></tr>");
            Lit(host, sb.Append("</tbody></table></div></div>").ToString());
        }

        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            if (IsDetail && Request.QueryString["saved"] == "1" && !IsPostBack) Succeed("नोंद जतन झाली.");
            if (_litList == null) return;
            int? pid = null; int p;
            if (_ddlProperty != null && int.TryParse(_ddlProperty.SelectedValue, out p)) pid = p;
            var r = Query(_txtSearch == null ? null : _txtSearch.Text, _ddlStatus == null ? null : _ddlStatus.SelectedValue, pid, PageNo);
            if (r.TotalPages > 0 && PageNo > r.TotalPages) { PageNo = r.TotalPages; r = Query(_txtSearch == null ? null : _txtSearch.Text, _ddlStatus == null ? null : _ddlStatus.SelectedValue, pid, PageNo); }
            var sb = new StringBuilder("<span class=\"muted\">एकूण " + r.Total + " नोंदी</span><div class=\"tscroll\"><table class=\"tbl\"><thead><tr>");
            foreach (var h in Headers) sb.Append("<th>").Append(Enc(h)).Append("</th>");
            sb.Append("</tr></thead><tbody>");
            if (r.Items.Count == 0) sb.Append("<tr><td colspan=\"").Append(Headers.Length).Append("\">नोंदी आढळल्या नाहीत.</td></tr>");
            foreach (var item in r.Items)
            {
                var cells = Cells(item); sb.Append("<tr>");
                for (var i = 0; i < cells.Length; i++)
                {
                    sb.Append("<td>");
                    if (i == 0) sb.Append("<a href=\"").Append(Enc(Url("id=" + IdOf(item)))).Append("\">").Append(Enc(cells[i])).Append("</a>"); else sb.Append(Enc(cells[i]));
                    sb.Append("</td>");
                }
                sb.Append("</tr>");
            }
            _litList.Text = sb.Append("</tbody></table></div>").ToString();
            _litPager.Text = " पृष्ठ " + r.Page + " / " + Math.Max(1, r.TotalPages) + " ";
            _btnPrev.Enabled = r.Page > 1; _btnNext.Enabled = r.Page < r.TotalPages;
        }
    }
}
