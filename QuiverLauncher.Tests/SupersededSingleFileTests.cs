using FluentAssertions;
using QuiverLauncher.Core.Services;

namespace QuiverLauncher.Tests;

public class SupersededSingleFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "QuiverLauncher.Tests", Guid.NewGuid().ToString("N"));

    public SupersededSingleFileTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => TestFixtures.CleanupDirectory(_dir);

    private string Touch(string name)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, "x");
        return path;
    }

    [Fact]
    public void Removes_the_previous_versioned_exe_and_retargets_the_saved_choice()
    {
        var old = Touch("client-v0.9.21.exe");
        var @new = Touch("client-v0.9.22.exe");
        File.WriteAllText(Path.Combine(_dir, "selected_executable.txt"), old);

        GameInstallationService.RemoveSupersededSingleFile(_dir, "client-v0.9.22.exe",
            new GameInstallationOptions { PreviousInstallFileName = "client-v0.9.21.exe" });

        File.Exists(old).Should().BeFalse();
        File.Exists(@new).Should().BeTrue();
        File.ReadAllText(Path.Combine(_dir, "selected_executable.txt")).Trim().Should().Be(@new);
    }

    [Fact]
    public void Leaves_a_companion_executable_alone()
    {
        var server = Touch("client-server.exe");
        Touch("client-v0.9.22.exe");

        GameInstallationService.RemoveSupersededSingleFile(_dir, "client-v0.9.22.exe",
            new GameInstallationOptions { PreviousInstallFileName = "client-server.exe" });

        File.Exists(server).Should().BeTrue("a different executable is not a versioned variant of the new one");
    }

    [Fact]
    public void Does_nothing_when_there_is_no_recorded_previous_file()
    {
        var other = Touch("client-v0.9.21.exe");
        Touch("client-v0.9.22.exe");

        GameInstallationService.RemoveSupersededSingleFile(_dir, "client-v0.9.22.exe",
            new GameInstallationOptions { PreviousInstallFileName = null });

        File.Exists(other).Should().BeTrue();
    }

    [Fact]
    public void Does_not_retarget_a_saved_choice_that_points_elsewhere()
    {
        Touch("client-v0.9.21.exe");
        Touch("client-v0.9.22.exe");
        var chosen = Touch("client-launcher.exe");
        File.WriteAllText(Path.Combine(_dir, "selected_executable.txt"), chosen);

        GameInstallationService.RemoveSupersededSingleFile(_dir, "client-v0.9.22.exe",
            new GameInstallationOptions { PreviousInstallFileName = "client-v0.9.21.exe" });

        File.ReadAllText(Path.Combine(_dir, "selected_executable.txt")).Trim().Should().Be(chosen);
    }
}
