using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KeyLine.Domain;
using KeyLine.Services.Features;

namespace KeyLine;

public sealed class SettingsCategoryRenderer
{
    public const string AboutCategory = "About";

    private readonly AppSettings _settings;
    private readonly ISettingsActions _actions;
    private readonly Action _closeRequested;
    private readonly SettingsSectionBuilder _ui;
    private readonly Dictionary<string, Action> _renderers;

    private Button? _updateCheckButton;
    private TextBlock? _updateStatusText;
    private bool _isCheckingForUpdates;

    public SettingsCategoryRenderer(
        AppSettings settings,
        ISettingsActions actions,
        StackPanel settingsPanel,
        Action<Button, Action<string>> beginShortcutCapture,
        Action closeRequested)
    {
        _settings = settings;
        _actions = actions;
        _closeRequested = closeRequested;
        _ui = new SettingsSectionBuilder(settingsPanel, actions, beginShortcutCapture);

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
    }

    public IEnumerable<string> Categories => _renderers.Keys;

    public bool IsUpdateAvailable =>
        _actions.LastUpdateCheckResult?.State == UpdateCheckState.UpdateAvailable;

    public bool HasCategory(string? category)
    {
        return !string.IsNullOrWhiteSpace(category) && _renderers.ContainsKey(category);
    }

    public void Render(string category)
    {
        if (_renderers.TryGetValue(category, out var renderer))
            renderer();
    }

    public void UpdateUpdateCheckControls()
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
                _updateStatusText.Foreground = SettingsSectionBuilder.DimBrush;
                _updateStatusText.Visibility = Visibility.Visible;
                break;

            case UpdateCheckState.Failed:
                _updateStatusText.Text = "Could not check for updates. Try again.";
                _updateStatusText.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
                _updateStatusText.Visibility = Visibility.Visible;
                break;
        }
    }

    private void RenderGeneral()
    {
        _ui.BeginSection("General");
        _ui.AddCheck("Launch on Windows startup", _settings.LaunchOnWindowsStartup, value =>
        {
            _settings.LaunchOnWindowsStartup = value;
            _actions.SetLaunchOnStartup(value);
            NotifyChanged();
        });
        _ui.AddCheck("Start minimized", _settings.StartMinimized, value => Set(value, v => _settings.StartMinimized = v));
        _ui.AddCheck("Minimize to tray", _settings.MinimizeToTray, value => Set(value, v => _settings.MinimizeToTray = v));
        _ui.AddCheck("Close to tray", _settings.CloseToTray, value => Set(value, v => _settings.CloseToTray = v));
        _ui.AddCheck("Confirm before closing while macros are running", _settings.ConfirmCloseWhileMacrosRunning, value => Set(value, v => _settings.ConfirmCloseWhileMacrosRunning = v));
    }

    private void RenderDefaults()
    {
        _ui.BeginSection("Defaults");
        if (!_settings.DefaultStandardDelayEnabled && !_settings.DefaultShowKeyUpDown)
        {
            _settings.DefaultShowKeyUpDown = true;
            NotifyChanged();
        }

        _ui.AddCheck("Default standard delay enabled", _settings.DefaultStandardDelayEnabled, value =>
        {
            _settings.DefaultStandardDelayEnabled = value;
            if (!value)
                _settings.DefaultShowKeyUpDown = true;

            NotifyChanged();
            RenderDefaults();
        });
        _ui.AddCheck("Default show key up/down", !_settings.DefaultStandardDelayEnabled || _settings.DefaultShowKeyUpDown, value =>
        {
            _settings.DefaultShowKeyUpDown = value;
            NotifyChanged();
            RenderDefaults();
        }, _settings.DefaultStandardDelayEnabled);
        _ui.AddNumber("Default standard delay value", _settings.DefaultStandardDelayMs, value => Set(value, v => _settings.DefaultStandardDelayMs = v), "ms");
        _ui.AddNumber("Default base delay", _settings.DefaultBaseDelayMs, value => Set(value, v => _settings.DefaultBaseDelayMs = v), "ms");
        _ui.AddNumber("Default timer value", _settings.DefaultTimerMs, value => Set(value, v => _settings.DefaultTimerMs = v), "ms");
        _ui.AddNumber("Default loop count", _settings.DefaultLoopCount, value => Set(value, v => _settings.DefaultLoopCount = v), "");
        _ui.AddChoice("Default loop mode", FormatLoopMode(_settings.DefaultLoopMode), new[] { "Async", "Sync", "Cycle", "Chain" }, value =>
        {
            _settings.DefaultLoopMode = ParseLoopMode(value);
            NotifyChanged();
        });
        _ui.AddChoice("Default input mode", _settings.DefaultTextInputMode ? "Text" : "Key", new[] { "Key", "Text" }, value =>
        {
            _settings.DefaultTextInputMode = value == "Text";
            NotifyChanged();
        });
    }

    private void RenderShortcuts()
    {
        _ui.BeginSection("Shortcuts");
        _ui.AddShortcut("Undo", _settings.UndoShortcut, value => _settings.UndoShortcut = value);
        _ui.AddShortcut("Redo", _settings.RedoShortcut, value => _settings.RedoShortcut = value);
        _ui.AddShortcut("Select all nodes", _settings.SelectAllShortcut, value => _settings.SelectAllShortcut = value);
        _ui.AddShortcut("Copy selected nodes/timeline/macro", _settings.CopyShortcut, value => _settings.CopyShortcut = value);
        _ui.AddShortcut("Paste nodes/timeline/macro", _settings.PasteShortcut, value => _settings.PasteShortcut = value);
        _ui.AddShortcut("Duplicate selection", _settings.DuplicateShortcut, value => _settings.DuplicateShortcut = value);
    }

    private void RenderRecording()
    {
        _ui.BeginSection("Recording");
        _ui.AddCheck("Merge repeated delay nodes", _settings.MergeRepeatedDelayNodes, value => Set(value, v => _settings.MergeRepeatedDelayNodes = v));
    }

    private void RenderPlayback()
    {
        _ui.BeginSection("Playback");
        _ui.AddCheck("Play sound when macro starts/stops", _settings.PlaySoundOnMacroStartStop, value => Set(value, v => _settings.PlaySoundOnMacroStartStop = v));
        _ui.AddChoice("Sound", _settings.PlaybackSoundName, new[] { "Beep", "Asterisk", "Exclamation", "Hand", "Question" }, value =>
        {
            _settings.PlaybackSoundName = value;
            _actions.PreviewPlaybackSound();
            NotifyChanged();
        });
        _ui.AddShortcut("Emergency stop shortcut", _settings.EmergencyStopShortcut, value => _settings.EmergencyStopShortcut = value);
        _ui.AddShortcut("Pause/resume all macros hotkey", _settings.PauseResumeAllMacrosShortcut, value => _settings.PauseResumeAllMacrosShortcut = value);
        _ui.AddShortcut("Toggle global remap shortcut", _settings.ToggleGlobalRemapShortcut, value => _settings.ToggleGlobalRemapShortcut = value);
    }

    private void RenderImportExport()
    {
        _ui.BeginSection("Import/Export");
        _ui.AddAction("Import", _actions.Import);
        _ui.AddAction("Export Macro", _actions.ExportMacro);
        _ui.AddFeatureAction("Export Profile", FeatureId.Profiles, _actions.ExportProfile);
        _ui.AddAction("Export EVERYTHING", _actions.ExportEverything);
        _ui.AddAction("Open Backups", _actions.OpenBackups);

        if (!string.IsNullOrWhiteSpace(_actions.ImportExportNotice))
            _ui.AddNotice(_actions.ImportExportNotice);
    }

    private void RenderReset()
    {
        _ui.BeginSection("Reset");
        _ui.AddAction("Reset defaults", () => { _actions.ResetDefaults(); NotifyChanged(); });
        _ui.AddAction("Reset settings", () => { _actions.ResetSettings(); NotifyChanged(); _closeRequested(); });
        _ui.AddAction("Reset all saved data", () => { _actions.ResetAllSavedData(); NotifyChanged(); _closeRequested(); });
    }

    private void RenderExperimental()
    {
        _ui.BeginSection("Experimental");
        _ui.AddCheck("Enable experimental nodes", _settings.ExperimentalFeaturesEnabled, value => Set(value, v => _settings.ExperimentalFeaturesEnabled = v));
        _ui.AddDescription("Shows background mouse down/up/click options in the add menu. These nodes are experimental because many games ignore or reinterpret background mouse messages.");
    }

    private void RenderAbout()
    {
        _ui.BeginSection(IsUpdateAvailable ? "About  !" : "About");
        _ui.AddDescription("KeyLine");
        _ui.AddDescription($"Version: {_actions.CurrentVersionText}");
        _ui.AddLicenseLink();
        _ui.AddDescription("Copyright © 2026 Iompaeqe(Iompa).");

        AddUpdateCheckButton();
        _ui.AddSpacer(90);
        _ui.AddFeedbackButton();
        BeginUpdateCheck(forceRefresh: true);
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
        AddToPanel(_updateStatusText);

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
        AddToPanel(button);
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

    private void AddToPanel(UIElement element)
    {
        _ui.Add(element);
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

    private static string FormatLoopMode(MacroLoopMode loopMode)
    {
        return loopMode switch
        {
            MacroLoopMode.Sync => "Sync",
            MacroLoopMode.Cycle => "Cycle",
            MacroLoopMode.Chain => "Chain",
            _ => "Async"
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
}
