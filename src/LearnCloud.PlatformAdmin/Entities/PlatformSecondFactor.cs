using LearnCloud.MultiTenancy.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LearnCloud.PlatformAdmin.Entities;

/// <summary>
/// A platform operator's authenticator secret, and how long their current step-up lasts.
///
/// This is in the database rather than in a session because the API runs on more than one
/// replica: a session verified on one would be unknown to the next. It also means a step-up
/// can be ended from the database if a phone is lost.
/// </summary>
public class PlatformSecondFactor : BaseEntity
{
    public long UserId { get; set; }

    /// <summary>Base32 TOTP secret, as scanned into the authenticator app.</summary>
    public string Secret { get; set; } = null!;

    /// <summary>Set the first time a code is accepted, so an unused enrolment is visible.</summary>
    public DateTime? ConfirmedAt { get; set; }

    /// <summary>The console is open to this operator until this moment, then asks again.</summary>
    public DateTime? StepUpUntil { get; set; }

    public DateTime? LastVerifiedAt { get; set; }
    public string? LastVerifiedIp { get; set; }

    /// <summary>Codes are single use within their window, so a captured code cannot be replayed.</summary>
    public string? LastAcceptedCode { get; set; }
}

public sealed class PlatformSecondFactorConfiguration : IEntityTypeConfiguration<PlatformSecondFactor>
{
    public void Configure(EntityTypeBuilder<PlatformSecondFactor> b)
    {
        b.Property(x => x.Secret).HasMaxLength(64);
        b.Property(x => x.LastVerifiedIp).HasMaxLength(64);
        b.Property(x => x.LastAcceptedCode).HasMaxLength(12);
        b.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("idx_platform_second_factors_user");
    }
}
