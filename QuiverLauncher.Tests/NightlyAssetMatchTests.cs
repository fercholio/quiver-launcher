using FluentAssertions;
using QuiverLauncher.Core.Models;
using QuiverLauncher.Core.Services;
using QuiverLauncher.Services;

namespace QuiverLauncher.Tests;

public class NightlyAssetMatchTests
{
    [Fact]
    public void NormalizedAssetKey_maps_renamed_git_hash_builds_together()
    {
        var a = DownloadAssetPolicy.NormalizedAssetKey("client-win64-g1a2b3c4.zip", "nightly");
        var b = DownloadAssetPolicy.NormalizedAssetKey("client-win64-gdeadbee.zip", "nightly");
        a.Should().Be(b);
    }

    [Fact]
    public void NormalizedAssetKey_maps_renamed_date_and_build_number_builds_together()
    {
        DownloadAssetPolicy.NormalizedAssetKey("App-2026-10-11-linux.tar.gz", "nightly")
            .Should().Be(DownloadAssetPolicy.NormalizedAssetKey("App-2026-09-10-linux.tar.gz", "nightly"));
        DownloadAssetPolicy.NormalizedAssetKey("App-build-123456-x64.zip", "nightly")
            .Should().Be(DownloadAssetPolicy.NormalizedAssetKey("App-build-987654-x64.zip", "nightly"));
    }

    [Fact]
    public void NormalizedAssetKey_keeps_architecture_tokens_distinct()
    {
        DownloadAssetPolicy.NormalizedAssetKey("client-win64-g1a2b3c4.zip", "nightly")
            .Should().NotBe(DownloadAssetPolicy.NormalizedAssetKey("client-arm64-g1a2b3c4.zip", "nightly"));
    }

    private static GitHubRelease Release(string tag, params (string Name, string? Sha)[] assets) => new()
    {
        tag_name = tag,
        assets = assets.Select(a => new GitHubAsset
        {
            name = a.Name,
            browser_download_url = $"https://x/{a.Name}",
            digest = a.Sha == null ? null : $"sha256:{a.Sha}",
        }).ToArray(),
    };

    private const string Sha1 = "1111111111111111111111111111111111111111111111111111111111111111";
    private const string Sha2 = "2222222222222222222222222222222222222222222222222222222222222222";

    [Fact]
    public void IsRebuiltIn_true_when_exact_asset_checksum_changed()
    {
        var installed = new InstalledBuild("game-nightly.zip", Sha1);
        installed.IsRebuiltIn(Release("nightly", ("game-nightly.zip", Sha2)), "nightly").Should().BeTrue();
    }

    [Fact]
    public void IsRebuiltIn_false_when_exact_asset_checksum_unchanged()
    {
        var installed = new InstalledBuild("game-nightly.zip", Sha1);
        installed.IsRebuiltIn(Release("nightly", ("game-nightly.zip", Sha1)), "nightly").Should().BeFalse();
    }

    [Fact]
    public void IsRebuiltIn_true_when_a_renamed_nightly_asset_has_a_new_checksum()
    {
        // The asset's git hash changed in the file name, but it is the same logical build slot.
        var installed = new InstalledBuild("client-win64-g1a2b3c4.zip", Sha1);
        var release = Release("nightly", ("client-win64-gdeadbee.zip", Sha2));
        installed.IsRebuiltIn(release, "nightly").Should().BeTrue();
    }

    [Fact]
    public void IsRebuiltIn_false_when_the_renamed_match_is_ambiguous()
    {
        // Two assets normalize to the installed identity, so we refuse to guess.
        var installed = new InstalledBuild("client-win64-g1a2b3c4.zip", Sha1);
        var release = Release("nightly",
            ("client-win64-gdeadbee.zip", Sha2),
            ("client-win64-gfeed000.zip", Sha2));
        installed.IsRebuiltIn(release, "nightly").Should().BeFalse();
    }

    [Fact]
    public void IsRebuiltIn_false_when_the_tag_is_different()
    {
        var installed = new InstalledBuild("game-1.0.0.zip", Sha1);
        installed.IsRebuiltIn(Release("v2.0.0", ("game-2.0.0.zip", Sha2)), "v1.0.0").Should().BeFalse();
    }

    [Fact]
    public void IsRebuiltIn_false_without_a_published_checksum()
    {
        var installed = new InstalledBuild("game-nightly.zip", Sha1);
        installed.IsRebuiltIn(Release("nightly", ("game-nightly.zip", null)), "nightly").Should().BeFalse();
    }
}
