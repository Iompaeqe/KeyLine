using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using KeyLine.Domain;
using KeyLine.Services.Features;

namespace KeyLine;

public sealed class SettingsController : ISettingsActions
{
    private readonly SettingsControllerContext _context;
    private readonly SettingsStartupService _startupService;
    private readonly SettingsUpdateService _updateService;
    private readonly SettingsImportService _importService;
    private readonly SettingsExportService _exportService;
    private readonly SettingsResetService _resetService;
    private readonly Action<bool> _setShortcutCaptureActive;

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
        _context = new SettingsControllerContext(
            owner,
            settings,
            modalHost,
            modalOverlay,
            workspaces,
            profiles,
            featureGate,
            getActiveWorkspace,
            getActiveProfileId,
            getActiveProfileWorkspaces,
            getActiveWorkspaceIndex,
            getShortcutsEnabled,
            getMainWindowWidth,
            createWorkspace,
            captureActiveWorkspaceState,
            activateWorkspace,
            activateProfile,
            resetProfiles,
            setShortcutsEnabled,
            applyMainWindowWidth,
            updateExperimentalAddMenuVisibility,
            applyShortcutHookState,
            scheduleSaveState,
            stopAllRunners,
            saveStateNow,
            setStatusText,
            previewPlaybackSound);

        _startupService = new SettingsStartupService();
        _updateService = new SettingsUpdateService();
        _setShortcutCaptureActive = setShortcutCaptureActive;

        var featureValidationService = new SettingsFeatureValidationService(featureGate);
        _importService = new SettingsImportService(
            _context,
            _startupService,
            featureValidationService,
            Show,
            Close);
        _exportService = new SettingsExportService(_context, Show);
        _resetService = new SettingsResetService(_context, _startupService);
    }

    public bool IsOpen => _context.ModalOverlay.Visibility == Visibility.Visible;

    public string CurrentVersionText => _updateService.CurrentVersionText;

    public string ImportExportNotice => _importService.Notice;

    public UpdateCheckResult? LastUpdateCheckResult => _updateService.LastUpdateCheckResult;

    public event EventHandler<UpdateCheckResult>? UpdateCheckCompleted
    {
        add => _updateService.UpdateCheckCompleted += value;
        remove => _updateService.UpdateCheckCompleted -= value;
    }

    public bool IsFeatureVisible(FeatureId feature) => _context.FeatureGate.IsVisible(feature);

    public bool IsFeatureLocked(FeatureId feature) => _context.FeatureGate.IsLocked(feature);

    public string GetLockedFeatureMessage(FeatureId feature) => _context.FeatureGate.GetLockedFeatureMessage(feature);

    public void Show(string? initialCategory = null)
    {
        _context.ModalHost.Content = new SettingsView(_context.Settings, this, initialCategory)
        {
            CloseRequested = Close
        };

        _context.ModalOverlay.Visibility = Visibility.Visible;
    }

    public void Close()
    {
        _context.ModalOverlay.Visibility = Visibility.Collapsed;
        _context.ModalHost.Content = null;
    }

    public void SetShortcutCaptureActive(bool isActive)
    {
        _setShortcutCaptureActive(isActive);
    }

    public void HandleFileDrop(DragEventArgs e)
    {
        _importService.HandleFileDrop(e);
    }

    public void ApplySettings()
    {
        _context.ApplySettings();
    }

    public void SetLaunchOnStartup(bool enabled)
    {
        _startupService.SetLaunchOnStartup(enabled);
    }

    public void Import()
    {
        _importService.Import();
    }

    public void ExportMacro()
    {
        _exportService.ExportMacro();
    }

    public void ExportProfile()
    {
        _exportService.ExportProfile();
    }

    public void ExportEverything()
    {
        _exportService.ExportEverything();
    }

    public void OpenBackups()
    {
        _exportService.OpenBackups();
    }

    public void PreviewPlaybackSound()
    {
        _context.PreviewPlaybackSound();
    }

    public void ResetDefaults()
    {
        _resetService.ResetDefaults();
    }

    public void ResetSettings()
    {
        _resetService.ResetSettings();
    }

    public void ResetAllSavedData()
    {
        _resetService.ResetAllSavedData();
    }

    public Task<UpdateCheckResult> CheckForUpdatesAsync(bool forceRefresh = false)
    {
        return _updateService.CheckForUpdatesAsync(forceRefresh);
    }

    public void OpenReleasesPage(string? releaseUrl = null)
    {
        _updateService.OpenReleasesPage(releaseUrl);
    }
}
