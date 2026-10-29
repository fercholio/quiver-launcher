using FluentAssertions;
using QuiverLauncher.Core.Services;

namespace QuiverLauncher.Tests;

public class ExecutableRankerTests
{
    [Fact]
    public void Picks_the_game_over_an_installer_and_redist()
    {
        var result = ExecutableRanker.Rank(
            ["unins000.exe", "vcredist_x64.exe", "Celeste64.exe"],
            "Celeste 64");

        result.ConfidentPick.Should().Be("Celeste64.exe");
        result.Ranked[0].Path.Should().Be("Celeste64.exe");
    }

    [Fact]
    public void Prefers_an_executable_matching_the_app_name()
    {
        var result = ExecutableRanker.Rank(
            ["data.exe", "StarshipParty.exe"],
            "Starship Party");

        result.ConfidentPick.Should().Be("StarshipParty.exe");
    }

    [Fact]
    public void Prefers_a_root_executable_over_a_buried_helper()
    {
        var result = ExecutableRanker.Rank(
            ["Game.exe", "tools/CrashHandler.exe", "redist/dxsetup.exe"],
            "Game");

        result.ConfidentPick.Should().Be("Game.exe");
    }

    [Fact]
    public void Single_candidate_is_always_a_confident_pick()
    {
        var result = ExecutableRanker.Rank(["OnlyOne.exe"], "Whatever");

        result.ConfidentPick.Should().Be("OnlyOne.exe");
    }

    [Fact]
    public void No_confident_pick_when_the_only_candidates_look_like_installers()
    {
        var result = ExecutableRanker.Rank(
            ["setup.exe", "unins000.exe"],
            "Some Game");

        result.ConfidentPick.Should().BeNull();
    }

    [Fact]
    public void No_confident_pick_when_two_plain_candidates_tie()
    {
        var result = ExecutableRanker.Rank(
            ["aaa.exe", "bbb.exe"],
            "Totally Unrelated Name");

        result.ConfidentPick.Should().BeNull();
    }

    [Fact]
    public void Prefers_a_bundled_launcher_over_the_game_and_helpers()
    {
        var result = ExecutableRanker.Rank(
            ["ao2_install.exe", "ao2_lobby_server.exe", "ArmyOfTwoLauncher.exe", "armyoftworecomp.exe"],
            "Army of Two");

        result.ConfidentPick.Should().Be("ArmyOfTwoLauncher.exe");
    }

    [Fact]
    public void Empty_input_returns_no_pick()
    {
        var result = ExecutableRanker.Rank([], "Game");

        result.Ranked.Should().BeEmpty();
        result.ConfidentPick.Should().BeNull();
    }
}
