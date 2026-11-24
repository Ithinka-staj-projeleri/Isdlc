namespace Isdlc.Application.Agents;

/// <summary>
/// Which AI assistants a company may use (Faz 6 · T6). Pure, no database. Contract: docs/multi-tenancy.md →
/// "Yapay zekâ ayarlarının etkisi".
/// </summary>
public static class CompanyAiRules
{
    public const string ClosedMessage = "Yapay zekâ yardımcıları şirketiniz için kapalı.";

    /// <summary>The company's two switches; a request without a company has both off.</summary>
    public record Settings(bool AiEnabled, bool DataSharingAllowed)
    {
        public static readonly Settings Off = new(false, false);
    }

    /// <summary>
    /// Assistants that send the person's text to the model provider (minutes, risks, e-mail): need both switches.
    /// </summary>
    public static bool MaySendText(Settings company) => company.AiEnabled && company.DataSharingAllowed;

    /// <summary>
    /// Turning a recording into text happens on the AI service's machine, so it needs only the company's AI switch,
    /// not its permission to send data out.
    /// </summary>
    public static bool MayTranscribe(Settings company) => company.AiEnabled;
}
