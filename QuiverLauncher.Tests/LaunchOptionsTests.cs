using FluentAssertions;
using QuiverLauncher.Core.Services;

namespace QuiverLauncher.Tests;

public class LaunchOptionsTests
{
    [Fact]
    public void SplitArguments_returns_empty_for_blank()
    {
        LaunchOptions.SplitArguments(null).Should().BeEmpty();
        LaunchOptions.SplitArguments("   ").Should().BeEmpty();
    }

    [Fact]
    public void SplitArguments_splits_on_whitespace()
    {
        LaunchOptions.SplitArguments("--fullscreen --vsync off")
            .Should().Equal("--fullscreen", "--vsync", "off");
    }

    [Fact]
    public void SplitArguments_keeps_quoted_paths_with_spaces_together()
    {
        LaunchOptions.SplitArguments("--mods \"C:\\path with spaces\\mods\" --hd")
            .Should().Equal("--mods", "C:\\path with spaces\\mods", "--hd");
    }

    [Fact]
    public void SplitArguments_honours_single_quotes_and_escapes()
    {
        LaunchOptions.SplitArguments("'a b' c\\ d")
            .Should().Equal("a b", "c d");
    }

    [Fact]
    public void SplitArguments_throws_on_unmatched_quote()
    {
        var act = () => LaunchOptions.SplitArguments("--name \"unterminated");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void ParseEnvironment_reads_key_value_lines()
    {
        var env = LaunchOptions.ParseEnvironment("HD_TEXTURES=1\nVK_ICD_FILENAMES=/x/y.json");
        env.Should().HaveCount(2);
        env["HD_TEXTURES"].Should().Be("1");
        env["VK_ICD_FILENAMES"].Should().Be("/x/y.json");
    }

    [Fact]
    public void ParseEnvironment_skips_blanks_comments_and_keyless_lines()
    {
        var env = LaunchOptions.ParseEnvironment("# a comment\n\nONLYKEY\n=novalue\nGOOD=yes");
        env.Should().ContainSingle();
        env["GOOD"].Should().Be("yes");
    }

    [Fact]
    public void ParseEnvironment_keeps_values_with_equals_and_spaces()
    {
        var env = LaunchOptions.ParseEnvironment("CONN=key=val; opt=2");
        env["CONN"].Should().Be("key=val; opt=2");
    }

    [Fact]
    public void FormatEnvironment_round_trips_sorted()
    {
        var env = LaunchOptions.ParseEnvironment("B=2\nA=1");
        var text = LaunchOptions.FormatEnvironment(env);
        text.Should().Be("A=1" + System.Environment.NewLine + "B=2");
        LaunchOptions.ParseEnvironment(text).Should().BeEquivalentTo(env);
    }

    [Fact]
    public void FormatEnvironment_empty_for_null_or_empty()
    {
        LaunchOptions.FormatEnvironment(null).Should().BeEmpty();
        LaunchOptions.FormatEnvironment(new Dictionary<string, string>()).Should().BeEmpty();
    }
}
