using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Mappings
{
    public class GalaTransferMap : EntityTypeConfiguration<GalaTransfer>
    {
        public GalaTransferMap()
        {
            ToTable("TR_AST_GalaTransfers", "dbo");
            HasKey(e => e.Id);
            Property(e => e.Id).HasColumnName("Id").HasColumnType("int").HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.PropertyId).HasColumnName("PropertyId").HasColumnType("int");
            Property(e => e.Category).HasColumnName("Category").HasColumnType("nvarchar").HasMaxLength(30).IsRequired();
            Property(e => e.ExistingHolder).HasColumnName("ExistingHolder").HasColumnType("nvarchar").HasMaxLength(200).IsRequired();
            Property(e => e.CurrentOccupant).HasColumnName("CurrentOccupant").HasColumnType("nvarchar").HasMaxLength(200).IsRequired();
            Property(e => e.NewHolder).HasColumnName("NewHolder").HasColumnType("nvarchar").HasMaxLength(200).IsRequired();
            Property(e => e.TransferType).HasColumnName("TransferType").HasColumnType("nvarchar").HasMaxLength(30).IsRequired();
            Property(e => e.IsBreach).HasColumnName("IsBreach").HasColumnType("bit");
            Property(e => e.TransferFee).HasColumnName("TransferFee").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.Penalty).HasColumnName("Penalty").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.Stage).HasColumnName("Stage").HasColumnType("nvarchar").HasMaxLength(40).IsRequired();
            Property(e => e.PaymentReference).HasColumnName("PaymentReference").HasColumnType("nvarchar").HasMaxLength(100);
            Property(e => e.PaymentVerifiedAt).HasColumnName("PaymentVerifiedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.ApprovedAt).HasColumnName("ApprovedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.ApprovedBy).HasColumnName("ApprovedBy").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.DecisionRemark).HasColumnName("DecisionRemark").HasColumnType("nvarchar").HasMaxLength(2000);
            Property(e => e.Version).HasColumnName("Version").IsRowVersion();
            Property(e => e.CreatedBy).HasColumnName("CreatedBy").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.UpdatedBy).HasColumnName("UpdatedBy").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.UpdatedAt).HasColumnName("UpdatedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.IsDeleted).HasColumnName("IsDeleted").HasColumnType("bit");
            Property(e => e.DeletedAt).HasColumnName("DeletedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.DeletedBy).HasColumnName("DeletedBy").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.Shera).HasColumnName("Shera").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.PropertyHolderAtRequest).HasColumnName("PropertyHolderAtRequest").HasColumnType("nvarchar").HasMaxLength(200);
        }
    }
}
