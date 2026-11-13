using System.Text.Json;
using System.Text.Json.Serialization;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Process;
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

    /// <summary>Every flow file in Seed/flows, one flow per file.</summary>
    public static IReadOnlyList<ProcessFlowFile> ReadFlows()
    {
        const string prefix = "Isdlc.Infrastructure.Seed.flows.";
        return typeof(SeedFiles).Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(name => Read<ProcessFlowFile>("flows." + name[prefix.Length..]))
            .ToList();
    }

    public record ProcessFlowFile(
        string Code,
        int Order,
        string Name,
        ProcessFlowKind Kind,
        int? ProjectStageOrder,
        List<StageSeed> Stages,
        List<StepSeed> Steps);

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
        List<JumpSeed> NoJumps,
        bool RequiresExternalRequest = false,
        bool RequiresOffer = false,
        int? MinLevel = null);

    public record JumpSeed(string TargetStepCode, string Label);

    public record UsersFile(List<UserSeed> Users);

    public record UserSeed(string FullName, string Email, UserRole Role);

    public record DocumentTypesFile(List<DocumentTypeSeed> DocumentTypes);

    public record DocumentTypeSeed(string Code, string Name, string? Description, int StageOrder, int Order);

    public record SampleProjectsFile(List<ProjectSeed> Projects);

    /// <summary>A sample record. With ParentCode it is an add-on of that project (listed before it) on the add-on flow.</summary>
    public record ProjectSeed(
        string Code,
        string? ParentCode,
        string Name,
        string Description,
        RequestSource Source,
        RequestChannel RequestChannel,
        string? RequestChannelOther,
        RequestType RequestType,
        string CustomerName,
        string RequesterName,
        string? RequesterContact,
        string? Notes,
        bool RequiresNda,
        bool RequiresOffer,
        string? BusinessAnalystEmail,
        string? ProjectManagerEmail,
        int? Level,
        ProjectPriority? Priority,
        ProjectStatus Status,
        string? CurrentStepCode,
        string? ClosedAtStepCode,
        DateOnly RequestDate,
        DateOnly? DueDate);
}
