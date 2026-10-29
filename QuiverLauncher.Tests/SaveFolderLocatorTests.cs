using FluentAssertions;
using QuiverLauncher.Core.Services;

namespace QuiverLauncher.Tests;

public class SaveFolderLocatorTests
{
    [Fact]
    public void Candidates_puts_install_save_subfolders_first()
    {
        var candidates = SaveFolderLocator.Candidates("C:\\games\\ISS2000", "ISS 2000");

        candidates[0].Should().Be(Path.Combine("C:\\games\\ISS2000", "save"));
        candidates.Should().Contain(Path.Combine("C:\\games\\ISS2000", "saves"));
    }

    [Fact]
    public void Candidates_includes_user_roots_by_app_name_when_provided()
    {
        var roots = new SaveFolderLocator.UserRoots("C:\\Users\\me\\AppData\\Roaming", null, "C:\\Users\\me\\Documents", null);
        var candidates = SaveFolderLocator.Candidates("C:\\games\\App", "My Game", roots);

        candidates.Should().Contain(Path.Combine("C:\\Users\\me\\AppData\\Roaming", "My Game"));
        candidates.Should().Contain(Path.Combine("C:\\Users\\me\\Documents", "My Game"));
    }

    [Fact]
    public void Candidates_ends_with_the_install_folder_as_fallback()
    {
        var candidates = SaveFolderLocator.Candidates("C:\\games\\App", "App");

        candidates[^1].Should().Be("C:\\games\\App");
    }

    [Fact]
    public void Candidates_skips_blank_name_user_roots()
    {
        var roots = new SaveFolderLocator.UserRoots("C:\\Roaming", null, null, null);
        var candidates = SaveFolderLocator.Candidates("C:\\games\\App", "  ", roots);

        candidates.Should().NotContain(c => c.StartsWith("C:\\Roaming", StringComparison.Ordinal));
    }

    [Fact]
    public void Candidates_empty_for_blank_install_path_and_name()
    {
        SaveFolderLocator.Candidates(null, null).Should().BeEmpty();
    }

    [Fact]
    public void Candidates_has_no_duplicates()
    {
        var candidates = SaveFolderLocator.Candidates("C:\\games\\App", "App");
        candidates.Should().OnlyHaveUniqueItems();
    }
}
