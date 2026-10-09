using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Mappings
{
    public class LeaseMap : EntityTypeConfiguration<Lease>
    {
        public LeaseMap()
        {
            ToTable("TR_AST_Leases", "dbo");
            HasKey(e => e.Id);
            Property(e => e.Id).HasColumnName("Id").HasColumnType("int").HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.PropertyId).HasColumnName("PropertyId").HasColumnType("int");
            Property(e => e.LesseeName).HasColumnName("LesseeName").HasColumnType("nvarchar").HasMaxLength(200).IsRequired();
            Property(e => e.LesseeMobile).HasColumnName("LesseeMobile").HasColumnType("nvarchar").HasMaxLength(15);
            Property(e => e.LesseeAddress).HasColumnName("LesseeAddress").HasColumnType("nvarchar").HasMaxLength(500);
            Property(e => e.DeedNumber).HasColumnName("DeedNumber").HasColumnType("nvarchar").HasMaxLength(100).IsRequired();
            Property(e => e.DeedDate).HasColumnName("DeedDate").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.DurationType).HasColumnName("DurationType").HasColumnType("nvarchar").HasMaxLength(30).IsRequired();
            Property(e => e.StartDate).HasColumnName("StartDate").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.EndDate).HasColumnName("EndDate").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.RentAmount).HasColumnName("RentAmount").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.SecurityDeposit).HasColumnName("SecurityDeposit").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.Status).HasColumnName("Status").HasColumnType("nvarchar").HasMaxLength(20).IsRequired();
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
