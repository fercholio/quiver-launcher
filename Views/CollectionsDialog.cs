using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using QuiverLauncher.Services;

namespace QuiverLauncher.Views;

/// <summary>
/// Chooses which collections an app belongs to: a checkbox per existing collection, plus a field to
/// create a new collection and add the app to it. Code-only, same shape as the other library dialogs.
/// </summary>
internal sealed class CollectionsDialog : Window
{
    internal bool Saved { get; private set; }
    /// <summary>Existing collection id → whether the app should be a member.</summary>
    internal IReadOnlyDictionary<string, bool> Membership => _membership;
    /// <summary>Names typed in to create new collections the app is added to.</summary>
    internal IReadOnlyList<string> NewCollectionNames { get; private set; } = [];

    private readonly Dictionary<string, bool> _membership = new(StringComparer.Ordinal);
    private readonly TextBox _newName;

    internal CollectionsDialog(string gameName, IReadOnlyList<AppCollection> collections, Func<AppCollection, bool> isMember)
    {
        Title = "Collections";
        Width = 460;
        Height = 440;
        MinWidth = 320;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetValue(DesktopInterfaceScaling.ManagesScrollingProperty, true);

        var checks = new StackPanel { Spacing = 4 };
        if (collections.Count == 0)
            checks.Children.Add(new TextBlock { Text = "No collections yet. Create one below.", Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap });
        foreach (var collection in collections)
        {
            var member = isMember(collection);
            _membership[collection.Id] = member;
            var box = new CheckBox { Content = collection.Name, IsChecked = member };
            var id = collection.Id;
            box.IsCheckedChanged += (_, _) => _membership[id] = box.IsChecked == true;
            checks.Children.Add(box);
        }

        _newName = new TextBox { Watermark = "New collection name", Margin = new Thickness(0, 6, 0, 0) };

        var save = new Button { Content = "Save", IsDefault = true, Padding = new Thickness(14, 8), Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(14, 8) };
        save.Click += (_, _) =>
        {
            var typed = _newName.Text?.Trim();
            NewCollectionNames = string.IsNullOrWhiteSpace(typed) ? [] : [typed];
            Saved = true;
            Close();
        };
        cancel.Click += (_, _) => Close();

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = $"Collections for {gameName}:", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(checks);
        panel.Children.Add(new TextBlock { Text = "Create a new collection", FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 8, 0, 0) });
        panel.Children.Add(_newName);

        var buttons = new WrapPanel { Children = { save, cancel } };
        var layout = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), RowSpacing = 12, Margin = new Thickness(20) };
        layout.Children.Add(new ScrollViewer { Content = panel });
        Grid.SetRow(buttons, 1);
        layout.Children.Add(buttons);
        Content = layout;
        GamepadModalDialogNavigation.Attach(this);
    }
}
