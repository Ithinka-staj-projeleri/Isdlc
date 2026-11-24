using System.Net;
using System.Text;
using Isdlc.Application.Agents;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Infrastructure.Agents;
using Microsoft.Extensions.Logging.Abstractions;

namespace Isdlc.Application.Tests.Agents;

/// <summary>
/// How the backend talks to the AI service for a recording (Faz 5 · F5) and for its health; no database, no real service:
/// a fake HTTP handler answers in place of ai/isdlc_ai/server.py.
/// Expectations: docs/open-issues-meetings-api.md → "Ses kaydından notlar" and "Hata durumları".
/// Cross test (PLANNING.md): Dide → Erva's modules.
/// </summary>
public class HttpAgentClientTests
{
    private const string Key = "test-key";

    /// <summary>Answers every request with the given status and body, and keeps what it was sent.</summary>
    private sealed class FakeService(HttpStatusCode status, string body, Exception? failure = null) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public byte[]? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (request.Content is not null)
            {
                LastBody = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            }

            if (failure is not null)
            {
                throw failure;
            }

            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static HttpAgentClient Client(FakeService service, string? url = "http://ai.test/") =>
        new(
            new HttpClient(service) { BaseAddress = url is null ? null : new Uri(url) },
            new AgentOptions { ServiceUrl = url, ServiceKey = Key },
            TimeProvider.System,
            NullLogger<HttpAgentClient>.Instance);

    private static Task<MeetingAgentTranscript> TranscribeAsync(HttpAgentClient client) =>
        client.TranscribeAsync(new MemoryStream("ses"u8.ToArray()), CancellationToken.None);

    // ---- Transcribe ----

    [Fact]
    public async Task Transcribe_SendsTheRawFileWithTheKeyAndReadsTheText()
    {
        var service = new FakeService(HttpStatusCode.OK, """
            {"text":"Merhaba.\nBaşlayalım.","durationSeconds":61,"language":"tr","truncated":true,"modelName":"small"}
            """);

        var transcript = await TranscribeAsync(Client(service));

        Assert.Equal(new MeetingAgentTranscript("Merhaba.\nBaşlayalım.", 61, "tr", true, "small"), transcript);
        var request = Assert.Single(service.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://ai.test/v1/meeting/transcribe", request.RequestUri!.ToString());
        Assert.Equal(Key, Assert.Single(request.Headers.GetValues("X-Agent-Key")));
        Assert.Equal("application/octet-stream", request.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("ses"u8.ToArray(), service.LastBody);
    }

    [Fact]
    public async Task Transcribe_UnreadableFile_IsABusinessRuleWithTheServicesMessage()
    {
        var service = new FakeService(HttpStatusCode.BadRequest, """{"detail":"Kayıt okunamadı; ses ya da video dosyası seçin."}""");

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => TranscribeAsync(Client(service)));

        Assert.Equal("Kayıt okunamadı; ses ya da video dosyası seçin.", error.Message);
    }

    [Fact]
    public async Task Transcribe_TooLarge_IsABusinessRuleWithTheServicesMessage()
    {
        var service = new FakeService(HttpStatusCode.RequestEntityTooLarge, """{"detail":"Kayıt en fazla 500 MB olabilir."}""");

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => TranscribeAsync(Client(service)));

        Assert.Equal("Kayıt en fazla 500 MB olabilir.", error.Message);
    }

    [Fact]
    public async Task Transcribe_BadRequestWithoutAMessage_UsesTheDefaultMessage()
    {
        // FastAPI's own validation error has a list in "detail", not a message for the person.
        var service = new FakeService(HttpStatusCode.BadRequest, """{"detail":[{"msg":"field required"}]}""");

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => TranscribeAsync(Client(service)));

        Assert.Equal("Kayıt okunamadı; ses ya da video dosyası seçin.", error.Message);
    }

    [Fact]
    public async Task Transcribe_NotInstalled_Is503WithTheServicesMessage()
    {
        var service = new FakeService(HttpStatusCode.ServiceUnavailable, """{"detail":"Ses kaydını metne çevirme bu ortamda açık değil."}""");

        var error = await Assert.ThrowsAsync<ServiceUnavailableException>(() => TranscribeAsync(Client(service)));

        Assert.Equal("Ses kaydını metne çevirme bu ortamda açık değil.", error.Message);
    }

    [Fact]
    public async Task Transcribe_ServerError_Is503WithAGeneralMessage()
    {
        var service = new FakeService(HttpStatusCode.InternalServerError, "Internal Server Error");

        var error = await Assert.ThrowsAsync<ServiceUnavailableException>(() => TranscribeAsync(Client(service)));

        Assert.Equal("Yapay zekâ servisi isteği işleyemedi. Biraz sonra tekrar deneyin.", error.Message);
    }

    [Fact]
    public async Task Transcribe_ServiceNotReachable_Is503()
    {
        var service = new FakeService(HttpStatusCode.OK, "", new HttpRequestException("connection refused"));

        var error = await Assert.ThrowsAsync<ServiceUnavailableException>(() => TranscribeAsync(Client(service)));

        Assert.Equal("Yapay zekâ servisine şu an ulaşılamıyor. Biraz sonra tekrar deneyin.", error.Message);
    }

    // ---- Health ----

    [Theory]
    [InlineData("""{"status":"ok","modelConfigured":true,"transcriptionAvailable":true}""", true, true)]
    [InlineData("""{"status":"ok","modelConfigured":true,"transcriptionAvailable":false}""", true, false)]
    [InlineData("""{"status":"ok","modelConfigured":false,"transcriptionAvailable":true}""", false, true)]
    [InlineData("""{"status":"ok","modelConfigured":true}""", true, false)] // a service from before F5
    public async Task Health_ReadsTheModelAndTheAudioPackage(string health, bool available, bool transcription)
    {
        var client = Client(new FakeService(HttpStatusCode.OK, health));

        Assert.Equal(available, await client.IsAvailableAsync(CancellationToken.None));
        Assert.Equal(transcription, await client.IsTranscriptionAvailableAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Health_BothAnswersComeFromOneCall()
    {
        var service = new FakeService(HttpStatusCode.OK, """{"modelConfigured":true,"transcriptionAvailable":true}""");
        var client = Client(service);

        await client.IsAvailableAsync(CancellationToken.None);
        await client.IsTranscriptionAvailableAsync(CancellationToken.None);

        Assert.Equal("http://ai.test/health", Assert.Single(service.Requests).RequestUri!.ToString());
    }

    [Fact]
    public async Task Health_ServiceDown_IsOffAndDoesNotThrow()
    {
        var client = Client(new FakeService(HttpStatusCode.OK, "", new HttpRequestException("connection refused")));

        Assert.False(await client.IsAvailableAsync(CancellationToken.None));
        Assert.False(await client.IsTranscriptionAvailableAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Health_NoServiceUrl_IsOffWithoutAnyCall()
    {
        var service = new FakeService(HttpStatusCode.OK, """{"modelConfigured":true,"transcriptionAvailable":true}""");
        var client = Client(service, url: null);

        Assert.False(await client.IsAvailableAsync(CancellationToken.None));
        Assert.False(await client.IsTranscriptionAvailableAsync(CancellationToken.None));
        Assert.Empty(service.Requests);
    }
}
