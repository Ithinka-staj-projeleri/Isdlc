using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Isdlc.Infrastructure.Persistence.Converters;

/// <summary>
/// SQL Server does not store DateTimeKind. Without this, dates read back as Unspecified
/// and are serialized without the "Z" suffix, so the browser treats them as local time.
/// Writes: converts to UTC. Reads: marks the value as UTC.
/// </summary>
public class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime(),
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
