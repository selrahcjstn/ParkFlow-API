using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Configurations;

public class VisitorVehicleConfiguration : IEntityTypeConfiguration<VisitorVehicle>
{
    public void Configure(EntityTypeBuilder<VisitorVehicle> entity)
    {
        entity.HasKey(v => v.Id);
        entity.Property(v => v.Id).HasDefaultValueSql("gen_random_uuid()");
        entity.Property(v => v.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        entity.Property(v => v.PlateNumber).HasMaxLength(20).IsRequired();
        entity.Property(v => v.NormalizedPlateNumber).HasMaxLength(20).IsRequired();
        entity.Property(v => v.Brand).HasMaxLength(100).IsRequired();
        entity.HasIndex(v => v.NormalizedPlateNumber).IsUnique();
        entity.HasAlternateKey(v => new { v.Id, v.VisitorId });
        entity.HasOne(v => v.Visitor).WithMany(v => v.Vehicles).HasForeignKey(v => v.VisitorId).OnDelete(DeleteBehavior.Restrict);
        entity.ToTable(t => t.HasCheckConstraint("CK_VisitorVehicles_NormalizedPlate",
            "\"NormalizedPlateNumber\" = upper(replace(replace(btrim(\"PlateNumber\"), ' ', ''), '-', '')) AND \"NormalizedPlateNumber\" ~ '^[A-Z0-9]{2,20}$'"));
    }
}
