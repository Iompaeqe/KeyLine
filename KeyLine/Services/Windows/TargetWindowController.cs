using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using KeyLine.Domain;
using KeyLine.Interop;

namespace KeyLine.Services.Windows;

public sealed class TargetWindowController
{
    private const string SelectWindowPlaceholderTitle = "Focused window (no target selected)";
    private const string ParentWindowTitle = "[Parent Window]";

    private readonly ComboBox _windowComboBox;
    private readonly ComboBox _handleComboBox;
    private readonly Func<MacroWorkspace> _getActiveWorkspace;
    private readonly Action<MacroWorkspace> _clearMacroError;
    private readonly Action<MacroWorkspace, string> _setMacroError;
    private readonly Action _scheduleSaveState;
    private readonly Action<bool> _setRestoringWindowSelection;

    private bool _isRestoringWindowSelection;
    private int _windowLoadVersion;

    public TargetWindowController(
        ComboBox windowComboBox,
        ComboBox handleComboBox,
        Func<MacroWorkspace> getActiveWorkspace,
        Action<MacroWorkspace> clearMacroError,
        Action<MacroWorkspace, string> setMacroError,
        Action scheduleSaveState,
        Action<bool> setRestoringWindowSelection)
    {
        _windowComboBox = windowComboBox;
        _handleComboBox = handleComboBox;
        _getActiveWorkspace = getActiveWorkspace;
        _clearMacroError = clearMacroError;
        _setMacroError = setMacroError;
        _scheduleSaveState = scheduleSaveState;
        _setRestoringWindowSelection = setRestoringWindowSelection;
    }

    private void SetRestoringWindowSelection(bool isRestoring)
    {
        _isRestoringWindowSelection = isRestoring;
        _setRestoringWindowSelection(isRestoring);
    }

    public void LoadWindows()
    {
        _windowLoadVersion++;
        var (selectedHandle, selectedTitle) = GetSelectedWindowSnapshot();
        ApplyWindowItems(CreateWindowItems(), selectedHandle, selectedTitle);
    }

    public async Task LoadWindowsAsync()
    {
        var requestVersion = ++_windowLoadVersion;
        var (selectedHandle, selectedTitle) = GetSelectedWindowSnapshot();
        var windows = await Task.Run(CreateWindowItems);

        if (requestVersion != _windowLoadVersion)
            return;

        ApplyWindowItems(windows, selectedHandle, selectedTitle);
    }

    private (nint Handle, string Title) GetSelectedWindowSnapshot()
    {
        return _windowComboBox.SelectedItem is TargetWindowInfo selected
            ? (selected.Handle, selected.Title)
            : (0, string.Empty);
    }

    private static List<TargetWindowInfo> CreateWindowItems()
    {
        var windows = new List<TargetWindowInfo>
        {
            new() { Handle = 0, Title = SelectWindowPlaceholderTitle }
        };
        windows.AddRange(WindowEnumerator.GetVisibleWindows());

        return windows;
    }

    private void ApplyWindowItems(List<TargetWindowInfo> windows, nint selectedHandle, string selectedTitle)
    {
        _windowComboBox.ItemsSource = windows;

        if (!SelectComboBoxItemByHandle(_windowComboBox, selectedHandle) &&
            !SelectComboBoxItemByTitle(_windowComboBox, selectedTitle))
        {
            _windowComboBox.SelectedIndex = 0;
        }
    }

    public void WindowSelectionChanged()
    {
        if (_isRestoringWindowSelection)
            return;

        if (_windowComboBox.SelectedItem is not TargetWindowInfo target)
            return;

        var activeWorkspace = _getActiveWorkspace();

        if (!_isRestoringWindowSelection)
            _clearMacroError(activeWorkspace);

        if (target.Handle == 0)
        {
            _handleComboBox.Visibility = Visibility.Collapsed;
            _handleComboBox.ItemsSource = null;
            CaptureSelectedTargetWindow(activeWorkspace);
            _scheduleSaveState();
            return;
        }

        LoadChildWindows(target);

        CaptureSelectedTargetWindow(activeWorkspace);
        _scheduleSaveState();
    }

    public void HandleSelectionChanged()
    {
        if (_isRestoringWindowSelection)
            return;

        var activeWorkspace = _getActiveWorkspace();

        if (!_isRestoringWindowSelection)
            _clearMacroError(activeWorkspace);

        CaptureSelectedTargetWindow(activeWorkspace);
        _scheduleSaveState();
    }

    public TargetWindowInfo? GetTargetHandle()
    {
        if (_handleComboBox.Visibility == Visibility.Visible)
            return _handleComboBox.SelectedItem as TargetWindowInfo;

        return _windowComboBox.SelectedItem as TargetWindowInfo;
    }

    public bool HasResolvedTargetSelection() => GetTargetHandle() is { Handle: not 0 };

    public TargetWindowInfo? GetPlaybackTarget(MacroWorkspace workspace, bool updateSelection)
    {
        var activeWorkspace = _getActiveWorkspace();

        if (!string.IsNullOrWhiteSpace(workspace.TargetWindowSearchName))
        {
            if (!TryResolveTargetWindowSearchName(workspace, updateSelection))
                return null;

            return ReferenceEquals(workspace, activeWorkspace)
                ? GetTargetHandle()
                : ResolveSavedTargetWindow(workspace);
        }

        var target = ReferenceEquals(workspace, activeWorkspace)
            ? GetTargetHandle()
            : ResolveSavedTargetWindow(workspace);

        if (target is { Handle: not 0 })
        {
            _clearMacroError(workspace);
            return target;
        }

        return HasSelectedTarget(workspace)
            ? null
            : ResolveFocusedWindowForPlayback(workspace);
    }

    public void CaptureSelectedTargetWindow(MacroWorkspace workspace)
    {
        if (_isRestoringWindowSelection)
            return;

        workspace.TargetWindowHandle = _windowComboBox.SelectedItem is TargetWindowInfo selectedTarget
                                       && selectedTarget.Handle != 0
            ? selectedTarget.Handle.ToInt64()
            : 0;

        workspace.TargetWindowTitle = _windowComboBox.SelectedItem is TargetWindowInfo selectedTargetTitle
                                     && selectedTargetTitle.Handle != 0
            ? selectedTargetTitle.Title
            : string.Empty;

        workspace.TargetChildWindowHandle = _handleComboBox.Visibility == Visibility.Visible &&
                                            _handleComboBox.SelectedItem is TargetWindowInfo selectedChild
            ? selectedChild.Handle.ToInt64()
            : 0;

        workspace.TargetChildWindowTitle = _handleComboBox.Visibility == Visibility.Visible &&
                                           _handleComboBox.SelectedItem is TargetWindowInfo selectedChildTitle
            ? selectedChildTitle.Title
            : string.Empty;
    }

    public void RestoreTargetWindowSelection(MacroWorkspace workspace)
    {
        var windows = CreateWindowItems();
        var target = FindWindowSelection(windows, workspace.TargetWindowHandle, workspace.TargetWindowTitle);
        var children = target == null
            ? null
            : ChildWindowFinder.GetChildWindows(target.Handle);

        SetRestoringWindowSelection(true);

        try
        {
            ApplyRestoredWindowSelection(
                workspace.TargetWindowHandle,
                workspace.TargetWindowTitle,
                workspace.TargetChildWindowHandle,
                workspace.TargetChildWindowTitle,
                windows,
                target,
                children);
        }
        finally
        {
            SetRestoringWindowSelection(false);
        }
    }

    public async Task RestoreTargetWindowSelectionAsync(MacroWorkspace workspace)
    {
        var targetWindowHandle = workspace.TargetWindowHandle;
        var targetWindowTitle = workspace.TargetWindowTitle;
        var targetChildWindowHandle = workspace.TargetChildWindowHandle;
        var targetChildWindowTitle = workspace.TargetChildWindowTitle;

        var windows = await Task.Run(CreateWindowItems);
        var target = FindWindowSelection(windows, targetWindowHandle, targetWindowTitle);
        var children = target == null
            ? null
            : await Task.Run(() => ChildWindowFinder.GetChildWindows(target.Handle));

        if (!ReferenceEquals(workspace, _getActiveWorkspace()))
            return;

        SetRestoringWindowSelection(true);

        try
        {
            ApplyRestoredWindowSelection(
                targetWindowHandle,
                targetWindowTitle,
                targetChildWindowHandle,
                targetChildWindowTitle,
                windows,
                target,
                children);
        }
        finally
        {
            SetRestoringWindowSelection(false);
        }
    }

    private void ApplyRestoredWindowSelection(
        long targetWindowHandle,
        string targetWindowTitle,
        long targetChildWindowHandle,
        string targetChildWindowTitle,
        List<TargetWindowInfo> windows,
        TargetWindowInfo? target,
        List<TargetWindowInfo>? children)
    {
        ApplyWindowItems(windows, 0, string.Empty);

        if (targetWindowHandle <= 0 && string.IsNullOrWhiteSpace(targetWindowTitle))
        {
            _windowComboBox.SelectedIndex = 0;
            ClearChildWindowItems();
            return;
        }

        if (target == null)
        {
            ClearChildWindowItems();
            return;
        }

        _windowComboBox.SelectedItem = target;
        ApplyChildWindowItems(
            target,
            children ?? new List<TargetWindowInfo>(),
            new IntPtr(targetChildWindowHandle),
            targetChildWindowTitle);
    }

    public bool TryResolveTargetWindowSearchName(MacroWorkspace workspace, bool updateSelection)
    {
        var searchName = workspace.TargetWindowSearchName.Trim();
        if (string.IsNullOrWhiteSpace(searchName))
            return false;

        var matches = FindTargetWindowSearchMatches(searchName);
        return ApplyTargetWindowSearchMatches(workspace, searchName, matches, updateSelection);
    }

    public async Task<bool> TryResolveTargetWindowSearchNameAsync(MacroWorkspace workspace, bool updateSelection)
    {
        var searchName = workspace.TargetWindowSearchName.Trim();
        if (string.IsNullOrWhiteSpace(searchName))
            return false;

        var matches = await Task.Run(() => FindTargetWindowSearchMatches(searchName));
        if (!ReferenceEquals(workspace, _getActiveWorkspace()))
            return false;

        if (!ApplyTargetWindowSearchMatches(workspace, searchName, matches, updateSelection: false))
            return false;

        if (updateSelection)
            await SelectTargetWindowAsync(matches[0]);

        return true;
    }

    private bool ApplyTargetWindowSearchMatches(
        MacroWorkspace workspace,
        string searchName,
        List<TargetWindowInfo> matches,
        bool updateSelection)
    {
        if (matches.Count == 0)
        {
            _setMacroError(workspace, $"No target window found for '{searchName}'");
            return false;
        }

        if (matches.Count > 1)
        {
            _setMacroError(workspace, $"Multiple target windows match '{searchName}'");
            return false;
        }

        _clearMacroError(workspace);

        var target = matches[0];
        workspace.TargetWindowHandle = target.Handle.ToInt64();
        workspace.TargetWindowTitle = target.Title;
        workspace.TargetChildWindowHandle = 0;
        workspace.TargetChildWindowTitle = string.Empty;

        var activeWorkspace = _getActiveWorkspace();
        if (updateSelection && ReferenceEquals(workspace, activeWorkspace))
            SelectTargetWindow(target);

        _scheduleSaveState();
        return true;
    }

    public static bool HasSelectedTarget(MacroWorkspace workspace) =>
        workspace.TargetWindowHandle > 0 || !string.IsNullOrWhiteSpace(workspace.TargetWindowTitle);

    private static TargetWindowInfo? ResolveSavedTargetWindow(MacroWorkspace workspace)
    {
        var target = ResolveWindowHandle(workspace.TargetWindowHandle, workspace.TargetWindowTitle);

        target ??= WindowEnumerator.GetVisibleWindows()
            .FirstOrDefault(window => string.Equals(
                window.Title,
                workspace.TargetWindowTitle,
                StringComparison.Ordinal));

        if (target == null)
            return ResolveTargetWindowSearchNameForPlayback(workspace);

        var child = ResolveWindowHandle(workspace.TargetChildWindowHandle, workspace.TargetChildWindowTitle);
        if (child != null)
            return child;

        if (string.IsNullOrWhiteSpace(workspace.TargetChildWindowTitle) ||
            string.Equals(workspace.TargetChildWindowTitle, ParentWindowTitle, StringComparison.Ordinal))
        {
            return target;
        }

        return ChildWindowFinder.GetChildWindows(target.Handle)
            .FirstOrDefault(child => string.Equals(
                child.Title,
                workspace.TargetChildWindowTitle,
                StringComparison.Ordinal))
            ?? target;
    }

    private static TargetWindowInfo? ResolveTargetWindowSearchNameForPlayback(MacroWorkspace workspace)
    {
        var matches = FindTargetWindowSearchMatches(workspace.TargetWindowSearchName);
        return matches.Count == 1 ? matches[0] : null;
    }

    private TargetWindowInfo? ResolveFocusedWindowForPlayback(MacroWorkspace workspace)
    {
        var handle = NativeMethods.GetForegroundWindow();
        if (handle == IntPtr.Zero || !NativeMethods.IsWindow(handle))
            return null;

        _clearMacroError(workspace);

        var title = GetWindowTitle(handle);
        return new TargetWindowInfo
        {
            Handle = handle,
            Title = string.IsNullOrWhiteSpace(title) ? "Focused window" : title,
            IsFocusedWindowFallback = true
        };
    }

    private static TargetWindowInfo? ResolveWindowHandle(long handleValue, string fallbackTitle)
    {
        if (handleValue <= 0)
            return null;

        var handle = new IntPtr(handleValue);
        if (!NativeMethods.IsWindow(handle))
            return null;

        var title = GetWindowTitle(handle);
        return new TargetWindowInfo
        {
            Handle = handle,
            Title = string.IsNullOrWhiteSpace(title) ? fallbackTitle : title
        };
    }

    private void SelectTargetWindow(TargetWindowInfo target)
    {
        var windows = CreateWindowItems();
        var selectedTarget = FindWindowSelection(windows, target.Handle.ToInt64(), target.Title);
        var children = selectedTarget == null
            ? null
            : ChildWindowFinder.GetChildWindows(selectedTarget.Handle);

        SetRestoringWindowSelection(true);

        try
        {
            ApplyWindowItems(windows, 0, string.Empty);
            if (selectedTarget == null)
            {
                ClearChildWindowItems();
                return;
            }

            _windowComboBox.SelectedItem = selectedTarget;
            ApplyChildWindowItems(selectedTarget, children ?? new List<TargetWindowInfo>(), 0, string.Empty);
        }
        finally
        {
            SetRestoringWindowSelection(false);
        }
    }

    private async Task SelectTargetWindowAsync(TargetWindowInfo target)
    {
        var windows = await Task.Run(CreateWindowItems);
        var selectedTarget = FindWindowSelection(windows, target.Handle.ToInt64(), target.Title);
        var children = selectedTarget == null
            ? null
            : await Task.Run(() => ChildWindowFinder.GetChildWindows(selectedTarget.Handle));

        SetRestoringWindowSelection(true);

        try
        {
            ApplyWindowItems(windows, 0, string.Empty);
            if (selectedTarget == null)
            {
                ClearChildWindowItems();
                return;
            }

            _windowComboBox.SelectedItem = selectedTarget;
            ApplyChildWindowItems(selectedTarget, children ?? new List<TargetWindowInfo>(), 0, string.Empty);
        }
        finally
        {
            SetRestoringWindowSelection(false);
        }
    }

    private void LoadChildWindows(TargetWindowInfo target)
    {
        ApplyChildWindowItems(target, ChildWindowFinder.GetChildWindows(target.Handle), 0, string.Empty);
    }

    private void ApplyChildWindowItems(
        TargetWindowInfo target,
        List<TargetWindowInfo> children,
        nint selectedHandle,
        string selectedTitle)
    {
        if (children.Count == 0)
        {
            ClearChildWindowItems();
            return;
        }

        var handles = new List<TargetWindowInfo>
        {
            new() { Handle = target.Handle, Title = ParentWindowTitle }
        };
        handles.AddRange(children);

        _handleComboBox.ItemsSource = handles;
        _handleComboBox.Visibility = Visibility.Visible;

        if (!SelectComboBoxItemByHandle(_handleComboBox, selectedHandle) &&
            !SelectComboBoxItemByTitle(_handleComboBox, selectedTitle))
        {
            _handleComboBox.SelectedIndex = 0;
        }
    }

    private void ClearChildWindowItems()
    {
        _handleComboBox.Visibility = Visibility.Collapsed;
        _handleComboBox.ItemsSource = null;
    }

    private static List<TargetWindowInfo> FindTargetWindowSearchMatches(string searchName)
    {
        if (string.IsNullOrWhiteSpace(searchName))
            return new List<TargetWindowInfo>();

        return WindowEnumerator.GetVisibleWindows()
            .Where(window => window.Title.Contains(searchName.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static bool SelectComboBoxItemByTitle(ComboBox comboBox, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        foreach (var item in comboBox.Items.OfType<TargetWindowInfo>())
        {
            if (!string.Equals(item.Title, title, StringComparison.Ordinal))
                continue;

            comboBox.SelectedItem = item;
            return true;
        }

        return false;
    }

    private static TargetWindowInfo? FindWindowSelection(
        IReadOnlyList<TargetWindowInfo> windows,
        long handleValue,
        string title)
    {
        if (handleValue > 0)
        {
            var handle = new IntPtr(handleValue);
            var handleMatch = windows.FirstOrDefault(window => window.Handle == handle);
            if (handleMatch != null)
                return handleMatch;
        }

        return string.IsNullOrWhiteSpace(title)
            ? null
            : windows.FirstOrDefault(window => string.Equals(window.Title, title, StringComparison.Ordinal));
    }

    private static bool SelectComboBoxItemByHandle(ComboBox comboBox, nint handle)
    {
        if (handle == 0)
            return false;

        foreach (var item in comboBox.Items.OfType<TargetWindowInfo>())
        {
            if (item.Handle != handle)
                continue;

            comboBox.SelectedItem = item;
            return true;
        }

        return false;
    }

    private static string GetWindowTitle(nint handle)
    {
        var builder = new StringBuilder(256);
        NativeMethods.GetWindowText(handle, builder, builder.Capacity);
        return builder.ToString().Trim();
    }
}
