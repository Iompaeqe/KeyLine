using System.Windows;

namespace KeyLine;

public partial class MainWindow
{
    private SettingsController? _settingsModalController;

    private void InitializeSettingsModal()
    {
        _settingsModalController = new SettingsController(
            owner: this,
            settings: _settings,
            modalHost: SettingsModalHost,
            modalOverlay: SettingsModalOverlay,
            workspaces: _workspaces,
            profiles: _profiles,
            getActiveWorkspace: () => _activeWorkspace,
            getActiveProfileId: () => _activeProfileId,
            getActiveProfileWorkspaces: GetActiveProfileWorkspaces,
            getActiveWorkspaceIndex: () => _activeWorkspaceIndex,
            getShortcutsEnabled: AnyMacroShortcutEnabled,
            getMainWindowWidth: GetPersistedMainWindowWidth,
            createWorkspace: (number, settings) => CreateWorkspace(number, settings),
            captureActiveWorkspaceState: CaptureActiveWorkspaceState,
            activateWorkspace: ActivateWorkspace,
            resetProfiles: ResetProfiles,
            setShortcutsEnabled: value =>
            {
                if (value && !_workspaces.Any(workspace => workspace.ShortcutsEnabled))
                {
                    foreach (var workspace in _workspaces.Where(workspace =>
                                 !string.IsNullOrWhiteSpace(workspace.ShortcutKeys)))
                    {
                        workspace.ShortcutsEnabled = true;
                    }
                }
                else if (!value)
                {
                    foreach (var workspace in _workspaces)
                        workspace.ShortcutsEnabled = false;
                }

                ApplyShortcutHookState();
                UpdateShortcutText();
            },
            applyMainWindowWidth: ApplySavedMainWindowWidth,
            updateExperimentalAddMenuVisibility: UpdateExperimentalAddMenuVisibility,
            applyShortcutHookState: ApplyShortcutHookState,
            scheduleSaveState: ScheduleSaveState,
            setShortcutCaptureActive: SetShortcutCaptureActive,
            stopAllRunners: StopAllRunners,
            saveStateNow: SaveStateNow,
            setStatusText: text => StatusText.Text = text,
            previewPlaybackSound: PreviewPlaybackSound);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        CloseInspector();
        _settingsModalController?.Show();
    }

    private void CloseSettingsModal()
    {
        _settingsModalController?.Close();
    }

    private bool IsSettingsModalOpen()
    {
        return _settingsModalController?.IsOpen == true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        _settingsModalController?.HandleFileDrop(e);
    }
}
