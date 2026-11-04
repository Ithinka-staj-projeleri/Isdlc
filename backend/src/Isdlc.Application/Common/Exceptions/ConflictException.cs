namespace Isdlc.Application.Common.Exceptions;

/// <summary>
/// The record is not in the state the request expects (e.g. it changed in the meantime). Returned as 409.
/// The message is shown to the user, so write it in Turkish.
/// </summary>
public class ConflictException(string message) : Exception(message);
