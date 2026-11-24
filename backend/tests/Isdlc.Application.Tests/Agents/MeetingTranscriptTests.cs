using System.ComponentModel.DataAnnotations;
using System.Text;
using Isdlc.Application.Agents;
using Isdlc.Application.Agents.Dtos;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Agents;

/// <summary>
/// "Ses kaydından metne çevir" in the minutes assistant, with the AI service replaced by FakeAgentClient.
/// Expectations: docs/open-issues-meetings-api.md → "Ses kaydından notlar".
/// </summary>
[Collection("Database")]
public class MeetingTranscriptTests(DatabaseFixture database)
{
    private static readonly byte[] Recording = Encoding.UTF8.GetBytes("fake audio");

    private async Task<MeetingTranscriptDto> TranscribeAsync(
        FakeAgentClient agent, int projectId, byte[]? recording = null, string? email = TestProjects.Analyst)
    {
        var userId = email is null ? (int?)null : await TestProjects.UserIdAsync(database, email);
        await using var scope = database.CreateScope();
        var service = new AgentService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), new TestUser(userId), agent);
        var bytes = recording ?? Recording;
        await using var stream = new MemoryStream(bytes);
        return await service.TranscribeMeetingAsync(projectId, stream, bytes.Length, CancellationToken.None);
    }

    [Fact]
    public async Task Transcribe_ActiveProject_SendsTheRecordingAndReturnsTheText()
    {
        var projectId = await TestProjects.CreateAsync(database, "6");
        var agent = new FakeAgentClient();

        var transcript = await TranscribeAsync(agent, projectId);

        Assert.Equal("Merhaba.\nBaşlayalım.", transcript.Text);
        Assert.Equal(61, transcript.DurationSeconds);
        Assert.False(transcript.Truncated);
        Assert.Equal(Recording, agent.LastRecording);
    }

    [Fact]
    public async Task Transcribe_EmptyFile_IsABusinessRuleAndTheServiceIsNotCalled()
    {
        var projectId = await TestProjects.CreateAsync(database, "6");
        var agent = new FakeAgentClient();

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => TranscribeAsync(agent, projectId, []));

        Assert.Equal("Kayıt dosyası seçilmelidir.", error.Message);
        Assert.False(agent.WasCalled);
    }

    [Fact]
    public async Task Transcribe_ClosedProject_IsAConflict()
    {
        var projectId = await TestProjects.CreateAsync(database, null, ProjectStatus.Closed);

        await Assert.ThrowsAsync<ConflictException>(() => TranscribeAsync(new FakeAgentClient(), projectId));
    }

    [Fact]
    public async Task Transcribe_ServiceNotConfigured_Is503()
    {
        var projectId = await TestProjects.CreateAsync(database, "6");

        await Assert.ThrowsAsync<ServiceUnavailableException>(
            () => TranscribeAsync(new FakeAgentClient { IsConfigured = false }, projectId));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public async Task GetStatus_TranscriptionNeedsTheServiceAndTheAudioPackage(bool available, bool audio, bool expected)
    {
        await using var scope = database.CreateScope();
        var service = new AgentService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), new TestUser(null),
            new FakeAgentClient { IsAvailable = available, IsTranscriptionAvailable = audio });

        var status = await service.GetStatusAsync(CancellationToken.None);

        Assert.Equal(expected, status.TranscriptionEnabled);
    }

    // ---- Cross tests (PLANNING.md): Dide → Erva's modules ----

    [Fact]
    public async Task Transcribe_NotSignedIn_ThrowsForbiddenAndDoesNotCallTheService()
    {
        var projectId = await TestProjects.CreateAsync(database, "6");
        var agent = new FakeAgentClient();

        await Assert.ThrowsAsync<ForbiddenException>(() => TranscribeAsync(agent, projectId, email: null));

        Assert.False(agent.WasCalled);
    }

    [Fact]
    public async Task Transcribe_ProjectMissing_ThrowsNotFoundAndDoesNotCallTheService()
    {
        var agent = new FakeAgentClient();

        await Assert.ThrowsAsync<NotFoundException>(() => TranscribeAsync(agent, int.MaxValue));

        Assert.False(agent.WasCalled);
    }

    [Fact]
    public async Task Transcribe_NoFile_IsABusinessRule()
    {
        var projectId = await TestProjects.CreateAsync(database, "6");
        var userId = await TestProjects.UserIdAsync(database, TestProjects.Analyst);
        var agent = new FakeAgentClient();
        await using var scope = database.CreateScope();
        var service = new AgentService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), new TestUser(userId), agent);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.TranscribeMeetingAsync(projectId, null, 0, CancellationToken.None));

        Assert.Equal("Kayıt dosyası seçilmelidir.", error.Message);
        Assert.False(agent.WasCalled);
    }

    [Theory]
    [InlineData(ProjectStatus.Rejected)]
    [InlineData(ProjectStatus.Postponed)]
    public async Task Transcribe_ProjectNotActive_IsAConflictAndDoesNotCallTheService(ProjectStatus status)
    {
        var projectId = await TestProjects.CreateAsync(database, null, status);
        var agent = new FakeAgentClient();

        await Assert.ThrowsAsync<ConflictException>(() => TranscribeAsync(agent, projectId));

        Assert.False(agent.WasCalled);
    }

    [Fact]
    public async Task Transcribe_CutTranscript_PassesOnTruncatedAndTheLanguage()
    {
        var projectId = await TestProjects.CreateAsync(database, "6");
        var agent = new FakeAgentClient { Transcript = new("Hello.", 3600, "en", true, "test-whisper") };

        var transcript = await TranscribeAsync(agent, projectId);

        Assert.Equal(new MeetingTranscriptDto("Hello.", 3600, "en", true), transcript);
    }

    [Fact]
    public async Task Transcribe_UnreadableFile_TheServicesMessageReachesThePerson()
    {
        var projectId = await TestProjects.CreateAsync(database, "6");
        var agent = new FakeAgentClient
        {
            Failure = new BusinessRuleException("Kayıt okunamadı; ses ya da video dosyası seçin."),
        };

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => TranscribeAsync(agent, projectId));

        Assert.Equal("Kayıt okunamadı; ses ya da video dosyası seçin.", error.Message);
    }

    // ---- Notes limits: a recording's transcript fits the minutes assistant, an e-mail keeps its own limit ----

    private static List<ValidationResult> Validate(object dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void MeetingNotes_AnHourLongTranscript_Fits()
    {
        Assert.Empty(Validate(new SuggestMeetingItemsDto
        {
            MeetingDate = new DateOnly(2026, 11, 5), Notes = new string('a', MeetingSuggestionLimits.MaxNotes),
        }));
    }

    [Fact]
    public void MeetingNotes_OverTheLimit_IsATurkishError()
    {
        var error = Assert.Single(Validate(new SuggestMeetingItemsDto
        {
            MeetingDate = new DateOnly(2026, 11, 5), Notes = new string('a', 100_001),
        }));

        Assert.Equal("Notlar en fazla 100000 karakter olabilir.", error.ErrorMessage);
    }

    [Fact]
    public void RequestEmail_KeepsItsOwnLimit()
    {
        // F5 raised the meeting notes limit; the e-mail of a request (F6) stays at 20000.
        Assert.Empty(Validate(new DraftRequestFromEmailDto { EmailText = new string('a', 20_000) }));

        var error = Assert.Single(Validate(new DraftRequestFromEmailDto { EmailText = new string('a', 20_001) }));
        Assert.Equal("E-posta en fazla 20000 karakter olabilir.", error.ErrorMessage);
    }
}
