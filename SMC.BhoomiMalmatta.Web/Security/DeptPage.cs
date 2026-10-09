using SMC.BhoomiMalmatta.Models.Constants;

namespace SMC.BhoomiMalmatta.Web.Security
{
    /// <summary>Base for every Department page: any officer role may enter; each page narrows further. Zone scope comes from the ticket only.</summary>
    public abstract class DeptPage : SmcPage
    {
        protected override string[] RequiredRoles { get { return Roles.OfficerRoles; } }
        /// <summary>An officer with a ZoneNumber sees only that zone; Admin and officers without a zone see all zones.</summary>
        protected int? ZoneScope { get { return Me == null || Me.Role == Roles.Admin ? null : Me.ZoneNumber; } }
    }
}
