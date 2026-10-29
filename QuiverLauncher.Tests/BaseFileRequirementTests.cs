using FluentAssertions;
using QuiverLauncher.Core.Services;

namespace QuiverLauncher.Tests;

public class BaseFileRequirementTests
{
    [Theory]
    [InlineData("port")]
    [InlineData("emulator")]
    [InlineData("PORT")]
    public void Port_and_emulator_project_types_need_a_copy(string type)
    {
        BaseFileRequirement.Detect(type, null, null).NeedsOwnCopy.Should().BeTrue();
    }

    [Theory]
    [InlineData("game")]
    [InlineData("tool")]
    [InlineData("")]
    public void Standalone_games_and_tools_do_not(string type)
    {
        BaseFileRequirement.Detect(type, null, null).NeedsOwnCopy.Should().BeFalse();
    }

    [Fact]
    public void Recomp_or_decomp_tags_need_a_copy()
    {
        BaseFileRequirement.Detect("game", ["n64", "recomp"], null).NeedsOwnCopy.Should().BeTrue();
        BaseFileRequirement.Detect("game", ["decomp"], null).NeedsOwnCopy.Should().BeTrue();
    }

    [Fact]
    public void Plain_console_tags_alone_do_not_need_a_copy()
    {
        BaseFileRequirement.Detect("game", ["n64", "platformer"], "An original homebrew platformer.")
            .NeedsOwnCopy.Should().BeFalse();
    }

    [Fact]
    public void Description_mentioning_a_rom_or_iso_needs_a_copy()
    {
        BaseFileRequirement.Detect("game", null, "You must provide your own copy of the game.")
            .NeedsOwnCopy.Should().BeTrue();
        BaseFileRequirement.Detect("game", null, "Requires an ISO you legally own.")
            .NeedsOwnCopy.Should().BeTrue();
    }

    [Fact]
    public void Note_never_tells_the_player_where_to_get_files()
    {
        var note = BaseFileRequirement.Detect("port", null, null).Note.ToLowerInvariant();
        note.Should().Contain("you already own");
        note.Should().NotContain("download");
        note.Should().NotContain("torrent");
    }
}
