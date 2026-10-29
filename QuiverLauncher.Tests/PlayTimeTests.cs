using FluentAssertions;
using QuiverLauncher.Models;
using QuiverLauncher.ViewModels;

namespace QuiverLauncher.Tests;

public class PlayTimeTests
{
    [Theory]
    [InlineData(0, "Never played")]
    [InlineData(5, "Less than a minute")]
    [InlineData(59, "Less than a minute")]
    [InlineData(60, "1m")]
    [InlineData(47 * 60, "47m")]
    [InlineData(3 * 3600, "3h")]
    [InlineData(3 * 3600 + 20 * 60, "3h 20m")]
    public void FormatPlayTime_reads_naturally(long seconds, string expected)
    {
        GameInfo.FormatPlayTime(seconds).Should().Be(expected);
    }

    [Fact]
    public void AddPlayTime_accumulates_and_ignores_tiny_and_huge_sessions()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"quiver-playtime-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var game = new GameInfo { Name = "Test", FolderName = "Test" };

            game.AddPlayTime(dir, TimeSpan.FromSeconds(2));   // too short — ignored
            GameInfo.ReadPlayTimeSeconds(dir).Should().Be(0);

            game.AddPlayTime(dir, TimeSpan.FromMinutes(30));  // counts
            game.AddPlayTime(dir, TimeSpan.FromMinutes(15));  // counts (accumulates)
            game.AddPlayTime(dir, TimeSpan.FromDays(3));       // implausible — ignored

            GameInfo.ReadPlayTimeSeconds(dir).Should().Be(45 * 60);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void MostPlayed_comparer_orders_more_played_first()
    {
        var less = new GameInfo { Name = "A", FolderName = "A", PlayTimeSortSeconds = 100 };
        var more = new GameInfo { Name = "B", FolderName = "B", PlayTimeSortSeconds = 5000 };

        GameGridViewModel.CompareForInsertion(more, less, "MostPlayed", ignoreArticles: true).Should().BeLessThan(0);
        GameGridViewModel.CompareForInsertion(less, more, "MostPlayed", ignoreArticles: true).Should().BeGreaterThan(0);
    }
}
