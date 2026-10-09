using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SMC.BhoomiMalmatta.Data;
using SMC.BhoomiMalmatta.Data.Repositories;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;

namespace SMC.BhoomiMalmatta.Tests
{
    [TestClass]
    public class OfficerQueryTranslationTests
    {
        [TestMethod]
        public void Officer_Kpi_And_List_Queries_Translate_To_Sql_Without_Opening_Database()
        {
            var builder = new DbModelBuilder();
            SmcDbContext.ConfigureModel(builder);
            var compiled = builder.Build(new DbProviderInfo("System.Data.SqlClient", "2012")).Compile();
            using (var connection = new SqlConnection("Data Source=(local);Initial Catalog=SMC_BhoomiMalmatta;Integrated Security=True;Connect Timeout=1"))
            using (var db = new SmcDbContext(connection, compiled))
            {
                var baseQuery = QueryService.Apply(QueryService.Base(db), new ApplicationFilter { ZoneScope = 3 });
                var dashboardSql = baseQuery.GroupBy(x => 1).Select(g => new DashboardCounts
                {
                    Total = g.Count(), Submitted = g.Count(x => x.Status == ApplicationStatuses.Submitted),
                    UnderScrutiny = g.Count(x => x.Status == ApplicationStatuses.UnderScrutiny),
                    FeePending = g.Count(x => x.Status == ApplicationStatuses.FeePending),
                    ApprovalPending = g.Count(x => x.Status == ApplicationStatuses.ApprovalPending),
                    Approved = g.Count(x => x.Status == ApplicationStatuses.Approved), Rejected = g.Count(x => x.Status == ApplicationStatuses.Rejected),
                    JePending = g.Count(x => x.Stage == ZoneStages.JEPending), OsPending = g.Count(x => x.Stage == ZoneStages.OSPending),
                    AcPending = g.Count(x => x.Stage == ZoneStages.AssistantCommissionerApprovalPending),
                    PaymentPending = g.Count(x => x.Stage == ZoneStages.PaymentRequired), Legacy = g.Count(x => x.Route != ProcessingRoutes.Zone)
                }).ToString();
                AssertSql(dashboardSql, "application dashboard aggregate");
                foreach (var role in new[] { Roles.JE, Roles.OS, Roles.AssistantCommissioner })
                {
                    var kpi = QueryService.BuildOfficerKpiAggregateQuery(QueryService.BuildOfficerKpiFlags(baseQuery, QueryService.OfficerPendingPredicate(role)));
                    var sql = kpi.ToString();
                    Assert.IsTrue(sql.IndexOf("SUM", StringComparison.OrdinalIgnoreCase) >= 0, sql);
                    Assert.IsTrue(sql.IndexOf("CASE", StringComparison.OrdinalIgnoreCase) >= 0, sql);
                    Assert.IsTrue(CountToken(sql, "SELECT") <= 4, "KPI SQL should remain one aggregate statement with only EF's fixed projection wrappers: " + sql);
                    Assert.IsFalse(sql.IndexOf("COUNT(", StringComparison.OrdinalIgnoreCase) >= 0, "KPI SQL must not embed per-row count subqueries: " + sql);
                }

                var pending = QueryService.BuildOfficerApplicationsQuery(db, Roles.JE, 23,
                    new ApplicationFilter { OfficerView = OfficerListViews.Mine, ZoneScope = 3, Page = 1, PageSize = 8 });
                var filtered = QueryService.BuildOfficerApplicationsQuery(db, Roles.JE, 23,
                    new ApplicationFilter { OfficerView = OfficerListViews.All, Search = "2026", ServiceType = "VariousBusinessSpace", From = new DateTime(2026, 1, 1), To = new DateTime(2026, 12, 31), ZoneScope = 3 });
                var processed = QueryService.BuildOfficerApplicationsQuery(db, Roles.JE, 23,
                    new ApplicationFilter { OfficerView = OfficerListViews.Processed, ZoneScope = 3 });
                var paged = QueryService.BuildOfficerPagedQuery(pending, 1, 8);
                var processedIds = QueryService.BuildOfficerProcessedIdQuery(db, 23, 3);
                var processedCount = QueryService.BuildOfficerProcessedCountQuery(baseQuery, new[] { 1, 2, 3 });

                AssertSql(pending.ToString(), "pending list");
                AssertSql(filtered.ToString(), "filtered list");
                AssertSql(processed.ToString(), "processed list");
                AssertSql(paged.ToString(), "paged list");
                AssertSql(processedIds.ToString(), "processed ID lookup");
                AssertSql(processedCount.ToString(), "processed ID count");

                // Changed predicates: OS pending includes submitted payments (not unpaid ones); AC pending requires payment done / not required.
                var osMine = QueryService.BuildOfficerApplicationsQuery(db, Roles.OS, 23, new ApplicationFilter { OfficerView = OfficerListViews.Mine, ZoneScope = 3 }).ToString();
                var acMine = QueryService.BuildOfficerApplicationsQuery(db, Roles.AssistantCommissioner, 23, new ApplicationFilter { OfficerView = OfficerListViews.Mine, ZoneScope = 3 }).ToString();
                var osKpi = QueryService.BuildOfficerKpiAggregateQuery(QueryService.BuildOfficerKpiFlags(baseQuery, QueryService.OfficerPendingPredicate(Roles.OS))).ToString();
                var acKpi = QueryService.BuildOfficerKpiAggregateQuery(QueryService.BuildOfficerKpiFlags(baseQuery, QueryService.OfficerPendingPredicate(Roles.AssistantCommissioner))).ToString();
                var osPaged = QueryService.BuildOfficerPagedQuery(QueryService.BuildOfficerApplicationsQuery(db, Roles.OS, 23, new ApplicationFilter { OfficerView = OfficerListViews.Mine, ZoneScope = 3 }), 1, 8).ToString();
                foreach (var sql in new[] { osMine, osKpi, osPaged })
                {
                    AssertSql(sql, "OS pending");
                    StringAssert.Contains(sql, "N'" + ZoneStages.PaymentRequired + "'"); StringAssert.Contains(sql, "N'" + PaymentStatuses.PaymentSubmitted + "'"); StringAssert.Contains(sql, "N'" + ZoneStages.OSPending + "'");
                }
                foreach (var sql in new[] { acMine, acKpi })
                {
                    AssertSql(sql, "AC pending");
                    StringAssert.Contains(sql, "N'" + ZoneStages.AssistantCommissionerApprovalPending + "'"); StringAssert.Contains(sql, "N'" + PaymentStatuses.PaymentDone + "'"); StringAssert.Contains(sql, "N'" + PaymentStatuses.NotRequired + "'");
                }
                Assert.IsFalse(osKpi.IndexOf("COUNT(", StringComparison.OrdinalIgnoreCase) >= 0, osKpi);
                Assert.AreEqual(System.Data.ConnectionState.Closed, connection.State, "Offline SQL generation must not open/connect to SQL Server.");
            }
        }

        private static void AssertSql(string sql, string name)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(sql), name + " produced no SQL.");
            Assert.IsTrue(sql.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase) >= 0, name + " did not produce a SELECT statement: " + sql);
        }

        private static int CountToken(string text, string token)
        {
            var count = 0; var index = 0;
            while ((index = text.IndexOf(token, index, StringComparison.OrdinalIgnoreCase)) >= 0) { count++; index += token.Length; }
            return count;
        }
    }
}
