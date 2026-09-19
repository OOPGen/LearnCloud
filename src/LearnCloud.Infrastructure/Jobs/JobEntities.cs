using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LearnCloud.Infrastructure.Jobs;

public static class JobStatus
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
}

/// <summary>
/// A unit of background work stored in PostgreSQL. Queued in the same transaction as the
/// change that caused it, so it survives restarts, and claimed with a lease, so any number
/// of API instances can run the worker without doing a job twice.
/// </summary>
/// <remarks>
/// Deliberately not a BaseEntity: the worker reads jobs of every school, so no tenant or
/// soft-delete filter applies, and job rows are not copied into the audit log.
/// </remarks>
public sealed class BackgroundJob
{
    public long Id { get; set; }
    public string Kind { get; set; } = null!;
    /// <summary>The school the job runs for; null for platform jobs.</summary>
    public long? TenantId { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string Status { get; set; } = JobStatus.Pending;
    public int Attempts { get; set; }
    public int MaxAttempts { get; set; } = 8;
    public DateTime RunAfter { get; set; }
    public string? LockedBy { get; set; }
    public DateTime? LockedUntil { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>When a recurring job last ran and is next due, shared by all API instances.</summary>
public sealed class ScheduledJobState
{
    public string Name { get; set; } = null!;
    public DateTime NextRunAt { get; set; }
    public DateTime? LastStartedAt { get; set; }
    public DateTime? LastFinishedAt { get; set; }
    public string? LastResult { get; set; }
    public string? LockedBy { get; set; }
    public DateTime? LockedUntil { get; set; }
}

public sealed class BackgroundJobConfiguration : IEntityTypeConfiguration<BackgroundJob>
{
    public void Configure(EntityTypeBuilder<BackgroundJob> b)
    {
        b.ToTable("background_jobs");
        b.Property(x => x.Kind).HasMaxLength(100);
        b.Property(x => x.Status).HasMaxLength(20);
        b.Property(x => x.LockedBy).HasMaxLength(200);
        b.Property(x => x.LastError).HasMaxLength(2000);
        // The worker's query: due pending jobs, and running jobs whose lease has expired.
        b.HasIndex(x => new { x.Status, x.RunAfter }).HasDatabaseName("idx_background_jobs_due")
            .HasFilter("status IN ('pending', 'running')");
        b.HasIndex(x => new { x.Status, x.CompletedAt }).HasDatabaseName("idx_background_jobs_completed");
    }
}

public sealed class ScheduledJobStateConfiguration : IEntityTypeConfiguration<ScheduledJobState>
{
    public void Configure(EntityTypeBuilder<ScheduledJobState> b)
    {
        b.ToTable("scheduled_jobs");
        b.HasKey(x => x.Name);
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.LastResult).HasMaxLength(2000);
        b.Property(x => x.LockedBy).HasMaxLength(200);
    }
}
