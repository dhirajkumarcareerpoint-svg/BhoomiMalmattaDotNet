using System;

namespace SMC.BhoomiMalmatta.Web.Security
{
    /// <summary>Signed-in officer name with the role printed once (a stored full name may already end with "(Admin)").</summary>
    public static class DisplayName
    {
        public static string WithRole(string fullName, string role)
        {
            var name = (fullName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(role)) return name;
            var suffix = "(" + role.Trim() + ")";
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return name;
            return name.Length == 0 ? suffix : name + " " + suffix;
        }
    }
}
