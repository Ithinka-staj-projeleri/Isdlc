using Isdlc.Application.Notifications;
using Isdlc.Domain.Notifications;

namespace Isdlc.Application.Tests.Notifications;

/// <summary>
/// Pure notification rules (no database). Expectations: docs/notifications-api.md → "Gönderim kuralları".
/// The service's database tests are Erva's cross tests (PLANNING.md: Erva → Baran's modules).
/// </summary>
public class NotificationRulesTests
{
    private static readonly IReadOnlySet<int> Active = new HashSet<int> { 1, 2, 3, 4 };

    // ---- Recipients ----

    [Fact]
    public void Recipients_ActiveUsers_AllReceiveInTheRequestedOrder()
    {
        Assert.Equal([3, 1, 2], NotificationRules.Recipients([3, 1, 2], Active, actingUserId: null));
    }

    [Fact]
    public void Recipients_SameUserTwice_ReceivesOnce()
    {
        // e.g. a project closed whose analyst is also its project manager.
        Assert.Equal([2], NotificationRules.Recipients([2, 2], Active, actingUserId: null));
    }

    [Fact]
    public void Recipients_InactiveOrUnknownUser_IsLeftOut()
    {
        Assert.Equal([1], NotificationRules.Recipients([1, 9], Active, actingUserId: null));
    }

    [Fact]
    public void Recipients_TheActingUser_IsLeftOut()
    {
        // A manager who moves a project to their own approval step is not told "onayınızı bekliyor".
        Assert.Equal([1, 3], NotificationRules.Recipients([1, 2, 3], Active, actingUserId: 2));
    }

    [Fact]
    public void Recipients_OnlyTheActingUser_IsNobody()
    {
        Assert.Empty(NotificationRules.Recipients([2], Active, actingUserId: 2));
    }

    [Fact]
    public void Recipients_NobodySignedIn_LeavesNobodyOutForThat()
    {
        // Background work (seeding) has no signed-in user; everyone active still receives it.
        Assert.Equal([1, 2], NotificationRules.Recipients([1, 2], Active, actingUserId: null));
    }

    [Fact]
    public void Recipients_NoUsers_IsNobody()
    {
        Assert.Empty(NotificationRules.Recipients([], Active, actingUserId: 1));
    }

    // ---- Message ----

    [Fact]
    public void Message_Short_IsKeptTrimmed()
    {
        Assert.Equal("ISDLC-2026-016 · 4.5 onayınızı bekliyor.",
            NotificationRules.Message("  ISDLC-2026-016 · 4.5 onayınızı bekliyor.  "));
    }

    [Fact]
    public void Message_ExactlyTheColumnLength_IsKept()
    {
        var text = new string('a', Notification.MessageMaxLength);

        Assert.Equal(text, NotificationRules.Message(text));
    }

    [Fact]
    public void Message_Longer_IsCutToTheColumnLengthWithEllipsis()
    {
        var stored = NotificationRules.Message(new string('a', Notification.MessageMaxLength + 50));

        Assert.Equal(Notification.MessageMaxLength, stored.Length);
        Assert.EndsWith("…", stored);
    }
}
