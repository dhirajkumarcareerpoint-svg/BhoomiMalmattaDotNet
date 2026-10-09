using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Mappings
{
    public class AllocationProcessMap : EntityTypeConfiguration<AllocationProcess>
    {
        public AllocationProcessMap()
        {
            ToTable("TR_AST_AllocationProcesses", "dbo");
            HasKey(e => e.Id);
            Property(e => e.Id).HasColumnName("Id").HasColumnType("int").HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.PropertyId).HasColumnName("PropertyId").HasColumnType("int");
            Property(e => e.Method).HasColumnName("Method").HasColumnType("nvarchar").HasMaxLength(30).IsRequired();
            Property(e => e.NoticeNumber).HasColumnName("NoticeNumber").HasColumnType("nvarchar").HasMaxLength(100);
            Property(e => e.PublishDate).HasColumnName("PublishDate").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.LastDateToApply).HasColumnName("LastDateToApply").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.AuctionDate).HasColumnName("AuctionDate").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.ReserveAmount).HasColumnName("ReserveAmount").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.HighestBidAmount).HasColumnName("HighestBidAmount").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.HighestBidderName).HasColumnName("HighestBidderName").HasColumnType("nvarchar").HasMaxLength(200);
            Property(e => e.HighestBidderMobile).HasColumnName("HighestBidderMobile").HasColumnType("nvarchar").HasMaxLength(15);
            Property(e => e.Status).HasColumnName("Status").HasColumnType("nvarchar").HasMaxLength(30).IsRequired();
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
