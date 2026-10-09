using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using SMC.BhoomiMalmatta.Business.Abstractions;

namespace SMC.BhoomiMalmatta.Web.Services
{
    /// <summary>
    /// ACL Gateway client. Credentials come ONLY from the environment (SMC_SMS_USERNAME / SMC_SMS_PASSWORD) or the git-ignored App_Data\secrets.config;
    /// they are never logged and never stored. Query-parameter names are configurable (Sms.Param.*) because the vendor manual is not part of this repository.
    /// The provider response is stored (truncated, secrets scrubbed); what a "success" body looks like must be confirmed against the ACL manual.
    /// </summary>
    public sealed class AclSmsGateway : ISmsGateway
    {
        private static string S(string key, string fallback = null) { return ConfigurationManager.AppSettings[key] ?? fallback; }

        public SmsSendResult Send(string mobile, string text, string dltTemplateId, string senderId)
        {
            var user = Environment.GetEnvironmentVariable("SMC_SMS_USERNAME") ?? S("Sms.Username");
            var pass = Environment.GetEnvironmentVariable("SMC_SMS_PASSWORD") ?? S("Sms.Password");
            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass)) return new SmsSendResult { Success = false, Error = "SMS credentials are not configured" };

            var number = new string(mobile.Where(char.IsDigit).ToArray());
            if (number.Length == 10) number = S("Sms.CountryCode", "91") + number;
            var q = S("Sms.Param.Username", "username") + "=" + Uri.EscapeDataString(user)
                + "&" + S("Sms.Param.Password", "password") + "=" + Uri.EscapeDataString(pass)
                + "&" + S("Sms.Param.To", "to") + "=" + Uri.EscapeDataString(number)
                + "&" + S("Sms.Param.From", "from") + "=" + Uri.EscapeDataString(senderId ?? S("Sms.SenderId", "MAHGOV"))
                + "&" + S("Sms.Param.Text", "text") + "=" + Uri.EscapeDataString(text)
                + (string.IsNullOrWhiteSpace(dltTemplateId) ? "" : "&" + S("Sms.Param.TemplateId", "dltTemplateId") + "=" + Uri.EscapeDataString(dltTemplateId));
            var baseUrl = S("Sms.BaseUrl");
            var url = baseUrl + (baseUrl.Contains("?") ? "&" : "?") + q;
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET"; req.Timeout = 10000; req.ReadWriteTimeout = 10000; req.UserAgent = "SMC-BhoomiMalmatta/1.0";
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var rd = new StreamReader(resp.GetResponseStream()))
                {
                    var body = (rd.ReadToEnd() ?? string.Empty).Trim().Replace(pass, "***").Replace(user, "***");
                    if (body.Length > 150) body = body.Substring(0, 150);
                    var ok = (int)resp.StatusCode >= 200 && (int)resp.StatusCode < 300;
                    return new SmsSendResult { Success = ok, ProviderMessageId = body, Error = ok ? null : "HTTP " + (int)resp.StatusCode };
                }
            }
            catch (WebException ex)
            {
                var status = ex.Response as HttpWebResponse;
                return new SmsSendResult { Success = false, Error = "ACL request failed: " + ex.Status + (status == null ? "" : " HTTP " + (int)status.StatusCode) };
            }
        }
    }
}
