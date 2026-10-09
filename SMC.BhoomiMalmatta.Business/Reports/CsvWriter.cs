using System.Collections.Generic;
using System.Text;

namespace SMC.BhoomiMalmatta.Business.Reports
{
    /// <summary>RFC-4180 CSV with spreadsheet formula-injection protection.</summary>
    public static class CsvWriter
    {
        public static string Cell(string value)
        {
            var s = value ?? string.Empty;
            if (s.Length > 0 && (s[0] == '=' || s[0] == '+' || s[0] == '-' || s[0] == '@' || s[0] == '\t' || s[0] == '\r')) s = "'" + s;
            if (s.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0) s = "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        public static string Write(IEnumerable<string> headers, IEnumerable<string[]> rows)
        {
            var sb = new StringBuilder();
            sb.Append(string.Join(",", Map(headers))).Append("\r\n");
            foreach (var r in rows) sb.Append(string.Join(",", Map(r))).Append("\r\n");
            return sb.ToString();
        }

        private static IEnumerable<string> Map(IEnumerable<string> cells) { foreach (var c in cells) yield return Cell(c); }
    }
}
