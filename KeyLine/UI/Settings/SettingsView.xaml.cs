using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using KeyLine.Domain;
using KeyLine.Services.Features;
using KeyLine.Services.Input;
using KeyLine.UI;

namespace KeyLine;

public partial class SettingsView : UserControl
{
    private const string AboutCategory = "About";

    private readonly AppSettings _settings;
    private readonly ISettingsActions _actions;
    private readonly Dictionary<string, Action> _renderers;
    private Button? _capturingShortcutButton;
    private Button? _updateCheckButton;
    private TextBlock? _updateStatusText;
    private TextBlock? _aboutCategoryWarningText;
    private Action<string>? _commitShortcut;
    private readonly List<int> _capturedShortcutKeys = new();
    private bool _isCheckingForUpdates;

    public SettingsView(AppSettings settings, ISettingsActions actions, string? initialCategory = null)
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
            [AboutCategory] = RenderAbout
        };

        InitializeComponent();

        foreach (var category in _renderers.Keys)
            CategoryListBox.Items.Add(CreateCategoryItem(category));

        _actions.UpdateCheckCompleted += Actions_UpdateCheckCompleted;
        Unloaded += (_, _) => _actions.UpdateCheckCompleted -= Actions_UpdateCheckCompleted;

        var selectedCategory = _renderers.ContainsKey(initialCategory ?? "")
            ? initialCategory!
            : _renderers.Keys.First();
        SelectCategory(selectedCategory);
        UpdateAboutCategoryBadge();
        Focusable = true;
    }

    public Action? CloseRequested { get; init; }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();

    private void Actions_UpdateCheckCompleted(object? sender, UpdateCheckResult result)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => Actions_UpdateCheckCompleted(sender, result)));
            return;
        }

        UpdateAboutCategoryBadge();
        UpdateUpdateCheckControls();
    }

    private ListBoxItem CreateCategoryItem(string category)
    {
        if (category != AboutCategory)
            return new ListBoxItem { Tag = category, Content = category };

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new TextBlock
        {
            Text = AboutCategory,
            Foreground = DimBrush
        });

        _aboutCategoryWarningText = new TextBlock
        {
            Text = "!",
            FontWeight = FontWeights.Black,
            Foreground = new SolidColorBrush(Color.FromRgb(253, 230, 138)),
            Margin = new Thickness(6, 0, 0, 0),
            Visibility = IsUpdateAvailable() ? Visibility.Visible : Visibility.Collapsed
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

    private bool IsUpdateAvailable()
    {
        return _actions.LastUpdateCheckResult?.State == UpdateCheckState.UpdateAvailable;
    }

    private void UpdateAboutCategoryBadge()
    {
        if (_aboutCategoryWarningText == null)
            return;

        _aboutCategoryWarningText.Visibility = IsUpdateAvailable()
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void CategoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GetSelectedCategory() is { } category &&
            _renderers.TryGetValue(category, out var renderer))
        {
            renderer();
        }
    }

    private void RenderCurrentCategory()
    {
        if (GetSelectedCategory() is { } category &&
            _renderers.TryGetValue(category, out var renderer))
        {
            renderer();
        }
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
        AddChoice("Default loop mode", FormatLoopMode(_settings.DefaultLoopMode), new[] { "Async", "Sync", "Cycle", "Chain" }, value =>
        {
            _settings.DefaultLoopMode = ParseLoopMode(value);
            NotifyChanged();
        });
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
        AddShortcut("Select all nodes", _settings.SelectAllShortcut, value => _settings.SelectAllShortcut = value);
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
        AddShortcut("Pause/resume all macros hotkey", _settings.PauseResumeAllMacrosShortcut, value => _settings.PauseResumeAllMacrosShortcut = value);
    }

    private void RenderImportExport()
    {
        BeginSection("Import/Export");
        AddAction("Import", _actions.Import);
        AddAction("Export Macro", _actions.ExportMacro);
        AddFeatureAction("Export Profile", FeatureId.Profiles, _actions.ExportProfile);
        AddAction("Export EVERYTHING", _actions.ExportEverything);
        AddAction("Open Backups", _actions.OpenBackups);

        if (!string.IsNullOrWhiteSpace(_actions.ImportExportNotice))
            AddNotice(_actions.ImportExportNotice);
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
        BeginSection(IsUpdateAvailable() ? "About  !" : "About");
        AddDescription("KeyLine");
        AddDescription($"Version: {_actions.CurrentVersionText}");
        AddLicenseLink();
        AddDescription("Copyright © 2026 Iompaeqe(Iompa). All rights reserved.");
        AddDescription("A compact window-targeted macro recorder with keyboard, mouse, timeline, and shortcut support.");

        AddUpdateCheckButton();
        BeginUpdateCheck(forceRefresh: true);
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

        checkBox.ToolTip = TooltipNotes.StandardDelayRequiresEnable;
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

    private static string FormatLoopMode(MacroLoopMode loopMode)
    {
        return loopMode switch
        {
            MacroLoopMode.Sync => "sync",
            MacroLoopMode.Cycle => "cycle",
            MacroLoopMode.Chain => "chain",
            _ => "async"
        };
    }

    private static MacroLoopMode ParseLoopMode(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "sync" or "synced" => MacroLoopMode.Sync,
            "cycle" or "sequence" => MacroLoopMode.Cycle,
            "chain" => MacroLoopMode.Chain,
            _ => MacroLoopMode.Async
        };
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
            _actions.SetShortcutCaptureActive(true);
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

    private void AddFeatureAction(string label, FeatureId feature, Action action)
    {
        if (!_actions.IsFeatureVisible(feature))
            return;

        var isLocked = _actions.IsFeatureLocked(feature);
        var button = new Button
        {
            Content = isLocked ? $"{label} (locked)" : label,
            Height = 28,
            HorizontalAlignment = HorizontalAlignment.Left,
            MinWidth = 180,
            Margin = new Thickness(0, 0, 0, 8),
            Opacity = isLocked ? 0.55 : 1.0,
            ToolTip = isLocked ? _actions.GetLockedFeatureMessage(feature) : null
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

    private void AddNotice(string text)
    {
        SettingsPanel.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(253, 230, 138)),
            Margin = new Thickness(0, 2, 0, 8)
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
    
    private void AddLicenseLink()
    {
        var textBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = DimBrush,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var link = new Hyperlink(new Run("License: Proprietary Freeware"))
        {
            Foreground = new SolidColorBrush(Color.FromRgb(147, 197, 253))
        };

        link.Click += (_, _) => ShowLicenseWindow();

        textBlock.Inlines.Add(link);
        SettingsPanel.Children.Add(textBlock);
    }
    
    private void ShowLicenseWindow()
    {
        var window = new Window
        {
            Content = new LicenseView(),
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            ResizeMode = ResizeMode.NoResize,
            Background = Brushes.Transparent,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            ShowInTaskbar = false
        };

        window.ShowDialog();
    }
    
    private void AddUpdateCheckButton()
    {
        _updateStatusText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(253, 230, 138)),
            Margin = new Thickness(0, 2, 0, 8),
            Visibility = Visibility.Collapsed
        };
        SettingsPanel.Children.Add(_updateStatusText);

        var button = new Button
        {
            Content = "Check for updates",
            Height = 28,
            HorizontalAlignment = HorizontalAlignment.Left,
            MinWidth = 190,
            Margin = new Thickness(0, 0, 0, 8)
        };

        button.Click += (_, _) =>
        {
            var result = _actions.LastUpdateCheckResult;
            if (result?.State == UpdateCheckState.UpdateAvailable)
            {
                _actions.OpenReleasesPage(result.ReleaseUrl);
                return;
            }

            BeginUpdateCheck(forceRefresh: true);
        };

        _updateCheckButton = button;
        SettingsPanel.Children.Add(button);
        UpdateUpdateCheckControls();
    }

    private async void BeginUpdateCheck(bool forceRefresh)
    {
        if (_isCheckingForUpdates)
            return;

        _isCheckingForUpdates = true;
        UpdateUpdateCheckControls();

        try
        {
            await _actions.CheckForUpdatesAsync(forceRefresh);
        }
        finally
        {
            _isCheckingForUpdates = false;
            UpdateUpdateCheckControls();
        }
    }

    private void UpdateUpdateCheckControls()
    {
        if (_updateCheckButton == null || _updateStatusText == null)
            return;

        if (_isCheckingForUpdates)
        {
            _updateCheckButton.Content = "Checking...";
            _updateCheckButton.IsEnabled = false;
            _updateStatusText.Text = "Checking GitHub releases...";
            _updateStatusText.Foreground = new SolidColorBrush(Color.FromRgb(147, 197, 253));
            _updateStatusText.Visibility = Visibility.Visible;
            return;
        }

        var result = _actions.LastUpdateCheckResult;
        if (result == null)
        {
            _updateCheckButton.Content = "Check for updates";
            _updateCheckButton.IsEnabled = true;
            _updateStatusText.Visibility = Visibility.Collapsed;
            return;
        }

        _updateCheckButton.Content = result.ButtonText;
        _updateCheckButton.IsEnabled = true;

        switch (result.State)
        {
            case UpdateCheckState.UpdateAvailable:
                _updateStatusText.Text = "! New version available. Click the button to open the release page and install it.";
                _updateStatusText.Foreground = new SolidColorBrush(Color.FromRgb(253, 230, 138));
                _updateStatusText.Visibility = Visibility.Visible;
                break;

            case UpdateCheckState.Latest:
                _updateStatusText.Text = "No update available.";
                _updateStatusText.Foreground = DimBrush;
                _updateStatusText.Visibility = Visibility.Visible;
                break;

            case UpdateCheckState.Failed:
                _updateStatusText.Text = "Could not check for updates. Try again.";
                _updateStatusText.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
                _updateStatusText.Visibility = Visibility.Visible;
                break;
        }
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

