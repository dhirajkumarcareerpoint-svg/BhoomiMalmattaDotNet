using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Mappings
{
    public class SchemeApplicationMap : EntityTypeConfiguration<SchemeApplication>
    {
        public SchemeApplicationMap()
        {
            ToTable("TR_AST_SchemeApplications", "dbo");
            HasKey(e => e.Id);
            Property(e => e.Id).HasColumnName("Id").HasColumnType("int").HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.PropertyId).HasColumnName("PropertyId").HasColumnType("int");
            Property(e => e.SchemeType).HasColumnName("SchemeType").HasColumnType("nvarchar").HasMaxLength(30).IsRequired();
            Property(e => e.ApplicantName).HasColumnName("ApplicantName").HasColumnType("nvarchar").HasMaxLength(200).IsRequired();
            Property(e => e.ApplicantMobile).HasColumnName("ApplicantMobile").HasColumnType("nvarchar").HasMaxLength(15);
            Property(e => e.ApplicationDate).HasColumnName("ApplicationDate").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.OriginalOutstanding).HasColumnName("OriginalOutstanding").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.WaivedAmount).HasColumnName("WaivedAmount").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.PayableAmount).HasColumnName("PayableAmount").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.Status).HasColumnName("Status").HasColumnType("nvarchar").HasMaxLength(20).IsRequired();
            Property(e => e.DecisionDate).HasColumnName("DecisionDate").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.ApprovedBy).HasColumnName("ApprovedBy").HasColumnType("nvarchar").HasMaxLength(150);
            Property(e => e.CreatedBy).HasColumnName("CreatedBy").HasColumnType("nvarchar").HasMaxLength(100);
            Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.UpdatedBy).HasColumnName("UpdatedBy").HasColumnType("nvarchar").HasMaxLength(100);
            Property(e => e.UpdatedAt).HasColumnName("UpdatedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.IsDeleted).HasColumnName("IsDeleted").HasColumnType("bit");
            Property(e => e.DeletedAt).HasColumnName("DeletedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.DeletedBy).HasColumnName("DeletedBy").HasColumnType("nvarchar").HasMaxLength(100);
            Property(e => e.Shera).HasColumnName("Shera").HasColumnType("nvarchar").HasMaxLength(2000);
            Property(e => e.PropertyCategory).HasColumnName("PropertyCategory").HasColumnType("nvarchar").HasMaxLength(40);
        }
    }
}
