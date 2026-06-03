namespace KeyLine;

public interface ISettingsActions
{
    string CurrentVersionText { get; }
    string ImportExportNotice { get; }

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

    Task<UpdateCheckResult> CheckForUpdatesAsync();
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
