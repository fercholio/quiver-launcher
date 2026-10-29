using FluentAssertions;
using QuiverLauncher.Services;

namespace QuiverLauncher.Tests;

public class AppCollectionsTests
{
    [Fact]
    public void Create_adds_a_named_collection_and_reuses_an_existing_name()
    {
        var settings = new AppSettings();
        var first = AppCollections.Create(settings, "  N64 Recomps  ");
        first.Name.Should().Be("N64 Recomps");
        settings.Collections.Should().ContainSingle();

        var again = AppCollections.Create(settings, "n64 recomps");
        again.Should().BeSameAs(first);
        settings.Collections.Should().ContainSingle();
    }

    [Fact]
    public void ToggleMember_adds_then_removes()
    {
        var c = new AppCollection();
        AppCollections.ToggleMember(c, "github:owner/repo").Should().BeTrue();
        AppCollections.IsMember(c, "github:owner/repo").Should().BeTrue();
        AppCollections.ToggleMember(c, "GITHUB:OWNER/REPO").Should().BeFalse();
        AppCollections.IsMember(c, "github:owner/repo").Should().BeFalse();
    }

    [Fact]
    public void SetMember_is_idempotent()
    {
        var c = new AppCollection();
        AppCollections.SetMember(c, "k", true);
        AppCollections.SetMember(c, "k", true);
        c.Members.Should().ContainSingle();
        AppCollections.SetMember(c, "k", false);
        c.Members.Should().BeEmpty();
    }

    [Fact]
    public void Find_matches_by_id()
    {
        var settings = new AppSettings();
        var c = AppCollections.Create(settings, "Favorites");
        AppCollections.Find(settings, c.Id).Should().BeSameAs(c);
        AppCollections.Find(settings, "missing").Should().BeNull();
        AppCollections.Find(settings, null).Should().BeNull();
    }

    [Fact]
    public void RemoveFromAll_drops_the_key_everywhere()
    {
        var settings = new AppSettings();
        var a = AppCollections.Create(settings, "A");
        var b = AppCollections.Create(settings, "B");
        AppCollections.SetMember(a, "k", true);
        AppCollections.SetMember(b, "k", true);

        AppCollections.RemoveFromAll(settings, "k").Should().BeTrue();
        a.Members.Should().BeEmpty();
        b.Members.Should().BeEmpty();
        AppCollections.RemoveFromAll(settings, "k").Should().BeFalse();
    }
}
