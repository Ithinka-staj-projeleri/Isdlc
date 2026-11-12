using System.Text.Json;
using System.Text.Json.Serialization;
using Isdlc.Domain.Meetings;
using Isdlc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isdlc.Infrastructure.Meetings;

public class MeetingConfiguration : IEntityTypeConfiguration<Meeting>
{
    /// <summary>[{"name":"Elif Yılmaz","role":"İş Analisti"}]; the migration writes old rows the same way.</summary>
    private static readonly JsonSerializerOptions ParticipantJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// The longest list the API accepts, as JSON, with room to spare. Above 4000 SQL Server uses nvarchar(max);
    /// the real limit is the API's (30 participants, 100 + 100 characters each).
    /// </summary>
    public const int MaxParticipantsJsonLength = 10_000;

    public void Configure(EntityTypeBuilder<Meeting> builder)
    {
        builder.Property(x => x.Code).HasMaxLength(20);
        builder.Property(x => x.Agenda).HasMaxLength(Meeting.MaxAgendaLength);
        builder.Property(x => x.Summary).HasMaxLength(Meeting.MaxSummaryLength);
        builder.Property(x => x.CompletedStages).HasMaxLength(Meeting.MaxStagesLength);
        builder.Property(x => x.OngoingStages).HasMaxLength(Meeting.MaxStagesLength);
        builder.Property(x => x.NextSteps).HasMaxLength(Meeting.MaxNextStepsLength);
        builder.Property(x => x.PlannedActivities).HasMaxLength(Meeting.MaxActivitiesLength);
        builder.Property(x => x.CompletedActivities).HasMaxLength(Meeting.MaxActivitiesLength);

        // A value, not rows: compared by content so a list saved again unchanged is not logged as a change.
        builder.Property(x => x.Participants)
            .HasMaxLength(MaxParticipantsJsonLength)
            .HasConversion(
                list => JsonSerializer.Serialize(list, ParticipantJson),
                json => JsonSerializer.Deserialize<List<MeetingParticipant>>(json, ParticipantJson) ?? new List<MeetingParticipant>(),
                new ValueComparer<List<MeetingParticipant>>(
                    (a, b) => a!.SequenceEqual(b!),
                    list => list.Aggregate(0, (hash, p) => HashCode.Combine(hash, p.GetHashCode())),
                    list => list.ToList()));

        // Codes increase within a project; two meetings saved at the same time cannot get the same code.
        builder.HasIndex(x => new { x.ProjectId, x.Code }).IsUnique();

        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId);
        builder.HasOne(x => x.RecordedBy).WithMany().HasForeignKey(x => x.RecordedById);

        builder.ConfigureAuditFields();
    }
}
