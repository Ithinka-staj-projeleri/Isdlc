using System.Text.Json;
using Isdlc.Domain.Projects;
using Isdlc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isdlc.Infrastructure.Projects;

public class CustomFieldConfiguration : IEntityTypeConfiguration<CustomField>
{
    /// <summary>50 options of 100 characters, quoted and separated, fit with room to spare.</summary>
    public const int MaxOptionsJsonLength = 6000;

    private static readonly JsonSerializerOptions OptionJson = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public void Configure(EntityTypeBuilder<CustomField> builder)
    {
        builder.Property(x => x.Key).HasMaxLength(CustomField.MaxKeyLength);
        builder.Property(x => x.Label).HasMaxLength(CustomField.MaxLabelLength);
        builder.Property(x => x.HelpText).HasMaxLength(CustomField.MaxHelpTextLength);

        // ["Var","Yok"]: a value, not rows, compared by content.
        builder.Property(x => x.Options)
            .HasMaxLength(MaxOptionsJsonLength)
            .HasConversion(
                list => JsonSerializer.Serialize(list, OptionJson),
                json => JsonSerializer.Deserialize<List<string>>(json, OptionJson) ?? new List<string>(),
                new ValueComparer<List<string>>(
                    (a, b) => a!.SequenceEqual(b!),
                    list => list.Aggregate(0, (hash, o) => HashCode.Combine(hash, o.GetHashCode())),
                    list => list.ToList()));

        // A key names the value on every record of the company (docs/multi-tenancy.md → Benzersizlik).
        builder.HasIndex(x => new { x.TenantId, x.Key }).IsUnique();

        builder.ConfigureAuditFields();
    }
}
