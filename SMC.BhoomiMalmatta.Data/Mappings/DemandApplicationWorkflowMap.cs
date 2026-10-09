using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Mappings
{
    public class DemandApplicationWorkflowMap : EntityTypeConfiguration<DemandApplicationWorkflow>
    {
        public DemandApplicationWorkflowMap()
        {
            ToTable("TR_CFC_DemandApplicationWorkflows", "dbo");
            HasKey(e => e.Id);
            Property(e => e.Id).HasColumnName("Id").HasColumnType("int").HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.DemandApplicationId).HasColumnName("DemandApplicationId").HasColumnType("int");
            Property(e => e.Stage).HasColumnName("Stage").HasColumnType("nvarchar").HasMaxLength(50).IsRequired();
            Property(e => e.RejectionReason).HasColumnName("RejectionReason").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.PayableAmount).HasColumnName("PayableAmount").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.PaymentStatus).HasColumnName("PaymentStatus").HasColumnType("nvarchar").HasMaxLength(50).IsRequired();
            Property(e => e.PaymentLink).HasColumnName("PaymentLink").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.Utr).HasColumnName("Utr").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.PaymentDate).HasColumnName("PaymentDate").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.PaymentScreenshotPath).HasColumnName("PaymentScreenshotPath").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.PaymentScreenshotFileName).HasColumnName("PaymentScreenshotFileName").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.PaymentScreenshotSizeBytes).HasColumnName("PaymentScreenshotSizeBytes").HasColumnType("bigint");
            Property(e => e.PaymentSubmittedBy).HasColumnName("PaymentSubmittedBy").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.PaymentSubmittedAt).HasColumnName("PaymentSubmittedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.OsPaymentNotificationSentAt).HasColumnName("OsPaymentNotificationSentAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.PaymentVerifiedBy).HasColumnName("PaymentVerifiedBy").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.PaymentVerifiedAt).HasColumnName("PaymentVerifiedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.CertificateFilePath).HasColumnName("CertificateFilePath").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.CertificateFileName).HasColumnName("CertificateFileName").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.CertificateGeneratedAt).HasColumnName("CertificateGeneratedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.ApprovedBy).HasColumnName("ApprovedBy").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.ApprovedAt).HasColumnName("ApprovedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.CreatedBy).HasColumnName("CreatedBy").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.UpdatedBy).HasColumnName("UpdatedBy").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.UpdatedAt).HasColumnName("UpdatedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.IsDeleted).HasColumnName("IsDeleted").HasColumnType("bit");
            Property(e => e.DeletedAt).HasColumnName("DeletedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.DeletedBy).HasColumnName("DeletedBy").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.Shera).HasColumnName("Shera").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.PaymentAccessToken).HasColumnName("PaymentAccessToken").HasColumnType("nvarchar").HasMaxLength(128);
            Property(e => e.RazorpayOrderId).HasColumnName("RazorpayOrderId").HasColumnType("nvarchar").HasMaxLength(64);
            Property(e => e.RazorpayPaymentId).HasColumnName("RazorpayPaymentId").HasColumnType("nvarchar").HasMaxLength(64);
            Property(e => e.RazorpaySignature).HasColumnName("RazorpaySignature").HasColumnType("nvarchar").HasMaxLength(128);
        }
    }
}
