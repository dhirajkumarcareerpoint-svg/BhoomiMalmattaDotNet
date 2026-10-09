using System;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Repositories
{
    /// <summary>Writes TR_AST_AuditLogs. UserId is NOT NULL with an FK to users, so entries without a signed-in officer are skipped.</summary>
    public sealed class AuditWriter : IAuditWriter
    {
        private static string Cut(string s, int max) { return s == null ? null : (s.Length > max ? s.Substring(0, max) : s); }

        public static AuditLog ToEntity(AuditEntry e)
        {
            return new AuditLog
            {
                UserId = e.UserId.Value, UserName = Cut(e.UserName ?? "", 150), Action = Cut(e.Action, 30), EntityName = Cut(e.EntityName, 100), EntityId = e.EntityId,
                OldValue = Cut(e.OldValue, 2000), NewValue = Cut(e.NewValue, 2000), Timestamp = e.TimestampUtc, IpAddress = Cut(e.IpAddress, 50)
            };
        }

        public void Write(AuditEntry entry)
        {
            if (!entry.UserId.HasValue) return;
            using (var db = new SmcDbContext())
            {
                db.AuditLogs.Add(ToEntity(entry));
                db.SaveChanges();
            }
        }
    }
}
