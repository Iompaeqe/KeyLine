using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MacroSpammer.Interop;
using MacroSpammer.Services.Input;

namespace MacroSpammer;

public partial class MainWindow
{
    private readonly HashSet<int> _globalPressedShortcutKeys = new();
    private readonly List<int> _capturedShortcutKeys = new();
    private readonly HashSet<int> _shortcutCaptureDownKeys = new();
    private NativeMethods.LowLevelKeyboardProc? _shortcutKeyboardProc;
    private IntPtr _shortcutKeyboardHook;
    private string _triggeredShortcutSignature = "";
    private bool _isCapturingShortcut;

    private void StartGlobalShortcutHook()
    {
        if (!_shortcutsEnabled)
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
            _globalPressedShortcutKeys.Add(virtualKey);
            TryTriggerShortcut();
        }
        else if (message is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP)
        {
            _globalPressedShortcutKeys.Remove(virtualKey);
            _triggeredShortcutSignature = "";
        }
    }

    private void TryTriggerShortcut()
    {
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
        Dispatcher.BeginInvoke(new Action(() => StartMacroFromShortcut(matchIndex)));
    }

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

    private void StartMacroFromShortcut(int workspaceIndex)
    {
        if (!_shortcutsEnabled || _isCapturingShortcut || _recorder.IsRecording)
            return;

        if (_runners.Values.Any(runner => runner.IsRunning))
            return;

        if (workspaceIndex != _activeWorkspaceIndex)
            ActivateWorkspace(workspaceIndex);

        StartStopButton_Click(this, new RoutedEventArgs());
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
        if (_shortcutsEnabled)
            StartGlobalShortcutHook();
        else
            StopGlobalShortcutHook();
    }

    private void ShortcutTextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        BeginShortcutCapture();
        e.Handled = true;
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
        if (!_isCapturingShortcut)
            return;

        if (_capturedShortcutKeys.Count > 0)
            CommitShortcutCapture(_capturedShortcutKeys);
        else
            CancelShortcutCapture();
    }

    private void BeginShortcutCapture()
    {
        _isCapturingShortcut = true;
        _capturedShortcutKeys.Clear();
        _shortcutCaptureDownKeys.Clear();
        ShortcutTextBlock.Text = "press shortcut";
        ShortcutTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
        ShortcutTextBlock.Focus();
    }

    private void CommitShortcutCapture(IEnumerable<int> virtualKeys)
    {
        _activeWorkspace.ShortcutKeys = ShortcutGesture.Serialize(virtualKeys);
        _isCapturingShortcut = false;
        _capturedShortcutKeys.Clear();
        _shortcutCaptureDownKeys.Clear();
        UpdateShortcutText();
        ScheduleSaveState();
    }

    private void CancelShortcutCapture()
    {
        _isCapturingShortcut = false;
        _capturedShortcutKeys.Clear();
        _shortcutCaptureDownKeys.Clear();
        UpdateShortcutText();
    }

    private void UpdateShortcutText()
    {
        if (ShortcutTextBlock == null)
            return;

        ShortcutTextBlock.Text = ShortcutGesture.Format(_activeWorkspace.ShortcutKeys);
        ShortcutTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(142, 160, 182));
        UpdateShortcutToggleText();
    }

    private void UpdateShortcutToggleText()
    {
        if (ShortcutToggleTextBlock == null)
            return;

        ShortcutToggleTextBlock.Text = _shortcutsEnabled ? "on" : "off";
        ShortcutToggleTextBlock.Foreground = new SolidColorBrush(_shortcutsEnabled
            ? Color.FromRgb(52, 211, 153)
            : Color.FromRgb(142, 160, 182));
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
