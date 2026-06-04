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
            featureGate: _featureGate,
            getActiveWorkspace: () => _activeWorkspace,
            getActiveProfileId: () => _activeProfileId,
            getActiveProfileWorkspaces: GetActiveProfileWorkspaces,
            getActiveWorkspaceIndex: () => _activeWorkspaceIndex,
            getShortcutsEnabled: AnyMacroShortcutEnabled,
            getMainWindowWidth: GetPersistedMainWindowWidth,
            createWorkspace: (number, settings) => CreateWorkspace(number, settings),
            captureActiveWorkspaceState: CaptureActiveWorkspaceState,
            activateWorkspace: ActivateWorkspace,
            activateProfile: ActivateProfile,
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

        _settingsModalController.UpdateCheckCompleted += SettingsModalController_UpdateCheckCompleted;
        UpdateSettingsUpdateBadge(_settingsModalController.LastUpdateCheckResult);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        CloseInspector();
        _settingsModalController?.Show(IsUpdateAvailable() ? "About" : null);
    }

    private void CloseSettingsModal()
    {
        _settingsModalController?.Close();
    }

    private bool IsSettingsModalOpen()
    {
        return _settingsModalController?.IsOpen == true;
    }

    private void SettingsModalController_UpdateCheckCompleted(object? sender, UpdateCheckResult result)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => SettingsModalController_UpdateCheckCompleted(sender, result)));
            return;
        }

        UpdateSettingsUpdateBadge(result);
    }

    private bool IsUpdateAvailable()
    {
        return _settingsModalController?.LastUpdateCheckResult?.State == UpdateCheckState.UpdateAvailable;
    }

    private void UpdateSettingsUpdateBadge(UpdateCheckResult? result)
    {
        var isAvailable = result?.State == UpdateCheckState.UpdateAvailable;
        TitleCommandButtons.SetSettingsUpdateAvailable(isAvailable, isAvailable ? result?.ButtonText : null);
    }

    private async void BeginSettingsUpdateCheck()
    {
        if (_settingsModalController == null)
            return;

        try
        {
            await _settingsModalController.CheckForUpdatesAsync();
        }
        catch
        {
            // CheckForUpdatesAsync reports failures through its result; this only protects startup.
        }
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        _settingsModalController?.HandleFileDrop(e);
    }
}
