using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Models.Common;
using SMC.BhoomiMalmatta.Models.Constants;

namespace SMC.BhoomiMalmatta.Business.Sms
{
    /// <summary>
    /// Renders approved templates from MR_CFC_SmsTemplates and records every attempt in TR_CFC_SmsEvents
    /// (one row per ApplicationNumber+EventType: the table has a unique index, so retries update the row).
    /// Never throws: an SMS problem must not cancel the main transaction.
    /// </summary>
    public sealed class SmsService : INotifier, IOtpSmsSender
    {
        private static readonly Regex Placeholder = new Regex(@"\{\{?#?\s*([A-Za-z_][A-Za-z0-9_]*)\s*#?\}?\}", RegexOptions.Compiled);
        private static readonly Regex Leftover = new Regex(@"\{#?[^{}]*#?\}", RegexOptions.Compiled);
        private readonly IMasterDataReader _masters; private readonly ISmsGateway _gateway; private readonly ISmsEventStore _events; private readonly IUserStore _users;
        private readonly IClock _clock; private readonly bool _enabled; private readonly string _defaultSender;

        public SmsService(IMasterDataReader masters, ISmsGateway gateway, ISmsEventStore events, IUserStore users, IClock clock, bool enabled, string defaultSenderId)
        { _masters = masters; _gateway = gateway; _events = events; _users = users; _clock = clock; _enabled = enabled; _defaultSender = defaultSenderId; }

        public static string Render(string body, IDictionary<string, string> vars, out bool complete)
        {
            var text = Placeholder.Replace(body ?? string.Empty, m =>
            {
                string v; return vars != null && vars.TryGetValue(m.Groups[1].Value, out v) ? v : m.Value;
            });
            complete = !Leftover.IsMatch(text);
            return text;
        }

        public void Notify(NotificationRequest r)
        {
            try
            {
                var mobiles = new List<string>();
                if (!string.IsNullOrWhiteSpace(r.RecipientMobile)) mobiles.Add(r.RecipientMobile.Trim());
                if (!string.IsNullOrWhiteSpace(r.RecipientRole)) mobiles.AddRange(_users.GetActiveMobilesByRole(r.RecipientRole).Where(m => !string.IsNullOrWhiteSpace(m)));
                mobiles = mobiles.Distinct().ToList();
                if (mobiles.Count == 0) { Record(r, "-", null, SmsStatuses.Suppressed, null, "No recipient mobile number", null); return; }

                var t = _masters.GetSmsTemplate(r.EventType, "mr");
                if (t == null) { Record(r, mobiles[0], null, SmsStatuses.Suppressed, null, "No approved active template", null); return; }
                bool complete;
                var text = Render(t.MessageBody, r.Variables, out complete);
                if (!complete) { Record(r, mobiles[0], t.DltTemplateId, SmsStatuses.Suppressed, null, "Template has unresolved placeholders", null); return; }
                if (!_enabled) { Record(r, mobiles[0], t.DltTemplateId, SmsStatuses.Suppressed, null, "SMS disabled by configuration", null); return; }

                SmsSendResult last = null; var anyOk = false;
                foreach (var m in mobiles)
                {
                    try { last = _gateway.Send(m, text, t.DltTemplateId, string.IsNullOrWhiteSpace(t.SenderId) ? _defaultSender : t.SenderId); }
                    catch (Exception ex) { last = new SmsSendResult { Success = false, Error = ex.GetType().Name }; }
                    if (last.Success) anyOk = true;
                }
                Record(r, mobiles[0], t.DltTemplateId, anyOk ? SmsStatuses.Sent : SmsStatuses.Failed, last == null ? null : last.ProviderMessageId, anyOk ? null : Trunc(last == null ? "no response" : last.Error), anyOk ? (DateTime?)_clock.UtcNow : null);
            }
            catch (Exception) { /* swallow: SMS must never break the business transaction */ }
        }

        public bool Enabled { get { return _enabled; } }

        /// <summary>Uses only an approved active SMS template and the configured gateway. No fallback OTP text is constructed.</summary>
        public OtpSmsSendStatus SendAccessRecoveryOtp(string applicationNumber, string registeredMobile, string otp, int expiryMinutes)
        {
            var request = new NotificationRequest { EventType = SmsEventTypes.AccessCodeRecovery, ApplicationNumber = applicationNumber, RecipientMobile = registeredMobile };
            if (!_enabled || string.IsNullOrWhiteSpace(registeredMobile) || string.IsNullOrWhiteSpace(otp))
            {
                Record(request, string.IsNullOrWhiteSpace(registeredMobile) ? "-" : registeredMobile, null, SmsStatuses.Suppressed, null, "OTP SMS unavailable or recipient missing", null);
                return OtpSmsSendStatus.Unavailable;
            }

            try
            {
                var template = _masters.GetSmsTemplate(SmsEventTypes.AccessCodeRecovery, "mr");
                if (template == null)
                {
                    Record(request, registeredMobile, null, SmsStatuses.Suppressed, null, "No approved active access recovery template", null);
                    return OtpSmsSendStatus.Unavailable;
                }

                var variables = new Dictionary<string, string> { { "Otp", otp }, { "ExpiryMinutes", expiryMinutes.ToString() } };
                bool complete;
                var text = Render(template.MessageBody, variables, out complete);
                if (!complete)
                {
                    Record(request, registeredMobile, template.DltTemplateId, SmsStatuses.Suppressed, null, "OTP template has unresolved placeholders", null);
                    return OtpSmsSendStatus.Unavailable;
                }

                SmsSendResult result;
                try { result = _gateway.Send(registeredMobile.Trim(), text, template.DltTemplateId, string.IsNullOrWhiteSpace(template.SenderId) ? _defaultSender : template.SenderId); }
                catch (Exception ex) { result = new SmsSendResult { Success = false, Error = ex.GetType().Name }; }
                if (result == null || !result.Success)
                {
                    Record(request, registeredMobile, template.DltTemplateId, SmsStatuses.Failed, null, result == null ? "no response" : Trunc(result.Error), null);
                    return OtpSmsSendStatus.Failed;
                }

                Record(request, registeredMobile, template.DltTemplateId, SmsStatuses.Sent, result.ProviderMessageId, null, _clock.UtcNow);
                return OtpSmsSendStatus.Sent;
            }
            catch (Exception ex)
            {
                Record(request, registeredMobile, null, SmsStatuses.Failed, null, ex.GetType().Name, null);
                return OtpSmsSendStatus.Failed;
            }
        }

        private void Record(NotificationRequest r, string mobile, string template, string status, string providerId, string reason, DateTime? sentAt)
        {
            try
            {
                _events.Upsert(new SmsRecord { Mobile = mobile.Length > 15 ? mobile.Substring(0, 15) : mobile, EventType = r.EventType, ApplicationNumber = r.ApplicationNumber, TemplateReference = template,
                    Status = status, ProviderMessageId = providerId, FailureReason = Trunc(reason), SentAtUtc = sentAt });
            }
            catch (Exception) { }
        }
        private static string Trunc(string s) { return s == null ? null : (s.Length > 900 ? s.Substring(0, 900) : s); }
    }
}
