using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using MacroSpammer.Domain;
using MacroSpammer.Interop;

namespace MacroSpammer.Services.Windows;

public sealed class TargetWindowController
{
    private const string SelectWindowPlaceholderTitle = "Select target window";
    private const string ParentWindowTitle = "[Parent Window]";

    private readonly ComboBox _windowComboBox;
    private readonly ComboBox _handleComboBox;
    private readonly Func<MacroWorkspace> _getActiveWorkspace;
    private readonly Action<MacroWorkspace> _clearMacroError;
    private readonly Action<MacroWorkspace, string> _setMacroError;
    private readonly Action _scheduleSaveState;
    private readonly Action<bool> _setRestoringWindowSelection;

    private bool _isRestoringWindowSelection;

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
        var selectedHandle = _windowComboBox.SelectedItem is TargetWindowInfo currentTargetHandle
            ? currentTargetHandle.Handle
            : 0;
        var selectedTitle = _windowComboBox.SelectedItem is TargetWindowInfo currentTargetTitle
            ? currentTargetTitle.Title
            : string.Empty;

        var windows = new List<TargetWindowInfo>
        {
            new() { Handle = 0, Title = SelectWindowPlaceholderTitle }
        };
        windows.AddRange(WindowEnumerator.GetVisibleWindows());

        _windowComboBox.ItemsSource = windows;

        if (!SelectComboBoxItemByHandle(_windowComboBox, selectedHandle) &&
            !SelectComboBoxItemByTitle(_windowComboBox, selectedTitle))
        {
            _windowComboBox.SelectedIndex = 0;
        }
    }

    public void WindowSelectionChanged()
    {
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
        var target = ReferenceEquals(workspace, activeWorkspace)
            ? GetTargetHandle()
            : ResolveSavedTargetWindow(workspace);

        if (target is { Handle: not 0 })
        {
            _clearMacroError(workspace);
            return target;
        }

        if (string.IsNullOrWhiteSpace(workspace.TargetWindowSearchName))
            return null;

        if (!TryResolveTargetWindowSearchName(workspace, updateSelection))
            return null;

        return ReferenceEquals(workspace, activeWorkspace)
            ? GetTargetHandle()
            : ResolveSavedTargetWindow(workspace);
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
        SetRestoringWindowSelection(true);

        try
        {
            LoadWindows();

            if (workspace.TargetWindowHandle <= 0 && string.IsNullOrWhiteSpace(workspace.TargetWindowTitle))
            {
                _windowComboBox.SelectedIndex = 0;
                _handleComboBox.Visibility = Visibility.Collapsed;
                _handleComboBox.ItemsSource = null;
                return;
            }

            if (!SelectComboBoxItemByHandle(_windowComboBox, new IntPtr(workspace.TargetWindowHandle)))
                SelectComboBoxItemByTitle(_windowComboBox, workspace.TargetWindowTitle);

            if (_windowComboBox.SelectedItem is TargetWindowInfo target)
            {
                LoadChildWindows(target);

                if (_handleComboBox.Visibility == Visibility.Visible)
                {
                    if (!SelectComboBoxItemByHandle(_handleComboBox, new IntPtr(workspace.TargetChildWindowHandle)))
                        SelectComboBoxItemByTitle(_handleComboBox, workspace.TargetChildWindowTitle);
                }
            }
        }
        finally
        {
            SetRestoringWindowSelection(false);
        }
    }

    public bool TryResolveTargetWindowSearchName(MacroWorkspace workspace, bool updateSelection)
    {
        var searchName = workspace.TargetWindowSearchName.Trim();
        if (string.IsNullOrWhiteSpace(searchName))
            return false;

        var matches = FindTargetWindowSearchMatches(searchName);
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
        SetRestoringWindowSelection(true);

        try
        {
            LoadWindows();
            if (!SelectComboBoxItemByHandle(_windowComboBox, target.Handle))
                SelectComboBoxItemByTitle(_windowComboBox, target.Title);

            if (_windowComboBox.SelectedItem is TargetWindowInfo selectedTarget)
                LoadChildWindows(selectedTarget);
        }
        finally
        {
            SetRestoringWindowSelection(false);
        }
    }

    private void LoadChildWindows(TargetWindowInfo target)
    {
        var children = ChildWindowFinder.GetChildWindows(target.Handle);
        if (children.Count == 0)
        {
            _handleComboBox.Visibility = Visibility.Collapsed;
            _handleComboBox.ItemsSource = null;
            return;
        }

        var handles = new List<TargetWindowInfo>
        {
            new() { Handle = target.Handle, Title = ParentWindowTitle }
        };
        handles.AddRange(children);

        _handleComboBox.ItemsSource = handles;
        _handleComboBox.SelectedIndex = 0;
        _handleComboBox.Visibility = Visibility.Visible;
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
