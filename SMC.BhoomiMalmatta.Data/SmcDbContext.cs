using System.Data.Entity;
using System.Data.Common;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.ModelConfiguration.Conventions;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data
{
    /// <summary>
    /// Entity Framework 6 context over the EXISTING SMC_BhoomiMalmatta database.
    /// The initializer is disabled: nothing here creates, alters, drops or migrates the database.
    /// </summary>
    public class SmcDbContext : DbContext
    {
        static SmcDbContext()
        {
            Database.SetInitializer<SmcDbContext>(null);
        }

        public SmcDbContext() : base("name=SmcDatabase")
        {
            ConfigureRuntime();
        }

        internal SmcDbContext(DbConnection connection, DbCompiledModel model) : base(connection, model, false)
        { ConfigureRuntime(); }

        private void ConfigureRuntime()
        {
            Configuration.LazyLoadingEnabled = false;
            Configuration.ProxyCreationEnabled = false;
        }

        public DbSet<GalaTransferRate> GalaTransferRates { get; set; }
        public DbSet<MasterData> MasterData { get; set; }
        public DbSet<Property> Properties { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<ApplicationNumberConfiguration> ApplicationNumberConfigurations { get; set; }
        public DbSet<DemandBusinessType> DemandBusinessTypes { get; set; }
        public DbSet<DemandServiceRate> DemandServiceRates { get; set; }
        public DbSet<DemandService> DemandServices { get; set; }
        public DbSet<PermitConfiguration> PermitConfigurations { get; set; }
        public DbSet<PermitTerm> PermitTerms { get; set; }
        public DbSet<SmsTemplate> SmsTemplates { get; set; }
        public DbSet<AllocationProcess> AllocationProcesses { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Calculation> Calculations { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<GalaTransferDocument> GalaTransferDocuments { get; set; }
        public DbSet<GalaTransfer> GalaTransfers { get; set; }
        public DbSet<LandRegistration> LandRegistrations { get; set; }
        public DbSet<Lease> Leases { get; set; }
        public DbSet<RecoveryCase> RecoveryCases { get; set; }
        public DbSet<SchemeApplication> SchemeApplications { get; set; }
        public DbSet<Complaint> Complaints { get; set; }
        public DbSet<DemandApplicationDocument> DemandApplicationDocuments { get; set; }
        public DbSet<DemandApplication> DemandApplications { get; set; }
        public DbSet<DemandApplicationWorkflow> DemandApplicationWorkflows { get; set; }
        public DbSet<DemandApplicationZoneDocument> DemandApplicationZoneDocuments { get; set; }
        public DbSet<DemandApplicationZoneWorkflow> DemandApplicationZoneWorkflows { get; set; }
        public DbSet<SmsEvent> SmsEvents { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            ConfigureModel(modelBuilder);
        }

        internal static void ConfigureModel(DbModelBuilder modelBuilder)
        {
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();
            modelBuilder.Configurations.AddFromAssembly(typeof(SmcDbContext).Assembly);
        }
    }
}
