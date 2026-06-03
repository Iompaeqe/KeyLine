using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using KeyLine.Domain;
using KeyLine.Interop;

namespace KeyLine.Services.Input;

public sealed class ShortcutController : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Func<AppSettings> _getSettings;
    private readonly Func<IReadOnlyList<MacroWorkspace>> _getWorkspaces;
    private readonly Func<bool> _areMacroShortcutsEnabled;
    private readonly Action<int> _toggleMacroFromShortcut;
    private readonly Action _emergencyStop;
    private readonly Action _pauseResumeAll;

    private readonly HashSet<int> _globalPressedKeys = new();
    private readonly HashSet<int> _suppressedKeys = new();

    private NativeMethods.LowLevelKeyboardProc? _keyboardProc;
    private IntPtr _keyboardHook;
    private string _triggeredShortcutSignature = "";

    public bool IsCapturingShortcut { get; private set; }

    public ShortcutController(
        Dispatcher dispatcher,
        Func<AppSettings> getSettings,
        Func<IReadOnlyList<MacroWorkspace>> getWorkspaces,
        Func<bool> areMacroShortcutsEnabled,
        Action<int> toggleMacroFromShortcut,
        Action emergencyStop,
        Action pauseResumeAll)
    {
        _dispatcher = dispatcher;
        _getSettings = getSettings;
        _getWorkspaces = getWorkspaces;
        _areMacroShortcutsEnabled = areMacroShortcutsEnabled;
        _toggleMacroFromShortcut = toggleMacroFromShortcut;
        _emergencyStop = emergencyStop;
        _pauseResumeAll = pauseResumeAll;
    }

    public void SetCaptureActive(bool isActive)
    {
        IsCapturingShortcut = isActive;

        if (isActive)
        {
            _triggeredShortcutSignature = "";
            _suppressedKeys.Clear();
        }
    }

    public void ApplyHookState()
    {
        if (ShouldRunGlobalShortcutHook())
            StartGlobalShortcutHook();
        else
            StopGlobalShortcutHook();
    }

    public void StopGlobalShortcutHook()
    {
        if (_keyboardHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }

        _keyboardProc = null;
        _globalPressedKeys.Clear();
        _suppressedKeys.Clear();
        _triggeredShortcutSignature = "";
    }

    public void SuppressCurrentlyHeldShortcutKeys(string shortcut)
    {
        foreach (var virtualKey in ShortcutGesture.Parse(shortcut))
            _suppressedKeys.Add(virtualKey);
    }

    public bool TryGetEditingCommand(
        KeyEventArgs e,
        bool isBlocked,
        bool isSettingsModalOpen,
        out AppShortcutCommand command)
    {
        command = default;

        if (isBlocked ||
            IsCapturingShortcut ||
            isSettingsModalOpen ||
            IsTextEditingShortcutSource(e.OriginalSource as DependencyObject))
        {
            return false;
        }

        var settings = _getSettings();
        var pressedKeys = GetCurrentShortcutKeys(e);

        if (MatchesLocalShortcut(pressedKeys, settings.UndoShortcut))
            return SetCommand(AppShortcutCommand.Undo, out command);

        if (MatchesLocalShortcut(pressedKeys, settings.RedoShortcut))
            return SetCommand(AppShortcutCommand.Redo, out command);

        if (MatchesLocalShortcut(pressedKeys, settings.SelectAllShortcut))
            return SetCommand(AppShortcutCommand.SelectAll, out command);

        if (MatchesLocalShortcut(pressedKeys, settings.CopyShortcut))
            return SetCommand(AppShortcutCommand.Copy, out command);

        if (MatchesLocalShortcut(pressedKeys, settings.PasteShortcut))
            return SetCommand(AppShortcutCommand.Paste, out command);

        if (MatchesLocalShortcut(pressedKeys, settings.DuplicateShortcut))
            return SetCommand(AppShortcutCommand.Duplicate, out command);

        return false;
    }

    private static bool SetCommand(AppShortcutCommand value, out AppShortcutCommand command)
    {
        command = value;
        return true;
    }

    private void StartGlobalShortcutHook()
    {
        if (!ShouldRunGlobalShortcutHook())
            return;

        StopGlobalShortcutHook();

        var moduleHandle = NativeMethods.GetModuleHandle(null);

        _keyboardProc = (code, wParam, lParam) =>
        {
            if (code >= 0)
                HandleGlobalShortcutKeyMessage(wParam, lParam);

            return NativeMethods.CallNextHookEx(_keyboardHook, code, wParam, lParam);
        };

        _keyboardHook = NativeMethods.SetWindowsHookExKeyboard(
            NativeMethods.WH_KEYBOARD_LL,
            _keyboardProc,
            moduleHandle,
            0);

        if (_keyboardHook == IntPtr.Zero)
            _keyboardProc = null;
    }

    private void HandleGlobalShortcutKeyMessage(IntPtr wParam, IntPtr lParam)
    {
        if (IsCapturingShortcut)
            return;

        var hookData = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
        var virtualKey = ShortcutGesture.NormalizeVirtualKey((int)hookData.vkCode);

        if (virtualKey <= 0)
            return;

        var message = wParam.ToInt32();

        if (message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN)
        {
            if (_suppressedKeys.Remove(virtualKey))
                return;

            _globalPressedKeys.Add(virtualKey);
            TryTriggerShortcut();
        }
        else if (message is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP)
        {
            _globalPressedKeys.Remove(virtualKey);
            _suppressedKeys.Remove(virtualKey);
            _triggeredShortcutSignature = "";
        }
    }

    private void TryTriggerShortcut()
    {
        if (TryTriggerSettingsShortcut())
            return;

        if (!_areMacroShortcutsEnabled())
            return;

        var matchIndex = FindMatchingShortcutWorkspaceIndex();

        if (matchIndex < 0)
            return;

        var workspaces = _getWorkspaces();
        var shortcutKeys = ShortcutGesture.Parse(workspaces[matchIndex].ShortcutKeys);
        var signature = $"macro:{matchIndex}:{ShortcutGesture.Serialize(shortcutKeys)}";

        if (_triggeredShortcutSignature == signature)
            return;

        _triggeredShortcutSignature = signature;
        _dispatcher.BeginInvoke(new Action(() => _toggleMacroFromShortcut(matchIndex)));
    }

    private bool TryTriggerSettingsShortcut()
    {
        var settings = _getSettings();

        if (TryTriggerSingleSettingsShortcut(
                settings.EmergencyStopShortcut,
                "emergency-stop",
                _emergencyStop))
        {
            return true;
        }

        return TryTriggerSingleSettingsShortcut(
            settings.PauseResumeAllMacrosShortcut,
            "pause-resume",
            _pauseResumeAll);
    }

    private bool TryTriggerSingleSettingsShortcut(string shortcut, string name, Action action)
    {
        var shortcutKeys = ShortcutGesture.Parse(shortcut);

        if (!ShortcutGesture.Matches(_globalPressedKeys, shortcutKeys))
            return false;

        var signature = $"settings:{name}:{ShortcutGesture.Serialize(shortcutKeys)}";

        if (_triggeredShortcutSignature == signature)
            return true;

        _triggeredShortcutSignature = signature;
        _dispatcher.BeginInvoke(new Action(action));

        return true;
    }

    private bool ShouldRunGlobalShortcutHook()
    {
        var settings = _getSettings();

        return _areMacroShortcutsEnabled() ||
               ShortcutGesture.Parse(settings.EmergencyStopShortcut).Length > 0 ||
               ShortcutGesture.Parse(settings.PauseResumeAllMacrosShortcut).Length > 0;
    }

    private int FindMatchingShortcutWorkspaceIndex()
    {
        var workspaces = _getWorkspaces();

        for (var i = 0; i < workspaces.Count; i++)
        {
            if (!workspaces[i].ShortcutsEnabled)
                continue;

            var shortcutKeys = ShortcutGesture.Parse(workspaces[i].ShortcutKeys);

            if (ShortcutGesture.Matches(_globalPressedKeys, shortcutKeys))
                return i;
        }

        return -1;
    }

    private static HashSet<int> GetCurrentShortcutKeys(KeyEventArgs e)
    {
        var keys = new HashSet<int>();

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            keys.Add(NativeMethods.VK_CONTROL);

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            keys.Add(NativeMethods.VK_SHIFT);

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            keys.Add(NativeMethods.VK_MENU);

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var virtualKey = ShortcutGesture.NormalizeVirtualKey(KeyInterop.VirtualKeyFromKey(key));

        if (virtualKey > 0)
            keys.Add(virtualKey);

        return keys;
    }

    private static bool MatchesLocalShortcut(IReadOnlySet<int> pressedKeys, string shortcut)
    {
        return ShortcutGesture.Matches(pressedKeys, ShortcutGesture.Parse(shortcut));
    }

    private static bool IsTextEditingShortcutSource(DependencyObject? source)
    {
        for (var current = source; current != null; current = GetShortcutSourceParent(current))
        {
            if (current is TextBoxBase or PasswordBox or ComboBox)
                return true;
        }

        return false;
    }

    private static DependencyObject? GetShortcutSourceParent(DependencyObject current)
    {
        return current switch
        {
            FrameworkContentElement contentElement => contentElement.Parent,
            FrameworkElement frameworkElement => frameworkElement.Parent,
            Visual or Visual3D => VisualTreeHelper.GetParent(current),
            _ => null
        };
    }

    public void Dispose()
    {
        StopGlobalShortcutHook();
    }
}
