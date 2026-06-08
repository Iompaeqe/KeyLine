using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Windows;
using KeyLine.Domain;
using KeyLine.Services.Features;
using KeyLine.Services.Macro;
using Microsoft.Win32;

namespace KeyLine;

internal sealed class SettingsImportService
{
    private const string ImportExportCategory = "Import/Export";

    private readonly SettingsControllerContext _context;
    private readonly SettingsStartupService _startupService;
    private readonly SettingsFeatureValidationService _featureValidationService;
    private readonly Action<string?> _showSettings;
    private readonly Action _closeSettings;

    private string _notice = "";

    public SettingsImportService(
        SettingsControllerContext context,
        SettingsStartupService startupService,
        SettingsFeatureValidationService featureValidationService,
        Action<string?> showSettings,
        Action closeSettings)
    {
        _context = context;
        _startupService = startupService;
        _featureValidationService = featureValidationService;
        _showSettings = showSettings;
        _closeSettings = closeSettings;
    }

    public string Notice => _notice;

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

    public void Import()
    {
        _notice = "";

        var dialog = new OpenFileDialog
        {
            Filter = "KeyLine package (*.keyline)|*.keyline|All files (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog(_context.Owner) == true)
            ImportFiles(dialog.FileNames);
    }

    private void ImportFiles(IEnumerable<string> paths)
    {
        _notice = "";

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

                ApplyFeatureValidationToWorkspaces(package.Workspaces);
                ImportWorkspaces(package.Workspaces);
            }
            catch
            {
                _context.SetStatusText($"Import failed: {Path.GetFileName(path)}");
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
                _closeSettings();
            },
            () => _showSettings(ImportExportCategory),
            string.IsNullOrWhiteSpace(featureWarning) ? null : featureWarning));
    }

    private void PreviewEverythingImport(string path)
    {
        var snapshot = MacroStateStore.ImportSnapshot(path);
        if (snapshot == null || snapshot.Workspaces.Count == 0)
        {
            _context.SetStatusText($"Import failed: {Path.GetFileName(path)}");
            return;
        }

        if (ContainsProfileData(snapshot) && !_context.FeatureGate.IsEnabled(FeatureId.Profiles))
        {
            _context.SetStatusText(_context.FeatureGate.GetLockedFeatureMessage(FeatureId.Profiles));
            _showSettings(ImportExportCategory);
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
            () => _showSettings(ImportExportCategory),
            warningText,
            CreateSettingsPreview(snapshot)));
    }

    private void ShowImportConfirmation(UIElement view)
    {
        _context.ModalHost.Content = view;
        _context.ModalOverlay.Visibility = Visibility.Visible;
    }

    private void ApplyEverythingImport(string path, MacroStateSnapshot snapshot)
    {
        _context.CaptureActiveWorkspaceState();

        string backupPath;
        try
        {
            backupPath = MacroStateStore.CreateAutoBackupBeforeImport(
                _context.Workspaces.ToList(),
                _context.GetActiveWorkspaceIndex(),
                _context.GetShortcutsEnabled(),
                _context.Settings,
                _context.GetMainWindowWidth(),
                _context.Profiles.ToList(),
                _context.GetActiveProfileId());
        }
        catch
        {
            _context.SetStatusText("Import failed: could not create backup");
            _showSettings(ImportExportCategory);
            return;
        }

        _context.StopAllRunners();

        _context.Settings.CopyFrom(snapshot.Settings);
        _startupService.SetLaunchOnStartup(_context.Settings.LaunchOnWindowsStartup);

        _context.Workspaces.Clear();
        foreach (var workspace in snapshot.Workspaces)
            _context.Workspaces.Add(workspace);

        _context.Profiles.Clear();
        foreach (var profile in snapshot.Profiles)
            _context.Profiles.Add(profile);

        _context.ApplyMainWindowWidth(snapshot.MainWindowWidth);
        _context.ActivateWorkspace(Math.Clamp(snapshot.ActiveWorkspaceIndex, 0, _context.Workspaces.Count - 1), false);
        _context.SetShortcutsEnabled(snapshot.ShortcutsEnabled);
        _context.ApplySettings();
        _context.SaveStateNow();
        _notice = $"Notice: the old state is saved in the backups. ({Path.GetFileName(backupPath)})";
        _showSettings(ImportExportCategory);
    }

    private void ImportWorkspaces(IReadOnlyList<MacroWorkspace> imported)
    {
        if (imported.Count == 0)
            return;

        var featureWarning = ApplyFeatureValidationToWorkspaces(imported);
        _context.CaptureActiveWorkspaceState();

        var activeProfileId = _context.GetActiveProfileId();
        foreach (var workspace in imported)
        {
            workspace.ProfileId = activeProfileId;
            workspace.Name = WorkspaceNameService.GetUniqueName(
                _context.GetActiveProfileWorkspaces(),
                workspace.Name);
            ResetImportedTargetHandles(workspace);
            _context.Workspaces.Add(workspace);
        }

        _context.ActivateWorkspace(_context.Workspaces.Count - imported.Count, true);
        _context.ScheduleSaveState();
        if (!string.IsNullOrWhiteSpace(featureWarning))
            _context.SetStatusText(featureWarning);
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
        _context.CaptureActiveWorkspaceState();

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

            _context.Profiles.Add(profile);
            profileIdMap[sourceId] = profile.Id;
        }

        foreach (var workspace in importedWorkspaces)
        {
            var sourceProfileId = MacroProfile.NormalizeId(workspace.ProfileId);
            if (!profileIdMap.TryGetValue(sourceProfileId, out var targetProfileId))
                continue;

            workspace.ProfileId = targetProfileId;
            workspace.Name = WorkspaceNameService.GetUniqueName(
                _context.Workspaces.Where(existing => string.Equals(
                    MacroProfile.NormalizeId(existing.ProfileId),
                    targetProfileId,
                    StringComparison.OrdinalIgnoreCase)),
                workspace.Name);
            ResetImportedTargetHandles(workspace);

            _context.Workspaces.Add(workspace);
        }

        var firstImportedProfileId = profileIdMap.Values.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(firstImportedProfileId))
            _context.ActivateProfile(firstImportedProfileId);
        else
            _context.ScheduleSaveState();

        if (!string.IsNullOrWhiteSpace(featureWarning))
            _context.SetStatusText(featureWarning);
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

    private string GetUniqueProfileName(string name)
    {
        var baseName = string.IsNullOrWhiteSpace(name) ? "Profile" : name.Trim();
        var usedNames = _context.Profiles
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

    private bool CanImportProfiles()
    {
        if (_context.FeatureGate.IsEnabled(FeatureId.Profiles))
            return true;

        _context.SetStatusText(_context.FeatureGate.GetLockedFeatureMessage(FeatureId.Profiles));
        _showSettings(ImportExportCategory);
        return false;
    }

    private string ApplyFeatureValidationToWorkspaces(IEnumerable<MacroWorkspace> workspaces)
    {
        var message = _featureValidationService.ValidateAndMarkWorkspaces(workspaces);
        _notice = message;
        return message;
    }

    private static void ResetImportedTargetHandles(MacroWorkspace workspace)
    {
        workspace.TargetWindowHandle = 0;
        workspace.TargetWindowTitle = "";
        workspace.TargetChildWindowHandle = 0;
        workspace.TargetChildWindowTitle = "";
    }

    private static bool ContainsProfileData(MacroStateSnapshot snapshot)
    {
        return snapshot.Profiles.Count > 0 ||
               !MacroProfile.IsNoProfile(snapshot.ActiveProfileId) ||
               snapshot.Workspaces.Any(workspace =>
                   !MacroProfile.IsNoProfile(MacroProfile.NormalizeId(workspace.ProfileId)));
    }
}
