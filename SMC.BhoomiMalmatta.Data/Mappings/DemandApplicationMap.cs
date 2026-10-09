using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.ModelConfiguration;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Mappings
{
    public class DemandApplicationMap : EntityTypeConfiguration<DemandApplication>
    {
        public DemandApplicationMap()
        {
            ToTable("TR_CFC_DemandApplications", "dbo");
            HasKey(e => e.Id);
            Property(e => e.Id).HasColumnName("Id").HasColumnType("int").HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            Property(e => e.ApplicationNumber).HasColumnName("ApplicationNumber").HasColumnType("nvarchar").HasMaxLength(40).IsRequired();
            Property(e => e.ServiceType).HasColumnName("ServiceType").HasColumnType("nvarchar").HasMaxLength(40).IsRequired();
            Property(e => e.BusinessType).HasColumnName("BusinessType").HasColumnType("nvarchar").HasMaxLength(40);
            Property(e => e.OtherBusinessType).HasColumnName("OtherBusinessType").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.ApplicantType).HasColumnName("ApplicantType").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.ApplicantName).HasColumnName("ApplicantName").HasColumnType("nvarchar").HasMaxLength(200).IsRequired();
            Property(e => e.Mobile).HasColumnName("Mobile").HasColumnType("nvarchar").HasMaxLength(10).IsRequired();
            Property(e => e.Email).HasColumnName("Email").HasColumnType("nvarchar").HasMaxLength(200).IsRequired();
            Property(e => e.IdentityNumber).HasColumnName("IdentityNumber").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.PanNumber).HasColumnName("PanNumber").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.GstNumber).HasColumnName("GstNumber").HasColumnType("nvarchar").HasMaxLength(15).IsRequired();
            Property(e => e.PermanentAddress).HasColumnName("PermanentAddress").HasColumnType("nvarchar").HasMaxLength(1000).IsRequired();
            Property(e => e.CorrespondenceAddress).HasColumnName("CorrespondenceAddress").HasColumnType("nvarchar").HasMaxLength(1000).IsRequired();
            Property(e => e.SameAddress).HasColumnName("SameAddress").HasColumnType("bit");
            Property(e => e.State).HasColumnName("State").HasColumnType("nvarchar").HasMaxLength(100).IsRequired();
            Property(e => e.District).HasColumnName("District").HasColumnType("nvarchar").HasMaxLength(100).IsRequired();
            Property(e => e.City).HasColumnName("City").HasColumnType("nvarchar").HasMaxLength(100).IsRequired();
            Property(e => e.Taluka).HasColumnName("Taluka").HasColumnType("nvarchar").HasMaxLength(100).IsRequired();
            Property(e => e.PinCode).HasColumnName("PinCode").HasColumnType("nvarchar").HasMaxLength(6).IsRequired();
            Property(e => e.Zone).HasColumnName("Zone").HasColumnType("nvarchar").HasMaxLength(30).IsRequired();
            Property(e => e.Prabhag).HasColumnName("Prabhag").HasColumnType("nvarchar").HasMaxLength(30).IsRequired();
            Property(e => e.Location).HasColumnName("Location").HasColumnType("nvarchar").IsMaxLength().IsRequired();
            Property(e => e.AvailableSpace).HasColumnName("AvailableSpace").HasColumnType("nvarchar").IsMaxLength().IsRequired();
            Property(e => e.AreaSqFt).HasColumnName("AreaSqFt").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.ServiceDescription).HasColumnName("ServiceDescription").HasColumnType("nvarchar").IsMaxLength().IsRequired();
            Property(e => e.SpaceRequirement).HasColumnName("SpaceRequirement").HasColumnType("nvarchar").IsMaxLength().IsRequired();
            Property(e => e.OtherInformation).HasColumnName("OtherInformation").HasColumnType("nvarchar").IsMaxLength().IsRequired();
            Property(e => e.StartDate).HasColumnName("StartDate").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.EndDate).HasColumnName("EndDate").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.RequiredDuration).HasColumnName("RequiredDuration").HasColumnType("nvarchar").IsMaxLength().IsRequired();
            Property(e => e.RequiredTime).HasColumnName("RequiredTime").HasColumnType("nvarchar").IsMaxLength().IsRequired();
            Property(e => e.ElectricityRequired).HasColumnName("ElectricityRequired").HasColumnType("bit");
            Property(e => e.WaterRequired).HasColumnName("WaterRequired").HasColumnType("bit");
            Property(e => e.OtherFacilities).HasColumnName("OtherFacilities").HasColumnType("nvarchar").IsMaxLength().IsRequired();
            Property(e => e.WasteManagement).HasColumnName("WasteManagement").HasColumnType("nvarchar").IsMaxLength().IsRequired();
            Property(e => e.DeclarationAccepted).HasColumnName("DeclarationAccepted").HasColumnType("bit");
            Property(e => e.FeeAmount).HasColumnName("FeeAmount").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.PaymentStatus).HasColumnName("PaymentStatus").HasColumnType("nvarchar").IsMaxLength().IsRequired();
            Property(e => e.Status).HasColumnName("Status").HasColumnType("nvarchar").HasMaxLength(40).IsRequired();
            Property(e => e.SubmittedAt).HasColumnName("SubmittedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.CreatedBy).HasColumnName("CreatedBy").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.UpdatedBy).HasColumnName("UpdatedBy").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.UpdatedAt).HasColumnName("UpdatedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.IsDeleted).HasColumnName("IsDeleted").HasColumnType("bit");
            Property(e => e.DeletedAt).HasColumnName("DeletedAt").HasColumnType("datetime2").HasPrecision(7);
            Property(e => e.DeletedBy).HasColumnName("DeletedBy").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.Shera).HasColumnName("Shera").HasColumnType("nvarchar").IsMaxLength();
            Property(e => e.ApplicantAccessTokenHash).HasColumnName("ApplicantAccessTokenHash").HasColumnType("nvarchar").HasMaxLength(64);
            Property(e => e.CalculatedRate).HasColumnName("CalculatedRate").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.LengthFt).HasColumnName("LengthFt").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.WidthFt).HasColumnName("WidthFt").HasColumnType("decimal").HasPrecision(18, 2);
            Property(e => e.ProcessingRoute).HasColumnName("ProcessingRoute").HasColumnType("nvarchar").IsMaxLength().IsRequired();
        }
    }
}
