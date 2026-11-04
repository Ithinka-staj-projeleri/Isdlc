using System.Text.Json;
using System.Text.Json.Serialization;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Projects;

namespace Isdlc.Infrastructure.Persistence.Seeding;

/// <summary>
/// Shapes of the JSON files in Isdlc.Infrastructure/Seed. They are embedded in the assembly.
/// </summary>
internal static class SeedFiles
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static T Read<T>(string fileName)
    {
        var resourceName = $"Isdlc.Infrastructure.Seed.{fileName}";
        using var stream = typeof(SeedFiles).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Seed file '{fileName}' is not embedded.");

        return JsonSerializer.Deserialize<T>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Seed file '{fileName}' is empty.");
    }

    public record ProcessFlowFile(List<StageSeed> Stages, List<StepSeed> Steps);

    public record StageSeed(int Order, string Title, string ShortTitle);

    public record StepSeed(
        string Code,
        int StageOrder,
        int Order,
        string Text,
        bool IsDecision,
        string? NoBranchText,
        UserRole ResponsibleRole,
        UserRole? ApproverRole,
        ProjectStatus? NoOutcome,
        List<JumpSeed> NoJumps);

    public record JumpSeed(string TargetStepCode, string Label);

    public record UsersFile(List<UserSeed> Users);

    public record UserSeed(string FullName, string Email, UserRole Role);

    public record SampleProjectsFile(List<ProjectSeed> Projects);

    public record ProjectSeed(
        string Code,
        string Name,
        string Description,
        RequestSource Source,
        RequestChannel RequestChannel,
        RequestType RequestType,
        string CustomerName,
        string RequesterName,
        bool RequiresNda,
        string? BusinessAnalystEmail,
        string? ProjectManagerEmail,
        int? Level,
        ProjectPriority? Priority,
        ProjectStatus Status,
        string? CurrentStepCode,
        string? ClosedAtStepCode,
        DateOnly StartDate,
        DateOnly? DueDate);
}
