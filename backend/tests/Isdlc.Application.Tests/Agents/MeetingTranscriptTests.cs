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
}
