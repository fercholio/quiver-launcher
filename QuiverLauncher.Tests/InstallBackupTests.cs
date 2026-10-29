using FluentAssertions;
using QuiverLauncher.Core.Services;

namespace QuiverLauncher.Tests;

public class InstallBackupTests
{
    private static readonly DateOnly Date = new(2026, 10, 29);

    [Fact]
    public void SuggestFileName_includes_name_version_and_date()
    {
        InstallBackup.SuggestFileName("Army of Two", "v1.2", Date)
            .Should().Be("Army of Two v1.2 backup 2026-10-29.zip");
    }

    [Fact]
    public void SuggestFileName_drops_a_blank_version()
    {
        InstallBackup.SuggestFileName("Top Spin", null, Date).Should().Be("Top Spin backup 2026-10-29.zip");
        InstallBackup.SuggestFileName("Top Spin", "   ", Date).Should().Be("Top Spin backup 2026-10-29.zip");
    }

    [Fact]
    public void SuggestFileName_sanitizes_invalid_characters()
    {
        var name = InstallBackup.SuggestFileName("Game: A/B?", "1.0", Date);
        name.Should().EndWith("backup 2026-10-29.zip");
        name.IndexOfAny(Path.GetInvalidFileNameChars()).Should().Be(-1);
    }

    [Fact]
    public void SuggestFileName_falls_back_when_name_is_empty()
    {
        InstallBackup.SuggestFileName("", null, Date).Should().Be("App backup 2026-10-29.zip");
    }
}
