using System.Text;
using KeyLine.Domain;
using KeyLine.Interop;
using KeyLine.Services.Features;
using KeyLine.Services.Input;

namespace KeyLine;

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
            getWorkspaces: GetActiveProfileWorkspaces,
            areMacroShortcutsEnabled: AnyActiveProfileMacroShortcutEnabled,
            toggleMacroFromShortcut: ToggleMacroFromShortcut,
            canRunRemapMacroFromShortcut: CanRunRemapMacroFromShortcut,
            startRemapMacroFromShortcut: StartRemapMacroFromShortcut,
            emergencyStop: StopAllPlaybackFromGlobalShortcut,
            pauseResumeAll: PauseResumeAllPlaybackFromGlobalShortcut,
            toggleGlobalRemap: ToggleGlobalRemapFromShortcut);
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
        UpdateGlobalRemapToggle();
    }

    private void StopGlobalShortcutHook()
    {
        _shortcutController?.StopGlobalShortcutHook();
    }

    private void SuppressCurrentlyHeldShortcutKeys(string shortcut)
    {
        _shortcutController?.SuppressCurrentlyHeldShortcutKeys(shortcut);
    }

    private void ToggleMacroFromShortcut(int profileWorkspaceIndex)
    {
        if (IsShortcutCaptureActive() || _recorder.IsRecording)
            return;

        var workspaceIndex = GetGlobalWorkspaceIndexFromActiveProfileIndex(profileWorkspaceIndex);
        if (workspaceIndex < 0 || workspaceIndex >= _workspaces.Count)
            return;

        CaptureActiveWorkspaceState();

        var workspace = _workspaces[workspaceIndex];
        if (!IsWorkspaceInProfile(workspace, _activeProfileId) ||
            !HasEnabledMacroShortcut(workspace))
        {
            return;
        }

        if (IsWorkspaceRunning(workspace))
        {
            StopPlaybackFromShortcut(workspace);
            return;
        }

        StartWorkspacePlaybackFromShortcut(workspaceIndex);
    }

    private bool AnyActiveProfileMacroShortcutEnabled()
    {
        return GetActiveProfileWorkspaces().Any(HasEnabledMacroShortcut);
    }

    private bool AnyMacroShortcutEnabled()
    {
        return _workspaces.Any(HasEnabledMacroShortcut);
    }

    private static bool HasEnabledMacroShortcut(MacroWorkspace workspace)
    {
        return workspace.ShortcutsEnabled &&
               !string.IsNullOrWhiteSpace(workspace.ShortcutKeys) &&
               (workspace.ShortcutTriggerBehavior != ShortcutTriggerBehavior.RemapConsume ||
                ShortcutGesture.IsSingleKeyboardKeyShortcut(workspace.ShortcutKeys));
    }

    private bool CanRunRemapMacroFromShortcut(int profileWorkspaceIndex)
    {
        if (!_featureGate.IsEnabled(FeatureId.ShortcutRemap) ||
            !_settings.GlobalRemapEnabled ||
            IsShortcutCaptureActive() ||
            _recorder.IsRecording)
        {
            return false;
        }

        var workspaceIndex = GetGlobalWorkspaceIndexFromActiveProfileIndex(profileWorkspaceIndex);
        if (workspaceIndex < 0 || workspaceIndex >= _workspaces.Count)
            return false;

        var workspace = _workspaces[workspaceIndex];
        if (!IsWorkspaceInProfile(workspace, _activeProfileId) ||
            !HasEnabledMacroShortcut(workspace) ||
            workspace.ShortcutTriggerBehavior != ShortcutTriggerBehavior.RemapConsume ||
            IsWorkspaceRunning(workspace))
        {
            return false;
        }

        if (!workspace.Document.Timelines.Any(timeline => timeline.Nodes.Count > 0))
            return false;

        if (!_macroFeatureValidator.ValidateWorkspace(workspace).CanRun)
            return false;

        return IsWorkspaceTargetFocused(workspace);
    }

    private void StartRemapMacroFromShortcut(int profileWorkspaceIndex)
    {
        if (!CanRunRemapMacroFromShortcut(profileWorkspaceIndex))
            return;

        var workspaceIndex = GetGlobalWorkspaceIndexFromActiveProfileIndex(profileWorkspaceIndex);
        if (workspaceIndex < 0)
            return;

        CaptureActiveWorkspaceState();
        StartWorkspacePlaybackFromShortcut(workspaceIndex);
    }

    private static bool IsWorkspaceTargetFocused(MacroWorkspace workspace)
    {
        var foregroundWindow = NativeMethods.GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
            return false;

        if (IsWorkspaceTargetHandleFocused(workspace, foregroundWindow))
            return true;

        var foregroundTitle = GetWindowTitle(foregroundWindow);
        if (string.IsNullOrWhiteSpace(foregroundTitle))
            return false;

        if (!string.IsNullOrWhiteSpace(workspace.TargetWindowTitle) &&
            string.Equals(foregroundTitle, workspace.TargetWindowTitle, StringComparison.Ordinal))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(workspace.TargetWindowSearchName) &&
               foregroundTitle.Contains(workspace.TargetWindowSearchName.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWorkspaceTargetHandleFocused(MacroWorkspace workspace, IntPtr foregroundWindow)
    {
        if (workspace.TargetWindowHandle > 0)
        {
            var targetWindow = new IntPtr(workspace.TargetWindowHandle);
            if (NativeMethods.IsWindow(targetWindow) &&
                GetRootWindow(targetWindow) == foregroundWindow)
            {
                return true;
            }
        }

        if (workspace.TargetChildWindowHandle <= 0)
            return false;

        var targetChildWindow = new IntPtr(workspace.TargetChildWindowHandle);
        return NativeMethods.IsWindow(targetChildWindow) &&
               GetRootWindow(targetChildWindow) == foregroundWindow;
    }

    private static IntPtr GetRootWindow(IntPtr window)
    {
        var root = NativeMethods.GetAncestor(window, NativeMethods.GA_ROOT);
        return root == IntPtr.Zero ? window : root;
    }

    private static string GetWindowTitle(IntPtr window)
    {
        var builder = new StringBuilder(256);
        NativeMethods.GetWindowText(window, builder, builder.Capacity);
        return builder.ToString().Trim();
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
