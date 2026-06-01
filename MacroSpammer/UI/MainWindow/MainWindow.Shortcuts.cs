using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MacroSpammer.Domain;
using MacroSpammer.Interop;
using MacroSpammer.Services.Input;

namespace MacroSpammer;

public partial class MainWindow
{
    private readonly HashSet<int> _globalPressedShortcutKeys = new();
    private readonly HashSet<int> _suppressedShortcutKeys = new();
    private readonly List<int> _capturedShortcutKeys = new();
    private readonly HashSet<int> _shortcutCaptureDownKeys = new();
    private NativeMethods.LowLevelKeyboardProc? _shortcutKeyboardProc;
    private IntPtr _shortcutKeyboardHook;
    private string _triggeredShortcutSignature = "";
    private bool _isCapturingShortcut;

    private void StartGlobalShortcutHook()
    {
        if (!ShouldRunGlobalShortcutHook())
            return;

        StopGlobalShortcutHook();

        var moduleHandle = NativeMethods.GetModuleHandle(null);
        _shortcutKeyboardProc = (code, wParam, lParam) =>
        {
            if (code >= 0)
                HandleGlobalShortcutKeyMessage(wParam, lParam);

            return NativeMethods.CallNextHookEx(_shortcutKeyboardHook, code, wParam, lParam);
        };

        _shortcutKeyboardHook = NativeMethods.SetWindowsHookExKeyboard(
            NativeMethods.WH_KEYBOARD_LL,
            _shortcutKeyboardProc,
            moduleHandle,
            0);

        if (_shortcutKeyboardHook == IntPtr.Zero)
            _shortcutKeyboardProc = null;
    }

    private void StopGlobalShortcutHook()
    {
        if (_shortcutKeyboardHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_shortcutKeyboardHook);
            _shortcutKeyboardHook = IntPtr.Zero;
        }

        _shortcutKeyboardProc = null;
        _globalPressedShortcutKeys.Clear();
        _suppressedShortcutKeys.Clear();
        _triggeredShortcutSignature = "";
    }

    private void HandleGlobalShortcutKeyMessage(IntPtr wParam, IntPtr lParam)
    {
        if (_isCapturingShortcut)
            return;

        var hookData = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
        var virtualKey = ShortcutGesture.NormalizeVirtualKey((int)hookData.vkCode);
        if (virtualKey <= 0)
            return;

        var message = wParam.ToInt32();
        if (message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN)
        {
            if (_suppressedShortcutKeys.Remove(virtualKey))
                return;

            _globalPressedShortcutKeys.Add(virtualKey);
            TryTriggerShortcut();
        }
        else if (message is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP)
        {
            _globalPressedShortcutKeys.Remove(virtualKey);
            _suppressedShortcutKeys.Remove(virtualKey);
            _triggeredShortcutSignature = "";
        }
    }

    private void TryTriggerShortcut()
    {
        if (TryTriggerPlaybackShortcut())
            return;

        if (!_shortcutsEnabled)
            return;

        var matchIndex = FindMatchingShortcutWorkspaceIndex();
        if (matchIndex < 0)
            return;

        var shortcutKeys = ShortcutGesture.Parse(_workspaces[matchIndex].ShortcutKeys);
        var signature = $"{matchIndex}:{ShortcutGesture.Serialize(shortcutKeys)}";
        if (_triggeredShortcutSignature == signature)
            return;

        _triggeredShortcutSignature = signature;
        Dispatcher.BeginInvoke(new Action(() => ToggleMacroFromShortcut(matchIndex)));
    }

    private bool TryTriggerPlaybackShortcut()
    {
        if (TryTriggerSettingsShortcut(_settings.EmergencyStopShortcut, "emergency-stop", StopAllPlaybackFromGlobalShortcut))
            return true;

        return TryTriggerSettingsShortcut(_settings.PauseResumeAllMacrosShortcut, "pause-resume", PauseResumeAllPlaybackFromGlobalShortcut);
    }

    private bool TryTriggerSettingsShortcut(string shortcut, string name, Action action)
    {
        var shortcutKeys = ShortcutGesture.Parse(shortcut);
        if (!ShortcutGesture.Matches(_globalPressedShortcutKeys, shortcutKeys))
            return false;

        var signature = $"settings:{name}:{ShortcutGesture.Serialize(shortcutKeys)}";
        if (_triggeredShortcutSignature == signature)
            return true;

        _triggeredShortcutSignature = signature;
        Dispatcher.BeginInvoke(new Action(action));
        return true;
    }

    private bool ShouldRunGlobalShortcutHook() =>
        _shortcutsEnabled ||
        ShortcutGesture.Parse(_settings.EmergencyStopShortcut).Length > 0 ||
        ShortcutGesture.Parse(_settings.PauseResumeAllMacrosShortcut).Length > 0;

    private int FindMatchingShortcutWorkspaceIndex()
    {
        for (var i = 0; i < _workspaces.Count; i++)
        {
            var shortcutKeys = ShortcutGesture.Parse(_workspaces[i].ShortcutKeys);
            if (ShortcutGesture.Matches(_globalPressedShortcutKeys, shortcutKeys))
                return i;
        }

        return -1;
    }

    private void ToggleMacroFromShortcut(int workspaceIndex)
    {
        if (!_shortcutsEnabled || _isCapturingShortcut || _recorder.IsRecording)
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
            if (_runners.Values.Any(runner => runner.IsRunning))
                return;

            SetStoppedStatus(true);
        }));
    }

    private void ShortcutToggleTextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _shortcutsEnabled = !_shortcutsEnabled;
        ApplyShortcutHookState();
        UpdateShortcutToggleText();
        ScheduleSaveState();
        e.Handled = true;
    }

    private void ApplyShortcutHookState()
    {
        if (ShouldRunGlobalShortcutHook())
            StartGlobalShortcutHook();
        else
            StopGlobalShortcutHook();
    }

    private void ShortcutTextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isShortcutClearConfirmationActive)
        {
            CommitShortcutCapture(Array.Empty<int>());
            ResetShortcutClearConfirmation();
            e.Handled = true;
            return;
        }

        BeginShortcutCapture();
        e.Handled = true;
    }

    private void ShortcutBorder_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_activeWorkspace.ShortcutKeys))
            return;

        if (_isShortcutClearConfirmationActive)
        {
            CommitShortcutCapture(Array.Empty<int>());
            ResetShortcutClearConfirmation();
        }
        else
        {
            BeginShortcutClearConfirmation();
        }

        e.Handled = true;
    }

    private void BeginShortcutClearConfirmation()
    {
        _isShortcutClearConfirmationActive = true;
        CancelShortcutCapture();
        
        ShortcutTextBlock.Text = "clear? confirm";
        ShortcutTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(254, 202, 202));
        ShortcutBorder.Background = new SolidColorBrush(Color.FromRgb(127, 29, 29));
        ShortcutBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(248, 113, 113));
    }

    private void ResetShortcutClearConfirmation()
    {
        _isShortcutClearConfirmationActive = false;
        ShortcutBorder.ClearValue(BackgroundProperty);
        ShortcutBorder.ClearValue(BorderBrushProperty);
        UpdateShortcutText();
    }

    private void ShortcutTextBlock_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isCapturingShortcut)
            return;

        e.Handled = true;

        if (e.Key == Key.Escape)
        {
            CancelShortcutCapture();
            return;
        }

        if (e.Key is Key.Back or Key.Delete)
        {
            CommitShortcutCapture(Array.Empty<int>());
            return;
        }

        if (e.Key == Key.Enter)
        {
            CommitShortcutCapture(_capturedShortcutKeys);
            return;
        }

        var virtualKey = GetVirtualKeyFromKeyEvent(e);
        if (virtualKey <= 0)
            return;

        _shortcutCaptureDownKeys.Add(virtualKey);

        if (!_capturedShortcutKeys.Contains(virtualKey) &&
            _capturedShortcutKeys.Count < ShortcutGesture.MaxKeyCount)
        {
            _capturedShortcutKeys.Add(virtualKey);
        }

        UpdateShortcutCaptureText();
    }

    private void ShortcutTextBlock_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (!_isCapturingShortcut)
            return;

        e.Handled = true;

        var virtualKey = GetVirtualKeyFromKeyEvent(e);
        if (virtualKey > 0)
            _shortcutCaptureDownKeys.Remove(virtualKey);

        if (_capturedShortcutKeys.Count > 0 && _shortcutCaptureDownKeys.Count == 0)
            CommitShortcutCapture(_capturedShortcutKeys);
    }

    private void ShortcutTextBlock_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_isShortcutClearConfirmationActive)
        {
            ResetShortcutClearConfirmation();
            return;
        }

        if (!_isCapturingShortcut)
            return;

        if (_capturedShortcutKeys.Count > 0)
            CommitShortcutCapture(_capturedShortcutKeys);
        else
            CancelShortcutCapture();
    }

    private void BeginShortcutCapture()
    {
        ResetShortcutClearConfirmation();
        _isCapturingShortcut = true;
        _capturedShortcutKeys.Clear();
        _shortcutCaptureDownKeys.Clear();
        ShortcutTextBlock.Text = "press shortcut";
        ShortcutTextBlock.Foreground = (SolidColorBrush)FindResource("Cyan");
        ShortcutPill.Focus();
    }

    private void CommitShortcutCapture(IEnumerable<int> virtualKeys)
    {
        if (virtualKeys.Any())
        {
            _activeWorkspace.ShortcutKeys = ShortcutGesture.Serialize(virtualKeys);
            if (!_shortcutsEnabled)
            {
                _shortcutsEnabled = true;
                ApplyShortcutHookState();
                SuppressCurrentlyHeldShortcutKeys(_activeWorkspace.ShortcutKeys);
            }
        }
        else
        {
            _activeWorkspace.ShortcutKeys = "";
            _shortcutsEnabled = false;
            ApplyShortcutHookState();
        }

        _isCapturingShortcut = false;
        _capturedShortcutKeys.Clear();
        _shortcutCaptureDownKeys.Clear();
        UpdateShortcutText();
        Keyboard.ClearFocus();
        ScheduleSaveState();
    }

    private void SuppressCurrentlyHeldShortcutKeys(string shortcut)
    {
        foreach (var virtualKey in ShortcutGesture.Parse(shortcut))
            _suppressedShortcutKeys.Add(virtualKey);
    }

    private void CancelShortcutCapture()
    {
        _isCapturingShortcut = false;
        _capturedShortcutKeys.Clear();
        _shortcutCaptureDownKeys.Clear();
        UpdateShortcutText();
        Keyboard.ClearFocus();
    }

    private void UpdateShortcutText()
    {
        if (ShortcutTextBlock == null)
            return;

        ShortcutTextBlock.Text = ShortcutGesture.Format(_activeWorkspace.ShortcutKeys);
        ShortcutTextBlock.ClearValue(ForegroundProperty);
        UpdateShortcutToggleText();
    }

    private void UpdateShortcutToggleText()
    {
        if (ShortcutToggleTextBlock == null)
            return;

        ShortcutToggleTextBlock.Text = _shortcutsEnabled ? "on" : "off";
        if (_shortcutsEnabled)
        {
            ShortcutToggleTextBlock.Foreground = (SolidColorBrush)FindResource("Cyan");
        }
        else
        {
            ShortcutToggleTextBlock.ClearValue(ForegroundProperty);
        }
    }

    private void UpdateShortcutCaptureText()
    {
        ShortcutTextBlock.Text = _capturedShortcutKeys.Count == 0
            ? "press shortcut"
            : ShortcutGesture.Format(_capturedShortcutKeys);
    }

    private static int GetVirtualKeyFromKeyEvent(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        return ShortcutGesture.NormalizeVirtualKey(KeyInterop.VirtualKeyFromKey(key));
    }
}
