using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Mappings
{
    public class ComplaintMap : EntityTypeConfiguration<Complaint>
    {
        public ComplaintMap()
        {
            ToTable("TR_CFC_Complaints", "dbo");
            HasKey(e => e.Id);
            Property(e => e.Id).HasColumnName("Id").HasColumnType("int").HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.RecordNumber).HasColumnName("RecordNumber").HasColumnType("varchar").HasMaxLength(24).IsUnicode(false).HasDatabaseGeneratedOption(DatabaseGeneratedOption.Computed);
            Property(e => e.ComplaintType).HasColumnName("ComplaintType").HasColumnType("nvarchar").HasMaxLength(200).IsRequired();
            Property(e => e.PropertyId).HasColumnName("PropertyId").HasColumnType("int");
            Property(e => e.ComplainantName).HasColumnName("ComplainantName").HasColumnType("nvarchar").HasMaxLength(200).IsRequired();
            Property(e => e.Mobile).HasColumnName("Mobile").HasColumnType("nvarchar").HasMaxLength(200).IsRequired();
            Property(e => e.ComplaintDetails).HasColumnName("ComplaintDetails").HasColumnType("nvarchar").HasMaxLength(2000).IsRequired();
            Property(e => e.ComplaintDate).HasColumnName("ComplaintDate").HasColumnType("date");
            Property(e => e.AssignedOfficer).HasColumnName("AssignedOfficer").HasColumnType("nvarchar").HasMaxLength(200);
            Property(e => e.Status).HasColumnName("Status").HasColumnType("nvarchar").HasMaxLength(200).IsRequired();
            Property(e => e.ResolutionDate).HasColumnName("ResolutionDate").HasColumnType("date");
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
