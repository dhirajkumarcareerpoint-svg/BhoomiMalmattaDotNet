using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Mappings
{
    public class DemandApplicationZoneWorkflowMap : EntityTypeConfiguration<DemandApplicationZoneWorkflow>
    {
        public DemandApplicationZoneWorkflowMap()
        {
            ToTable("TR_CFC_DemandApplicationZoneWorkflows", "dbo");
            HasKey(e => e.Id);
            Property(e => e.Id).HasColumnName("Id").HasColumnType("int").HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.DemandApplicationId).HasColumnName("DemandApplicationId").HasColumnType("int");
            Property(e => e.Zone).HasColumnName("Zone").HasColumnType("int");
            Property(e => e.ZoneOfficerUserId).HasColumnName("ZoneOfficerUserId").HasColumnType("int");
            Property(e => e.ZoneStage).HasColumnName("ZoneStage").HasColumnType("nvarchar").HasMaxLength(60).IsRequired();
            Property(e => e.PaymentStatus).HasColumnName("PaymentStatus").HasColumnType("nvarchar").HasMaxLength(60).IsRequired();
            Property(e => e.PayableAmount).HasColumnName("PayableAmount").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.DocumentsVerified).HasColumnName("DocumentsVerified").HasColumnType("bit");
            Property(e => e.CorrectionRemark).HasColumnName("CorrectionRemark").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.CorrectionVersion).HasColumnName("CorrectionVersion").HasColumnType("int");
            Property(e => e.CorrectionRequestedAt).HasColumnName("CorrectionRequestedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.CorrectionResubmittedAt).HasColumnName("CorrectionResubmittedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.ApplicantReply).HasColumnName("ApplicantReply").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.ApprovalRemark).HasColumnName("ApprovalRemark").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.RejectionRemark).HasColumnName("RejectionRemark").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.PaymentMode).HasColumnName("PaymentMode").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.ReceiptNumber).HasColumnName("ReceiptNumber").HasColumnType("nvarchar").HasMaxLength(100);
            Property(e => e.ReceiptDate).HasColumnName("ReceiptDate").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.ReceivedBy).HasColumnName("ReceivedBy").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.PaymentConfirmedAt).HasColumnName("PaymentConfirmedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.ApprovedAt).HasColumnName("ApprovedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.ApprovedBy).HasColumnName("ApprovedBy").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.CertificatePath).HasColumnName("CertificatePath").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.CertificateFileName).HasColumnName("CertificateFileName").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.ActionHistoryJson).HasColumnName("ActionHistoryJson").HasColumnType("nvarchar").IsMaxLength().IsRequired();
            Property(e => e.RowVersion).HasColumnName("RowVersion").IsRowVersion();
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
