using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Mappings
{
    public class AuditLogMap : EntityTypeConfiguration<AuditLog>
    {
        public AuditLogMap()
        {
            ToTable("TR_AST_AuditLogs", "dbo");
            HasKey(e => e.Id);
            Property(e => e.Id).HasColumnName("Id").HasColumnType("int").HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.UserId).HasColumnName("UserId").HasColumnType("int");
            Property(e => e.UserName).HasColumnName("UserName").HasColumnType("nvarchar").HasMaxLength(150).IsRequired();
            Property(e => e.Action).HasColumnName("Action").HasColumnType("nvarchar").HasMaxLength(30).IsRequired();
            Property(e => e.EntityName).HasColumnName("EntityName").HasColumnType("nvarchar").HasMaxLength(100).IsRequired();
            Property(e => e.EntityId).HasColumnName("EntityId").HasColumnType("int");
            Property(e => e.FieldName).HasColumnName("FieldName").HasColumnType("nvarchar").HasMaxLength(100);
            Property(e => e.OldValue).HasColumnName("OldValue").HasColumnType("nvarchar").HasMaxLength(2000);
            Property(e => e.NewValue).HasColumnName("NewValue").HasColumnType("nvarchar").HasMaxLength(2000);
            Property(e => e.Timestamp).HasColumnName("Timestamp").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.IpAddress).HasColumnName("IpAddress").HasColumnType("nvarchar").HasMaxLength(50);
        }
    }
}
