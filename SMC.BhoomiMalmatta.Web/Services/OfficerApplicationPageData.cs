using System;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Models.Dto;

namespace SMC.BhoomiMalmatta.Web.Services
{
    /// <summary>The officer application page is deliberately limited to one application page query and one grouped KPI query.</summary>
    public sealed class OfficerApplicationPageData
    {
        public PagedResult<ApplicationListItem> Applications { get; private set; }
        public OfficerDashboardCounts Counts { get; private set; }

        public static OfficerApplicationPageData Load(IQueryService query, string role, int userId, int? zoneScope, ApplicationFilter filter, Action<Exception> onKpiFailure = null)
        {
            var data = new OfficerApplicationPageData
            {
                Applications = query.SearchOfficerApplications(role, userId, filter),
                Counts = new OfficerDashboardCounts()
            };
            try { data.Counts = query.GetOfficerDashboardCounts(role, zoneScope, userId) ?? new OfficerDashboardCounts(); }
            catch (Exception ex)
            {
                // One attempt only. Keep the successfully loaded application list visible.
                if (onKpiFailure != null) onKpiFailure(ex);
            }
            return data;
        }
    }
}
