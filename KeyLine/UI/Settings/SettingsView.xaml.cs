using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KeyLine.Domain;

namespace KeyLine;

public partial class SettingsView : UserControl
{
    private const string AboutCategory = SettingsCategoryRenderer.AboutCategory;

    private readonly ISettingsActions _actions;
    private readonly SettingsCategoryRenderer _renderer;
    private readonly SettingsShortcutCapture _shortcutCapture;
    private TextBlock? _aboutCategoryWarningText;

    public SettingsView(AppSettings settings, ISettingsActions actions, string? initialCategory = null)
    {
        _actions = actions;

        InitializeComponent();

        _shortcutCapture = new SettingsShortcutCapture(
            actions,
            RenderCurrentCategory,
            NotifyChanged,
            () => Focus());

        _renderer = new SettingsCategoryRenderer(
            settings,
            actions,
            SettingsPanel,
            _shortcutCapture.Begin,
            () => CloseRequested?.Invoke());

        foreach (var category in _renderer.Categories)
            CategoryListBox.Items.Add(CreateCategoryItem(category));

        _actions.UpdateCheckCompleted += Actions_UpdateCheckCompleted;
        Unloaded += (_, _) => _actions.UpdateCheckCompleted -= Actions_UpdateCheckCompleted;

        var selectedCategory = _renderer.HasCategory(initialCategory)
            ? initialCategory!
            : _renderer.Categories.First();

        SelectCategory(selectedCategory);
        UpdateAboutCategoryBadge();
        Focusable = true;
    }

    public Action? CloseRequested { get; init; }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();

    private void CategoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GetSelectedCategory() is { } category)
            _renderer.Render(category);
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (_shortcutCapture.HandlePreviewKeyDown(e))
            return;

        base.OnPreviewKeyDown(e);
    }

    protected override void OnPreviewKeyUp(System.Windows.Input.KeyEventArgs e)
    {
        if (_shortcutCapture.HandlePreviewKeyUp(e))
            return;

        base.OnPreviewKeyUp(e);
    }

    private void Actions_UpdateCheckCompleted(object? sender, UpdateCheckResult result)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => Actions_UpdateCheckCompleted(sender, result)));
            return;
        }

        UpdateAboutCategoryBadge();
        _renderer.UpdateUpdateCheckControls();
    }

    private ListBoxItem CreateCategoryItem(string category)
    {
        if (category != AboutCategory)
            return new ListBoxItem { Tag = category, Content = category };

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new TextBlock
        {
            Text = AboutCategory,
            Foreground = SettingsSectionBuilder.DimBrush
        });

        _aboutCategoryWarningText = new TextBlock
        {
            Text = "!",
            FontWeight = FontWeights.Black,
            Foreground = new SolidColorBrush(Color.FromRgb(253, 230, 138)),
            Margin = new Thickness(6, 0, 0, 0),
            Visibility = _renderer.IsUpdateAvailable ? Visibility.Visible : Visibility.Collapsed
        };
        panel.Children.Add(_aboutCategoryWarningText);

        return new ListBoxItem
        {
            Tag = category,
            Content = panel
        };
    }

    private void SelectCategory(string category)
    {
        foreach (var item in CategoryListBox.Items.OfType<ListBoxItem>())
        {
            if (item.Tag as string != category)
                continue;

            CategoryListBox.SelectedItem = item;
            return;
        }
    }

    private string? GetSelectedCategory()
    {
        return CategoryListBox.SelectedItem is ListBoxItem { Tag: string category }
            ? category
            : CategoryListBox.SelectedItem as string;
    }

    private void UpdateAboutCategoryBadge()
    {
        if (_aboutCategoryWarningText == null)
            return;

        _aboutCategoryWarningText.Visibility = _renderer.IsUpdateAvailable
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void RenderCurrentCategory()
    {
        if (GetSelectedCategory() is { } category)
            _renderer.Render(category);
    }

    private void NotifyChanged()
    {
        _actions.ApplySettings();
    }
}
