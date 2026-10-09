using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Mappings
{
    public class PermitConfigurationMap : EntityTypeConfiguration<PermitConfiguration>
    {
        public PermitConfigurationMap()
        {
            ToTable("MR_CFC_PermitConfigurations", "dbo");
            HasKey(e => e.Id);
            Property(e => e.Id).HasColumnName("Id").HasColumnType("int").HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.OrganizationName).HasColumnName("OrganizationName").HasColumnType("nvarchar").HasMaxLength(200).IsRequired();
            Property(e => e.DepartmentName).HasColumnName("DepartmentName").HasColumnType("nvarchar").HasMaxLength(250).IsRequired();
            Property(e => e.CertificateTitle).HasColumnName("CertificateTitle").HasColumnType("nvarchar").HasMaxLength(250).IsRequired();
            Property(e => e.AuthorityDesignation).HasColumnName("AuthorityDesignation").HasColumnType("nvarchar").HasMaxLength(150).IsRequired();
            Property(e => e.FooterText).HasColumnName("FooterText").HasColumnType("nvarchar").HasMaxLength(300).IsRequired();
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
