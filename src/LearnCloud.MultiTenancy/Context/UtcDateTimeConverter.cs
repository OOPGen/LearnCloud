using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LearnCloud.MultiTenancy.Context;

// PostgreSQL timestamptz columns only accept UTC DateTime values through Npgsql.
// Dates bound from JSON requests arrive as DateTimeKind.Unspecified, which Npgsql
// rejects at runtime. Treat unspecified values as UTC on the way in, and mark values
// read back as UTC so callers never see Unspecified.
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter() : base(
        v => v.Kind == DateTimeKind.Utc ? v : v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    { }
}

public sealed class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public NullableUtcDateTimeConverter() : base(
        v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v : v.Value.Kind == DateTimeKind.Local ? v.Value.ToUniversalTime() : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)) : v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v)
    { }
}
