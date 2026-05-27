using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using MacroSpammer.Domain;
using MacroSpammer.Services.Input;

namespace MacroSpammer;

public partial class SettingsView : UserControl
{
    private readonly AppSettings _settings;
    private readonly ISettingsActions _actions;
    private readonly Dictionary<string, Action> _renderers;
    private Button? _capturingShortcutButton;
    private Action<string>? _commitShortcut;
    private readonly List<int> _capturedShortcutKeys = new();

    public SettingsView(AppSettings settings, ISettingsActions actions)
    {
        _settings = settings;
        _actions = actions;
        _renderers = new Dictionary<string, Action>
        {
            ["General"] = RenderGeneral,
            ["Defaults"] = RenderDefaults,
            ["Shortcuts"] = RenderShortcuts,
            ["Recording"] = RenderRecording,
            ["Playback"] = RenderPlayback,
            ["Import/Export"] = RenderImportExport,
            ["Reset"] = RenderReset,
            ["Experimental"] = RenderExperimental,
            ["About"] = RenderAbout
        };

        InitializeComponent();

        foreach (var category in _renderers.Keys)
            CategoryListBox.Items.Add(category);

        CategoryListBox.SelectedIndex = 0;
        Focusable = true;
    }

    public Action? CloseRequested { get; init; }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();

    private void CategoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoryListBox.SelectedItem is string category && _renderers.TryGetValue(category, out var renderer))
            renderer();
    }

    private void RenderCurrentCategory()
    {
        if (CategoryListBox.SelectedItem is string category && _renderers.TryGetValue(category, out var renderer))
            renderer();
    }

    private void RenderGeneral()
    {
        BeginSection("General");
        AddCheck("Launch on Windows startup", _settings.LaunchOnWindowsStartup, value =>
        {
            _settings.LaunchOnWindowsStartup = value;
            _actions.SetLaunchOnStartup(value);
            NotifyChanged();
        });
        AddCheck("Start minimized", _settings.StartMinimized, value => Set(value, v => _settings.StartMinimized = v));
        AddCheck("Minimize to tray", _settings.MinimizeToTray, value => Set(value, v => _settings.MinimizeToTray = v));
        AddCheck("Close to tray", _settings.CloseToTray, value => Set(value, v => _settings.CloseToTray = v));
        AddCheck("Confirm before closing while macros are running", _settings.ConfirmCloseWhileMacrosRunning, value => Set(value, v => _settings.ConfirmCloseWhileMacrosRunning = v));
    }

    private void RenderDefaults()
    {
        BeginSection("Defaults");
        if (!_settings.DefaultStandardDelayEnabled && !_settings.DefaultShowKeyUpDown)
        {
            _settings.DefaultShowKeyUpDown = true;
            NotifyChanged();
        }

        AddCheck("Default standard delay enabled", _settings.DefaultStandardDelayEnabled, value =>
        {
            _settings.DefaultStandardDelayEnabled = value;
            if (!value)
                _settings.DefaultShowKeyUpDown = true;

            NotifyChanged();
            RenderDefaults();
        });
        AddCheck("Default show key up/down", !_settings.DefaultStandardDelayEnabled || _settings.DefaultShowKeyUpDown, value =>
        {
            _settings.DefaultShowKeyUpDown = value;
            NotifyChanged();
            RenderDefaults();
        }, _settings.DefaultStandardDelayEnabled);
        AddNumber("Default standard delay value", _settings.DefaultStandardDelayMs, value => Set(value, v => _settings.DefaultStandardDelayMs = v), "ms");
        AddNumber("Default base delay", _settings.DefaultBaseDelayMs, value => Set(value, v => _settings.DefaultBaseDelayMs = v), "ms");
        AddNumber("Default timer value", _settings.DefaultTimerMs, value => Set(value, v => _settings.DefaultTimerMs = v), "ms");
        AddNumber("Default loop count", _settings.DefaultLoopCount, value => Set(value, v => _settings.DefaultLoopCount = v), "");
        AddChoice("Default input mode", _settings.DefaultTextInputMode ? "Text" : "Key", new[] { "Key", "Text" }, value =>
        {
            _settings.DefaultTextInputMode = value == "Text";
            NotifyChanged();
        });
    }

    private void RenderRecording()
    {
        BeginSection("Recording");
        AddCheck("Merge repeated delay nodes", _settings.MergeRepeatedDelayNodes, value => Set(value, v => _settings.MergeRepeatedDelayNodes = v));
    }

    private void RenderShortcuts()
    {
        BeginSection("Shortcuts");
        AddShortcut("Undo", _settings.UndoShortcut, value => _settings.UndoShortcut = value);
        AddShortcut("Redo", _settings.RedoShortcut, value => _settings.RedoShortcut = value);
        AddShortcut("Copy selected nodes/timeline/macro", _settings.CopyShortcut, value => _settings.CopyShortcut = value);
        AddShortcut("Paste nodes/timeline/macro", _settings.PasteShortcut, value => _settings.PasteShortcut = value);
        AddShortcut("Duplicate selection", _settings.DuplicateShortcut, value => _settings.DuplicateShortcut = value);
    }

    private void RenderPlayback()
    {
        BeginSection("Playback");
        AddCheck("Play sound when macro starts/stops", _settings.PlaySoundOnMacroStartStop, value => Set(value, v => _settings.PlaySoundOnMacroStartStop = v));
        AddChoice("Sound", _settings.PlaybackSoundName, new[] { "Beep", "Asterisk", "Exclamation", "Hand", "Question" }, value =>
        {
            _settings.PlaybackSoundName = value;
            _actions.PreviewPlaybackSound();
            NotifyChanged();
        });
        AddShortcut("Emergency stop shortcut", _settings.EmergencyStopShortcut, value => _settings.EmergencyStopShortcut = value);
        AddShortcut("Stop all macros hotkey", _settings.StopAllMacrosShortcut, value => _settings.StopAllMacrosShortcut = value);
        AddShortcut("Pause/resume all macros hotkey", _settings.PauseResumeAllMacrosShortcut, value => _settings.PauseResumeAllMacrosShortcut = value);
    }

    private void RenderImportExport()
    {
        BeginSection("Import/Export");
        AddAction("Import macro file", _actions.ImportMacroFile);
        AddAction("Import multiple macro files", _actions.ImportMultipleMacroFiles);
        AddAction("Export selected macros", _actions.ExportSelectedMacro);
        AddAction("Export all macros", _actions.ExportAllMacros);
        AddAction("Open macro storage folder", _actions.OpenMacroStorageFolder);
    }

    private void RenderReset()
    {
        BeginSection("Reset");
        AddAction("Reset defaults", () => { _actions.ResetDefaults(); NotifyChanged(); });
        AddAction("Reset settings", () => { _actions.ResetSettings(); NotifyChanged(); CloseRequested?.Invoke(); });
        AddAction("Reset all saved data", () => { _actions.ResetAllSavedData(); NotifyChanged(); CloseRequested?.Invoke(); });
    }

    private void RenderExperimental()
    {
        BeginSection("Experimental");
        AddCheck("Enable experimental nodes", _settings.ExperimentalFeaturesEnabled, value => Set(value, v => _settings.ExperimentalFeaturesEnabled = v));
        AddDescription("Shows background mouse down/up/click options in the add menu. These nodes are experimental because many games ignore or reinterpret background mouse messages.");
    }

    private void RenderAbout()
    {
        BeginSection("About");
        AddDescription("KeyLine");
        AddDescription("License: MIT");
        AddDescription("A compact window-targeted macro recorder with keyboard, mouse, timeline, and shortcut support.");
    }

    private void BeginSection(string title)
    {
        SettingsPanel.Children.Clear();
        SettingsPanel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 10)
        });
    }

    private void AddCheck(string text, bool value, Action<bool> changed, bool isEnabled = true)
    {
        var checkBox = new CheckBox
        {
            Content = text,
            IsChecked = value,
            Margin = new Thickness(0, 0, 0, 9),
            Foreground = isEnabled ? Brushes.White : DimBrush
        };
        ApplySettingsCheckBoxStyle(checkBox, isEnabled);
        checkBox.Checked += (_, _) => changed(true);
        checkBox.Unchecked += (_, _) => changed(false);
        SettingsPanel.Children.Add(checkBox);
    }

    private static void ApplySettingsCheckBoxStyle(CheckBox checkBox, bool isEnabled)
    {
        checkBox.IsHitTestVisible = isEnabled;
        checkBox.Focusable = isEnabled;
        checkBox.Opacity = isEnabled ? 1.0 : 0.42;

        if (isEnabled)
            return;

        checkBox.ToolTip = "Enable standard delay to edit this setting.";
        checkBox.Template = CreateDisabledCheckBoxTemplate();
    }

    private static ControlTemplate CreateDisabledCheckBoxTemplate()
    {
        var template = new ControlTemplate(typeof(CheckBox));

        var panel = new FrameworkElementFactory(typeof(StackPanel));
        panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

        var box = new FrameworkElementFactory(typeof(Border));
        box.SetValue(FrameworkElement.WidthProperty, 14.0);
        box.SetValue(FrameworkElement.HeightProperty, 14.0);
        box.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 7, 0));
        box.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
        box.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(15, 23, 42)));
        box.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(51, 65, 85)));
        box.SetValue(Border.BorderThicknessProperty, new Thickness(1));

        var mark = new FrameworkElementFactory(typeof(TextBlock));
        mark.SetValue(TextBlock.TextProperty, "✓");
        mark.SetValue(TextBlock.FontSizeProperty, 10.0);
        mark.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
        mark.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(71, 85, 105)));
        mark.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        mark.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        box.AppendChild(mark);

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        content.SetValue(TextElement.ForegroundProperty, new SolidColorBrush(Color.FromRgb(100, 116, 139)));

        panel.AppendChild(box);
        panel.AppendChild(content);
        template.VisualTree = panel;
        return template;
    }

    private void AddNumber(string label, int value, Action<int> changed, string unit)
    {
        var panel = AddRow(label);
        var box = new TextBox { Text = value.ToString(), Width = 86, Height = 24, Margin = new Thickness(0, 0, 6, 0) };
        box.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
        box.LostFocus += (_, _) =>
        {
            if (int.TryParse(box.Text, out var parsed))
                changed(Math.Max(0, parsed));
        };
        panel.Children.Add(box);
        if (!string.IsNullOrWhiteSpace(unit))
            panel.Children.Add(new TextBlock { Text = unit, VerticalAlignment = VerticalAlignment.Center, Foreground = DimBrush });
    }

    private void AddChoice(string label, string value, IEnumerable<string> options, Action<string> changed)
    {
        var panel = AddRow(label);
        var comboBox = new ComboBox { Width = 112, Height = 24, ItemsSource = options.ToArray(), SelectedItem = value };
        comboBox.SelectionChanged += (_, _) =>
        {
            if (comboBox.SelectedItem is string selected)
                changed(selected);
        };
        panel.Children.Add(comboBox);
    }

    private void AddShortcut(string label, string shortcut, Action<string> changed)
    {
        var panel = AddRow(label);
        var button = new Button { Content = ShortcutGesture.Format(shortcut), Width = 150, Height = 24 };
        button.Click += (_, _) =>
        {
            _capturingShortcutButton = button;
            _commitShortcut = changed;
            _capturedShortcutKeys.Clear();
            button.Content = "press shortcut";
            Focus();
        };
        panel.Children.Add(button);
    }

    private void AddAction(string label, Action action)
    {
        var button = new Button
        {
            Content = label,
            Height = 28,
            HorizontalAlignment = HorizontalAlignment.Left,
            MinWidth = 180,
            Margin = new Thickness(0, 0, 0, 8)
        };
        button.Click += (_, _) => action();
        SettingsPanel.Children.Add(button);
    }

    private void AddDescription(string text)
    {
        SettingsPanel.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = DimBrush,
            Margin = new Thickness(0, 0, 0, 8)
        });
    }

    private StackPanel AddRow(string label)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 9) };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            Width = 220,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.White
        });
        SettingsPanel.Children.Add(panel);
        return panel;
    }

    private void Set(bool value, Action<bool> setter)
    {
        setter(value);
        NotifyChanged();
    }

    private void Set(int value, Action<int> setter)
    {
        setter(value);
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        _actions.ApplySettings();
    }

    private static Brush DimBrush => new SolidColorBrush(Color.FromRgb(142, 160, 182));
}
