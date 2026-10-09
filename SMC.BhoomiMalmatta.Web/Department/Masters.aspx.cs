using System;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Web.Security;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Web.Department
{
    public partial class MastersPage : DeptPage
    {
        protected override string[] RequiredRoles { get { return new[] { Roles.Admin, Roles.Staff }; } }

        protected void Page_Load(object sender, EventArgs e)
        {
            var m = AppServices.Masters;
            gvServices.DataSource = m.GetServices(); gvServices.DataBind();
            gvRates.DataSource = m.GetRates(); gvRates.DataBind();
            gvBusiness.DataSource = m.GetBusinessTypes(); gvBusiness.DataBind();
            gvSms.DataSource = m.GetSmsTemplates(); gvSms.DataBind();
            var n = m.GetNumberConfig();
            litNumber.Text = n == null ? "सक्रिय संरचना नाही." : Enc("Prefix: " + n.Prefix + " | वर्ष पद्धत: " + n.YearStrategy + " | क्रम लांबी: " + n.SequenceLength + " | पुढील क्रमांक: " + n.NextNumber);
        }
    }
}
