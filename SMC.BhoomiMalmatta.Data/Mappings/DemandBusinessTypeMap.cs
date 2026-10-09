using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Mappings
{
    public class DemandBusinessTypeMap : EntityTypeConfiguration<DemandBusinessType>
    {
        public DemandBusinessTypeMap()
        {
            ToTable("MR_CFC_DemandBusinessTypes", "dbo");
            HasKey(e => e.Id);
            Property(e => e.Id).HasColumnName("Id").HasColumnType("int").HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.Value).HasColumnName("Value").HasColumnType("int");
            Property(e => e.Code).HasColumnName("Code").HasColumnType("nvarchar").HasMaxLength(80).IsRequired();
            Property(e => e.Name).HasColumnName("Name").HasColumnType("nvarchar").HasMaxLength(160).IsRequired();
            Property(e => e.NameMr).HasColumnName("NameMr").HasColumnType("nvarchar").HasMaxLength(160).IsRequired();
            Property(e => e.SortOrder).HasColumnName("SortOrder").HasColumnType("int");
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
