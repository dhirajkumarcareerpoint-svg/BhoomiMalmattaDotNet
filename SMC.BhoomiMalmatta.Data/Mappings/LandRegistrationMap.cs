using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Mappings
{
    public class LandRegistrationMap : EntityTypeConfiguration<LandRegistration>
    {
        public LandRegistrationMap()
        {
            ToTable("TR_AST_LandRegistrations", "dbo");
            HasKey(e => e.Id);
            Property(e => e.Id).HasColumnName("Id").HasColumnType("int").HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.RecordNumber).HasColumnName("RecordNumber").HasColumnType("varchar").HasMaxLength(25).IsUnicode(false).HasDatabaseGeneratedOption(DatabaseGeneratedOption.Computed);
            Property(e => e.SourceType).HasColumnName("SourceType").HasColumnType("nvarchar").HasMaxLength(200).IsRequired();
            Property(e => e.PropertyId).HasColumnName("PropertyId").HasColumnType("int");
            Property(e => e.SurveyNumber).HasColumnName("SurveyNumber").HasColumnType("nvarchar").HasMaxLength(200).IsRequired();
            Property(e => e.Address).HasColumnName("Address").HasColumnType("nvarchar").HasMaxLength(2000).IsRequired();
            Property(e => e.Area).HasColumnName("Area").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.PurchaseDeedNumber).HasColumnName("PurchaseDeedNumber").HasColumnType("nvarchar").HasMaxLength(200).IsRequired();
            Property(e => e.PurchaseDeedDate).HasColumnName("PurchaseDeedDate").HasColumnType("date");
            Property(e => e.OwnerDetails).HasColumnName("OwnerDetails").HasColumnType("nvarchar").HasMaxLength(2000).IsRequired();
            Property(e => e.RegistrationStatus).HasColumnName("RegistrationStatus").HasColumnType("nvarchar").HasMaxLength(200).IsRequired();
            Property(e => e.RegistrationDate).HasColumnName("RegistrationDate").HasColumnType("date");
            Property(e => e.Status).HasColumnName("Status").HasColumnType("nvarchar").HasMaxLength(200).IsRequired();
            Property(e => e.CreatedBy).HasColumnName("CreatedBy").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.UpdatedBy).HasColumnName("UpdatedBy").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.UpdatedAt).HasColumnName("UpdatedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.IsDeleted).HasColumnName("IsDeleted").HasColumnType("bit");
            Property(e => e.DeletedAt).HasColumnName("DeletedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.DeletedBy).HasColumnName("DeletedBy").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.Shera).HasColumnName("Shera").HasColumnType("nvarchar").HasMaxLength(2000);
        }
    }
}
