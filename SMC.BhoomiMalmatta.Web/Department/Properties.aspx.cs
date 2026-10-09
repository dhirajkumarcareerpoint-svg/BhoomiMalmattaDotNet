using System;
using System.Globalization;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Web.Security;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Web.Department
{
    /// <summary>
    /// Property register (Admin only). The list and the new / edit / view dialog are static markup driven by Scripts/properties.js over
    /// api/v1/properties; no ViewState. "?documents=&lt;id&gt;" shows the existing document list and upload box for one property.
    /// </summary>
    public partial class PropertiesPage : SmcPage
    {
        protected override string[] RequiredRoles { get { return new[] { Roles.Admin }; } }

        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            int id;
            var raw = Request.QueryString["documents"];
            if (raw == null || !int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out id) || id <= 0) return;
            phList.Visible = false; phDocuments.Visible = true;
            var p = AppServices.Properties.Detail(id);
            if (p == null) { litDocTitle.Text = Enc("नोंद आढळली नाही."); return; }
            litDocTitle.Text = Enc(p.PropertyCode + " — " + p.Name + " : कागदपत्रे");
            // Built in OnInit so the upload button's click event fires on postback.
            RegisterUi.Documents(phDocHost, "Property", id, true, Actor(), ResolveUrl("~/Department/Properties.aspx") + "?documents=" + id, m => Notify(m, true));
        }
    }
}
