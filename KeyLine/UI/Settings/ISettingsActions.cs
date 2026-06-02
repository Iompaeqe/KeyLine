namespace KeyLine;

public interface ISettingsActions
{
    string CurrentVersionText { get; }

    void ApplySettings();
    void SetLaunchOnStartup(bool enabled);
    void ImportMacroFile();
    void ImportMultipleMacroFiles();
    void ExportSelectedMacro();
    void ExportAllMacros();
    void OpenMacroStorageFolder();
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