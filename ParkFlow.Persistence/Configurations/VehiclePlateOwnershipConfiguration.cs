using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Configurations;

public class VehiclePlateOwnershipConfiguration : IEntityTypeConfiguration<VehiclePlateOwnership>
{
    public void Configure(EntityTypeBuilder<VehiclePlateOwnership> entity)
    {
        entity.HasKey(x => x.NormalizedPlateNumber);
        entity.Property(x => x.NormalizedPlateNumber).HasMaxLength(20);
        entity.HasIndex(x => x.VehicleId).IsUnique();
        entity.HasIndex(x => x.VisitorVehicleId).IsUnique();
        entity.HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<VisitorVehicle>().WithMany().HasForeignKey(x => x.VisitorVehicleId).OnDelete(DeleteBehavior.Cascade);
        entity.ToTable(t => t.HasCheckConstraint("CK_VehiclePlateOwnerships_OneOwner", "(\"VehicleId\" IS NOT NULL) <> (\"VisitorVehicleId\" IS NOT NULL)"));
    }
}
