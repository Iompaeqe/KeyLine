using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using MacroSpammer.Domain;
using MacroSpammer.Services.Macro;

namespace MacroSpammer;

public partial class MainWindow : ISettingsActions
{
    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ShowSettingsView();
    }

    private void ShowSettingsView()
    {
        SettingsModalHost.Content = new SettingsView(_settings, this)
        {
            CloseRequested = CloseSettingsModal
        };
        SettingsModalOverlay.Visibility = Visibility.Visible;
    }

    private void CloseSettingsModal()
    {
        SettingsModalOverlay.Visibility = Visibility.Collapsed;
        SettingsModalHost.Content = null;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
            return;

        ImportMacroFiles(files.Where(file =>
            string.Equals(Path.GetExtension(file), MacroFileStore.Extension, StringComparison.OrdinalIgnoreCase)));
    }

    public void ApplySettings()
    {
        UpdateExperimentalAddMenuVisibility();
        ApplyShortcutHookState();
        ScheduleSaveState();
    }

    public void SetLaunchOnStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (key == null)
            return;

        if (enabled)
            key.SetValue("KeyLine", $"\"{Environment.ProcessPath}\"");
        else
            key.DeleteValue("KeyLine", false);
    }

    public void ImportMacroFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "KeyLine macro (*.keyline)|*.keyline|All files (*.*)|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
            ImportMacroFiles(new[] { dialog.FileName });
    }

    public void ImportMultipleMacroFiles()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "KeyLine macro (*.keyline)|*.keyline|All files (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog(this) == true)
            ImportMacroFiles(dialog.FileNames);
    }

    public void ExportSelectedMacro()
    {
        if (_workspaces.Count == 1)
        {
            ExportWorkspaces(new[] { _workspaces[0] });
            return;
        }

        SettingsModalHost.Content = new ExportMacroSelectionView(
            _workspaces,
            _activeWorkspace,
            selected =>
            {
                if (selected.Count > 0)
                    ExportWorkspaces(selected);

                ShowSettingsView();
            },
            ShowSettingsView);
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

        if (dialog.ShowDialog(this) == true)
            MacroFileStore.Export(dialog.FileName, selectedWorkspaces);
    }

    public void ExportAllMacros()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "KeyLine macro (*.keyline)|*.keyline",
            FileName = "KeyLine macros.keyline"
        };

        if (dialog.ShowDialog(this) == true)
            MacroFileStore.Export(dialog.FileName, _workspaces);
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

    public void ResetDefaults()
    {
        if (MessageBox.Show(this, "Reset default macro values?", "Reset defaults", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        _settings.DefaultStandardDelayEnabled = false;
        _settings.DefaultStandardDelayMs = 50;
        _settings.DefaultBaseDelayMs = 50;
        _settings.DefaultTimerMs = 0;
        _settings.DefaultLoopCount = 0;
        _settings.DefaultTextInputMode = false;
        ApplySettings();
    }

    public void ResetSettings()
    {
        if (MessageBox.Show(this, "Reset all settings? Macros will be kept.", "Reset settings", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        _settings.CopyFrom(new AppSettings());
        ApplySettings();
    }

    public void ResetAllSavedData()
    {
        if (MessageBox.Show(this, "Reset all saved data? This removes settings and all macros. This cannot be undone.", "Reset all saved data", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        StopAllRunners();
        _workspaces.Clear();
        _settings.CopyFrom(new AppSettings());
        _workspaces.Add(CreateWorkspace(1, _settings));
        _activeWorkspaceIndex = 0;
        ActivateWorkspace(0, false);
        SaveStateNow();
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
                StatusText.Text = $"Import failed: {Path.GetFileName(path)}";
            }
        }

        if (imported.Count == 0)
            return;

        CaptureActiveWorkspaceState();

        foreach (var workspace in imported)
        {
            workspace.Name = GetUniqueWorkspaceName(workspace.Name);
            workspace.TargetWindowHandle = 0;
            workspace.TargetWindowTitle = "";
            workspace.TargetChildWindowHandle = 0;
            workspace.TargetChildWindowTitle = "";
            _workspaces.Add(workspace);
        }

        ActivateWorkspace(_workspaces.Count - imported.Count);
        ScheduleSaveState();
    }

    private string GetUniqueWorkspaceName(string preferredName)
    {
        var baseName = string.IsNullOrWhiteSpace(preferredName) ? "Imported Macro" : preferredName.Trim();
        if (_workspaces.All(workspace => !string.Equals(workspace.Name, baseName, StringComparison.OrdinalIgnoreCase)))
            return baseName;

        for (var i = 2; ; i++)
        {
            var candidate = $"{baseName} {i}";
            if (_workspaces.All(workspace => !string.Equals(workspace.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;
        }
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');

        return string.IsNullOrWhiteSpace(name) ? "Macro" : name;
    }

}
