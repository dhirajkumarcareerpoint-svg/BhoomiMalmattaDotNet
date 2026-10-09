using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Mappings
{
    public class UserMap : EntityTypeConfiguration<User>
    {
        public UserMap()
        {
            ToTable("MR_AST_Users", "dbo");
            HasKey(e => e.Id);
            Property(e => e.Id).HasColumnName("Id").HasColumnType("int").HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.Username).HasColumnName("Username").HasColumnType("nvarchar").HasMaxLength(50).IsRequired();
            Property(e => e.PasswordHash).HasColumnName("PasswordHash").HasColumnType("nvarchar").IsMaxLength().IsRequired();
            Property(e => e.FullName).HasColumnName("FullName").HasColumnType("nvarchar").HasMaxLength(150).IsRequired();
            Property(e => e.Designation).HasColumnName("Designation").HasColumnType("nvarchar").HasMaxLength(100);
            Property(e => e.Mobile).HasColumnName("Mobile").HasColumnType("nvarchar").HasMaxLength(15);
            Property(e => e.Email).HasColumnName("Email").HasColumnType("nvarchar").HasMaxLength(150);
            Property(e => e.Role).HasColumnName("Role").HasColumnType("nvarchar").HasMaxLength(50).IsRequired();
            Property(e => e.IsActive).HasColumnName("IsActive").HasColumnType("bit");
            Property(e => e.LastLoginAt).HasColumnName("LastLoginAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.CreatedBy).HasColumnName("CreatedBy").HasColumnType("nvarchar").HasMaxLength(100);
            Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.UpdatedBy).HasColumnName("UpdatedBy").HasColumnType("nvarchar").HasMaxLength(100);
            Property(e => e.UpdatedAt).HasColumnName("UpdatedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.IsDeleted).HasColumnName("IsDeleted").HasColumnType("bit");
            Property(e => e.DeletedAt).HasColumnName("DeletedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.DeletedBy).HasColumnName("DeletedBy").HasColumnType("nvarchar").HasMaxLength(100);
            Property(e => e.Shera).HasColumnName("Shera").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.ZoneNumber).HasColumnName("ZoneNumber").HasColumnType("int");
        }
    }
}
