using System.Diagnostics;
using Isdlc.Application.Agents.Dtos;
using Isdlc.Application.Common;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Identity;
using Isdlc.Application.Meetings.Dtos;
using Isdlc.Application.OpenIssues;
using Isdlc.Application.Risks;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Meetings;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Projects;
using Isdlc.Domain.Risks;
using Isdlc.Domain.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Isdlc.Application.Agents;

public class AgentService(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUser,
    IAgentClient agents,
    ILogger<AgentService>? logger = null) : IAgentService
{
    private readonly ILogger _logger = (ILogger?)logger ?? NullLogger.Instance;

    public async Task<AgentStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var available = await agents.IsAvailableAsync(cancellationToken);
        var company = (await CompanyAiAsync(cancellationToken)).Switches;

        // The company's switches come on top of the service (docs/multi-tenancy.md → Yapay zekâ ayarlarının etkisi).
        var sendsText = available && CompanyAiRules.MaySendText(company);
        var transcription = available && CompanyAiRules.MayTranscribe(company)
            && await agents.IsTranscriptionAvailableAsync(cancellationToken);
        return new AgentStatusDto(sendsText, sendsText, sendsText, transcription);
    }

    public async Task<MeetingTranscriptDto> TranscribeMeetingAsync(
        int projectId, Stream? recording, long length, CancellationToken cancellationToken)
    {
        // Same checks as the minutes suggestions (signed in, project active, service configured), but the recording
        // stays on the AI service's machine: the company's permission to send data out is not needed.
        await PrepareAsync(projectId, cancellationToken, sendsText: false);

        if (recording is null || length == 0)
        {
            throw new BusinessRuleException("Kayıt dosyası seçilmelidir.");
        }

        // Speech to text runs on the service's own model (Whisper): the company's text model is not sent.
        var transcript = await TrackAsync(
            AiAssistant.Transcription, projectId, null,
            () => agents.TranscribeAsync(recording, cancellationToken),
            t => t.Usage ?? new AgentUsage(t.ModelName, null, null, t.DurationSeconds),
            cancellationToken);
        return new MeetingTranscriptDto(transcript.Text, transcript.DurationSeconds, transcript.Language, transcript.Truncated);
    }

    public async Task<MeetingSuggestionsDto> SuggestMeetingItemsAsync(
        int projectId, SuggestMeetingItemsDto request, CancellationToken cancellationToken)
    {
        // The current step tells the agent what kind of meeting this probably was (e.g. 7.1 analysis meetings).
        var (project, currentStep, model) = await PrepareAsync(projectId, cancellationToken);

        // [Required] on the DTO guarantees these values.
        var result = await TrackAsync(
            AiAssistant.MeetingSuggestions, projectId, model,
            () => agents.SuggestOpenIssuesAsync(
                new MeetingAgentRequest(project.Code, request.MeetingDate!.Value, request.Notes!.Trim(), currentStep, model),
                cancellationToken),
            r => r.Usage,
            cancellationToken);

        var users = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => new ActionOwners.User(u.Id, u.FullName))
            .ToListAsync(cancellationToken);
        var participants = result.Minutes?.Participants.Select(p => p.Name.Trim()).Where(n => n.Length > 0).ToList() ?? [];

        var decisions = new List<SuggestedDecisionDto>();
        var actions = new List<SuggestedActionDto>();
        var others = new List<SuggestedOpenIssueDto>();
        var skipped = result.InvalidCount;

        foreach (var item in result.Items)
        {
            if (!Enum.TryParse<OpenIssueType>(item.Type, out var type) || string.IsNullOrWhiteSpace(item.Title))
            {
                skipped++;
                continue;
            }

            var title = item.Title.Trim();
            switch (type)
            {
                case OpenIssueType.Decision:
                    // A decision is saved owned by whoever saves the minutes, so the agent's owner is not used.
                    decisions.Add(new SuggestedDecisionDto(title, item.Evidence));
                    break;

                case OpenIssueType.Action:
                    var owner = ActionOwners.Resolve(OpenIssueRules.Clean(item.OwnerName), users, participants);
                    actions.Add(new SuggestedActionDto(
                        title,
                        owner.UserId,
                        owner.Name,
                        item.DueDate,
                        item.Evidence,
                        owner.Warning is null ? item.Warnings : [.. item.Warnings, owner.Warning]));
                    break;

                default:
                    others.Add(new SuggestedOpenIssueDto(type, title, OpenIssueRules.Clean(item.OwnerName), item.DueDate, item.Evidence));
                    break;
            }
        }

        return new MeetingSuggestionsDto(
            result.Summary, decisions, actions, others, skipped, result.ModelName, MinutesDraftOf(result.Minutes));
    }

    /// <summary>
    /// The minutes' info only when it would pass the minutes form's own rules (known type, 1–30 participants with
    /// names up to 100 characters, an agenda up to 4000); otherwise the person fills it in as before.
    /// A role, summary or next steps over its limit is dropped on its own: the rest of the draft is still useful.
    /// </summary>
    private static MinutesDraftDto? MinutesDraftOf(MeetingAgentMinutes? minutes)
    {
        if (minutes is null || !Enum.TryParse<MeetingType>(minutes.Type, out var type) || !Enum.IsDefined(type))
        {
            return null;
        }

        var participants = minutes.Participants
            .Select(p => (Name: p.Name.Trim(), Role: OpenIssueRules.Clean(p.Role)))
            .Where(p => p.Name.Length > 0)
            .ToList();
        var agenda = minutes.Agenda.Trim();
        if (participants.Count is 0 or > MeetingParticipant.MaxCount
            || participants.Any(p => p.Name.Length > MeetingParticipant.MaxNameLength)
            || agenda.Length is 0 or > Meeting.MaxAgendaLength)
        {
            return null;
        }

        return new MinutesDraftDto(
            type,
            OpenIssueRules.Clean(minutes.TypeReason),
            participants
                .Select(p => new MeetingParticipantDto
                {
                    Name = p.Name,
                    Role = p.Role is { Length: <= MeetingParticipant.MaxRoleLength } ? p.Role : null,
                })
                .ToList(),
            agenda,
            WithinLimit(minutes.Summary, Meeting.MaxSummaryLength),
            WithinLimit(minutes.NextSteps, Meeting.MaxNextStepsLength),
            minutes.Warnings);
    }

    /// <summary>Trimmed text, or null when it is empty or longer than the form accepts.</summary>
    private static string? WithinLimit(string? text, int limit) =>
        OpenIssueRules.Clean(text) is { } value && value.Length <= limit ? value : null;

    public async Task<RiskSuggestionsDto> SuggestRisksAsync(
        int projectId, SuggestRisksDto request, CancellationToken cancellationToken)
    {
        var (project, currentStep, model) = await PrepareAsync(projectId, cancellationToken);

        // [Required] on the DTO guarantees the value.
        var result = await TrackAsync(
            AiAssistant.RiskSuggestions, projectId, model,
            () => agents.SuggestRisksAsync(
                new RiskAgentRequest(project.Code, request.SourceText!.Trim(), currentStep, model), cancellationToken),
            r => r.Usage,
            cancellationToken);

        var items = new List<SuggestedRiskDto>();
        var skipped = result.InvalidCount;
        foreach (var item in result.Items)
        {
            var title = item.Title.Trim();
            if (title.Length is 0 or > 300)
            {
                skipped++;
                continue;
            }

            var warnings = item.Warnings.ToList();
            var (probability, impact) = CheckScore(item.Probability, item.Impact, warnings);
            items.Add(new SuggestedRiskDto(
                title,
                OpenIssueRules.Clean(item.Description),
                Enum.TryParse<RiskCategory>(item.Category, out var category) ? category : null,
                probability,
                impact,
                ScoreOf(probability, impact),
                SeverityOf(probability, impact),
                OpenIssueRules.Clean(item.AssessmentReason),
                OpenIssueRules.Clean(item.Mitigation),
                OpenIssueRules.Clean(item.Evidence),
                warnings));
        }

        return new RiskSuggestionsDto(items, skipped, result.ModelName);
    }

    public async Task<RiskAssessmentSuggestionDto> SuggestRiskAssessmentAsync(
        int projectId, SuggestRiskAssessmentDto request, CancellationToken cancellationToken)
    {
        var (project, currentStep, model) = await PrepareAsync(projectId, cancellationToken);

        // [Required] on the DTO guarantees the title.
        var result = await TrackAsync(
            AiAssistant.RiskAssessment, projectId, model,
            () => agents.AssessRiskAsync(
                new RiskAssessmentAgentRequest(
                    project.Code, request.Title!.Trim(), OpenIssueRules.Clean(request.Description), currentStep, model),
                cancellationToken),
            r => r.Usage,
            cancellationToken);

        var warnings = result.Warnings.ToList();
        var (probability, impact) = CheckScore(result.Probability, result.Impact, warnings);
        return new RiskAssessmentSuggestionDto(
            probability,
            impact,
            ScoreOf(probability, impact),
            SeverityOf(probability, impact),
            OpenIssueRules.Clean(result.AssessmentReason),
            OpenIssueRules.Clean(result.Mitigation),
            warnings,
            result.ModelName);
    }

    /// <summary>
    /// Checks shared by every suggestion: signed in, the project exists and is active (suggestions are only useful
    /// where the records can be saved), the service is configured. Returns the project and its current step.
    /// </summary>
    public async Task<RequestDraftDto> DraftRequestFromEmailAsync(
        DraftRequestFromEmailDto request, CancellationToken cancellationToken)
    {
        // Only who may open a request may have one drafted (the same rule as ProjectService.CreateAsync).
        var userId = currentUser.UserId
            ?? throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");
        if (!await dbContext.IsActiveUserWithAsync(userId, Permission.Analysis, cancellationToken))
        {
            throw new ForbiddenException(
                PermissionMessages.ForNewRequest(await dbContext.TenantAdminNamesAsync(cancellationToken)));
        }

        if (!agents.IsConfigured)
        {
            throw new ServiceUnavailableException("Yapay zekâ servisi bu ortamda açık değil.");
        }

        var model = await EnsureCompanyAllowsAsync(sendsText: true, cancellationToken);

        // [Required] on the DTO guarantees the text.
        var draft = await TrackAsync(
            AiAssistant.RequestDraft, null, model,
            () => agents.DraftRequestAsync(
                new RequestAgentRequest(request.EmailText!.Trim(), DateOnly.FromDateTime(DateTime.UtcNow), model),
                cancellationToken),
            d => d.Usage,
            cancellationToken);

        // The agent checks the same limits; checked again here so a suggestion never breaks the form.
        RequestType? type = Enum.TryParse<RequestType>(draft.RequestType, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : null;
        var dueDate = draft.RequestDate is { } requested && draft.DueDate is { } due && due < requested ? null : draft.DueDate;

        return new RequestDraftDto(
            WithinLimit(draft.Name, RequestDraftLimits.Short),
            WithinLimit(draft.Description, RequestDraftLimits.Long),
            WithinLimit(draft.CustomerName, RequestDraftLimits.Short),
            WithinLimit(draft.RequesterName, RequestDraftLimits.Short),
            WithinLimit(draft.RequesterContact, RequestDraftLimits.Short),
            type,
            draft.RequestDate,
            dueDate,
            WithinLimit(draft.Notes, RequestDraftLimits.Long),
            draft.Warnings,
            draft.ModelName);
    }

    /// <summary>
    /// Checks shared by the project's assistants: signed in, project active, service configured, and the company
    /// allows it. sendsText: the assistant sends the person's text to the model provider (all but transcription).
    /// </summary>
    private async Task<(ProjectRef Project, string? CurrentStep, string? Model)> PrepareAsync(
        int projectId, CancellationToken cancellationToken, bool sendsText = true)
    {
        if (currentUser.UserId is null)
        {
            throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");
        }

        var project = await dbContext.GetWritableProjectAsync(projectId, cancellationToken);

        if (!agents.IsConfigured)
        {
            throw new ServiceUnavailableException("Yapay zekâ servisi bu ortamda açık değil.");
        }

        var model = await EnsureCompanyAllowsAsync(sendsText, cancellationToken);

        var currentStep = await dbContext.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId && p.CurrentStep != null)
            .Select(p => p.CurrentStep!.Code + " " + p.CurrentStep.Text)
            .SingleOrDefaultAsync(cancellationToken);

        return (project, currentStep, model);
    }

    /// <summary>
    /// The service is never called for a company that switched the assistant off, or whose monthly token limit is used
    /// up (text assistants only; a recording costs no tokens): 503. Returns the company's model (null: the default).
    /// </summary>
    private async Task<string?> EnsureCompanyAllowsAsync(bool sendsText, CancellationToken cancellationToken)
    {
        var company = await CompanyAiAsync(cancellationToken);
        var allowed = sendsText
            ? CompanyAiRules.MaySendText(company.Switches)
            : CompanyAiRules.MayTranscribe(company.Switches);
        if (!allowed)
        {
            throw new ServiceUnavailableException(CompanyAiRules.ClosedMessage);
        }

        if (sendsText && company.MonthlyTokenLimit is { } limit)
        {
            var used = await dbContext.AiUsages.TokensInMonthAsync(AiUsageRules.MonthOf(DateTime.UtcNow), cancellationToken);
            if (used >= limit)
            {
                throw new ServiceUnavailableException(AiUsageRules.LimitReachedMessage);
            }
        }

        return company.Model;
    }

    private sealed record CompanyAi(CompanyAiRules.Settings Switches, string? Model, int? MonthlyTokenLimit);

    /// <summary>The signed-in user's company AI settings; nobody signed in (or no company) means both switches off.</summary>
    private async Task<CompanyAi> CompanyAiAsync(CancellationToken cancellationToken)
    {
        var off = new CompanyAi(CompanyAiRules.Settings.Off, null, null);
        if (currentUser.TenantId is not { } tenantId)
        {
            return off;
        }

        // Tenants has no query filter; the company is the one of the signed-in user.
        return await dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new CompanyAi(
                new CompanyAiRules.Settings(t.AiEnabled, t.AiDataSharingAllowed), t.AiModel, t.AiMonthlyTokenLimit))
            .SingleOrDefaultAsync(cancellationToken)
            ?? off;
    }

    /// <summary>
    /// Calls the service and writes what it used to AiUsage (Faz 7 · A9), a failed call too. Writing the record never
    /// breaks the assistant: a failure is logged and the answer is still returned.
    /// </summary>
    private async Task<T> TrackAsync<T>(
        AiAssistant assistant,
        int? projectId,
        string? requestedModel,
        Func<Task<T>> call,
        Func<T, AgentUsage?> usageOf,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        T result;
        try
        {
            result = await call();
        }
        catch (Exception error) when (error is ServiceUnavailableException or BusinessRuleException)
        {
            await RecordAsync(assistant, projectId, null, requestedModel, false, started, cancellationToken);
            throw;
        }

        await RecordAsync(assistant, projectId, usageOf(result), requestedModel, true, started, cancellationToken);
        return result;
    }

    private async Task RecordAsync(
        AiAssistant assistant,
        int? projectId,
        AgentUsage? usage,
        string? requestedModel,
        bool succeeded,
        long started,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return;
        }

        var model = usage?.Model ?? requestedModel ?? string.Empty;
        var record = new AiUsage
        {
            UserId = userId,
            ProjectId = projectId,
            Assistant = assistant,
            Model = model.Length > AiUsage.MaxModelLength ? model[..AiUsage.MaxModelLength] : model,
            InputTokens = usage?.InputTokens,
            OutputTokens = usage?.OutputTokens,
            AudioSeconds = usage?.AudioSeconds,
            DurationMs = (int)Math.Min(int.MaxValue, Stopwatch.GetElapsedTime(started).TotalMilliseconds),
            Succeeded = succeeded,
            CreatedAt = DateTime.UtcNow,
        };

        dbContext.AiUsages.Add(record);
        try
        {
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception error)
        {
            dbContext.Entry(record).State = EntityState.Detached;
            _logger.LogError(error, "AI usage could not be written for {Assistant}", assistant);
        }
    }

    /// <summary>Both values 1–5, or neither: a half or out-of-range score is dropped and the person scores it.</summary>
    private static (int? Probability, int? Impact) CheckScore(int? probability, int? impact, List<string> warnings)
    {
        if (probability is null && impact is null)
        {
            return (null, null);
        }

        if (probability is >= RiskRules.MinLevel and <= RiskRules.MaxLevel
            && impact is >= RiskRules.MinLevel and <= RiskRules.MaxLevel)
        {
            return (probability, impact);
        }

        warnings.Add("Önerilen puan geçersizdi; olasılık ve etkiyi siz seçin.");
        return (null, null);
    }

    private static int? ScoreOf(int? probability, int? impact) =>
        probability is { } p && impact is { } i ? RiskRules.Score(p, i) : null;

    private static RiskSeverity? SeverityOf(int? probability, int? impact) =>
        ScoreOf(probability, impact) is { } score ? RiskRules.SeverityOf(score) : null;
}
