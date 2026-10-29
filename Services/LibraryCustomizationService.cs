using Avalonia.Platform.Storage;
using QuiverLauncher.Models;
using QuiverLauncher.ViewModels;

namespace QuiverLauncher.Services;

public sealed class LibraryCustomizationService(GameManager manager, LibraryViewModel library, LauncherSession session,
    Func<IStorageProvider> storage, Func<string, string, bool, Task<bool>> message)
{
    public Task SetIconAsync(GameInfo? game) => session.RunAsync(async () =>
    {
        if (game == null) { await message("Unable to identify the selected app.", "Error", false); return; }
        var replacing = !string.IsNullOrEmpty(game.CustomIconPath);
        if (replacing && !await message($"Replace the existing custom icon for {game.Name}?", "Confirm Icon Replacement", true)) return;
        session.Token.ThrowIfCancellationRequested();
        var files = await storage().OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Select Custom Icon for {game.Name}",
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Image Files") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp", "*.gif", "*.ico" } },
                new FilePickerFileType("PNG Files") { Patterns = new[] { "*.png" } },
                new FilePickerFileType("JPEG Files") { Patterns = new[] { "*.jpg", "*.jpeg" } },
                new FilePickerFileType("WebP Files") { Patterns = new[] { "*.webp" } },
                new FilePickerFileType("Bitmap Files") { Patterns = new[] { "*.bmp" } },
                new FilePickerFileType("Icon Files") { Patterns = new[] { "*.ico" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*" } },
            },
            AllowMultiple = false,
        }).WaitAsync(session.Token);
        session.Token.ThrowIfCancellationRequested();
        if (files.Count == 0) return;
        try { game.SetCustomIcon(files[0].Path.LocalPath, manager.CacheFolder); }
        catch (Exception ex) { await message($"Failed to {(replacing ? "replace" : "set")} custom icon: {ex.Message}", "Error", false); }
    });

    public Task GenerateAiCoverAsync(GameInfo? game) => session.RunAsync(async () =>
    {
        if (game == null) { await message("Unable to identify the selected app.", "Error", false); return; }
        if (!AiConfig.HasComfyUi)
        {
            await message("No image generator is configured. Set QUIVER_COMFYUI_URL to a ComfyUI server to use AI covers.", "AI Cover Unavailable", false);
            return;
        }
        var replacing = !string.IsNullOrEmpty(game.CustomIconPath);
        if (!await message($"Generate a cover image for {game.Name} with AI?{(replacing ? " This replaces the current custom image." : "")}\n\nThis can take a little while.", "Generate AI Cover", true)) return;
        session.Token.ThrowIfCancellationRequested();

        byte[]? image;
        try { image = await CoverArtService.GenerateAsync(game.Name, game.Project, session.Token); }
        catch (OperationCanceledException) when (session.Token.IsCancellationRequested) { return; }
        catch (Exception ex) { await message($"Couldn't generate a cover: {ex.Message}", "AI Cover Failed", false); return; }

        if (image == null || image.Length == 0)
        {
            await message("The image generator didn't return a cover. Check that the ComfyUI server is reachable and the checkpoint is installed.", "AI Cover Failed", false);
            return;
        }

        var temp = Path.Combine(Path.GetTempPath(), $"quiver-cover-{Guid.NewGuid():N}.png");
        try
        {
            await File.WriteAllBytesAsync(temp, image, session.Token);
            game.SetCustomIcon(temp, manager.CacheFolder);
        }
        catch (Exception ex) { await message($"Couldn't apply the generated cover: {ex.Message}", "AI Cover Failed", false); }
        finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { /* best effort */ } }
    });

    public Task UpscaleCoverAsync(GameInfo? game) => session.RunAsync(async () =>
    {
        if (game == null) { await message("Unable to identify the selected app.", "Error", false); return; }
        if (!AiConfig.HasComfyUi)
        {
            await message("No image generator is configured. Set QUIVER_COMFYUI_URL to a ComfyUI server to upscale covers.", "Upscale Unavailable", false);
            return;
        }
        if (string.IsNullOrEmpty(game.CustomIconPath) || !File.Exists(game.CustomIconPath))
        {
            await message($"{game.Name} has no custom image to upscale. Add or generate one first.", "Nothing to Upscale", false);
            return;
        }
        if (!await message($"Upscale {game.Name}'s cover with AI? This can take a little while.", "Upscale Cover", true)) return;
        session.Token.ThrowIfCancellationRequested();

        byte[]? upscaled;
        try
        {
            var source = await File.ReadAllBytesAsync(game.CustomIconPath, session.Token);
            upscaled = await CoverArtService.UpscaleAsync(source, session.Token);
        }
        catch (OperationCanceledException) when (session.Token.IsCancellationRequested) { return; }
        catch (Exception ex) { await message($"Couldn't upscale the cover: {ex.Message}", "Upscale Failed", false); return; }

        if (upscaled == null || upscaled.Length == 0)
        {
            await message("The image generator didn't return an upscaled cover. Check that the ComfyUI server is reachable.", "Upscale Failed", false);
            return;
        }

        var temp = Path.Combine(Path.GetTempPath(), $"quiver-cover-up-{Guid.NewGuid():N}.png");
        try
        {
            await File.WriteAllBytesAsync(temp, upscaled, session.Token);
            game.SetCustomIcon(temp, manager.CacheFolder);
        }
        catch (Exception ex) { await message($"Couldn't apply the upscaled cover: {ex.Message}", "Upscale Failed", false); }
        finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { /* best effort */ } }
    });

    public Task RemoveIconAsync(GameInfo? game) => session.RunAsync(async () =>
    {
        if (game == null) { await message("Unable to identify the selected app.", "Error", false); return; }
        if (string.IsNullOrEmpty(game.CustomIconPath))
        {
            await message($"{game.Name} is already using the default icon.", "No Custom Icon", false);
            return;
        }
        var confirmed = await message($"Remove custom icon for {game.Name}?", "Confirm Removal", true);
        session.Token.ThrowIfCancellationRequested();
        if (!confirmed) return;
        try { game.RemoveCustomIcon(); }
        catch (Exception ex) { await message($"Failed to remove custom icon: {ex.Message}", "Error", false); }
    });

    public Task ToggleHiddenAsync(GameInfo? game) => session.RunAsync(async () =>
    {
        if (game == null) { await message("Unable to identify the selected app.", "Error", false); return; }
        try
        {
            var wasHidden = manager.IsManuallyHidden(game);
            manager.ToggleUserHide(game);
            await manager.LoadGamesAsync();
            session.Token.ThrowIfCancellationRequested();
            library.ApplySorting();
            if (!wasHidden) await message("App hidden from the library.\n\nYou can find it again under Show → Hidden, then choose Customize → Unhide App.", "App Hidden", false);
        }
        catch (OperationCanceledException) when (session.Token.IsCancellationRequested) { }
        catch (Exception ex) { if (!session.IsClosed) await message($"Failed to update app visibility: {ex.Message}", "Error", false); }
    });
}
