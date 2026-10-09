using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Mappings
{
    public class SmsEventMap : EntityTypeConfiguration<SmsEvent>
    {
        public SmsEventMap()
        {
            ToTable("TR_CFC_SmsEvents", "dbo");
            HasKey(e => e.Id);
            Property(e => e.Id).HasColumnName("Id").HasColumnType("int").HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.RecipientMobile).HasColumnName("RecipientMobile").HasColumnType("nvarchar").HasMaxLength(15).IsRequired();
            Property(e => e.EventType).HasColumnName("EventType").HasColumnType("nvarchar").HasMaxLength(80).IsRequired();
            Property(e => e.ApplicationNumber).HasColumnName("ApplicationNumber").HasColumnType("nvarchar").HasMaxLength(40);
            Property(e => e.TemplateReference).HasColumnName("TemplateReference").HasColumnType("nvarchar").HasMaxLength(200);
            Property(e => e.Status).HasColumnName("Status").HasColumnType("nvarchar").HasMaxLength(30).IsRequired();
            Property(e => e.ProviderMessageId).HasColumnName("ProviderMessageId").HasColumnType("nvarchar").HasMaxLength(200);
            Property(e => e.SentAt).HasColumnName("SentAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.FailureReason).HasColumnName("FailureReason").HasColumnType("nvarchar").HasMaxLength(1000);
            Property(e => e.RetryCount).HasColumnName("RetryCount").HasColumnType("int");
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
