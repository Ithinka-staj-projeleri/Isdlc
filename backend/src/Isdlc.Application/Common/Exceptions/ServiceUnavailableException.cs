namespace Isdlc.Application.Common.Exceptions;

/// <summary>
/// A service the request depends on (e.g. the AI agent service) is not configured or not reachable. Returned as 503.
/// The message is shown to the user, so write it in Turkish.
/// </summary>
public class ServiceUnavailableException(string message) : Exception(message);
