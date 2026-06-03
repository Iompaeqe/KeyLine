using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using KeyLine.Domain;
using KeyLine.Services.Macro;
using Microsoft.Win32;

namespace KeyLine;

public sealed class SettingsController : ISettingsActions
{
    private readonly Window _owner;
    private readonly AppSettings _settings;
    private readonly ContentControl _modalHost;
    private readonly UIElement _modalOverlay;
    private readonly IList<MacroWorkspace> _workspaces;
    private readonly IList<MacroProfile> _profiles;

    private readonly Func<MacroWorkspace> _getActiveWorkspace;
    private readonly Func<string> _getActiveProfileId;
    private readonly Func<IReadOnlyList<MacroWorkspace>> _getActiveProfileWorkspaces;
    private readonly Func<int> _getActiveWorkspaceIndex;
    private readonly Func<bool> _getShortcutsEnabled;
    private readonly Func<double> _getMainWindowWidth;
    private readonly Func<int, AppSettings, MacroWorkspace> _createWorkspace;
    private readonly Action _captureActiveWorkspaceState;
    private readonly Action<int, bool> _activateWorkspace;
    private readonly Action _resetProfiles;
    private readonly Action<bool> _setShortcutsEnabled;
    private readonly Action<double> _applyMainWindowWidth;
    private readonly Action _updateExperimentalAddMenuVisibility;
    private readonly Action _applyShortcutHookState;
    private readonly Action _scheduleSaveState;
    private readonly Action _stopAllRunners;
    private readonly Action _saveStateNow;
    private readonly Action<string> _setStatusText;
    private readonly Action _previewPlaybackSound;

    private readonly Action<bool> _setShortcutCaptureActive;

    private const string ImportExportCategory = "Import/Export";
    private const string GitHubLatestReleaseApi = "https://api.github.com/repos/mkurtt96/KeyLine/releases/latest";
    private const string GitHubReleasesPage = "https://github.com/mkurtt96/KeyLine/releases";
    public string CurrentVersionText => GetCurrentVersionText();

    public SettingsController(
        Window owner,
        AppSettings settings,
        ContentControl modalHost,
        UIElement modalOverlay,
        IList<MacroWorkspace> workspaces,
        IList<MacroProfile> profiles,
        Func<MacroWorkspace> getActiveWorkspace,
        Func<string> getActiveProfileId,
        Func<IReadOnlyList<MacroWorkspace>> getActiveProfileWorkspaces,
        Func<int> getActiveWorkspaceIndex,
        Func<bool> getShortcutsEnabled,
        Func<double> getMainWindowWidth,
        Func<int, AppSettings, MacroWorkspace> createWorkspace,
        Action captureActiveWorkspaceState,
        Action<int, bool> activateWorkspace,
        Action resetProfiles,
        Action<bool> setShortcutsEnabled,
        Action<double> applyMainWindowWidth,
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
        _profiles = profiles;
        _getActiveWorkspace = getActiveWorkspace;
        _getActiveProfileId = getActiveProfileId;
        _getActiveProfileWorkspaces = getActiveProfileWorkspaces;
        _getActiveWorkspaceIndex = getActiveWorkspaceIndex;
        _getShortcutsEnabled = getShortcutsEnabled;
        _getMainWindowWidth = getMainWindowWidth;
        _createWorkspace = createWorkspace;
        _captureActiveWorkspaceState = captureActiveWorkspaceState;
        _activateWorkspace = activateWorkspace;
        _resetProfiles = resetProfiles;
        _setShortcutsEnabled = setShortcutsEnabled;
        _applyMainWindowWidth = applyMainWindowWidth;
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
        Show(null);
    }

    private void Show(string? initialCategory)
    {
        _modalHost.Content = new SettingsView(_settings, this, initialCategory)
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

        ImportFiles(files.Where(file =>
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

    public void Import()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "KeyLine package (*.keyline)|*.keyline|All files (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog(_owner) == true)
            ImportFiles(dialog.FileNames);
    }

    public void ExportMacro()
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

                Show(ImportExportCategory);
            },
            () => Show(ImportExportCategory));
    }

    public void ExportProfile()
    {
        if (_profiles.Count == 0)
        {
            _setStatusText("No profiles to export");
            return;
        }

        var activeProfileId = MacroProfile.NormalizeId(_getActiveProfileId());
        var selectedProfiles = _profiles
            .Where(profile => string.Equals(profile.Id, activeProfileId, StringComparison.OrdinalIgnoreCase))
            .Cast<object>()
            .ToList();

        if (selectedProfiles.Count == 0)
            selectedProfiles.Add(_profiles[0]);

        _modalHost.Content = new ExportMacroSelectionView(
            "Export profiles",
            _profiles.Cast<object>().ToList(),
            selectedProfiles,
            selected =>
            {
                var profiles = selected.OfType<MacroProfile>().ToList();
                if (profiles.Count > 0)
                    ExportProfiles(profiles);

                Show(ImportExportCategory);
            },
            () => Show(ImportExportCategory));
    }

    public void ExportEverything()
    {
        _captureActiveWorkspaceState();

        var dialog = new SaveFileDialog
        {
            Filter = "KeyLine package (*.keyline)|*.keyline",
            FileName = "KeyLine EVERYTHING.keyline"
        };

        if (dialog.ShowDialog(_owner) != true)
            return;

        MacroStateStore.ExportSnapshot(
            dialog.FileName,
            _workspaces.ToList(),
            _getActiveWorkspaceIndex(),
            _getShortcutsEnabled(),
            _settings,
            _getMainWindowWidth(),
            _profiles.ToList(),
            _getActiveProfileId());
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
        _resetProfiles();
        _settings.CopyFrom(new AppSettings());
        SetLaunchOnStartup(_settings.LaunchOnWindowsStartup);

        _workspaces.Add(_createWorkspace(1, _settings));

        _activateWorkspace(0, false);
        _saveStateNow();
    }

    private void ImportFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            try
            {
                if (MacroStateStore.IsEverythingExport(path))
                {
                    ImportEverything(path);
                    continue;
                }

                var package = MacroFileStore.ImportPackage(path);
                if (package.Kind == MacroFileKind.Profiles)
                    ImportProfiles(package.Profiles, package.Workspaces);
                else
                    ImportWorkspaces(package.Workspaces);
            }
            catch
            {
                _setStatusText($"Import failed: {Path.GetFileName(path)}");
            }
        }
    }

    private void ImportEverything(string path)
    {
        if (MessageBox.Show(
                _owner,
                "Importing this file will rewrite every setting, profile, and macro. Continue?",
                "Import EVERYTHING",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        var snapshot = MacroStateStore.ImportSnapshot(path);
        if (snapshot == null || snapshot.Workspaces.Count == 0)
        {
            _setStatusText($"Import failed: {Path.GetFileName(path)}");
            return;
        }

        _stopAllRunners();

        _settings.CopyFrom(snapshot.Settings);
        SetLaunchOnStartup(_settings.LaunchOnWindowsStartup);

        _workspaces.Clear();
        foreach (var workspace in snapshot.Workspaces)
            _workspaces.Add(workspace);

        _profiles.Clear();
        foreach (var profile in snapshot.Profiles)
            _profiles.Add(profile);

        _applyMainWindowWidth(snapshot.MainWindowWidth);
        _activateWorkspace(Math.Clamp(snapshot.ActiveWorkspaceIndex, 0, _workspaces.Count - 1), false);
        _setShortcutsEnabled(snapshot.ShortcutsEnabled);
        ApplySettings();
        _saveStateNow();
    }

    private void ImportWorkspaces(IReadOnlyList<MacroWorkspace> imported)
    {
        if (imported.Count == 0)
            return;

        _captureActiveWorkspaceState();

        var activeProfileId = _getActiveProfileId();
        foreach (var workspace in imported)
        {
            workspace.ProfileId = activeProfileId;
            workspace.Name = WorkspaceNameService.GetUniqueName(
                _getActiveProfileWorkspaces(),
                workspace.Name);
            ResetImportedTargetHandles(workspace);
            _workspaces.Add(workspace);
        }

        _activateWorkspace(_workspaces.Count - imported.Count, true);
        _scheduleSaveState();
    }

    private void ImportProfiles(
        IReadOnlyList<MacroProfile> importedProfiles,
        IReadOnlyList<MacroWorkspace> importedWorkspaces)
    {
        if (importedProfiles.Count == 0)
        {
            ImportWorkspaces(importedWorkspaces);
            return;
        }

        _captureActiveWorkspaceState();

        var profileIdMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var importedProfile in importedProfiles)
        {
            var sourceId = MacroProfile.NormalizeId(importedProfile.Id);
            if (MacroProfile.IsNoProfile(sourceId))
                continue;

            var profile = new MacroProfile
            {
                Name = GetUniqueProfileName(importedProfile.Name)
            };

            _profiles.Add(profile);
            profileIdMap[sourceId] = profile.Id;
        }

        var firstImportedWorkspaceIndex = -1;
        foreach (var workspace in importedWorkspaces)
        {
            var sourceProfileId = MacroProfile.NormalizeId(workspace.ProfileId);
            if (!profileIdMap.TryGetValue(sourceProfileId, out var targetProfileId))
                continue;

            workspace.ProfileId = targetProfileId;
            workspace.Name = WorkspaceNameService.GetUniqueName(
                _workspaces.Where(existing => string.Equals(
                    MacroProfile.NormalizeId(existing.ProfileId),
                    targetProfileId,
                    StringComparison.OrdinalIgnoreCase)),
                workspace.Name);
            ResetImportedTargetHandles(workspace);

            if (firstImportedWorkspaceIndex < 0)
                firstImportedWorkspaceIndex = _workspaces.Count;

            _workspaces.Add(workspace);
        }

        if (firstImportedWorkspaceIndex >= 0)
            _activateWorkspace(firstImportedWorkspaceIndex, true);
        else
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

    private void ExportProfiles(IReadOnlyList<MacroProfile> selectedProfiles)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "KeyLine package (*.keyline)|*.keyline",
            FileName = selectedProfiles.Count == 1
                ? $"{SanitizeFileName(selectedProfiles[0].Name)} profile.keyline"
                : "Selected KeyLine profiles.keyline"
        };

        if (dialog.ShowDialog(_owner) == true)
            MacroFileStore.ExportProfiles(dialog.FileName, selectedProfiles, _workspaces);
    }

    private string GetUniqueProfileName(string name)
    {
        var baseName = string.IsNullOrWhiteSpace(name) ? "Profile" : name.Trim();
        var usedNames = _profiles
            .Select(profile => profile.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!usedNames.Contains(baseName))
            return baseName;

        for (var i = 2;; i++)
        {
            var candidate = $"{baseName} {i}";
            if (!usedNames.Contains(candidate))
                return candidate;
        }
    }

    private static void ResetImportedTargetHandles(MacroWorkspace workspace)
    {
        workspace.TargetWindowHandle = 0;
        workspace.TargetWindowTitle = "";
        workspace.TargetChildWindowHandle = 0;
        workspace.TargetChildWindowTitle = "";
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');

        return string.IsNullOrWhiteSpace(name) ? "Macro" : name;
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync()
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("KeyLine");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            using var response = await client.GetAsync(GitHubLatestReleaseApi);

            if (!response.IsSuccessStatusCode)
                return new UpdateCheckResult(UpdateCheckState.Failed, "Update check failed - retry");

            await using var stream = await response.Content.ReadAsStreamAsync();
            var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream);

            if (release == null || string.IsNullOrWhiteSpace(release.TagName))
                return new UpdateCheckResult(UpdateCheckState.Failed, "Update check failed - retry");

            var currentVersion = TryParseVersion(GetCurrentVersionText());
            var latestVersion = TryParseVersion(release.TagName);

            if (currentVersion == null || latestVersion == null)
                return new UpdateCheckResult(
                    UpdateCheckState.UpdateAvailable,
                    $"Latest release: {release.TagName} - open",
                    release.HtmlUrl);

            if (latestVersion > currentVersion)
                return new UpdateCheckResult(
                    UpdateCheckState.UpdateAvailable,
                    $"Update available: {release.TagName} - open",
                    release.HtmlUrl);

            return new UpdateCheckResult(
                UpdateCheckState.Latest,
                "Latest version");
        }
        catch
        {
            return new UpdateCheckResult(
                UpdateCheckState.Failed,
                "Update check failed - retry");
        }
    }

    public void OpenReleasesPage(string? releaseUrl = null)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = string.IsNullOrWhiteSpace(releaseUrl)
                ? GitHubReleasesPage
                : releaseUrl,
            UseShellExecute = true
        });
    }

    private static string GetCurrentVersionText()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(version))
            return CleanVersionText(version);

        return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static Version? TryParseVersion(string value)
    {
        value = CleanVersionText(value);

        return Version.TryParse(value, out var version)
            ? version
            : null;
    }

    private static string CleanVersionText(string value)
    {
        value = value.Trim();

        if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            value = value[1..];

        var plusIndex = value.IndexOf('+');
        if (plusIndex >= 0)
            value = value[..plusIndex];

        var dashIndex = value.IndexOf('-');
        if (dashIndex >= 0)
            value = value[..dashIndex];

        return value.Trim();
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string TagName { get; set; } = "";

        [JsonPropertyName("html_url")] public string HtmlUrl { get; set; } = "";
    }
}
