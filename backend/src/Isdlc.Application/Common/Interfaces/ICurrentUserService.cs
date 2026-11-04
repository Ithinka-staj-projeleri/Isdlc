namespace Isdlc.Application.Common.Interfaces;

/// <summary>
/// The user making the current request.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>Null when nobody is signed in.</summary>
    int? UserId { get; }
}
