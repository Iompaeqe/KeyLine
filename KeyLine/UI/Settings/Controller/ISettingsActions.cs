using KeyLine.Services.Features;

namespace KeyLine;

public interface ISettingsActions
{
    string CurrentVersionText { get; }
    string ImportExportNotice { get; }
    UpdateCheckResult? LastUpdateCheckResult { get; }
    event EventHandler<UpdateCheckResult>? UpdateCheckCompleted;

    void ApplySettings();
    void SetLaunchOnStartup(bool enabled);
    void Import();
    void ExportMacro();
    void ExportProfile();
    void ExportEverything();
    void OpenBackups();
    void PreviewPlaybackSound();
    void ResetDefaults();
    void ResetSettings();
    void ResetAllSavedData();
    void SetShortcutCaptureActive(bool isActive);
    bool IsFeatureVisible(FeatureId feature);
    bool IsFeatureLocked(FeatureId feature);
    string GetLockedFeatureMessage(FeatureId feature);

    Task<UpdateCheckResult> CheckForUpdatesAsync(bool forceRefresh = false);
    void OpenReleasesPage(string? releaseUrl = null);
}

public enum UpdateCheckState
{
    Latest,
    UpdateAvailable,
    Failed
}

public sealed record UpdateCheckResult(
    UpdateCheckState State,
    string ButtonText,
    string? ReleaseUrl = null);
