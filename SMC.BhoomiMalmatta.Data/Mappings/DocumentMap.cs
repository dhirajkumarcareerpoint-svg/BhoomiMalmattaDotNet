using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Mappings
{
    public class DocumentMap : EntityTypeConfiguration<Document>
    {
        public DocumentMap()
        {
            ToTable("TR_AST_Documents", "dbo");
            HasKey(e => e.Id);
            Property(e => e.Id).HasColumnName("Id").HasColumnType("int").HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.EntityType).HasColumnName("EntityType").HasColumnType("nvarchar").HasMaxLength(30).IsRequired();
            Property(e => e.EntityId).HasColumnName("EntityId").HasColumnType("int");
            Property(e => e.FileName).HasColumnName("FileName").HasColumnType("nvarchar").HasMaxLength(300).IsRequired();
            Property(e => e.StoredFileName).HasColumnName("StoredFileName").HasColumnType("nvarchar").HasMaxLength(300).IsRequired();
            Property(e => e.FilePath).HasColumnName("FilePath").HasColumnType("nvarchar").HasMaxLength(500).IsRequired();
            Property(e => e.ContentType).HasColumnName("ContentType").HasColumnType("nvarchar").HasMaxLength(100).IsRequired();
            Property(e => e.FileSizeBytes).HasColumnName("FileSizeBytes").HasColumnType("bigint");
            Property(e => e.PropertyId).HasColumnName("PropertyId").HasColumnType("int");
            Property(e => e.LeaseId).HasColumnName("LeaseId").HasColumnType("int");
            Property(e => e.RecoveryCaseId).HasColumnName("RecoveryCaseId").HasColumnType("int");
            Property(e => e.SchemeApplicationId).HasColumnName("SchemeApplicationId").HasColumnType("int");
            Property(e => e.AllocationProcessId).HasColumnName("AllocationProcessId").HasColumnType("int");
            Property(e => e.CreatedBy).HasColumnName("CreatedBy").HasColumnType("nvarchar").HasMaxLength(100);
            Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.UpdatedBy).HasColumnName("UpdatedBy").HasColumnType("nvarchar").HasMaxLength(100);
            Property(e => e.UpdatedAt).HasColumnName("UpdatedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.IsDeleted).HasColumnName("IsDeleted").HasColumnType("bit");
            Property(e => e.DeletedAt).HasColumnName("DeletedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.DeletedBy).HasColumnName("DeletedBy").HasColumnType("nvarchar").HasMaxLength(100);
            Property(e => e.Shera).HasColumnName("Shera").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.CalculationId).HasColumnName("CalculationId").HasColumnType("int");
            Property(e => e.ComplaintId).HasColumnName("ComplaintId").HasColumnType("int");
            Property(e => e.LandRegistrationId).HasColumnName("LandRegistrationId").HasColumnType("int");
        }
    }
}
