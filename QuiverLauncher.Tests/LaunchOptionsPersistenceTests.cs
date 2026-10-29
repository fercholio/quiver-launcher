using FluentAssertions;
using QuiverLauncher.Models;
using QuiverLauncher.Services;

namespace QuiverLauncher.Tests;

public class LaunchOptionsPersistenceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "QuiverLauncher.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose() => TestFixtures.CleanupDirectory(_directory);

    [Fact]
    public async Task Launch_options_round_trip_through_apps_json()
    {
        Directory.CreateDirectory(_directory);
        var service = new AppCatalogService(dataDirectory: _directory);
        var app = new GameInfo
        {
            Name = "Alpha",
            FolderName = "Alpha",
            Repository = "owner/alpha",
            LaunchArguments = "--fullscreen --mods \"C:\\my mods\"",
            LaunchWorkingDirectory = "C:\\games\\alpha",
            LaunchEnvironment = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["HD_TEXTURES"] = "1",
                ["VK_ICD_FILENAMES"] = "/x/y.json",
            },
        };

        await service.SaveLocalAppsAsync([app]);
        var loaded = (await service.LoadLocalAppsAsync()).Single();

        loaded.LaunchArguments.Should().Be("--fullscreen --mods \"C:\\my mods\"");
        loaded.LaunchWorkingDirectory.Should().Be("C:\\games\\alpha");
        loaded.LaunchEnvironment.Should().BeEquivalentTo(app.LaunchEnvironment);
        loaded.HasCustomLaunchOptions.Should().BeTrue();
    }

    [Fact]
    public async Task No_launch_options_leaves_them_unset_after_round_trip()
    {
        Directory.CreateDirectory(_directory);
        var service = new AppCatalogService(dataDirectory: _directory);
        var app = new GameInfo { Name = "Beta", FolderName = "Beta", Repository = "owner/beta" };

        await service.SaveLocalAppsAsync([app]);
        var loaded = (await service.LoadLocalAppsAsync()).Single();

        loaded.LaunchArguments.Should().BeNull();
        loaded.LaunchWorkingDirectory.Should().BeNull();
        loaded.LaunchEnvironment.Should().BeEmpty();
        loaded.HasCustomLaunchOptions.Should().BeFalse();
    }
}
