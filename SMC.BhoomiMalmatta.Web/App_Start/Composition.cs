using SMC.BhoomiMalmatta.Data.Repositories;
using SMC.BhoomiMalmatta.Web.Services;

namespace SMC.BhoomiMalmatta.Web
{
    /// <summary>The only class that knows the concrete Entity Framework repositories, PDF library, bcrypt library and SMS gateway.</summary>
    internal static class Composition
    {
        public static void Wire()
        {
            AppServices.Masters = new MasterDataRepository();
            AppServices.Query = new QueryService();
            AppServices.Register = new RegisterStore();
            var properties = new PropertyRegisterStore();
            AppServices.PropertyStore = properties;
            AppServices.PropertySource = properties;
            AppServices.Users = new UserStore();
            AppServices.Audit = new AuditWriter();
            AppServices.SmsEvents = new SmsEventStore();
            AppServices.Numbers = new ApplicationNumberStore();
            AppServices.Applications = new DemandApplicationStore();
            AppServices.WorkflowStore = new ZoneWorkflowStore();
            AppServices.PasswordVerifier = new Security.BcryptPasswordVerifier();
            AppServices.Pdf = new Services.PdfGenerator();
            AppServices.SmsGateway = new Services.AclSmsGateway();
            AppServices.Build();
        }
    }
}
