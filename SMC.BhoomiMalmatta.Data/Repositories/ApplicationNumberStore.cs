using System.Data;
using System.Data.Entity;
using System.Linq;
using SMC.BhoomiMalmatta.Business.Abstractions;

namespace SMC.BhoomiMalmatta.Data.Repositories
{
    public sealed class ApplicationNumberStore : IApplicationNumberStore
    {
        private sealed class NumberConfigRow
        {
            public int Id { get; set; }
            public string Prefix { get; set; }
            public string YearStrategy { get; set; }
            public int SequenceLength { get; set; }
            public int NextNumber { get; set; }
        }

        /// <summary>
        /// Serializable transaction with UPDLOCK/HOLDLOCK on the active configuration row, so two concurrent submissions can never take the same
        /// sequence value. This increments the existing NextNumber counter (a data change only; no schema change).
        /// </summary>
        public NumberReservation ReserveNext()
        {
            using (var db = new SmcDbContext())
            using (var tx = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                var row = db.Database.SqlQuery<NumberConfigRow>(
                    "SELECT TOP 1 Id, Prefix, YearStrategy, SequenceLength, NextNumber FROM dbo.MR_CFC_ApplicationNumberConfigurations WITH (UPDLOCK, HOLDLOCK) WHERE IsActive = 1 AND IsDeleted = 0 ORDER BY Id").FirstOrDefault();
                if (row == null) { tx.Rollback(); return null; }
                db.Database.ExecuteSqlCommand(
                    "UPDATE dbo.MR_CFC_ApplicationNumberConfigurations SET NextNumber = NextNumber + 1, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = N'system' WHERE Id = @p0", row.Id);
                tx.Commit();
                return new NumberReservation { Prefix = row.Prefix, YearStrategy = row.YearStrategy, SequenceLength = row.SequenceLength, Number = row.NextNumber };
            }
        }

        /// <summary>Includes soft-deleted rows: the unique index covers every row.</summary>
        public bool Exists(string applicationNumber)
        {
            using (var db = new SmcDbContext()) return db.DemandApplications.AsNoTracking().Any(a => a.ApplicationNumber == applicationNumber);
        }
    }
}
