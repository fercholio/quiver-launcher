using Avalonia.Platform.Storage;
using System.IO.Compression;
using QuiverLauncher.Core.Models;
using QuiverLauncher.Core.Services;
using QuiverLauncher.Models;
using QuiverLauncher.ViewModels;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace QuiverLauncher.Services;
/// <summary>Library management actions using the existing install and persistence services.</summary>
public sealed class LibraryActions
{
    private readonly GameManager _gameManager;
    private readonly LibraryPersistenceService _libraryPersistence;
    private readonly SettingsViewModel _settingsModel;
    private readonly LauncherSession _session;
    private readonly LibraryViewModel _library;
    private readonly Func<IStorageProvider> _storage;
    private readonly Func<string, string, bool, bool, Task<bool>> _prompt;
    private readonly Action<string> _openUrl;
    private readonly Func<Task> _libraryChanged;
    private AppSettings _settings => _settingsModel.Current;

    public LibraryActions(GameManager manager, LibraryPersistenceService persistence, SettingsViewModel settings, LauncherSession session, LibraryViewModel library, Func<IStorageProvider> storage, Func<string, string, bool, bool, Task<bool>> prompt, Action<string> openUrl, Func<Task> libraryChanged)
    {
        _gameManager = manager;
        _libraryPersistence = persistence;
        _settingsModel = settings;
        _session = session;
        _library = library;
        _storage = storage;
        _prompt = prompt;
        _openUrl = openUrl;
        _libraryChanged = libraryChanged;
    }

    private bool RefuseKiosk() => _settingsModel.KioskLocked;
    private Task<bool> ShowMessageBoxAsync(string message, string title, bool isQuestion = false, bool preferCancelDefault = false) => _session.IsClosed ? Task.FromResult(false) : _prompt(message, title, isQuestion, preferCancelDefault);
    private void OpenUrl(string url)
    {
        if (!_session.IsClosed)
            _openUrl(url);
    }

    private void Changed()
    {
        if (!_session.IsClosed)
        {
            _library.ApplySorting();
            _library.RefreshContinue();
        }
    }

    public Task OpenRepositoryAsync(GameInfo? game)
    {
        if (RefuseKiosk()) return Task.CompletedTask;
        return _session.RunAsync(async () =>
    {
        if (string.IsNullOrEmpty(game?.Repository)) { await ShowMessageBoxAsync("Failed to open repository page", "Error"); return; }
        try { OpenUrl(RepositorySourceHelper.GetRepositoryPageUrl(game.RepositorySource, game.Repository)); }
        catch (Exception ex) { await ShowMessageBoxAsync($"Failed to open repository page: {ex.Message}", "Error"); }
    });
    }

    public void OpenGameFolder(GameInfo game)
    {
        if (RefuseKiosk() || _session.IsClosed) return;
        if (string.IsNullOrEmpty(game.FolderName) && string.IsNullOrWhiteSpace(game.InstallPath))
        {
            _ = _session.RunAsync(() => ShowMessageBoxAsync("Unable to identify the game folder.", "Action Error"));
            return;
        }

        try
        {
            if (game.IsManuallyManaged)
                ManualAppFolderService.EnsurePrepared(game, _gameManager.GamesFolder);
            var folderPath = game.GetInstallPath(_gameManager.GamesFolder);
            if (FlatpakService.HasReceipt(folderPath))
            {
                var receipt = FlatpakService.ReadReceipt(folderPath) ?? FlatpakService.ReadReceipt(folderPath, pending: true);
                if (receipt != null && Directory.Exists(FlatpakService.DataDirectory(receipt)))
                    folderPath = FlatpakService.DataDirectory(receipt);
            }
            else if (WindowsInstallerService.HasReceipt(folderPath))
            {
                // An installer app lives under Program Files, not in Quiver's folder, so open where it
                // actually installed: the chosen executable's folder, else a best guess in Program Files.
                var installerReceipt = WindowsInstallerService.ReadReceipt(folderPath);
                var exeDir = Path.GetDirectoryName(installerReceipt?.ExecutablePath ?? "");
                folderPath = !string.IsNullOrEmpty(exeDir) && Directory.Exists(exeDir)
                    ? exeDir
                    : WindowsInstallerService.GuessInstalledFolder(game.Name) ?? folderPath;
            }
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                _ = _session.RunAsync(() => ShowMessageBoxAsync("Unable to identify the game folder.", "Action Error"));
                return;
            }

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
            OpenUrl(folderPath);
        }
        catch (Exception ex)
        {
            _ = _session.RunAsync(() => ShowMessageBoxAsync($"Failed to open folder: {ex.Message}", "Action Error"));
        }
    }

    /// <summary>
    /// Best-guess save-data folder for <paramref name="game"/>: the first candidate from
    /// <see cref="SaveFolderLocator"/> that exists on disk, else the install folder. Never null when the
    /// app has an install folder.
    /// </summary>
    public string? ResolveSaveFolder(GameInfo game)
    {
        var installPath = game.GetInstallPath(_gameManager.GamesFolder);
        if (string.IsNullOrWhiteSpace(installPath))
            return null;

        string? HomePath(Environment.SpecialFolder folder)
        {
            try { return Environment.GetFolderPath(folder); } catch { return null; }
        }

        var userProfile = HomePath(Environment.SpecialFolder.UserProfile);
        var roots = new SaveFolderLocator.UserRoots(
            HomePath(Environment.SpecialFolder.ApplicationData),
            HomePath(Environment.SpecialFolder.LocalApplicationData),
            HomePath(Environment.SpecialFolder.MyDocuments),
            string.IsNullOrWhiteSpace(userProfile) ? null : Path.Combine(userProfile, "Saved Games"));

        foreach (var candidate in SaveFolderLocator.Candidates(installPath, game.Name, roots))
        {
            try { if (Directory.Exists(candidate)) return candidate; } catch { /* unreadable: skip */ }
        }

        return installPath;
    }

    public void OpenSaveFolder(GameInfo game)
    {
        if (RefuseKiosk() || _session.IsClosed) return;
        try
        {
            var folder = ResolveSaveFolder(game);
            if (string.IsNullOrWhiteSpace(folder))
            {
                _ = _session.RunAsync(() => ShowMessageBoxAsync("Unable to identify the save folder for this app.", "Save Folder"));
                return;
            }
            OpenUrl(folder);
        }
        catch (Exception ex)
        {
            _ = _session.RunAsync(() => ShowMessageBoxAsync($"Failed to open the save folder: {ex.Message}", "Save Folder"));
        }
    }

    public async Task BackUpAsync(GameInfo? game)
    {
        if (RefuseKiosk()) return;
        await _session.RunAsync(async () =>
        {
            if (game == null || string.IsNullOrWhiteSpace(game.FolderName))
            {
                await ShowMessageBoxAsync("Unable to identify the selected app.", "Error");
                return;
            }
            var installPath = game.GetInstallPath(_gameManager.GamesFolder);
            if (string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath))
            {
                await ShowMessageBoxAsync("This app has no install folder to back up.", "Back Up");
                return;
            }

            var suggested = InstallBackup.SuggestFileName(game.Name, game.InstalledVersion, DateOnly.FromDateTime(DateTime.Now));
            var file = await _storage().SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = $"Back up {game.Name}",
                SuggestedFileName = suggested,
                DefaultExtension = "zip",
                FileTypeChoices = [new FilePickerFileType("Zip archive") { Patterns = ["*.zip"] }],
            }).WaitAsync(_session.Token);
            if (file == null || _session.IsClosed)
                return;

            var dest = file.Path.LocalPath;
            try
            {
                game.IsLoading = true;
                await Task.Run(() =>
                {
                    if (File.Exists(dest)) File.Delete(dest);
                    ZipFile.CreateFromDirectory(installPath, dest, CompressionLevel.Optimal, includeBaseDirectory: false);
                }, _session.Token);
                await ShowMessageBoxAsync($"Backed up {game.Name} to:\n{dest}", "Backup Complete");
            }
            catch (Exception ex)
            {
                await ShowMessageBoxAsync($"Backup failed: {ex.Message}", "Back Up");
            }
            finally { game.IsLoading = false; }
        });
    }

    public async Task RestoreBackupAsync(GameInfo? game)
    {
        if (RefuseKiosk()) return;
        await _session.RunAsync(async () =>
        {
            if (game == null || string.IsNullOrWhiteSpace(game.FolderName))
            {
                await ShowMessageBoxAsync("Unable to identify the selected app.", "Error");
                return;
            }
            var installPath = game.GetInstallPath(_gameManager.GamesFolder);
            if (string.IsNullOrWhiteSpace(installPath))
            {
                await ShowMessageBoxAsync("This app has no install folder to restore into.", "Restore Backup");
                return;
            }

            var files = await _storage().OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = $"Restore {game.Name} from backup",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Zip archive") { Patterns = ["*.zip"] }],
            }).WaitAsync(_session.Token);
            if (files == null || files.Count == 0 || _session.IsClosed)
                return;
            var src = files[0].Path.LocalPath;

            if (!await ShowMessageBoxAsync(
                    $"Restore {game.Name} from this backup?\n\nFiles in the install folder with the same names will be overwritten.",
                    "Restore Backup", isQuestion: true, preferCancelDefault: true) || _session.IsClosed)
                return;

            try
            {
                game.IsLoading = true;
                await Task.Run(() =>
                {
                    Directory.CreateDirectory(installPath);
                    ZipFile.ExtractToDirectory(src, installPath, overwriteFiles: true);
                }, _session.Token);
                await game.CheckStatusAsync(_gameManager.HttpClient, _gameManager.GamesFolder, checkRemoteVersion: false);
                Changed();
                await ShowMessageBoxAsync($"Restored {game.Name} from the backup.", "Restore Complete");
            }
            catch (Exception ex)
            {
                await ShowMessageBoxAsync($"Restore failed: {ex.Message}", "Restore Backup");
            }
            finally { game.IsLoading = false; }
        });
    }

    public async Task ConfigureRunnerAsync(GameInfo? game)
    {
        if (RefuseKiosk()) return;
        await _session.RunAsync(async () =>
        {
            if (game == null || string.IsNullOrWhiteSpace(game.FolderName))
            {
                await ShowMessageBoxAsync("Unable to identify the selected app.", "Error");
                return;
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                await ShowMessageBoxAsync("Windows runner settings are only used on Linux.", "Windows Runner");
                return;
            }

            try
            {
                var gamePath = game.GetInstallPath(_gameManager.GamesFolder);
                var config = await GameDialogService.ShowLinuxWindowsRunnerDialogAsync(gamePath, LinuxWindowsRunnerConfig.FromGame(game), isInstall: false, cancellationToken: _session.Token);
                if (config == null || _session.IsClosed)
                    return;
                config.ApplyTo(game);
                var allGames = await LoadGamesFromJsonAsync();
                var matchingGame = FindMatchingSavedApp(allGames, game);
                if (matchingGame != null)
                {
                    matchingGame.LinuxRunner = game.LinuxRunner;
                    matchingGame.LinuxPrefixPath = game.LinuxPrefixPath;
                    matchingGame.LinuxProtonPath = game.LinuxProtonPath;
                    matchingGame.LinuxCustomLaunchCommand = game.LinuxCustomLaunchCommand;
                    await SaveGamesToJsonAsync(allGames);
                }
            }
            catch (Exception ex)
            {
                await ShowMessageBoxAsync($"Failed to save Windows runner settings: {ex.Message}", "Windows Runner");
            }
        });
    }

    public async Task ManageCollectionsAsync(GameInfo? game)
    {
        if (RefuseKiosk()) return;
        await _session.RunAsync(async () =>
        {
            if (game == null || string.IsNullOrWhiteSpace(game.InstanceKey))
            {
                await ShowMessageBoxAsync("Unable to identify the selected app.", "Error");
                return;
            }

            var settings = _settingsModel.Current;
            settings.EnsureInitialized();
            var key = game.InstanceKey;
            var dialog = new QuiverLauncher.Views.CollectionsDialog(
                game.DisplayName ?? game.Name ?? "this app",
                settings.Collections,
                collection => AppCollections.IsMember(collection, key));
            await GameDialogService.ShowWindowAsync(dialog, _session.Token);
            if (!dialog.Saved || _session.IsClosed)
                return;

            foreach (var (collectionId, member) in dialog.Membership)
                if (AppCollections.Find(settings, collectionId) is { } collection)
                    AppCollections.SetMember(collection, key, member);

            foreach (var name in dialog.NewCollectionNames)
                AppCollections.SetMember(AppCollections.Create(settings, name), key, true);

            _settingsModel.SaveCurrent();
            _gameManager.ApplyTagDisplayFilter(settings);
            Changed();
        });
    }

    public async Task ConfigureLaunchOptionsAsync(GameInfo? game)
    {
        if (RefuseKiosk()) return;
        await _session.RunAsync(async () =>
        {
            if (game == null || (string.IsNullOrWhiteSpace(game.FolderName) && string.IsNullOrWhiteSpace(game.InstallPath)))
            {
                await ShowMessageBoxAsync("Unable to identify the selected app.", "Error");
                return;
            }

            try
            {
                var dialog = new QuiverLauncher.Views.LaunchOptionsDialog(
                    game.Name ?? "this app",
                    game.LaunchArguments,
                    game.LaunchWorkingDirectory,
                    game.LaunchEnvironment);
                await GameDialogService.ShowWindowAsync(dialog, _session.Token);
                if (!dialog.Saved || _session.IsClosed)
                    return;

                game.LaunchArguments = string.IsNullOrWhiteSpace(dialog.Arguments) ? null : dialog.Arguments;
                game.LaunchWorkingDirectory = string.IsNullOrWhiteSpace(dialog.WorkingDirectory) ? null : dialog.WorkingDirectory;
                game.LaunchEnvironment = LaunchOptions.ParseEnvironment(dialog.EnvironmentText);

                var allGames = await LoadGamesFromJsonAsync();
                var matchingGame = FindMatchingSavedApp(allGames, game);
                if (matchingGame != null)
                {
                    matchingGame.LaunchArguments = game.LaunchArguments;
                    matchingGame.LaunchWorkingDirectory = game.LaunchWorkingDirectory;
                    matchingGame.LaunchEnvironment = game.LaunchEnvironment;
                    await SaveGamesToJsonAsync(allGames);
                }
            }
            catch (Exception ex)
            {
                await ShowMessageBoxAsync($"Failed to save launch options: {ex.Message}", "Launch Options");
            }
        });
    }

    public async Task LocateInstallAsync(GameInfo? game)
    {
        if (RefuseKiosk()) return;
        await _session.RunAsync(async () =>
        {
            if (game == null)
            {
                await ShowMessageBoxAsync("Unable to identify the selected app.", "Error");
                return;
            }

            var folders = await _storage().OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = $"Select existing install folder for {game.Name}", AllowMultiple = false }).WaitAsync(_session.Token);
            _session.Token.ThrowIfCancellationRequested();
            if (folders == null || folders.Count == 0)
                return;
            var selectedPath = folders[0].Path.LocalPath;
            if (string.IsNullOrWhiteSpace(selectedPath) || !Directory.Exists(selectedPath))
            {
                await ShowMessageBoxAsync("The selected install folder could not be found.", "Install Folder Not Found");
                return;
            }

            GameInstallLocationService.ApplyLocatedPath(game, selectedPath);
            await _libraryPersistence.SaveInstallLocationAsync(game);
            await game.CheckStatusAsync(_gameManager.HttpClient, _gameManager.GamesFolder, forceUpdateCheck: true);
            Changed();
        });
    }

    public async Task UninstallAsync(GameInfo? game)
    {
        if (RefuseKiosk()) return;
        await _session.RunAsync(async () =>
        {
            if (game == null)
                return;
            var flatpakPath = game.GetInstallPath(_gameManager.GamesFolder);
            if (WindowsInstallerService.HasReceipt(flatpakPath))
            {
                if (!OperatingSystem.IsWindows())
                {
                    await ShowMessageBoxAsync("Uninstall this application using Installed apps on Windows.", "Windows Installation");
                    return;
                }
                try { OpenUrl(WindowsInstallerService.InstalledAppsUri); }
                catch (Exception ex) { await ShowMessageBoxAsync($"Open Windows Settings → Apps → Installed apps to uninstall {game.Name}. {ex.Message}", "Windows Settings"); }
                return;
            }
            if (FlatpakService.HasReceipt(flatpakPath))
            {
                if (!await ShowMessageBoxAsync($"Uninstall {game.Name}?\n\nYour saves and application data will be preserved.",
                    "Uninstall Flatpak", isQuestion: true, preferCancelDefault: true) || _session.IsClosed) return;
                game.IsLoading = true;
                try
                {
                    await FlatpakService.Current.UninstallAsync(flatpakPath);
                    TrackUninstalled(game);
                }
                catch (Exception ex) { await ShowMessageBoxAsync($"Could not uninstall {game.Name}: {ex.Message}", "Uninstall Failed"); }
                finally
                {
                    await game.CheckStatusAsync(_gameManager.HttpClient, _gameManager.GamesFolder, checkRemoteVersion: false);
                    game.IsLoading = false;
                    Changed();
                }
                return;
            }
            if (game.Status == GameStatus.NotInstalled)
            {
                _ = _session.RunAsync(() => ShowMessageBoxAsync($"{game.Name} is not installed.", "Nothing to Delete"));
                return;
            }

            if (PlatformCapabilities.IsMobile)
            {
                try
                {
                    await AndroidLibraryUninstall.RunAsync(game, game.GetInstallPath(_gameManager.GamesFolder), AppInstallLaunch.Current);
                    TrackUninstalled(game);
                }
                catch (Exception ex)
                {
                    await ShowMessageBoxAsync($"Could not uninstall {game.Name}: {ex.Message}", "Uninstall Failed");
                }
                finally { Changed(); }
                return;
            }

            if (!string.IsNullOrWhiteSpace(game.InstallPath))
            {
                await RemoveFromLocatedFolderAsync(game);
                return;
            }

            var confirmMessage = PlatformCapabilities.IsMobile ? $"Are you sure you want to uninstall {game.Name} from this device?" : $"Are you sure you want to delete {game.Name}?\n\n" + "Quiver Launcher will attempt to move game files to your system's Recycle Bin / Trash so you can restore them if needed. " + "Portable installs may include save data in the same folder.";
            var result = await ShowMessageBoxAsync(confirmMessage, "Confirm Deletion", isQuestion: true, preferCancelDefault: true);
            if (result && !_session.IsClosed)
            {
                try
                {
                    if (string.IsNullOrEmpty(game.FolderName))
                    {
                        await ShowMessageBoxAsync($"Failed to delete {game.Name}: game folder is not configured.", "Deletion Failed");
                        return;
                    }

                    game.Status = GameStatus.Installing;
                    game.IsLoading = true;
                    var gamePath = game.GetInstallPath(_gameManager.GamesFolder);
                    if (Directory.Exists(gamePath))
                    {
                        await Task.Run(() => RecycleBinHelper.MoveToRecycleBin(gamePath));
                    }
                    TrackUninstalled(game);

                    game.IsLoading = false;
                    await game.CheckStatusAsync(_gameManager.HttpClient, _gameManager.GamesFolder);
                    Changed();
                }
                catch (Exception ex)
                {
                    game.IsLoading = false;
                    _ = _session.RunAsync(() => ShowMessageBoxAsync($"Failed to delete {game.Name}: {ex.Message}", "Deletion Failed"));
                }
            }
        });
    }

    private async Task RemoveFromLocatedFolderAsync(GameInfo game)
    {
        var folder = game.InstallPath!;
        if (!await ShowMessageBoxAsync($"Delete {game.Name}?\n\nYou chose its folder, {folder}, so Quiver Launcher leaves the folder " +
            "and your other files in it alone. Only the file Quiver Launcher downloaded there, if any, moves to the Recycle Bin / Trash, " +
            "and Quiver Launcher stops using the folder.", "Confirm Deletion", isQuestion: true, preferCancelDefault: true) || _session.IsClosed)
            return;

        game.IsLoading = true;
        try
        {
            var folderHasOtherApps = LocatedInstallRemoval.FolderHasOtherApps(game, _gameManager.Games, _gameManager.GamesFolder);
            await Task.Run(() => LocatedInstallRemoval.Remove(folder, folderHasOtherApps, RecycleBinHelper.MoveToRecycleBin));
            game.InstallPath = null;
            await _libraryPersistence.SaveInstallLocationAsync(game);
            TrackUninstalled(game);
        }
        catch (Exception ex)
        {
            _ = _session.RunAsync(() => ShowMessageBoxAsync($"Failed to delete {game.Name}: {ex.Message}", "Deletion Failed"));
        }
        finally
        {
            game.IsLoading = false;
            await game.CheckStatusAsync(_gameManager.HttpClient, _gameManager.GamesFolder);
            Changed();
        }
    }

    private static void TrackUninstalled(GameInfo game) => Telemetry.Current.Track("app_uninstalled", Telemetry.AppRef(game));

    private Task<List<GameInfo>> LoadGamesFromJsonAsync() => _gameManager.CatalogService.LoadLocalAppsAsync();
    private async Task SaveGamesToJsonAsync(List<GameInfo> appsToSave)
    {
        foreach (var app in appsToSave)
            app.GameManager ??= _gameManager;
        await _gameManager.CatalogService.SaveLocalAppsAsync(appsToSave);
    }

    private async Task SaveGamesToJsonAsync(Dictionary<string, JsonElement> gamesData)
    {
        var imported = _gameManager.CatalogService.ParseAppsFromDictionary(gamesData);
        foreach (var app in imported)
            app.GameManager = _gameManager;
        await _gameManager.CatalogService.SaveLocalAppsAsync(imported);
    }

    private static GameInfo? FindMatchingSavedApp(IEnumerable<GameInfo> apps, GameInfo game) => apps.FirstOrDefault(g => string.Equals(g.InstanceKey, game.InstanceKey, StringComparison.OrdinalIgnoreCase));
    /// <summary>
    /// Removes the app from the library; its files stay. From the Library it asks first, so a card isn't removed by
    /// accident, but only for an installed app: one that isn't installed has nothing to keep and is one click to add back.
    /// From the App Catalog it doesn't ask (<paramref name="confirm"/> false): the page shows the change.
    /// </summary>
    public async Task RemoveEntryAsync(GameInfo? game, bool confirm = true)
    {
        if (RefuseKiosk()) return;
        await _session.RunAsync(async () =>
        {
            if (game == null || string.IsNullOrEmpty(game.Name))
            {
                _ = _session.RunAsync(() => ShowMessageBoxAsync("Unable to identify the selected app.", "Error"));
                return;
            }

            if (!game.IsInLocalAppsJson)
            {
                _ = _session.RunAsync(() => ShowMessageBoxAsync("This app is not in your local list.", "Error"));
                return;
            }

            try
            {
                var installed = game.IsInstalled || Directory.Exists(game.GetInstallPath(_gameManager.GamesFolder));
                if (confirm && installed && !await ShowMessageBoxAsync($"Remove '{game.Name}' from your Library?\n\nYour files will not be deleted.", "Remove from Library", true))
                    return;
                if (_session.IsClosed)
                    return;
                var games = await LoadGamesFromJsonAsync();
                var gameToRemove = FindMatchingSavedApp(games, game);
                if (gameToRemove == null)
                {
                    await ShowMessageBoxAsync($"Could not find '{game.Name}' in the saved apps list.", "Error");
                    return;
                }

                games.Remove(gameToRemove);
                await SaveGamesToJsonAsync(games);
                if (AppCollections.RemoveFromAll(_settings, game.InstanceKey))
                    _settingsModel.SaveCurrent();
                Telemetry.Current.Track("app_removed", Telemetry.AppRef(game));
                // Drop only Quiver's link; Windows continues to own the installed application.
                var metadataPath = game.GetInstallPath(_gameManager.GamesFolder);
                if (WindowsInstallerService.HasReceipt(metadataPath))
                    File.Delete(Path.Combine(metadataPath, WindowsInstallerService.ReceiptFileName));
                await _gameManager.LoadGamesAsync();
                if (_session.IsClosed)
                    return;
                Changed();
                _settingsModel.SaveCurrent();
                // No "removed" message: the card going away says it.
                await _libraryChanged();
            }
            catch (Exception ex)
            {
                _ = _session.RunAsync(() => ShowMessageBoxAsync($"Error removing app: {ex.Message}", "Error"));
            }
        });
    }

    public async Task CreateShortcutAsync(GameInfo? game)
    {
        if (RefuseKiosk()) return;
        await _session.RunAsync(async () =>
        {
            if (game == null)
            {
                await ShowMessageBoxAsync("Unable to identify the selected app.", "Error");
                return;
            }

            try
            {
                var target = await PrepareShortcutAsync(game);
                if (target == null || _session.IsClosed) return;
                await ShortcutHelper.CreateGameShortcutAsync(game, target, _gameManager.CacheFolder);
            }
            catch (Exception ex)
            {
                await ShowMessageBoxAsync($"Failed to create shortcut: {ex.Message}", "Error");
            }
        });
    }

    public async Task AddToSteamAsync(GameInfo? game)
    {
        if (RefuseKiosk()) return;
        await _session.RunAsync(async () =>
        {
            if (game == null)
            {
                await ShowMessageBoxAsync("Unable to identify the selected app.", "Error");
                return;
            }

            try
            {
                var target = await PrepareShortcutAsync(game);
                if (target == null || _session.IsClosed) return;
                string resultMessage = ShortcutHelper.IsSteamRunning()
                    ? ShortcutHelper.QueueGameAddToSteam(game, ShortcutHelper.ResolveLauncherPath() ?? "")
                    : await Task.Run(() => ShortcutHelper.AddGameToSteam(game, target, _gameManager.CacheFolder), _session.Token);
                await ShowMessageBoxAsync(resultMessage, "Steam Shortcut");
            }
            catch (Exception ex)
            {
                await ShowMessageBoxAsync($"Failed to add the game to Steam: {ex.Message}", "Error");
            }
        });
    }

    private Task<GameShortcutTarget?> PrepareShortcutAsync(GameInfo game) =>
        GameShortcutLaunch.PrepareAsync(game, _gameManager.GamesFolder, _settings, async candidates =>
        {
            var dialog = new QuiverLauncher.Views.ShortcutExecutableDialog(game.Name ?? "this app", candidates);
            await GameDialogService.ShowWindowAsync(dialog, _session.Token);
            return dialog.SelectedExecutable;
        }, _session.Token);
}
