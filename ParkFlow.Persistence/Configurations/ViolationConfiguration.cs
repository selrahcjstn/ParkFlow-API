using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Configurations;

public class ViolationConfiguration : IEntityTypeConfiguration<Violation>
{
    public void Configure(EntityTypeBuilder<Violation> entity)
    {
        entity.HasOne(e => e.ParkingLog).WithMany().HasForeignKey(e => e.ParkingLogId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.VisitSession).WithMany().HasForeignKey(e => e.VisitSessionId)
            .OnDelete(DeleteBehavior.Cascade);
        // One ordinary parking charge per visitor session, including concurrent exit requests.
        entity.HasIndex(e => e.VisitSessionId).IsUnique();
        entity.ToTable("Violations", table => table.HasCheckConstraint("CK_Violations_Session",
            "(\"ParkingLogId\" IS NOT NULL) <> (\"VisitSessionId\" IS NOT NULL)"));
    }
}
