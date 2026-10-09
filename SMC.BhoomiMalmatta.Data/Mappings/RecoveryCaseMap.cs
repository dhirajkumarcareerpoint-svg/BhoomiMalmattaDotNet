using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Mappings
{
    public class RecoveryCaseMap : EntityTypeConfiguration<RecoveryCase>
    {
        public RecoveryCaseMap()
        {
            ToTable("TR_AST_RecoveryCases", "dbo");
            HasKey(e => e.Id);
            Property(e => e.Id).HasColumnName("Id").HasColumnType("int").HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.PropertyId).HasColumnName("PropertyId").HasColumnType("int");
            Property(e => e.LeaseId).HasColumnName("LeaseId").HasColumnType("int");
            Property(e => e.MonthsOverdue).HasColumnName("MonthsOverdue").HasColumnType("int");
            Property(e => e.OutstandingAmount).HasColumnName("OutstandingAmount").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.Stage).HasColumnName("Stage").HasColumnType("nvarchar").HasMaxLength(30).IsRequired();
            Property(e => e.NoticeNumber).HasColumnName("NoticeNumber").HasColumnType("nvarchar").HasMaxLength(100);
            Property(e => e.NoticeDate).HasColumnName("NoticeDate").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.RecoveredAmount).HasColumnName("RecoveredAmount").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.RecoveryDate).HasColumnName("RecoveryDate").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.SealDate).HasColumnName("SealDate").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.ReAuctionDate).HasColumnName("ReAuctionDate").HasColumnType("datetime2").HasPrecision(7);
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
