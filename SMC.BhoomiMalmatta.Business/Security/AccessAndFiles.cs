using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SMC.BhoomiMalmatta.Business.Workflow;
using SMC.BhoomiMalmatta.Models.Common;
using SMC.BhoomiMalmatta.Models.Constants;

namespace SMC.BhoomiMalmatta.Business.Security
{
    /// <summary>Citizen status-access token. Only the SHA-256 hex (64 chars = ApplicantAccessTokenHash) is stored.</summary>
    public static class AccessToken
    {
        public static string NewToken()
        {
            var buf = new byte[24];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(buf);
            return Convert.ToBase64String(buf).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }
        public static string Hash(string token)
        {
            using (var sha = SHA256.Create()) return ActionHistory.Hex(sha.ComputeHash(Encoding.UTF8.GetBytes((token ?? "").Trim()))).ToLowerInvariant();
        }
        public static bool Verify(string token, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(storedHash)) return false;
            return FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(token)), Encoding.ASCII.GetBytes(storedHash.Trim().ToLowerInvariant()));
        }
        /// <summary>Constant-time comparison (CryptographicOperations is not available on .NET Framework).</summary>
        public static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            var diff = 0;
            for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }

    public sealed class FileCheck { public string StoredName { get; set; } public string SafeDisplayName { get; set; } public string Extension { get; set; } public string ContentType { get; set; } }

    /// <summary>Extension + MIME + magic-number + size validation; generated storage names; sanitized display names (no paths, no executables).</summary>
    public static class FileValidator
    {
        public const long MaxBytes = 5 * 1024 * 1024;
        public const long MaxApplicationAttachmentBytes = 20 * 1024 * 1024;
        private sealed class Rule { public string Mime; public byte[][] Magic; }
        private static readonly Dictionary<string, Rule> Allowed = new Dictionary<string, Rule>
        {
            { ".pdf", new Rule { Mime = "application/pdf", Magic = new[] { new byte[] { 0x25, 0x50, 0x44, 0x46 } } } },
            { ".png", new Rule { Mime = "image/png", Magic = new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } } },
            { ".jpg", new Rule { Mime = "image/jpeg", Magic = new[] { new byte[] { 0xFF, 0xD8, 0xFF } } } },
            { ".jpeg", new Rule { Mime = "image/jpeg", Magic = new[] { new byte[] { 0xFF, 0xD8, 0xFF } } } },
        };

        public static string SanitizeName(string name)
        {
            var n = Path.GetFileName((name ?? "file").Replace('\\', '/'));
            n = Regex.Replace(n, @"[^\p{L}\p{N}._ -]", "_").Trim('.', ' ');
            n = Regex.Replace(n, @"\.{2,}", ".");
            if (string.IsNullOrWhiteSpace(n)) return "file";
            return n.Length > 120 ? n.Substring(n.Length - 120) : n;
        }

        public static Result<FileCheck> Validate(string fileName, string declaredContentType, byte[] content)
        { return Validate(fileName, declaredContentType, content, MaxBytes); }

        public static Result<FileCheck> Validate(string fileName, string declaredContentType, byte[] content, long maxBytes)
        {
            if (content == null || content.Length == 0) return Result.Failure<FileCheck>(new Error("FILE_EMPTY", "फाईल रिकामी आहे."));
            if (maxBytes <= 0) throw new ArgumentOutOfRangeException("maxBytes");
            if (content.LongLength > maxBytes)
            {
                var mb = maxBytes == MaxBytes ? "५" : ((int)Math.Ceiling(maxBytes / (1024d * 1024d))).ToString(System.Globalization.CultureInfo.InvariantCulture);
                return Result.Failure<FileCheck>(new Error("FILE_TOO_LARGE", "फाईलचा आकार " + mb + " MB पेक्षा जास्त आहे."));
            }
            var safe = SanitizeName(fileName);
            var ext = Path.GetExtension(safe).ToLowerInvariant();
            var stem = Path.GetFileNameWithoutExtension(safe).ToLowerInvariant();
            if (Regex.IsMatch(stem, @"\.(exe|dll|bat|cmd|com|msi|js|vbs|ps1|sh|scr|jar|aspx?|php|html?)$"))
                return Result.Failure<FileCheck>(new Error("FILE_TYPE", "या प्रकारची फाईल स्वीकारली जात नाही."));
            Rule rule;
            if (!Allowed.TryGetValue(ext, out rule)) return Result.Failure<FileCheck>(new Error("FILE_TYPE", "फक्त PDF, JPG, PNG फाईल स्वीकारल्या जातात."));
            var mimeOk = string.Equals(declaredContentType, rule.Mime, StringComparison.OrdinalIgnoreCase)
                         || (rule.Mime == "image/jpeg" && string.Equals(declaredContentType, "image/jpg", StringComparison.OrdinalIgnoreCase));
            if (!mimeOk) return Result.Failure<FileCheck>(new Error("FILE_MIME", "फाईलचा प्रकार (MIME) जुळत नाही."));
            if (!rule.Magic.Any(m => StartsWith(content, m))) return Result.Failure<FileCheck>(new Error("FILE_SIGNATURE", "फाईलची सामग्री तिच्या प्रकाराशी जुळत नाही."));
            return Result.Success(new FileCheck { StoredName = Guid.NewGuid().ToString("N") + ext, SafeDisplayName = safe, Extension = ext, ContentType = rule.Mime });
        }

        private static bool StartsWith(byte[] data, byte[] magic)
        {
            if (data.Length < magic.Length) return false;
            for (var i = 0; i < magic.Length; i++) if (data[i] != magic[i]) return false;
            return true;
        }
    }

    public enum DocumentVisibility { CitizenVisible, OfficerOnly }

    public static class DocumentAccessPolicy
    {
        /// <summary>Officers (JE/OS/AC/Admin) may open all application documents; Staff only citizen-visible ones; a citizen only with a valid token for that application.</summary>
        public static bool CanDownload(string role, DocumentVisibility visibility, bool citizenTokenValidForThisApplication)
        {
            if (role == Roles.Citizen) return citizenTokenValidForThisApplication && visibility == DocumentVisibility.CitizenVisible;
            if (role == Roles.Admin || role == Roles.JE || role == Roles.OS || role == Roles.AssistantCommissioner) return true;
            if (role == Roles.Staff) return visibility == DocumentVisibility.CitizenVisible;
            return false;
        }
    }
}
