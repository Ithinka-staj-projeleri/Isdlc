using Isdlc.Application.Agents;

namespace Isdlc.Application.Tests.Agents;

/// <summary>
/// Who an action from the minutes belongs to; no database.
/// Expectations: docs/open-issues-meetings-api.md → "Yapay zekâ önerileri", actions[].ownerUserId.
/// </summary>
public class ActionOwnersTests
{
    private static readonly ActionOwners.User Can = new(1, "Can Öztürk");
    private static readonly ActionOwners.User Elif = new(2, "Elif Yılmaz");
    private static readonly ActionOwners.User Ayse = new(3, "Ayşe Demir");
    private static readonly IReadOnlyList<ActionOwners.User> Users = [Can, Elif, Ayse];

    private static ActionOwners.Owner Resolve(string? name, params string[] participants) =>
        ActionOwners.Resolve(name, Users, participants);

    [Fact]
    public void Resolve_NoName_NoOwner()
    {
        Assert.Equal(new ActionOwners.Owner(null, null, null), Resolve(null, "Can Öztürk"));
    }

    [Theory]
    [InlineData("Can Öztürk")]
    [InlineData("can öztürk")]
    [InlineData("ELİF YILMAZ")]
    public void Resolve_FullNameOfAUser_IsThatUser(string name)
    {
        var owner = Resolve(name);

        Assert.NotNull(owner.UserId);
        Assert.Null(owner.Name);
        Assert.Null(owner.Warning);
    }

    [Theory]
    [InlineData("Can", 1)]
    [InlineData("öztürk", 1)]
    [InlineData("ELİF", 2)]
    [InlineData("Demir", 3)]
    public void Resolve_FirstOrLastNameOfOneUser_IsThatUser(string name, int userId)
    {
        Assert.Equal(new ActionOwners.Owner(userId, null, null), Resolve(name));
    }

    [Fact]
    public void Resolve_FirstNameOfTwoUsers_KeepsTheNameWithAWarning()
    {
        ActionOwners.User[] users = [Can, new(4, "Can Yılmaz")];

        var owner = ActionOwners.Resolve("Can", users, []);

        Assert.Null(owner.UserId);
        Assert.Equal("Can", owner.Name);
        Assert.Equal("“Can” birden fazla kişiye uyuyor: Can Öztürk, Can Yılmaz. Sorumluyu seçin.", owner.Warning);
    }

    [Fact]
    public void Resolve_TwoUsersWithTheSameFullName_KeepsTheNameWithAWarning()
    {
        ActionOwners.User[] users = [Can, new(4, "Can Öztürk")];

        var owner = ActionOwners.Resolve("Can Öztürk", users, []);

        Assert.Null(owner.UserId);
        Assert.Equal("Can Öztürk", owner.Name);
        Assert.NotNull(owner.Warning);
    }

    [Fact]
    public void Resolve_FirstNameOfAParticipantWhoIsNotAUser_IsTheParticipantsFullName()
    {
        Assert.Equal(new ActionOwners.Owner(null, "Hakan Yıldız", null), Resolve("Hakan", "Hakan Yıldız", "Can Öztürk"));
    }

    [Fact]
    public void Resolve_ParticipantNarrowsTwoUsersToOne_IsThatUser()
    {
        // Two users are called Can; the notes' participants say which one was in the meeting.
        ActionOwners.User[] users = [Can, new(4, "Can Yılmaz")];

        var owner = ActionOwners.Resolve("Can", users, ["Can Yılmaz", "Hakan Yıldız"]);

        Assert.Equal(new ActionOwners.Owner(4, null, null), owner);
    }

    [Fact]
    public void Resolve_FirstNameOfTwoParticipants_KeepsTheNameWithAWarning()
    {
        var owner = Resolve("Hakan", "Hakan Yıldız", "Hakan Kara");

        Assert.Null(owner.UserId);
        Assert.Equal("Hakan", owner.Name);
        Assert.Equal("“Hakan” birden fazla kişiye uyuyor: Hakan Kara, Hakan Yıldız. Sorumluyu seçin.", owner.Warning);
    }

    [Theory]
    [InlineData("Müşteri BT")]
    [InlineData("Depo")]
    [InlineData("Can Kara")]
    public void Resolve_NobodyFits_KeepsTheName(string name)
    {
        Assert.Equal(new ActionOwners.Owner(null, name, null), Resolve(name, "Hakan Yıldız"));
    }
}
