using System.Diagnostics;
using System.IO;
using System.Media;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using KeyLine.Domain;
using KeyLine.Services.Features;
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
    private readonly FeatureGate _featureGate;
    private readonly MacroFeatureValidator _macroFeatureValidator;

    private readonly Func<MacroWorkspace> _getActiveWorkspace;
    private readonly Func<string> _getActiveProfileId;
    private readonly Func<IReadOnlyList<MacroWorkspace>> _getActiveProfileWorkspaces;
    private readonly Func<int> _getActiveWorkspaceIndex;
    private readonly Func<bool> _getShortcutsEnabled;
    private readonly Func<double> _getMainWindowWidth;
    private readonly Func<int, AppSettings, MacroWorkspace> _createWorkspace;
    private readonly Action _captureActiveWorkspaceState;
    private readonly Action<int, bool> _activateWorkspace;
    private readonly Action<string> _activateProfile;
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
    private const string GitHubLatestReleaseApi = "https://api.github.com/repos/Iompaeqe/KeyLine/releases/latest";
    private const string GitHubReleasesPage = "https://github.com/Iompaeqe/KeyLine/releases";
    private string _importExportNotice = "";
    private Task<UpdateCheckResult>? _updateCheckTask;
    public string CurrentVersionText => GetCurrentVersionText();
    public string ImportExportNotice => _importExportNotice;
    public UpdateCheckResult? LastUpdateCheckResult { get; private set; }
    public event EventHandler<UpdateCheckResult>? UpdateCheckCompleted;
    public bool IsFeatureVisible(FeatureId feature) => _featureGate.IsVisible(feature);
    public bool IsFeatureLocked(FeatureId feature) => _featureGate.IsLocked(feature);
    public string GetLockedFeatureMessage(FeatureId feature) => _featureGate.GetLockedFeatureMessage(feature);

    public SettingsController(
        Window owner,
        AppSettings settings,
        ContentControl modalHost,
        UIElement modalOverlay,
        IList<MacroWorkspace> workspaces,
        IList<MacroProfile> profiles,
        FeatureGate featureGate,
        Func<MacroWorkspace> getActiveWorkspace,
        Func<string> getActiveProfileId,
        Func<IReadOnlyList<MacroWorkspace>> getActiveProfileWorkspaces,
        Func<int> getActiveWorkspaceIndex,
        Func<bool> getShortcutsEnabled,
        Func<double> getMainWindowWidth,
        Func<int, AppSettings, MacroWorkspace> createWorkspace,
        Action captureActiveWorkspaceState,
        Action<int, bool> activateWorkspace,
        Action<string> activateProfile,
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
        _featureGate = featureGate;
        _macroFeatureValidator = new MacroFeatureValidator(_featureGate);
        _getActiveWorkspace = getActiveWorkspace;
        _getActiveProfileId = getActiveProfileId;
        _getActiveProfileWorkspaces = getActiveProfileWorkspaces;
        _getActiveWorkspaceIndex = getActiveWorkspaceIndex;
        _getShortcutsEnabled = getShortcutsEnabled;
        _getMainWindowWidth = getMainWindowWidth;
        _createWorkspace = createWorkspace;
        _captureActiveWorkspaceState = captureActiveWorkspaceState;
        _activateWorkspace = activateWorkspace;
        _activateProfile = activateProfile;
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

    public void Show(string? initialCategory = null)
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
        _importExportNotice = "";

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
        var exportableWorkspaces = _featureGate.IsEnabled(FeatureId.Profiles)
            ? _workspaces.ToList()
            : _workspaces
                .Where(workspace => MacroProfile.IsNoProfile(MacroProfile.NormalizeId(workspace.ProfileId)))
                .ToList();

        if (exportableWorkspaces.Count == 1)
        {
            ExportWorkspaces(new[] { exportableWorkspaces[0] });
            return;
        }

        _modalHost.Content = new ExportMacroSelectionView(
            exportableWorkspaces,
            _featureGate.IsEnabled(FeatureId.Profiles) ? _profiles.ToList() : new List<MacroProfile>(),
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
        if (!_featureGate.IsEnabled(FeatureId.Profiles))
        {
            _setStatusText(_featureGate.GetLockedFeatureMessage(FeatureId.Profiles));
            return;
        }

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

    public void OpenBackups()
    {
        Directory.CreateDirectory(MacroStateStore.BackupsDirectory);

        Process.Start(new ProcessStartInfo
        {
            FileName = MacroStateStore.BackupsDirectory,
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
        _resetProfiles();
        _settings.CopyFrom(new AppSettings());
        SetLaunchOnStartup(_settings.LaunchOnWindowsStartup);

        _workspaces.Add(_createWorkspace(1, _settings));

        _activateWorkspace(0, false);
        _saveStateNow();
    }

    private void ImportFiles(IEnumerable<string> paths)
    {
        _importExportNotice = "";

        foreach (var path in paths)
        {
            try
            {
                if (MacroStateStore.IsEverythingExport(path))
                {
                    PreviewEverythingImport(path);
                    return;
                }

                var package = MacroFileStore.ImportPackage(path);
                if (package.Kind == MacroFileKind.Profiles || package.Profiles.Count > 0)
                {
                    if (!CanImportProfiles())
                        return;

                    ApplyFeatureValidationToWorkspaces(package.Workspaces);
                    PreviewProfileImport(package.Profiles, package.Workspaces);
                    return;
                }
                else
                {
                    ApplyFeatureValidationToWorkspaces(package.Workspaces);
                    ImportWorkspaces(package.Workspaces);
                }
            }
            catch
            {
                _setStatusText($"Import failed: {Path.GetFileName(path)}");
            }
        }
    }

    private void PreviewProfileImport(
        IReadOnlyList<MacroProfile> importedProfiles,
        IReadOnlyList<MacroWorkspace> importedWorkspaces)
    {
        if (!CanImportProfiles())
            return;

        var featureWarning = ApplyFeatureValidationToWorkspaces(importedWorkspaces);

        if (importedProfiles.Count == 0)
        {
            ImportWorkspaces(importedWorkspaces);
            return;
        }

        ShowImportConfirmation(new ImportConfirmationView(
            "Confirm profile import",
            "Import",
            CreateProfileImportPreview(importedProfiles, importedWorkspaces),
            () =>
            {
                ImportProfiles(importedProfiles, importedWorkspaces);
                Close();
            },
            () => Show(ImportExportCategory),
            string.IsNullOrWhiteSpace(featureWarning) ? null : featureWarning));
    }

    private void PreviewEverythingImport(string path)
    {
        var snapshot = MacroStateStore.ImportSnapshot(path);
        if (snapshot == null || snapshot.Workspaces.Count == 0)
        {
            _setStatusText($"Import failed: {Path.GetFileName(path)}");
            return;
        }

        if (ContainsProfileData(snapshot) && !_featureGate.IsEnabled(FeatureId.Profiles))
        {
            _setStatusText(_featureGate.GetLockedFeatureMessage(FeatureId.Profiles));
            Show(ImportExportCategory);
            return;
        }

        var featureWarning = ApplyFeatureValidationToWorkspaces(snapshot.Workspaces);
        var warningText = "Warning: importing EVERYTHING will replace every setting, profile, and macro. The current state will be backed up first.";
        if (!string.IsNullOrWhiteSpace(featureWarning))
            warningText += Environment.NewLine + Environment.NewLine + featureWarning;

        SystemSounds.Exclamation.Play();
        ShowImportConfirmation(new ImportConfirmationView(
            "Confirm EVERYTHING import",
            "Import EVERYTHING",
            CreateEverythingImportPreview(snapshot),
            () => ApplyEverythingImport(path, snapshot),
            () => Show(ImportExportCategory),
            warningText,
            CreateSettingsPreview(snapshot)));
    }

    private void ShowImportConfirmation(UIElement view)
    {
        _modalHost.Content = view;
        _modalOverlay.Visibility = Visibility.Visible;
    }

    private void ApplyEverythingImport(string path, MacroStateSnapshot snapshot)
    {
        _captureActiveWorkspaceState();

        string backupPath;
        try
        {
            backupPath = MacroStateStore.CreateAutoBackupBeforeImport(
                _workspaces.ToList(),
                _getActiveWorkspaceIndex(),
                _getShortcutsEnabled(),
                _settings,
                _getMainWindowWidth(),
                _profiles.ToList(),
                _getActiveProfileId());
        }
        catch
        {
            _setStatusText("Import failed: could not create backup");
            Show(ImportExportCategory);
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
        _importExportNotice =
            $"Notice: the old state is saved in the backups. ({Path.GetFileName(backupPath)})";
        Show(ImportExportCategory);
    }

    private void ImportWorkspaces(IReadOnlyList<MacroWorkspace> imported)
    {
        if (imported.Count == 0)
            return;

        var featureWarning = ApplyFeatureValidationToWorkspaces(imported);
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
        if (!string.IsNullOrWhiteSpace(featureWarning))
            _setStatusText(featureWarning);
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

        if (!CanImportProfiles())
            return;

        var featureWarning = ApplyFeatureValidationToWorkspaces(importedWorkspaces);
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

            _workspaces.Add(workspace);
        }

        var firstImportedProfileId = profileIdMap.Values.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(firstImportedProfileId))
            _activateProfile(firstImportedProfileId);
        else
            _scheduleSaveState();

        if (!string.IsNullOrWhiteSpace(featureWarning))
            _setStatusText(featureWarning);
    }

    private static IReadOnlyList<ImportProfilePreview> CreateProfileImportPreview(
        IReadOnlyList<MacroProfile> importedProfiles,
        IReadOnlyList<MacroWorkspace> importedWorkspaces)
    {
        var result = new List<ImportProfilePreview>();

        foreach (var profile in importedProfiles)
        {
            var profileId = MacroProfile.NormalizeId(profile.Id);
            if (MacroProfile.IsNoProfile(profileId))
                continue;

            var macroNames = importedWorkspaces
                .Where(workspace => string.Equals(
                    MacroProfile.NormalizeId(workspace.ProfileId),
                    profileId,
                    StringComparison.OrdinalIgnoreCase))
                .Select(workspace => workspace.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            result.Add(new ImportProfilePreview(
                string.IsNullOrWhiteSpace(profile.Name) ? "Profile" : profile.Name,
                macroNames));
        }

        return result;
    }

    private static IReadOnlyList<ImportProfilePreview> CreateEverythingImportPreview(MacroStateSnapshot snapshot)
    {
        var result = new List<ImportProfilePreview>();

        var namedProfiles = snapshot.Profiles
            .Select(profile => new
            {
                Id = MacroProfile.NormalizeId(profile.Id),
                Name = string.IsNullOrWhiteSpace(profile.Name) ? "Profile" : profile.Name
            })
            .Where(profile => !MacroProfile.IsNoProfile(profile.Id))
            .ToList();

        foreach (var profile in namedProfiles)
        {
            var macroNames = snapshot.Workspaces
                .Where(workspace => string.Equals(
                    MacroProfile.NormalizeId(workspace.ProfileId),
                    profile.Id,
                    StringComparison.OrdinalIgnoreCase))
                .Select(workspace => workspace.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            result.Add(new ImportProfilePreview(profile.Name, macroNames));
        }

        var noProfileMacros = snapshot.Workspaces
            .Where(workspace => MacroProfile.IsNoProfile(MacroProfile.NormalizeId(workspace.ProfileId)))
            .Select(workspace => workspace.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        if (noProfileMacros.Count > 0 || result.Count == 0)
            result.Insert(0, new ImportProfilePreview(MacroProfile.NoProfileName, noProfileMacros));

        return result;
    }

    private static ImportSettingsPreview CreateSettingsPreview(MacroStateSnapshot snapshot)
    {
        var activeProfileId = MacroProfile.NormalizeId(snapshot.ActiveProfileId);
        var activeProfileName = MacroProfile.NoProfileName;
        if (!MacroProfile.IsNoProfile(activeProfileId))
        {
            activeProfileName = snapshot.Profiles.FirstOrDefault(profile => string.Equals(
                    MacroProfile.NormalizeId(profile.Id),
                    activeProfileId,
                    StringComparison.OrdinalIgnoreCase))?.Name ?? MacroProfile.NoProfileName;
        }

        return new ImportSettingsPreview(
            snapshot.Workspaces.Count,
            snapshot.Profiles.Count,
            snapshot.ActiveWorkspaceIndex,
            activeProfileName,
            snapshot.ShortcutsEnabled,
            snapshot.Settings.DefaultTimerMs,
            snapshot.Settings.DefaultBaseDelayMs,
            snapshot.Settings.DefaultLoopCount,
            snapshot.Settings.DefaultLoopMode,
            snapshot.Settings.ExperimentalFeaturesEnabled);
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

    private bool CanImportProfiles()
    {
        if (_featureGate.IsEnabled(FeatureId.Profiles))
            return true;

        _setStatusText(_featureGate.GetLockedFeatureMessage(FeatureId.Profiles));
        Show(ImportExportCategory);
        return false;
    }

    private string ApplyFeatureValidationToWorkspaces(IEnumerable<MacroWorkspace> workspaces)
    {
        var workspaceList = workspaces.ToList();
        foreach (var workspace in workspaceList)
        {
            var validation = _macroFeatureValidator.ValidateWorkspace(workspace);
            if (!validation.CanRun)
                workspace.ErrorMessage = MacroFeatureValidator.FormatErrors(validation);
        }

        var aggregateValidation = _macroFeatureValidator.ValidateWorkspaces(workspaceList);
        if (aggregateValidation.CanRun)
            return "";

        var message = MacroFeatureValidator.FormatErrors(aggregateValidation);
        _importExportNotice = message;
        return message;
    }

    private static bool ContainsProfileData(MacroStateSnapshot snapshot)
    {
        return snapshot.Profiles.Count > 0 ||
               !MacroProfile.IsNoProfile(snapshot.ActiveProfileId) ||
               snapshot.Workspaces.Any(workspace =>
                   !MacroProfile.IsNoProfile(MacroProfile.NormalizeId(workspace.ProfileId)));
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');

        return string.IsNullOrWhiteSpace(name) ? "Macro" : name;
    }

    public Task<UpdateCheckResult> CheckForUpdatesAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && LastUpdateCheckResult != null)
            return Task.FromResult(LastUpdateCheckResult);

        if (_updateCheckTask is { IsCompleted: false })
            return _updateCheckTask;

        _updateCheckTask = CheckForUpdatesCoreAsync();
        return _updateCheckTask;
    }

    private async Task<UpdateCheckResult> CheckForUpdatesCoreAsync()
    {
        UpdateCheckResult result;

        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(8)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("KeyLine");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            using var response = await client.GetAsync(GitHubLatestReleaseApi);

            if (!response.IsSuccessStatusCode)
            {
                result = new UpdateCheckResult(UpdateCheckState.Failed, "Update check failed - retry");
                return PublishUpdateCheckResult(result);
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream);

            if (release == null || string.IsNullOrWhiteSpace(release.TagName))
            {
                result = new UpdateCheckResult(UpdateCheckState.Failed, "Update check failed - retry");
                return PublishUpdateCheckResult(result);
            }

            var currentVersion = TryParseVersion(GetCurrentVersionText());
            var latestVersion = TryParseVersion(release.TagName);

            if (currentVersion == null || latestVersion == null)
            {
                result = new UpdateCheckResult(
                    UpdateCheckState.UpdateAvailable,
                    $"Latest release: {release.TagName} - install",
                    release.HtmlUrl);
                return PublishUpdateCheckResult(result);
            }

            if (latestVersion > currentVersion)
            {
                result = new UpdateCheckResult(
                    UpdateCheckState.UpdateAvailable,
                    $"Update available: {release.TagName} - install",
                    release.HtmlUrl);
                return PublishUpdateCheckResult(result);
            }

            result = new UpdateCheckResult(
                UpdateCheckState.Latest,
                "Latest version");
            return PublishUpdateCheckResult(result);
        }
        catch
        {
            result = new UpdateCheckResult(
                UpdateCheckState.Failed,
                "Update check failed - retry");
            return PublishUpdateCheckResult(result);
        }
    }

    private UpdateCheckResult PublishUpdateCheckResult(UpdateCheckResult result)
    {
        if (result.State == UpdateCheckState.Failed &&
            LastUpdateCheckResult?.State == UpdateCheckState.UpdateAvailable)
        {
            UpdateCheckCompleted?.Invoke(this, LastUpdateCheckResult);
            return LastUpdateCheckResult;
        }

        LastUpdateCheckResult = result;
        UpdateCheckCompleted?.Invoke(this, result);
        return result;
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
