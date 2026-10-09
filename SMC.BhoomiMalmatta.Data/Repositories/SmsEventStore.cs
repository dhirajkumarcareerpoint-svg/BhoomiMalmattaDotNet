using System;
using System.Linq;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Repositories
{
    /// <summary>TR_CFC_SmsEvents has a UNIQUE index on (ApplicationNumber, EventType), so a retry updates the existing row.</summary>
    public sealed class SmsEventStore : ISmsEventStore
    {
        public void Upsert(SmsRecord r)
        {
            using (var db = new SmcDbContext())
            {
                SmsEvent row = null;
                if (r.ApplicationNumber != null)
                    row = db.SmsEvents.FirstOrDefault(x => x.ApplicationNumber == r.ApplicationNumber && x.EventType == r.EventType); // includes soft-deleted rows: the index does too
                var now = DateTime.UtcNow;
                if (row == null)
                {
                    row = new SmsEvent { RecipientMobile = r.Mobile, EventType = r.EventType, ApplicationNumber = r.ApplicationNumber, RetryCount = 0, CreatedBy = "system", CreatedAt = now, IsDeleted = false };
                    db.SmsEvents.Add(row);
                }
                else { row.RetryCount = row.RetryCount + 1; row.UpdatedBy = "system"; row.UpdatedAt = now; row.IsDeleted = false; }
                row.RecipientMobile = r.Mobile; row.TemplateReference = r.TemplateReference; row.Status = r.Status; row.ProviderMessageId = r.ProviderMessageId;
                row.FailureReason = r.FailureReason; row.SentAt = r.SentAtUtc;
                db.SaveChanges();
            }
        }
    }
}
