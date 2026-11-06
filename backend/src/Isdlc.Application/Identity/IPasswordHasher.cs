namespace Isdlc.Application.Identity;

public interface IPasswordHasher
{
    string Hash(string password);
    /// <summary>False for a wrong password and also for an empty or unreadable hash.</summary>
    bool Verify(string hash, string password);
}