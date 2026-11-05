namespace Isdlc.Application.Common.Exceptions;

/// <summary>
/// The user is signed in but their role cannot do this. Returned to the client as 403.
/// The message is shown to the user, so write it in Turkish.
/// </summary>
public class ForbiddenException(string message) : Exception(message);
