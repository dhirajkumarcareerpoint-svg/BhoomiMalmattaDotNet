using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Mappings
{
    public class GalaTransferDocumentMap : EntityTypeConfiguration<GalaTransferDocument>
    {
        public GalaTransferDocumentMap()
        {
            ToTable("TR_AST_GalaTransferDocuments", "dbo");
            HasKey(e => e.Id);
            Property(e => e.Id).HasColumnName("Id").HasColumnType("int").HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.GalaTransferId).HasColumnName("GalaTransferId").HasColumnType("int");
            Property(e => e.Kind).HasColumnName("Kind").HasColumnType("nvarchar").HasMaxLength(20).IsRequired();
            Property(e => e.FileName).HasColumnName("FileName").HasColumnType("nvarchar").HasMaxLength(255).IsRequired();
            Property(e => e.FilePath).HasColumnName("FilePath").HasColumnType("nvarchar").HasMaxLength(500).IsRequired();
            Property(e => e.ContentType).HasColumnName("ContentType").HasColumnType("nvarchar").HasMaxLength(100).IsRequired();
            Property(e => e.Size).HasColumnName("Size").HasColumnType("bigint");
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
