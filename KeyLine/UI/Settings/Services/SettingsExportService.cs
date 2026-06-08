using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using KeyLine.Domain;
using KeyLine.Services.Features;
using KeyLine.Services.Macro;
using Microsoft.Win32;

namespace KeyLine;

internal sealed class SettingsExportService
{
    private const string ImportExportCategory = "Import/Export";

    private readonly SettingsControllerContext _context;
    private readonly Action<string?> _showSettings;

    public SettingsExportService(SettingsControllerContext context, Action<string?> showSettings)
    {
        _context = context;
        _showSettings = showSettings;
    }

    public void ExportMacro()
    {
        var exportableWorkspaces = _context.FeatureGate.IsEnabled(FeatureId.Profiles)
            ? _context.Workspaces.ToList()
            : _context.Workspaces
                .Where(workspace => MacroProfile.IsNoProfile(MacroProfile.NormalizeId(workspace.ProfileId)))
                .ToList();

        if (exportableWorkspaces.Count == 1)
        {
            ExportWorkspaces(new[] { exportableWorkspaces[0] });
            return;
        }

        _context.ModalHost.Content = new ExportMacroSelectionView(
            exportableWorkspaces,
            _context.FeatureGate.IsEnabled(FeatureId.Profiles) ? _context.Profiles.ToList() : new List<MacroProfile>(),
            _context.GetActiveWorkspace(),
            selected =>
            {
                if (selected.Count > 0)
                    ExportWorkspaces(selected);

                _showSettings(ImportExportCategory);
            },
            () => _showSettings(ImportExportCategory));
    }

    public void ExportProfile()
    {
        if (!_context.FeatureGate.IsEnabled(FeatureId.Profiles))
        {
            _context.SetStatusText(_context.FeatureGate.GetLockedFeatureMessage(FeatureId.Profiles));
            return;
        }

        if (_context.Profiles.Count == 0)
        {
            _context.SetStatusText("No profiles to export");
            return;
        }

        var activeProfileId = MacroProfile.NormalizeId(_context.GetActiveProfileId());
        var selectedProfiles = _context.Profiles
            .Where(profile => string.Equals(profile.Id, activeProfileId, StringComparison.OrdinalIgnoreCase))
            .Cast<object>()
            .ToList();

        if (selectedProfiles.Count == 0)
            selectedProfiles.Add(_context.Profiles[0]);

        _context.ModalHost.Content = new ExportMacroSelectionView(
            "Export profiles",
            _context.Profiles.Cast<object>().ToList(),
            selectedProfiles,
            selected =>
            {
                var profiles = selected.OfType<MacroProfile>().ToList();
                if (profiles.Count > 0)
                    ExportProfiles(profiles);

                _showSettings(ImportExportCategory);
            },
            () => _showSettings(ImportExportCategory));
    }

    public void ExportEverything()
    {
        _context.CaptureActiveWorkspaceState();

        var dialog = new SaveFileDialog
        {
            Filter = "KeyLine package (*.keyline)|*.keyline",
            FileName = "KeyLine EVERYTHING.keyline"
        };

        if (dialog.ShowDialog(_context.Owner) != true)
            return;

        MacroStateStore.ExportSnapshot(
            dialog.FileName,
            _context.Workspaces.ToList(),
            _context.GetActiveWorkspaceIndex(),
            _context.GetShortcutsEnabled(),
            _context.Settings,
            _context.GetMainWindowWidth(),
            _context.Profiles.ToList(),
            _context.GetActiveProfileId());
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

    private void ExportWorkspaces(IReadOnlyList<MacroWorkspace> selectedWorkspaces)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "KeyLine macro (*.keyline)|*.keyline",
            FileName = selectedWorkspaces.Count == 1
                ? $"{SanitizeFileName(selectedWorkspaces[0].Name)}.keyline"
                : "Selected KeyLine macros.keyline"
        };

        if (dialog.ShowDialog(_context.Owner) == true)
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

        if (dialog.ShowDialog(_context.Owner) == true)
            MacroFileStore.ExportProfiles(dialog.FileName, selectedProfiles, _context.Workspaces);
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');

        return string.IsNullOrWhiteSpace(name) ? "Macro" : name;
    }
}
