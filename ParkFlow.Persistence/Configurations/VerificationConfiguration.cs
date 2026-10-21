using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Configurations;

public class VerificationRecordConfiguration : IEntityTypeConfiguration<VerificationRecord>
{
    public void Configure(EntityTypeBuilder<VerificationRecord> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        entity.Property(x => x.Method).HasConversion<string>().HasMaxLength(20);
        entity.Property(x => x.Fingerprint).HasMaxLength(64);
        entity.Property(x => x.Reason).HasMaxLength(2000);
        entity.Property(x => x.OcrOutcome).HasMaxLength(100);
        entity.Property(x => x.ExtractedFieldsJson).HasColumnType("jsonb");
        entity.Property(x => x.Version).IsConcurrencyToken();
        entity.HasIndex(x => new { x.Type, x.SubjectId }).IsUnique().HasFilter("\"IsCurrent\" = TRUE");
        entity.HasIndex(x => new { x.Status, x.IsCurrent, x.NextAttemptAt });
        entity.HasIndex(x => x.UserAccountId);
        entity.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.Cascade);
    }
}
public class VerificationPolicyConfiguration : IEntityTypeConfiguration<VerificationPolicy>
{
    public void Configure(EntityTypeBuilder<VerificationPolicy> entity)
    {
        entity.HasKey(x => x.Id);
        entity.ToTable(t => t.HasCheckConstraint("CK_VerificationPolicy_Singleton", "\"Id\" = 1"));
        entity.Property(x => x.AcademicYear).HasMaxLength(20);
        entity.Property(x => x.Semester).HasMaxLength(30);
        entity.Property(x => x.Version).IsConcurrencyToken();
        entity.HasData(new VerificationPolicy());
    }
}
public class VerificationAuditConfiguration : IEntityTypeConfiguration<VerificationAudit>
{
    public void Configure(EntityTypeBuilder<VerificationAudit> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Action).HasMaxLength(50);
        entity.Property(x => x.Details).HasMaxLength(4000);
        entity.HasIndex(x => new { x.VerificationRecordId, x.CreatedAt });
        // Audits survive resource deletion and retain identifiers without cascading.
    }
}
