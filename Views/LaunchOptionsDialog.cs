using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using QuiverLauncher.Core.Services;
using QuiverLauncher.Services;

namespace QuiverLauncher.Views;

/// <summary>
/// Edits a game's per-app launch options: extra command-line arguments, a working-directory override,
/// and extra environment variables (one <c>KEY=VALUE</c> per line). Code-only dialog, same shape as
/// <see cref="ShortcutExecutableDialog"/>. <see cref="Saved"/> is true only when the player confirms.
/// </summary>
internal sealed class LaunchOptionsDialog : Window
{
    internal bool Saved { get; private set; }
    internal string Arguments => _arguments.Text?.Trim() ?? string.Empty;
    internal string WorkingDirectory => _workingDirectory.Text?.Trim() ?? string.Empty;
    internal string EnvironmentText => _environment.Text ?? string.Empty;

    private readonly TextBox _arguments;
    private readonly TextBox _workingDirectory;
    private readonly TextBox _environment;

    internal LaunchOptionsDialog(
        string gameName,
        string? arguments,
        string? workingDirectory,
        IReadOnlyDictionary<string, string>? environment)
    {
        Title = "Launch options";
        Width = 560;
        Height = 500;
        MinWidth = 340;
        MinHeight = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetValue(DesktopInterfaceScaling.ManagesScrollingProperty, true);

        _arguments = new TextBox
        {
            Text = arguments ?? string.Empty,
            Watermark = "--fullscreen --mods \"C:\\path with spaces\\mods\"",
        };
        _workingDirectory = new TextBox
        {
            Text = workingDirectory ?? string.Empty,
            Watermark = "(defaults to the executable's folder)",
        };
        _environment = new TextBox
        {
            Text = LaunchOptions.FormatEnvironment(environment),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            Watermark = "VK_ICD_FILENAMES=...\nHD_TEXTURES=1",
            MinHeight = 110,
            VerticalContentAlignment = VerticalAlignment.Top,
        };

        var browse = new Button { Content = "Browse…", Padding = new Thickness(12, 6) };
        browse.Click += async (_, _) =>
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose working directory",
                AllowMultiple = false,
            });
            if (folders.Count > 0)
                _workingDirectory.Text = folders[0].Path.LocalPath;
        };

        var save = new Button { Content = "Save", IsDefault = true, Padding = new Thickness(14, 8), Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(14, 8) };
        save.Click += (_, _) =>
        {
            // Reject malformed arguments up front so the player fixes them here instead of at launch.
            try { LaunchOptions.SplitArguments(_arguments.Text); }
            catch (FormatException ex)
            {
                _ = GameDialogService.ShowMessageBoxAsync(ex.Message, "Launch Options");
                return;
            }
            Saved = true;
            Close();
        };
        cancel.Click += (_, _) => Close();

        var workingRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        workingRow.Children.Add(_workingDirectory);
        Grid.SetColumn(browse, 1);
        workingRow.Children.Add(browse);

        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(new TextBlock
        {
            Text = $"Launch options for {gameName}. These are saved to your library and reused every time you play.",
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(SectionLabel("Command-line arguments"));
        panel.Children.Add(_arguments);
        panel.Children.Add(SectionLabel("Working directory"));
        panel.Children.Add(workingRow);
        panel.Children.Add(SectionLabel("Environment variables (one KEY=VALUE per line)"));
        panel.Children.Add(_environment);

        var scroller = new ScrollViewer { Content = panel };

        var buttons = new WrapPanel { Children = { save, cancel } };
        var layout = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), RowSpacing = 12, Margin = new Thickness(20) };
        layout.Children.Add(scroller);
        Grid.SetRow(buttons, 1);
        layout.Children.Add(buttons);
        Content = layout;
        GamepadModalDialogNavigation.Attach(this);
    }

    private static TextBlock SectionLabel(string text) => new()
    {
        Text = text,
        FontWeight = FontWeight.SemiBold,
        Margin = new Thickness(0, 6, 0, 0),
    };
}
