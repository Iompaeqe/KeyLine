namespace MacroSpammer;

public interface ISettingsActions
{
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
}
