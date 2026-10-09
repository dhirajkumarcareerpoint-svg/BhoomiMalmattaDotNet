using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Mappings
{
    public class PropertyMap : EntityTypeConfiguration<Property>
    {
        public PropertyMap()
        {
            ToTable("MR_AST_Properties", "dbo");
            HasKey(e => e.Id);
            Property(e => e.Id).HasColumnName("Id").HasColumnType("int").HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.Category).HasColumnName("Category").HasColumnType("nvarchar").HasMaxLength(40).IsRequired();
            Property(e => e.PropertyCode).HasColumnName("PropertyCode").HasColumnType("nvarchar").HasMaxLength(50).IsRequired();
            Property(e => e.Name).HasColumnName("Name").HasColumnType("nvarchar").HasMaxLength(250).IsRequired();
            Property(e => e.Ward).HasColumnName("Ward").HasColumnType("nvarchar").HasMaxLength(50);
            Property(e => e.Zone).HasColumnName("Zone").HasColumnType("nvarchar").HasMaxLength(50);
            Property(e => e.Address).HasColumnName("Address").HasColumnType("nvarchar").HasMaxLength(500);
            Property(e => e.AreaSqFt).HasColumnName("AreaSqFt").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.MonthlyRent).HasColumnName("MonthlyRent").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.AnnualDemand).HasColumnName("AnnualDemand").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.SurveyNumber).HasColumnName("SurveyNumber").HasColumnType("nvarchar").HasMaxLength(50);
            Property(e => e.TpNumber).HasColumnName("TpNumber").HasColumnType("nvarchar").HasMaxLength(50);
            Property(e => e.Status).HasColumnName("Status").HasColumnType("nvarchar").HasMaxLength(30).IsRequired();
            Property(e => e.CurrentOccupant).HasColumnName("CurrentOccupant").HasColumnType("nvarchar").HasMaxLength(200);
            Property(e => e.CreatedBy).HasColumnName("CreatedBy").HasColumnType("nvarchar").HasMaxLength(100);
            Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.UpdatedBy).HasColumnName("UpdatedBy").HasColumnType("nvarchar").HasMaxLength(100);
            Property(e => e.UpdatedAt).HasColumnName("UpdatedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.IsDeleted).HasColumnName("IsDeleted").HasColumnType("bit");
            Property(e => e.DeletedAt).HasColumnName("DeletedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.DeletedBy).HasColumnName("DeletedBy").HasColumnType("nvarchar").HasMaxLength(100);
            Property(e => e.Shera).HasColumnName("Shera").HasColumnType("nvarchar").HasMaxLength(2000);
        }
    }
}
