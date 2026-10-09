using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Mappings
{
    public class DemandServiceRateMap : EntityTypeConfiguration<DemandServiceRate>
    {
        public DemandServiceRateMap()
        {
            ToTable("MR_CFC_DemandServiceRates", "dbo");
            HasKey(e => e.Id);
            Property(e => e.Id).HasColumnName("Id").HasColumnType("int").HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.ServiceCode).HasColumnName("ServiceCode").HasColumnType("nvarchar").HasMaxLength(80).IsRequired();
            Property(e => e.Rate).HasColumnName("Rate").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.Unit).HasColumnName("Unit").HasColumnType("nvarchar").HasMaxLength(40).IsRequired();
            Property(e => e.IsActive).HasColumnName("IsActive").HasColumnType("bit");
            Property(e => e.CreatedBy).HasColumnName("CreatedBy").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.UpdatedBy).HasColumnName("UpdatedBy").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.UpdatedAt).HasColumnName("UpdatedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.IsDeleted).HasColumnName("IsDeleted").HasColumnType("bit");
            Property(e => e.DeletedAt).HasColumnName("DeletedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.DeletedBy).HasColumnName("DeletedBy").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.Shera).HasColumnName("Shera").HasColumnType("nvarchar").IsMaxLength();
        }
    }
}
