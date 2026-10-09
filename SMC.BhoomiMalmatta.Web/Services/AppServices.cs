using System;
using System.Configuration;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Business.Documents;
using SMC.BhoomiMalmatta.Business.Numbering;
using SMC.BhoomiMalmatta.Business.PropertyRegister;
using SMC.BhoomiMalmatta.Business.Security;
using SMC.BhoomiMalmatta.Business.Sms;
using SMC.BhoomiMalmatta.Business.Storage;
using SMC.BhoomiMalmatta.Business.Submission;
using SMC.BhoomiMalmatta.Business.Workflow;
using SMC.BhoomiMalmatta.Models.Common;
using SMC.BhoomiMalmatta.Web.Security;

namespace SMC.BhoomiMalmatta.Web.Services
{
    /// <summary>
    /// Application-wide service graph (simple composition root; no DI container needed). The data-bound members are assigned by
    /// <see cref="Composition"/>, which is the only place that touches the Entity Framework layer.
    /// </summary>
    public static class AppServices
    {
        public static string StartupError;
        public static bool IsHealthy { get { return StartupError == null; } }

        public static IMasterDataReader Masters;
        public static IQueryService Query;
        public static IRegisterStore Register;
        /// <summary>Dedicated Properties page: data access, option source (MR_AST_MasterData + values in use), cached options and use cases.</summary>
        public static IPropertyRegister PropertyStore;
        public static IPropertyOptionSource PropertySource;
        public static PropertyMasterOptions PropertyOptions;
        public static PropertyService Properties;
        public static IUserStore Users;
        public static IAuditWriter Audit;
        public static ISmsEventStore SmsEvents;
        public static IApplicationNumberStore Numbers;
        public static IDemandApplicationStore Applications;
        public static IZoneWorkflowStore WorkflowStore;
        public static IPasswordVerifier PasswordVerifier;
        public static IPdfGenerator Pdf;
        public static ISmsGateway SmsGateway;

        public static IClock Clock = new SystemClock();
        public static IFileStorage Files;
        public static INotifier Notifier;
        public static IOtpSmsSender OtpSms;
        public static ZoneWorkflowService Workflow;
        /// <summary>Workflow switches from Web.config (Workflow.AcAllowReturnReject, default false).</summary>
        public static WorkflowPolicy WorkflowPolicy = WorkflowPolicy.Default;
        /// <summary>Apply.CollectApplicantDetails (default false): the wizard and POST api/v1/applications ask for / require name and mobile only when true.</summary>
        public static bool CollectApplicantDetails;
        public static DemandSubmissionService Submission;
        public static AuthService Auth;
        public static DocumentService Documents;
        public static CertificateService Certificates;
        public static readonly RateLimiter Limiter = new RateLimiter();
        public static readonly LoginThrottle StatusThrottle = new LoginThrottle(8, 15);

        public static string Setting(string key, string fallback = null) { return ConfigurationManager.AppSettings[key] ?? fallback; }
        public static bool SettingBool(string key) { bool b; return bool.TryParse(Setting(key), out b) && b; }

        /// <summary>Called once at start-up after the data-bound members were assigned.</summary>
        public static void Build()
        {
            Files = new LocalFileStorage(Environment.ExpandEnvironmentVariables(Setting("Storage.Root", @"%LOCALAPPDATA%\SMC_BhoomiMalmatta\Uploads")));
            var sms = new SmsService(Masters, SmsGateway, SmsEvents, Users, Clock, SettingBool("Sms.Enabled"), Setting("Sms.SenderId", "MAHGOV"));
            Notifier = sms;
            OtpSms = sms;
            Certificates = new CertificateService(Query, Masters, Pdf, Files, WorkflowStore, Notifier, Audit, Clock);
            WorkflowPolicy = new WorkflowPolicy(SettingBool("Workflow.AcAllowReturnReject"));
            Workflow = new ZoneWorkflowService(WorkflowStore, Clock, Notifier, Certificates, Files, WorkflowPolicy, new ErrorLog());
            CollectApplicantDetails = SettingBool(ApplyWizard.CollectApplicantDetailsSetting);
            Submission = new DemandSubmissionService(Masters, new ApplicationNumberService(Numbers, Clock, Setting("Numbering.Separator", "-")), Applications, Files, Clock, Notifier, CollectApplicantDetails);
            Auth = new AuthService(Users, PasswordVerifier, Audit, Clock, new LoginThrottle(5, 15));
            Documents = new DocumentService(Query, Files, Audit, Clock);
            if (PropertySource != null)
            {
                PropertyOptions = new PropertyMasterOptions(PropertySource, Clock, new PropertyOptionGroups
                {
                    CategoryGroup = Setting("Properties.CategoryGroup", PropertyOptionGroups.DefaultCategoryGroup), StatusGroup = Setting("Properties.StatusGroup", PropertyOptionGroups.DefaultStatusGroup),
                    WardGroup = Setting("Properties.WardGroup", string.Empty), ZoneGroup = Setting("Properties.ZoneGroup", string.Empty)
                });
                Properties = new PropertyService(PropertyStore, PropertyOptions, Clock);
            }
        }
    }
}
