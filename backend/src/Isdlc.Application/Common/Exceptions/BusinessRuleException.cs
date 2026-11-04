namespace Isdlc.Application.Common.Exceptions;

/// <summary>
/// The request breaks a business rule. Returned to the client as 400.
/// The message is shown to the user, so write it in Turkish.
/// </summary>
public class BusinessRuleException(string message) : Exception(message);
