using System;
using System.Configuration;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SMC.BhoomiMalmatta.Web.Services
{
    /// <summary>
    /// Production error log with no personal-data leakage: it records the correlation id, exception type and stack frames, but never the request
    /// body, query string, cookies, user names or connection strings; long digit runs (phone / Aadhaar / PAN-like) in messages are masked.
    /// </summary>
    public static class ErrorLogger
    {
        private static readonly object Gate = new object();
        private static readonly Regex Digits = new Regex(@"\d{6,}", RegexOptions.Compiled);
        private static readonly Regex Email = new Regex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+", RegexOptions.Compiled);

        public static string Root
        {
            get { return Environment.ExpandEnvironmentVariables(ConfigurationManager.AppSettings["Log.Root"] ?? @"%LOCALAPPDATA%\SMC_BhoomiMalmatta\Logs"); }
        }

        public static string Sanitize(string message)
        {
            if (string.IsNullOrEmpty(message)) return string.Empty;
            var m = Email.Replace(Digits.Replace(message, "######"), "***@***");
            m = m.Replace("\r", " ").Replace("\n", " ");
            return m.Length > 240 ? m.Substring(0, 240) : m;
        }

        public static void Log(string correlationId, string where, Exception ex)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append(DateTime.UtcNow.ToString("o")).Append(" | ").Append(correlationId).Append(" | ").Append(where).Append(" | ");
                for (var e = ex; e != null; e = e.InnerException) sb.Append(e.GetType().FullName).Append(": ").Append(Sanitize(e.Message)).Append(" || ");
                var st = (ex == null ? null : ex.StackTrace) ?? string.Empty;
                sb.Append(st.Replace("\r", " ").Replace("\n", " "));
                var dir = Root; Directory.CreateDirectory(dir);
                lock (Gate) File.AppendAllText(Path.Combine(dir, "error-" + DateTime.UtcNow.ToString("yyyyMMdd") + ".log"), sb + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception) { /* logging must never throw */ }
        }
    }

    /// <summary>Business-layer error log port backed by <see cref="ErrorLogger"/>.</summary>
    public sealed class ErrorLog : SMC.BhoomiMalmatta.Business.Abstractions.IErrorLog
    {
        public void Log(string correlationId, string area, Exception ex) { ErrorLogger.Log(correlationId, area, ex); }
    }
}
