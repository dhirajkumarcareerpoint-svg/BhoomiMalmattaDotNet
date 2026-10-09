using System;
using System.Configuration;
using System.Globalization;

namespace SMC.BhoomiMalmatta.Business.Citizen
{
    public static class ApplicantDateFormatter
    {
        public static bool UseDevanagariDigits
        {
            get { bool value; return !bool.TryParse(ConfigurationManager.AppSettings["UseDevanagariDigits"], out value) || value; }
        }

        public static string Format(DateTime value, string format, bool devanagariDigits)
        {
            var text = value.ToString(format, CultureInfo.InvariantCulture);
            if (!devanagariDigits) return text;
            const string latin = "0123456789";
            const string dev = "०१२३४५६७८९";
            for (var i = 0; i < latin.Length; i++) text = text.Replace(latin[i], dev[i]);
            return text;
        }
    }
}
