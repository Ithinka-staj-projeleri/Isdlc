using System.Globalization;

namespace Isdlc.Application.Agents;

/// <summary>
/// Who an action from the minutes belongs to. Notes usually name people by their first name only ("Can hazırlayacak");
/// the full name comes from a user or from the participants the agent found in the same notes.
/// The code never guesses: a name that fits more than one person stays a name, with a warning.
/// </summary>
public static class ActionOwners
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly StringComparer Names = StringComparer.Create(Turkish, ignoreCase: true);

    public record User(int Id, string FullName);

    /// <summary>Exactly one of UserId and Name is set, or neither when the notes name nobody.</summary>
    public record Owner(int? UserId, string? Name, string? Warning);

    /// <param name="name">The owner in the notes, already cleaned (trimmed, blank is null).</param>
    /// <param name="users">Active users.</param>
    /// <param name="participants">Participant names the agent suggested for the minutes.</param>
    public static Owner Resolve(string? name, IReadOnlyList<User> users, IReadOnlyList<string> participants)
    {
        if (name is null)
        {
            return new Owner(null, null, null);
        }

        // 1. The full name of a user.
        var exact = users.Where(u => Names.Equals(u.FullName, name)).ToList();
        if (exact.Count > 0)
        {
            return exact.Count == 1 ? new Owner(exact[0].Id, null, null) : Ambiguous(name, exact.Select(u => u.FullName));
        }

        // 2. A first or last name completed from the participants; the full name may be a user.
        var fullName = name;
        var participantMatches = participants.Where(p => FitsShortName(p, name)).Distinct(Names).ToList();
        if (participantMatches.Count == 1)
        {
            fullName = participantMatches[0];
            var user = users.Where(u => Names.Equals(u.FullName, fullName)).ToList();
            if (user.Count > 0)
            {
                return user.Count == 1 ? new Owner(user[0].Id, null, null) : Ambiguous(fullName, user.Select(u => u.FullName));
            }
        }
        else if (participantMatches.Count > 1)
        {
            return Ambiguous(name, participantMatches);
        }

        // 3. A first or last name of exactly one user.
        if (fullName == name)
        {
            var userMatches = users.Where(u => FitsShortName(u.FullName, name)).ToList();
            if (userMatches.Count == 1)
            {
                return new Owner(userMatches[0].Id, null, null);
            }

            if (userMatches.Count > 1)
            {
                return Ambiguous(name, userMatches.Select(u => u.FullName));
            }
        }

        return new Owner(null, fullName, null);
    }

    /// <summary>"Can" fits "Can Öztürk" and "Öztürk" fits "Can Öztürk"; a name of more than one word does not.</summary>
    private static bool FitsShortName(string fullName, string shortName)
    {
        if (shortName.Contains(' '))
        {
            return false;
        }

        var words = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 1 && (Names.Equals(words[0], shortName) || Names.Equals(words[^1], shortName));
    }

    private static Owner Ambiguous(string name, IEnumerable<string> candidates) =>
        new(null, name, $"“{name}” birden fazla kişiye uyuyor: {string.Join(", ", candidates.Order(Names))}. Sorumluyu seçin.");
}
