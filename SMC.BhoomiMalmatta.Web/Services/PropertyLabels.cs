using SMC.BhoomiMalmatta.Business.PropertyRegister;

namespace SMC.BhoomiMalmatta.Web.Services
{
    /// <summary>Property category / status labels for reports and PDFs, from the same database-backed provider as the Properties page (raw code when unknown).</summary>
    public static class PropertyLabels
    {
        public static string Category(string code) { var o = AppServices.PropertyOptions; return o == null ? code : PropertyMasterOptions.LabelOf(o.Get().Categories, code); }
        public static string Status(string code) { var o = AppServices.PropertyOptions; return o == null ? code : PropertyMasterOptions.LabelOf(o.Get().Statuses, code); }
    }
}
