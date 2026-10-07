using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Configurations;

public class VisitorConfiguration : IEntityTypeConfiguration<Visitor>
{
    public void Configure(EntityTypeBuilder<Visitor> entity)
    {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(e => e.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        entity.Property(e => e.FullName)
            .IsRequired()
            .HasMaxLength(100);

        entity.Property(e => e.ContactNumber)
            .HasMaxLength(20);

        entity.Property(e => e.PlateNumber)
            .IsRequired()
            .HasMaxLength(20);

        entity.Property(e => e.Brand)
            .HasMaxLength(100);

        entity.HasIndex(e => e.PlateNumber);

        entity.HasMany(e => e.VisitSessions)
            .WithOne(s => s.Visitor)
            .HasForeignKey(s => s.VisitorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
