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

    // ---- Cross tests (PLANNING.md): Dide → Erva's modules ----

    [Fact]
    public void Resolve_FullNameBeatsTheParticipants()
    {
        // "Can Öztürk" is a user; a participant called Can Yılmaz does not change that.
        Assert.Equal(new ActionOwners.Owner(1, null, null), Resolve("Can Öztürk", "Can Yılmaz"));
    }

    [Fact]
    public void Resolve_ParticipantWhoIsNotAUser_WinsOverAUserWhoWasNotInTheMeeting()
    {
        // Can Öztürk is a user but was not in the meeting; the Can of the notes is the participant Can Kara.
        Assert.Equal(new ActionOwners.Owner(null, "Can Kara", null), Resolve("Can", "Can Kara", "Elif Yılmaz"));
    }

    [Fact]
    public void Resolve_ParticipantsFullNameFitsTwoUsers_KeepsTheFullNameWithAWarning()
    {
        ActionOwners.User[] users = [Can, new(4, "Can Öztürk")];

        var owner = ActionOwners.Resolve("Öztürk", users, ["Can Öztürk"]);

        Assert.Null(owner.UserId);
        Assert.Equal("Can Öztürk", owner.Name);
        Assert.NotNull(owner.Warning);
    }

    [Fact]
    public void Resolve_SameParticipantWrittenTwice_IsNotAmbiguous()
    {
        ActionOwners.User[] users = [Can, new(4, "Can Yılmaz")];

        var owner = ActionOwners.Resolve("Can", users, ["Can Öztürk", "CAN ÖZTÜRK"]);

        Assert.Equal(new ActionOwners.Owner(1, null, null), owner);
    }

    [Theory]
    [InlineData("Ayşe", 5)]
    [InlineData("Demir", 5)]
    public void Resolve_FirstOrLastNameWithAMiddleName_IsThatUser(string name, int userId)
    {
        Assert.Equal(new ActionOwners.Owner(userId, null, null), ActionOwners.Resolve(name, [new(5, "Ayşe Nur Demir")], []));
    }

    [Fact]
    public void Resolve_MiddleName_IsNotMatched()
    {
        Assert.Equal(new ActionOwners.Owner(null, "Nur", null), ActionOwners.Resolve("Nur", [new(5, "Ayşe Nur Demir")], []));
    }

    [Fact]
    public void Resolve_SingleWordParticipant_DoesNotCompleteTheName()
    {
        // "Hakan" alone in the participants is not a full name; nothing to complete.
        Assert.Equal(new ActionOwners.Owner(null, "Hakan", null), Resolve("Hakan", "Hakan"));
    }

    [Fact]
    public void Resolve_LastNameWithTurkishCapitalI_IsThatUser()
    {
        // "IŞIK" lowers to "ışık" the Turkish way (I → ı), not "işik".
        Assert.Equal(new ActionOwners.Owner(7, null, null), ActionOwners.Resolve("IŞIK", [new(7, "Deniz Işık")], []));
    }

    [Fact]
    public void Resolve_Warning_ListsTheCandidatesInTurkishOrder()
    {
        ActionOwners.User[] users = [new(1, "Çağrı Kaya"), new(2, "Can Kaya"), new(3, "Zeynep Kaya")];

        var owner = ActionOwners.Resolve("Kaya", users, []);

        Assert.Equal("“Kaya” birden fazla kişiye uyuyor: Can Kaya, Çağrı Kaya, Zeynep Kaya. Sorumluyu seçin.", owner.Warning);
    }
}
