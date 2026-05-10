using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ParkFlow.Persistence.Configurations;

public class CorSubmissionConfiguration : IEntityTypeConfiguration<CorSubmission>
{
    public void Configure(EntityTypeBuilder<CorSubmission> entity)
    {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(e => e.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        entity.Property(e => e.AcademicTerm)
            .IsRequired()
            .HasMaxLength(50);

        entity.Property(e => e.CorDocumentUrl)
            .IsRequired()
            .HasMaxLength(2048);

        entity.HasOne(e => e.UserAccount)
            .WithOne()
            .HasForeignKey<CorSubmission>(e => e.UserAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => e.UserAccountId)
            .IsUnique();
    }
}
