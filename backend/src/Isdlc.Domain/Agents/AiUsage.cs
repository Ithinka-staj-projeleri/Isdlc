using Isdlc.Domain.Common;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Projects;

namespace Isdlc.Domain.Agents;

/// <summary>
/// One call to the AI service (Faz 7 · A9): who used which assistant with which model, and what it cost. A failed call is
/// written too. Not audited; never changed after it is written. Rules: docs/configurability.md → Yapay zekâ modeli ve kullanımı.
/// </summary>
public class AiUsage : ITenantOwned
{
    public const int MaxModelLength = 100;

    public long Id { get; set; }

    public int TenantId { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Empty for the e-mail request draft (no project yet).</summary>
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }

    public AiAssistant Assistant { get; set; }

    /// <summary>The model that answered (a fallback one when the chosen model was busy); the asked one when the call failed.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Empty for a recording and when the provider did not report them.</summary>
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }

    /// <summary>Only for a recording.</summary>
    public int? AudioSeconds { get; set; }

    public int DurationMs { get; set; }

    public bool Succeeded { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }
}

public enum AiAssistant
{
    MeetingSuggestions,
    RiskSuggestions,
    RiskAssessment,
    RequestDraft,
    Transcription,
}
