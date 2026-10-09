using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Mappings
{
    public class CalculationMap : EntityTypeConfiguration<Calculation>
    {
        public CalculationMap()
        {
            ToTable("TR_AST_Calculations", "dbo");
            HasKey(e => e.Id);
            Property(e => e.Id).HasColumnName("Id").HasColumnType("int").HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.PropertyId).HasColumnName("PropertyId").HasColumnType("int");
            Property(e => e.Rate).HasColumnName("Rate").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.PeriodMonths).HasColumnName("PeriodMonths").HasColumnType("int");
            Property(e => e.PreviousOutstanding).HasColumnName("PreviousOutstanding").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.CurrentDemand).HasColumnName("CurrentDemand").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.CalculatedAmount).HasColumnName("CalculatedAmount").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.TotalAmount).HasColumnName("TotalAmount").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.CalculationDate).HasColumnName("CalculationDate").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.Status).HasColumnName("Status").HasColumnType("nvarchar").HasMaxLength(30).IsRequired();
            Property(e => e.Shera).HasColumnName("Shera").HasColumnType("nvarchar").HasMaxLength(2000);
            Property(e => e.CreatedBy).HasColumnName("CreatedBy").HasColumnType("nvarchar").HasMaxLength(100);
            Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.UpdatedBy).HasColumnName("UpdatedBy").HasColumnType("nvarchar").HasMaxLength(100);
            Property(e => e.UpdatedAt).HasColumnName("UpdatedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.IsDeleted).HasColumnName("IsDeleted").HasColumnType("bit");
            Property(e => e.DeletedAt).HasColumnName("DeletedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.DeletedBy).HasColumnName("DeletedBy").HasColumnType("nvarchar").HasMaxLength(100);
        }
    }
}
