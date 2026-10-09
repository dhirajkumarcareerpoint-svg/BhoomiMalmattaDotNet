using System.Text.RegularExpressions;

namespace SMC.BhoomiMalmatta.Business.Citizen
{
    public static class ApplicantNumberRules
    {
        private static readonly Regex Valid = new Regex(@"\A[A-Za-z0-9]{1,40}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static string Normalize(string value)
        {
            var normalized = (value ?? string.Empty).Trim();
            return Valid.IsMatch(normalized) ? normalized : null;
        }
    }
}
