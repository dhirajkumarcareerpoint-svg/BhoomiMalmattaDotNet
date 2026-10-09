using System;
using System.IO;

namespace SMC.BhoomiMalmatta.Business.Citizen
{
    public sealed class ApplicantQrPathResult
    {
        public bool IsVirtual { get; set; }
        public string Value { get; set; }
    }

    /// <summary>Resolves the one configured QR file without accepting any request-supplied path.</summary>
    public static class ApplicantQrPath
    {
        public static ApplicantQrPathResult Resolve(string configuredPath, string applicationRoot, Func<string, bool> exists)
        {
            if (string.IsNullOrWhiteSpace(configuredPath) || exists == null) return null;
            var candidate = configuredPath.Trim();
            if (candidate.StartsWith("~/", StringComparison.Ordinal))
            {
                var configuredRelative = candidate.Substring(2);
                if (configuredRelative.Contains("..") || configuredRelative.IndexOf(':') >= 0 || configuredRelative.IndexOf('\\') >= 0) return null;
                var relative = configuredRelative.Replace('/', Path.DirectorySeparatorChar);
                var physical = Path.GetFullPath(Path.Combine(applicationRoot, relative));
                return exists(physical) ? new ApplicantQrPathResult { IsVirtual = true, Value = candidate } : null;
            }
            if (!Path.IsPathRooted(candidate)) return null;
            var full = Path.GetFullPath(candidate);
            return exists(full) ? new ApplicantQrPathResult { IsVirtual = false, Value = full } : null;
        }
    }
}
