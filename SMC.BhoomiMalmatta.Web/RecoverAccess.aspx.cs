using System;
using System.Security.Cryptography;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Web.Security;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Web
{
    public partial class RecoverAccessPage : SmcPage
    {
        private const string ChallengeKey = "CitizenAccessRecoveryChallenge";
        private const int OtpExpiryMinutes = 10;
        private const int MaxAttempts = 5;

        [Serializable]
        private sealed class Challenge
        {
            public int ApplicationId { get; set; }
            public string ApplicationNumber { get; set; }
            public string Salt { get; set; }
            public string Hash { get; set; }
            public DateTime ExpiresUtc { get; set; }
            public int AttemptsLeft { get; set; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            Response.Redirect("~/Status.aspx", true);
        }

        protected void btnSendOtp_Click(object sender, EventArgs e)
        {
            Page.Validate("request");
            if (!Page.IsValid) return;
            if (AppServices.OtpSms == null || !AppServices.OtpSms.Enabled)
            {
                ShowMessage("OTP द्वारे प्रवेश पुनर्प्राप्ती सध्या उपलब्ध नाही. SMS सेवा सुरू व कॉन्फिगर होईपर्यंत कार्यालयाशी संपर्क साधा.", true);
                return;
            }

            var number = (txtNumber.Text ?? string.Empty).Trim();
            var keyHash = Hash(number.ToLowerInvariant());
            Session.Remove(ChallengeKey);
            if (!AppServices.Limiter.Allow("recovery-request-ip:" + ClientIp, 5, TimeSpan.FromMinutes(15)) ||
                !AppServices.Limiter.Allow("recovery-request-app:" + ClientIp + ":" + keyHash, 3, TimeSpan.FromMinutes(15)))
            {
                ShowMessage(GenericRequestMessage, false);
                return;
            }

            var id = AppServices.Query.FindIdByNumber(number);
            if (id.HasValue)
            {
                var mobile = AppServices.Query.GetRegisteredMobile(id.Value);
                var otp = NewOtp();
                var sent = AppServices.OtpSms.SendAccessRecoveryOtp(number, mobile, otp, OtpExpiryMinutes);
                if (sent == OtpSmsSendStatus.Sent)
                {
                    var salt = NewSalt();
                    Session[ChallengeKey] = new Challenge
                    {
                        ApplicationId = id.Value, ApplicationNumber = number, Salt = salt, Hash = Hash(salt + "|" + otp),
                        ExpiresUtc = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes), AttemptsLeft = MaxAttempts
                    };
                    pnlVerify.Visible = true;
                }
            }

            // Same response for unknown numbers and SMS send failures; never confirms whether an application exists.
            ShowMessage(GenericRequestMessage, false);
        }

        protected void btnVerifyOtp_Click(object sender, EventArgs e)
        {
            Page.Validate("verify");
            if (!Page.IsValid) return;
            if (!AppServices.Limiter.Allow("recovery-verify-ip:" + ClientIp, 20, TimeSpan.FromMinutes(15)))
            { ShowMessage("अनेक प्रयत्न झाले. कृपया काही वेळाने पुन्हा प्रयत्न करा.", true); return; }

            var challenge = Session[ChallengeKey] as Challenge;
            var inputNumber = (txtNumber.Text ?? string.Empty).Trim();
            if (challenge == null || challenge.ExpiresUtc <= DateTime.UtcNow || challenge.AttemptsLeft <= 0 ||
                !string.Equals(inputNumber, challenge.ApplicationNumber, StringComparison.OrdinalIgnoreCase))
            {
                Session.Remove(ChallengeKey);
                pnlVerify.Visible = false;
                ShowMessage("OTP अवैध किंवा कालबाह्य आहे. पुन्हा विनंती करा.", true);
                return;
            }

            challenge.AttemptsLeft--;
            if (!FixedTimeEquals(challenge.Hash, Hash(challenge.Salt + "|" + (txtOtp.Text ?? string.Empty).Trim())))
            {
                if (challenge.AttemptsLeft == 0) { Session.Remove(ChallengeKey); pnlVerify.Visible = false; }
                else Session[ChallengeKey] = challenge;
                ShowMessage("OTP अवैध किंवा कालबाह्य आहे. पुन्हा प्रयत्न करा.", true);
                return;
            }

            Session.Remove(ChallengeKey);
            AuthCookies.SignInCitizen(Context, challenge.ApplicationId, challenge.ApplicationNumber);
            Response.Redirect("~/Citizen/Dashboard.aspx", false);
            Context.ApplicationInstance.CompleteRequest();
        }

        private const string GenericRequestMessage = "अर्ज क्रमांक वैध असल्यास आणि SMS उपलब्ध असल्यास, OTP नोंदणीकृत मोबाइलवर पाठवला जाईल. OTP न मिळाल्यास कार्यालयाशी संपर्क साधा.";

        private void ShowMessage(string message, bool error)
        {
            lblMessage.Text = Enc(message);
            lblMessage.CssClass = error ? "notice bad" : "notice";
            lblMessage.Visible = true;
        }

        private static string NewOtp()
        {
            var bytes = new byte[4];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            var value = BitConverter.ToUInt32(bytes, 0) % 1000000U;
            return value.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string NewSalt()
        {
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        private static string Hash(string value)
        {
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)));
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            var left = Encoding.UTF8.GetBytes(a ?? string.Empty); var right = Encoding.UTF8.GetBytes(b ?? string.Empty);
            var diff = left.Length ^ right.Length;
            for (var i = 0; i < Math.Min(left.Length, right.Length); i++) diff |= left[i] ^ right[i];
            return diff == 0;
        }
    }
}
