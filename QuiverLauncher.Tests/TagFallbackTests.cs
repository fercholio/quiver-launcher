using FluentAssertions;
using QuiverLauncher.Core.Services;

namespace QuiverLauncher.Tests;

public class TagFallbackTests
{
    [Fact]
    public void Synthesizes_newest_tag_first_with_an_archive_asset()
    {
        var body = """[{"name":"v1.2.0"},{"name":"v1.3.0"},{"name":"v1.1.0"}]""";
        var releases = GitHubReleaseService.SynthesizeReleasesFromTags("id-Software/quake2", body);

        releases.Select(r => r.tag_name).Should().Equal("v1.3.0", "v1.2.0", "v1.1.0");
        releases[0].assets.Should().ContainSingle();
        releases[0].assets[0].name.Should().Be("quake2-v1.3.0.zip");
        releases[0].assets[0].browser_download_url
            .Should().Be("https://github.com/id-Software/quake2/archive/refs/tags/v1.3.0.zip");
    }

    [Fact]
    public void Selects_the_newest_tag_as_the_latest_release()
    {
        var body = """[{"name":"8.00"},{"name":"8.20"},{"name":"8.10"}]""";
        var releases = GitHubReleaseService.SynthesizeReleasesFromTags("yquake2/yquake2", body);

        CatalogReleaseSelection.SelectLatestRelease(releases)!.tag_name.Should().Be("8.20");
    }

    [Fact]
    public void Marks_labeled_tags_as_prereleases()
    {
        var releases = GitHubReleaseService.SynthesizeReleasesFromTags("o/r", """[{"name":"v2.0.0-rc.1"},{"name":"v1.0.0"}]""");
        releases.Single(r => r.tag_name == "v2.0.0-rc.1").prerelease.Should().BeTrue();
        releases.Single(r => r.tag_name == "v1.0.0").prerelease.Should().BeFalse();
    }

    [Fact]
    public void Empty_or_invalid_bodies_yield_no_releases()
    {
        GitHubReleaseService.SynthesizeReleasesFromTags("o/r", "[]").Should().BeEmpty();
        GitHubReleaseService.SynthesizeReleasesFromTags("o/r", "not json").Should().BeEmpty();
        GitHubReleaseService.SynthesizeReleasesFromTags("o/r", """[{"name":""},{"name":null}]""").Should().BeEmpty();
    }

    [Fact]
    public void Tag_archive_asset_is_installable_not_auxiliary()
    {
        var release = GitHubReleaseService.SynthesizeReleasesFromTags("o/myapp", """[{"name":"v1.0.0"}]""").Single();
        GitHubReleaseService.GetDownloadableAssets(release).Should().ContainSingle();
    }
}
