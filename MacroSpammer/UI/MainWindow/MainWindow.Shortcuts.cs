using MacroSpammer.Domain;
using MacroSpammer.Services.Input;

namespace MacroSpammer;

public partial class MainWindow
{
    private ShortcutController? _shortcutController;
    private readonly List<int> _capturedShortcutKeys = new();
    private readonly HashSet<int> _shortcutCaptureDownKeys = new();

    private void InitializeShortcuts()
    {
        _shortcutController = new ShortcutController(
            dispatcher: Dispatcher,
            getSettings: () => _settings,
            getWorkspaces: () => _workspaces,
            areMacroShortcutsEnabled: () => _shortcutsEnabled,
            toggleMacroFromShortcut: ToggleMacroFromShortcut,
            emergencyStop: StopAllPlaybackFromGlobalShortcut,
            pauseResumeAll: PauseResumeAllPlaybackFromGlobalShortcut);
    }

    private bool IsShortcutCaptureActive()
    {
        return _shortcutController?.IsCapturingShortcut == true;
    }

    private void SetShortcutCaptureActive(bool isActive)
    {
        _shortcutController?.SetCaptureActive(isActive);
    }

    private void ApplyShortcutHookState()
    {
        _shortcutController?.ApplyHookState();
    }

    private void StopGlobalShortcutHook()
    {
        _shortcutController?.StopGlobalShortcutHook();
    }

    private void SuppressCurrentlyHeldShortcutKeys(string shortcut)
    {
        _shortcutController?.SuppressCurrentlyHeldShortcutKeys(shortcut);
    }

    private void ToggleMacroFromShortcut(int workspaceIndex)
    {
        if (!_shortcutsEnabled || IsShortcutCaptureActive() || _recorder.IsRecording)
            return;

        if (workspaceIndex < 0 || workspaceIndex >= _workspaces.Count)
            return;

        CaptureActiveWorkspaceState();

        var workspace = _workspaces[workspaceIndex];

        if (IsWorkspaceRunning(workspace))
        {
            StopPlaybackFromShortcut(workspace);
            return;
        }

        StartWorkspacePlaybackFromShortcut(workspaceIndex);
    }

    private void StopPlaybackFromShortcut(MacroWorkspace workspace)
    {
        _restoreInputsOnStop = true;
        StopWorkspaceRunners(workspace);

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (AnyPlaybackRunning())
                return;

            SetStoppedStatus(true);
        }));
    }
}