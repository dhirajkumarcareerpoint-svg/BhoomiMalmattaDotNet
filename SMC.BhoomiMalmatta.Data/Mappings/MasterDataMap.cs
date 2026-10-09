using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Mappings
{
    public class MasterDataMap : EntityTypeConfiguration<MasterData>
    {
        public MasterDataMap()
        {
            ToTable("MR_AST_MasterData", "dbo");
            HasKey(e => e.Id);
            Property(e => e.Id).HasColumnName("Id").HasColumnType("int").HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.GroupName).HasColumnName("GroupName").HasColumnType("nvarchar").IsMaxLength().IsRequired();
            Property(e => e.Key).HasColumnName("Key").HasColumnType("nvarchar").IsMaxLength().IsRequired();
            Property(e => e.MarathiName).HasColumnName("MarathiName").HasColumnType("nvarchar").IsMaxLength().IsRequired();
            Property(e => e.EnglishName).HasColumnName("EnglishName").HasColumnType("nvarchar").IsMaxLength().IsRequired();
            Property(e => e.Description).HasColumnName("Description").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.IsActive).HasColumnName("IsActive").HasColumnType("bit");
            Property(e => e.DisplayOrder).HasColumnName("DisplayOrder").HasColumnType("int");
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
