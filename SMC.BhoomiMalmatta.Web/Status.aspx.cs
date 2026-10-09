using System;
using System.Threading;
using SMC.BhoomiMalmatta.Business.Citizen;
using SMC.BhoomiMalmatta.Web.Security;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Web
{
    public partial class StatusPage : SmcPage
    {
        private const string GenericFailure = "अर्ज आढळला नाही. कृपया क्रमांक तपासा.";
        private const string Throttled = "कृपया थोड्या वेळाने प्रयत्न करा.";
        private static readonly TimeSpan LookupWindow = TimeSpan.FromMinutes(10);
        private const int LookupLimit = 30;
        private const int FailureDelayMilliseconds = 200;

        protected void btnCheck_Click(object sender, EventArgs e)
        {
            if (!AppServices.Limiter.Allow("public-status:" + ClientIp, LookupLimit, LookupWindow))
            {
                Fail(Throttled);
                return;
            }

            var number = ApplicantNumberRules.Normalize(txtNumber.Text);
            if (number == null) { FailGeneric(); return; }

            try
            {
                var applicationId = AppServices.Query.FindIdByNumber(number);
                if (!applicationId.HasValue) { FailGeneric(); return; }
                var detail = AppServices.Query.GetDetail(applicationId.Value);
                if (detail == null || !string.Equals(detail.ApplicationNumber, number, StringComparison.Ordinal)) { FailGeneric(); return; }

                AuthCookies.SignInCitizen(Context, applicationId.Value, number);
                Response.Redirect(ResolveUrl("~/Citizen/Dashboard.aspx"), false);
                Context.ApplicationInstance.CompleteRequest();
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(CorrelationId, "ApplicantStatusLookup", ex);
                FailGeneric();
            }
        }

        private void FailGeneric()
        {
            Thread.Sleep(FailureDelayMilliseconds);
            Fail(GenericFailure);
        }

        private void Fail(string message)
        {
            lblError.Text = Enc(message);
            lblError.Visible = true;
        }
    }
}
