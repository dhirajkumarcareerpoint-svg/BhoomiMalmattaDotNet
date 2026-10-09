using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Mappings
{
    public class SmsTemplateMap : EntityTypeConfiguration<SmsTemplate>
    {
        public SmsTemplateMap()
        {
            ToTable("MR_CFC_SmsTemplates", "dbo");
            HasKey(e => e.Id);
            Property(e => e.Id).HasColumnName("Id").HasColumnType("int").HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.EventType).HasColumnName("EventType").HasColumnType("nvarchar").HasMaxLength(80).IsRequired();
            Property(e => e.TemplateName).HasColumnName("TemplateName").HasColumnType("nvarchar").HasMaxLength(160).IsRequired();
            Property(e => e.MessageBody).HasColumnName("MessageBody").HasColumnType("nvarchar").IsMaxLength().IsRequired();
            Property(e => e.VariableMapping).HasColumnName("VariableMapping").HasColumnType("nvarchar").HasMaxLength(1000).IsRequired();
            Property(e => e.DltTemplateId).HasColumnName("DltTemplateId").HasColumnType("nvarchar").HasMaxLength(200);
            Property(e => e.SenderId).HasColumnName("SenderId").HasColumnType("nvarchar").HasMaxLength(30);
            Property(e => e.Language).HasColumnName("Language").HasColumnType("nvarchar").HasMaxLength(20).IsRequired();
            Property(e => e.Version).HasColumnName("Version").HasColumnType("int");
            Property(e => e.IsActive).HasColumnName("IsActive").HasColumnType("bit");
            Property(e => e.ApprovalStatus).HasColumnName("ApprovalStatus").HasColumnType("nvarchar").HasMaxLength(40).IsRequired();
            Property(e => e.SampleMessage).HasColumnName("SampleMessage").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.CreatedBy).HasColumnName("CreatedBy").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.UpdatedBy).HasColumnName("UpdatedBy").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.UpdatedAt).HasColumnName("UpdatedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.IsDeleted).HasColumnName("IsDeleted").HasColumnType("bit");
            Property(e => e.DeletedAt).HasColumnName("DeletedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.DeletedBy).HasColumnName("DeletedBy").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.Shera).HasColumnName("Shera").HasColumnType("nvarchar").IsMaxLength();
        }
    }
}
