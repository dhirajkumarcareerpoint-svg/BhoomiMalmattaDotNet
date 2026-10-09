using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Mappings
{
    public class DemandApplicationDocumentMap : EntityTypeConfiguration<DemandApplicationDocument>
    {
        public DemandApplicationDocumentMap()
        {
            ToTable("TR_CFC_DemandApplicationDocuments", "dbo");
            HasKey(e => e.Id);
            Property(e => e.Id).HasColumnName("Id").HasColumnType("int").HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.DemandApplicationId).HasColumnName("DemandApplicationId").HasColumnType("int");
            Property(e => e.DocumentType).HasColumnName("DocumentType").HasColumnType("nvarchar").HasMaxLength(100).IsRequired();
            Property(e => e.FileName).HasColumnName("FileName").HasColumnType("nvarchar").HasMaxLength(300).IsRequired();
            Property(e => e.StoredFileName).HasColumnName("StoredFileName").HasColumnType("nvarchar").HasMaxLength(300).IsRequired();
            Property(e => e.FilePath).HasColumnName("FilePath").HasColumnType("nvarchar").HasMaxLength(500).IsRequired();
            Property(e => e.ContentType).HasColumnName("ContentType").HasColumnType("nvarchar").HasMaxLength(100).IsRequired();
            Property(e => e.FileSizeBytes).HasColumnName("FileSizeBytes").HasColumnType("bigint");
            Property(e => e.CreatedBy).HasColumnName("CreatedBy").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.UpdatedBy).HasColumnName("UpdatedBy").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.UpdatedAt).HasColumnName("UpdatedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.IsDeleted).HasColumnName("IsDeleted").HasColumnType("bit");
            Property(e => e.DeletedAt).HasColumnName("DeletedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.DeletedBy).HasColumnName("DeletedBy").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.Shera).HasColumnName("Shera").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.VerificationStatus).HasColumnName("VerificationStatus").HasColumnType("nvarchar").HasMaxLength(30).IsRequired();
            Property(e => e.RequestRemark).HasColumnName("RequestRemark").HasColumnType("nvarchar").HasMaxLength(1000);
            Property(e => e.RequestedAt).HasColumnName("RequestedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.RequestedBy).HasColumnName("RequestedBy").HasColumnType("nvarchar").HasMaxLength(100);
            Property(e => e.RespondedAt).HasColumnName("RespondedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.RequestTokenHash).HasColumnName("RequestTokenHash").HasColumnType("nvarchar").HasMaxLength(64);
            Property(e => e.RequestTokenCreatedAt).HasColumnName("RequestTokenCreatedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.RequestTokenConsumedAt).HasColumnName("RequestTokenConsumedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.VerifiedAt).HasColumnName("VerifiedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.VerifiedBy).HasColumnName("VerifiedBy").HasColumnType("nvarchar").HasMaxLength(100);
            Property(e => e.VerifiedRole).HasColumnName("VerifiedRole").HasColumnType("nvarchar").HasMaxLength(40);
        }
    }
}
