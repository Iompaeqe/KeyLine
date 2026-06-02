using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MacroSpammer.Domain;
using MacroSpammer.Services.Macro;

namespace MacroSpammer;

public sealed class SettingsController : ISettingsActions
{
    private readonly Window _owner;
    private readonly AppSettings _settings;
    private readonly ContentControl _modalHost;
    private readonly UIElement _modalOverlay;
    private readonly IList<MacroWorkspace> _workspaces;

    private readonly Func<MacroWorkspace> _getActiveWorkspace;
    private readonly Func<int, AppSettings, MacroWorkspace> _createWorkspace;
    private readonly Action _captureActiveWorkspaceState;
    private readonly Action<int, bool> _activateWorkspace;
    private readonly Action _updateExperimentalAddMenuVisibility;
    private readonly Action _applyShortcutHookState;
    private readonly Action _scheduleSaveState;
    private readonly Action _stopAllRunners;
    private readonly Action _saveStateNow;
    private readonly Action<string> _setStatusText;
    private readonly Action _previewPlaybackSound;
    
    private readonly Action<bool> _setShortcutCaptureActive;

    public SettingsController(
        Window owner,
        AppSettings settings,
        ContentControl modalHost,
        UIElement modalOverlay,
        IList<MacroWorkspace> workspaces,
        Func<MacroWorkspace> getActiveWorkspace,
        Func<int, AppSettings, MacroWorkspace> createWorkspace,
        Action captureActiveWorkspaceState,
        Action<int, bool> activateWorkspace,
        Action updateExperimentalAddMenuVisibility,
        Action applyShortcutHookState,
        Action scheduleSaveState,
        Action stopAllRunners,
        Action saveStateNow,
        Action<string> setStatusText,
        Action previewPlaybackSound,
        Action<bool> setShortcutCaptureActive)
    {
        _owner = owner;
        _settings = settings;
        _modalHost = modalHost;
        _modalOverlay = modalOverlay;
        _workspaces = workspaces;
        _getActiveWorkspace = getActiveWorkspace;
        _createWorkspace = createWorkspace;
        _captureActiveWorkspaceState = captureActiveWorkspaceState;
        _activateWorkspace = activateWorkspace;
        _updateExperimentalAddMenuVisibility = updateExperimentalAddMenuVisibility;
        _applyShortcutHookState = applyShortcutHookState;
        _scheduleSaveState = scheduleSaveState;
        _stopAllRunners = stopAllRunners;
        _saveStateNow = saveStateNow;
        _setStatusText = setStatusText;
        _previewPlaybackSound = previewPlaybackSound;
        _setShortcutCaptureActive = setShortcutCaptureActive;
    }

    public bool IsOpen => _modalOverlay.Visibility == Visibility.Visible;

    public void Show()
    {
        _modalHost.Content = new SettingsView(_settings, this)
        {
            CloseRequested = Close
        };

        _modalOverlay.Visibility = Visibility.Visible;
    }
    
    public void SetShortcutCaptureActive(bool isActive)
    {
        _setShortcutCaptureActive(isActive);
    }

    public void Close()
    {
        _modalOverlay.Visibility = Visibility.Collapsed;
        _modalHost.Content = null;
    }

    public void HandleFileDrop(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
            return;

        ImportMacroFiles(files.Where(file =>
            string.Equals(Path.GetExtension(file), MacroFileStore.Extension, StringComparison.OrdinalIgnoreCase)));

        e.Handled = true;
    }

    public void ApplySettings()
    {
        _updateExperimentalAddMenuVisibility();
        _applyShortcutHookState();
        _scheduleSaveState();
    }

    public void SetLaunchOnStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run",
            writable: true);

        if (key == null)
            return;

        if (enabled)
            key.SetValue("KeyLine", $"\"{Environment.ProcessPath}\"");
        else
            key.DeleteValue("KeyLine", throwOnMissingValue: false);
    }

    public void ImportMacroFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "KeyLine macro (*.keyline)|*.keyline|All files (*.*)|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog(_owner) == true)
            ImportMacroFiles(new[] { dialog.FileName });
    }

    public void ImportMultipleMacroFiles()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "KeyLine macro (*.keyline)|*.keyline|All files (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog(_owner) == true)
            ImportMacroFiles(dialog.FileNames);
    }

    public void ExportSelectedMacro()
    {
        if (_workspaces.Count == 1)
        {
            ExportWorkspaces(new[] { _workspaces[0] });
            return;
        }

        _modalHost.Content = new ExportMacroSelectionView(
            _workspaces.ToList(),
            _getActiveWorkspace(),
            selected =>
            {
                if (selected.Count > 0)
                    ExportWorkspaces(selected);

                Show();
            },
            Show);
    }

    public void ExportAllMacros()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "KeyLine macro (*.keyline)|*.keyline",
            FileName = "KeyLine macros.keyline"
        };

        if (dialog.ShowDialog(_owner) == true)
            MacroFileStore.Export(dialog.FileName, _workspaces.ToList());
    }

    public void OpenMacroStorageFolder()
    {
        Directory.CreateDirectory(MacroStateStore.StateDirectory);

        Process.Start(new ProcessStartInfo
        {
            FileName = MacroStateStore.StateDirectory,
            UseShellExecute = true
        });
    }

    public void PreviewPlaybackSound()
    {
        _previewPlaybackSound();
    }

    public void ResetDefaults()
    {
        if (MessageBox.Show(
                _owner,
                "Reset default macro values?",
                "Reset defaults",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        _settings.DefaultStandardDelayEnabled = false;
        _settings.DefaultShowKeyUpDown = true;
        _settings.DefaultStandardDelayMs = 50;
        _settings.DefaultBaseDelayMs = 50;
        _settings.DefaultTimerMs = 0;
        _settings.DefaultLoopCount = 0;
        _settings.DefaultLoopMode = MacroLoopMode.Async;
        _settings.DefaultTextInputMode = false;

        ApplySettings();
    }

    public void ResetSettings()
    {
        if (MessageBox.Show(
                _owner,
                "Reset all settings? Macros will be kept.",
                "Reset settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        _settings.CopyFrom(new AppSettings());
        SetLaunchOnStartup(_settings.LaunchOnWindowsStartup);
        ApplySettings();
    }

    public void ResetAllSavedData()
    {
        if (MessageBox.Show(
                _owner,
                "Reset all saved data? This removes settings and all macros. This cannot be undone.",
                "Reset all saved data",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        _stopAllRunners();

        _workspaces.Clear();
        _settings.CopyFrom(new AppSettings());
        SetLaunchOnStartup(_settings.LaunchOnWindowsStartup);

        _workspaces.Add(_createWorkspace(1, _settings));

        _activateWorkspace(0, false);
        _saveStateNow();
    }

    private void ImportMacroFiles(IEnumerable<string> paths)
    {
        var imported = new List<MacroWorkspace>();

        foreach (var path in paths)
        {
            try
            {
                imported.AddRange(MacroFileStore.Import(path));
            }
            catch
            {
                _setStatusText($"Import failed: {Path.GetFileName(path)}");
            }
        }

        if (imported.Count == 0)
            return;

        _captureActiveWorkspaceState();

        foreach (var workspace in imported)
        {
            workspace.Name = WorkspaceNameService.GetUniqueName(_workspaces, workspace.Name);
            workspace.TargetWindowHandle = 0;
            workspace.TargetWindowTitle = "";
            workspace.TargetChildWindowHandle = 0;
            workspace.TargetChildWindowTitle = "";
            _workspaces.Add(workspace);
        }

        _activateWorkspace(_workspaces.Count - imported.Count, true);
        _scheduleSaveState();
    }

    private void ExportWorkspaces(IReadOnlyList<MacroWorkspace> selectedWorkspaces)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "KeyLine macro (*.keyline)|*.keyline",
            FileName = selectedWorkspaces.Count == 1
                ? $"{SanitizeFileName(selectedWorkspaces[0].Name)}.keyline"
                : "Selected KeyLine macros.keyline"
        };

        if (dialog.ShowDialog(_owner) == true)
            MacroFileStore.Export(dialog.FileName, selectedWorkspaces);
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');

        return string.IsNullOrWhiteSpace(name) ? "Macro" : name;
    }
}

