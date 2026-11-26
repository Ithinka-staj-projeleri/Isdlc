using System.Text.Json;
using System.Text.Json.Serialization;
using Isdlc.Domain.Process;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isdlc.Infrastructure.Process;

public class ProcessStepConfiguration : IEntityTypeConfiguration<ProcessStep>
{
    /// <summary>A step has a handful of prerequisites; the longest list fits with room to spare.</summary>
    public const int MaxPrerequisitesJsonLength = 2000;

    /// <summary>[{"kind":"QuestionnaireComplete","value":"customer-fit"}], the same shape as in the seed files.</summary>
    private static readonly JsonSerializerOptions PrerequisiteJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public void Configure(EntityTypeBuilder<ProcessStep> builder)
    {
        builder.Property(x => x.Code).HasMaxLength(10);
        builder.Property(x => x.Text).HasMaxLength(500);
        builder.Property(x => x.NoBranchText).HasMaxLength(500);

        // A value, not rows: compared by content so the seed sync only saves a list that really changed.
        builder.Property(x => x.Prerequisites)
            .HasMaxLength(MaxPrerequisitesJsonLength)
            .HasConversion(
                list => JsonSerializer.Serialize(list, PrerequisiteJson),
                json => JsonSerializer.Deserialize<List<StepPrerequisite>>(json, PrerequisiteJson) ?? new List<StepPrerequisite>(),
                new ValueComparer<List<StepPrerequisite>>(
                    (a, b) => a!.SequenceEqual(b!),
                    list => list.Aggregate(0, (hash, p) => HashCode.Combine(hash, p.GetHashCode())),
                    list => list.ToList()));

        // Codes and orders repeat across flows ("1" exists in every flow), so they are unique per flow.
        builder.HasIndex(x => new { x.ProcessFlowId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.ProcessFlowId, x.Order }).IsUnique();

        builder.HasOne(x => x.ProcessStage).WithMany(x => x.Steps).HasForeignKey(x => x.ProcessStageId);
        builder.HasOne(x => x.ProcessFlow).WithMany().HasForeignKey(x => x.ProcessFlowId);
    }
}
