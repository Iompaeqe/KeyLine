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
            getActiveWorkspace: () => _activeWorkspace,
            createWorkspace: (number, settings) => CreateWorkspace(number, settings),
            captureActiveWorkspaceState: CaptureActiveWorkspaceState,
            activateWorkspace: ActivateWorkspace,
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