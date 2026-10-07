using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Configurations;

public class VisitSessionConfiguration : IEntityTypeConfiguration<VisitSession>
{
    public void Configure(EntityTypeBuilder<VisitSession> entity)
    {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(e => e.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        entity.Property(e => e.Purpose)
            .HasMaxLength(255);

        entity.Property(e => e.Destination)
            .HasMaxLength(255);

        entity.Property(e => e.EntryGate)
            .HasMaxLength(50);

        entity.Property(e => e.ExitGate)
            .HasMaxLength(50);

        entity.HasIndex(e => new { e.VisitorId, e.Status });

        entity.HasOne(e => e.Visitor)
            .WithMany(v => v.VisitSessions)
            .HasForeignKey(e => e.VisitorId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.EntryGuard)
            .WithMany()
            .HasForeignKey(e => e.EntryGuardId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.ExitGuard)
            .WithMany()
            .HasForeignKey(e => e.ExitGuardId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
