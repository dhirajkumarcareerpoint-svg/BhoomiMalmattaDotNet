using System;
using System.Data.Common;
using SMC.BhoomiMalmatta.Models.Common;

namespace SMC.BhoomiMalmatta.Data.Safety
{
    /// <summary>Start-up guard: the application only ever talks to the existing SMC_BhoomiMalmatta database.</summary>
    public static class DatabaseSafety
    {
        public const string ExpectedDatabase = "SMC_BhoomiMalmatta";

        public static Result AssertSafe(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return Result.Failure(new Error("DB_CONFIG_MISSING", "Connection string 'SmcDatabase' is not configured."));
            DbConnectionStringBuilder b;
            try { b = new DbConnectionStringBuilder { ConnectionString = connectionString }; }
            catch (ArgumentException) { return Result.Failure(new Error("DB_CONFIG_INVALID", "Connection string is not valid.")); }

            string db = null;
            foreach (var key in new[] { "Initial Catalog", "Database" })
            {
                object v;
                if (b.TryGetValue(key, out v)) { db = v == null ? null : v.ToString(); break; }
            }
            if (!string.Equals(db == null ? null : db.Trim(), ExpectedDatabase, StringComparison.Ordinal))
                return Result.Failure(new Error("DB_NAME_MISMATCH", "Refusing to start: the configured database must be exactly '" + ExpectedDatabase + "'."));
            foreach (var key in new[] { "AttachDbFilename", "AttachDBFileName", "User Instance" })
                if (b.ContainsKey(key)) return Result.Failure(new Error("DB_ATTACH_FORBIDDEN", "Attaching or creating database files is not allowed."));
            return Result.Success();
        }

        /// <summary>Removes secrets so a connection string can appear in logs.</summary>
        public static string Redact(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) return string.Empty;
            try
            {
                var b = new DbConnectionStringBuilder { ConnectionString = connectionString };
                foreach (var k in new[] { "Password", "Pwd" }) if (b.ContainsKey(k)) b[k] = "***";
                return b.ConnectionString;
            }
            catch (ArgumentException) { return "(unparseable)"; }
        }
    }
}
