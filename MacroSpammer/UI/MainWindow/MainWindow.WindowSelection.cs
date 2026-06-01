using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MacroSpammer.Domain;
using MacroSpammer.Interop;
using MacroSpammer.Services.Windows;

namespace MacroSpammer;

public partial class MainWindow
{
    private const string SelectWindowPlaceholderTitle = "Select target window";
    private const string ParentWindowTitle = "[Parent Window]";

    private void LoadWindows()
    {
        var selectedHandle = WindowComboBox.SelectedItem is TargetWindowInfo currentTargetHandle
            ? currentTargetHandle.Handle
            : 0;
        var selectedTitle = WindowComboBox.SelectedItem is TargetWindowInfo currentTargetTitle
            ? currentTargetTitle.Title
            : "";

        var windows = new List<TargetWindowInfo>
        {
            new() { Handle = 0, Title = SelectWindowPlaceholderTitle }
        };
        windows.AddRange(WindowEnumerator.GetVisibleWindows());

        WindowComboBox.ItemsSource = windows;

        if (!SelectComboBoxItemByHandle(WindowComboBox, selectedHandle) &&
            !SelectComboBoxItemByTitle(WindowComboBox, selectedTitle))
        {
            WindowComboBox.SelectedIndex = 0;
        }
    }

    private void WindowComboBox_DropDownOpened(object? sender, EventArgs e) => LoadWindows();

    private void WindowComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WindowComboBox.SelectedItem is not TargetWindowInfo target)
            return;

        if (!_isRestoringWindowSelection)
            ClearMacroError(_activeWorkspace);

        if (target.Handle == 0)
        {
            HandleComboBox.Visibility = Visibility.Collapsed;
            HandleComboBox.ItemsSource = null;
            CaptureSelectedTargetWindow(_activeWorkspace);
            ScheduleSaveState();
            return;
        }

        LoadChildWindows(target);

        CaptureSelectedTargetWindow(_activeWorkspace);
        ScheduleSaveState();
    }

    private void HandleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isRestoringWindowSelection)
            ClearMacroError(_activeWorkspace);

        CaptureSelectedTargetWindow(_activeWorkspace);
        ScheduleSaveState();
    }

    private void TargetWindowSearchPill_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!TargetWindowSearchPill.IsEnabled)
            return;

        TargetWindowSearchPill.IsTextInput = true;
        TargetWindowSearchPill.FocusInput();
        e.Handled = true;
    }

    private void TargetWindowSearchTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitTargetWindowSearchName(resolveIfMissingTarget: true);
        TargetWindowSearchPill.IsTextInput = false;
    }

    private void TargetWindowSearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                CommitTargetWindowSearchName(resolveIfMissingTarget: true);
                Keyboard.ClearFocus();
                e.Handled = true;
                break;
            case Key.Escape:
                TargetWindowSearchTextBox.Text = _activeWorkspace.TargetWindowSearchName;
                TargetWindowSearchPill.IsTextInput = false;
                Keyboard.ClearFocus();
                e.Handled = true;
                break;
        }
    }

    private void CommitTargetWindowSearchName(bool resolveIfMissingTarget)
    {
        var searchName = TargetWindowSearchTextBox.Text.Trim();
        if (string.Equals(_activeWorkspace.TargetWindowSearchName, searchName, StringComparison.Ordinal))
        {
            if (resolveIfMissingTarget && !string.IsNullOrWhiteSpace(searchName))
                TryResolveTargetWindowSearchName(_activeWorkspace, updateSelection: true);

            return;
        }

        _activeWorkspace.TargetWindowSearchName = searchName;
        ClearMacroError(_activeWorkspace);

        if (resolveIfMissingTarget && !string.IsNullOrWhiteSpace(searchName))
            TryResolveTargetWindowSearchName(_activeWorkspace, updateSelection: true);

        ScheduleSaveState();
    }

    private TargetWindowInfo? GetTargetHandle()
    {
        if (HandleComboBox.Visibility == Visibility.Visible)
            return HandleComboBox.SelectedItem as TargetWindowInfo;
        return WindowComboBox.SelectedItem as TargetWindowInfo;
    }

    private bool HasResolvedTargetSelection() => GetTargetHandle() is { Handle: not 0 };

    private TargetWindowInfo? GetPlaybackTarget(MacroWorkspace workspace, bool updateSelection)
    {
        var target = ReferenceEquals(workspace, _activeWorkspace)
            ? GetTargetHandle()
            : ResolveSavedTargetWindow(workspace);

        if (target is { Handle: not 0 })
        {
            ClearMacroError(workspace);
            return target;
        }

        if (string.IsNullOrWhiteSpace(workspace.TargetWindowSearchName))
            return null;

        if (!TryResolveTargetWindowSearchName(workspace, updateSelection))
            return null;

        return ReferenceEquals(workspace, _activeWorkspace)
            ? GetTargetHandle()
            : ResolveSavedTargetWindow(workspace);
    }

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

    private void CaptureSelectedTargetWindow(MacroWorkspace workspace)
    {
        if (_isRestoringWindowSelection)
            return;

        workspace.TargetWindowHandle = WindowComboBox.SelectedItem is TargetWindowInfo selectedTarget
                                       && selectedTarget.Handle != 0
            ? selectedTarget.Handle.ToInt64()
            : 0;

        workspace.TargetWindowTitle = WindowComboBox.SelectedItem is TargetWindowInfo selectedTargetTitle
                                     && selectedTargetTitle.Handle != 0
            ? selectedTargetTitle.Title
            : "";

        workspace.TargetChildWindowHandle = HandleComboBox.Visibility == Visibility.Visible &&
                                            HandleComboBox.SelectedItem is TargetWindowInfo selectedChild
            ? selectedChild.Handle.ToInt64()
            : 0;

        workspace.TargetChildWindowTitle = HandleComboBox.Visibility == Visibility.Visible &&
                                           HandleComboBox.SelectedItem is TargetWindowInfo selectedChildTitle
            ? selectedChildTitle.Title
            : "";
    }

    private void RestoreTargetWindowSelection(MacroWorkspace workspace)
    {
        _isRestoringWindowSelection = true;

        try
        {
            LoadWindows();

            if (workspace.TargetWindowHandle <= 0 && string.IsNullOrWhiteSpace(workspace.TargetWindowTitle))
            {
                WindowComboBox.SelectedIndex = 0;
                HandleComboBox.Visibility = Visibility.Collapsed;
                HandleComboBox.ItemsSource = null;
                return;
            }

            if (!SelectComboBoxItemByHandle(WindowComboBox, new IntPtr(workspace.TargetWindowHandle)))
                SelectComboBoxItemByTitle(WindowComboBox, workspace.TargetWindowTitle);

            if (WindowComboBox.SelectedItem is TargetWindowInfo target)
            {
                LoadChildWindows(target);

                if (HandleComboBox.Visibility == Visibility.Visible)
                {
                    if (!SelectComboBoxItemByHandle(HandleComboBox, new IntPtr(workspace.TargetChildWindowHandle)))
                        SelectComboBoxItemByTitle(HandleComboBox, workspace.TargetChildWindowTitle);
                }
            }
        }
        finally
        {
            _isRestoringWindowSelection = false;
        }
    }

    private bool TryResolveTargetWindowSearchName(MacroWorkspace workspace, bool updateSelection)
    {
        var searchName = workspace.TargetWindowSearchName.Trim();
        if (string.IsNullOrWhiteSpace(searchName))
            return false;

        var matches = FindTargetWindowSearchMatches(searchName);
        if (matches.Count == 0)
        {
            SetMacroError(workspace, $"No target window found for '{searchName}'");
            return false;
        }

        if (matches.Count > 1)
        {
            SetMacroError(workspace, $"Multiple target windows match '{searchName}'");
            return false;
        }

        ClearMacroError(workspace);

        var target = matches[0];
        workspace.TargetWindowHandle = target.Handle.ToInt64();
        workspace.TargetWindowTitle = target.Title;
        workspace.TargetChildWindowHandle = 0;
        workspace.TargetChildWindowTitle = "";

        if (updateSelection && ReferenceEquals(workspace, _activeWorkspace))
            SelectTargetWindow(target);

        ScheduleSaveState();
        return true;
    }

    private void SelectTargetWindow(TargetWindowInfo target)
    {
        _isRestoringWindowSelection = true;

        try
        {
            LoadWindows();
            if (!SelectComboBoxItemByHandle(WindowComboBox, target.Handle))
                SelectComboBoxItemByTitle(WindowComboBox, target.Title);

            if (WindowComboBox.SelectedItem is TargetWindowInfo selectedTarget)
                LoadChildWindows(selectedTarget);
        }
        finally
        {
            _isRestoringWindowSelection = false;
        }
    }

    private static List<TargetWindowInfo> FindTargetWindowSearchMatches(string searchName)
    {
        if (string.IsNullOrWhiteSpace(searchName))
            return new List<TargetWindowInfo>();

        return WindowEnumerator.GetVisibleWindows()
            .Where(window => window.Title.Contains(searchName.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static bool HasSelectedTarget(MacroWorkspace workspace) =>
        workspace.TargetWindowHandle > 0 || !string.IsNullOrWhiteSpace(workspace.TargetWindowTitle);

    private void SetMacroError(MacroWorkspace workspace, string message)
    {
        workspace.ErrorMessage = message;

        if (ReferenceEquals(workspace, _activeWorkspace))
        {
            StatusText.Text = message;
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
        }

        RefreshMacroTabs();
    }

    private void ClearMacroError(MacroWorkspace workspace)
    {
        if (string.IsNullOrWhiteSpace(workspace.ErrorMessage))
            return;

        var previousMessage = workspace.ErrorMessage;
        workspace.ErrorMessage = "";

        if (ReferenceEquals(workspace, _activeWorkspace) &&
            string.Equals(StatusText.Text, previousMessage, StringComparison.Ordinal))
        {
            RestoreDefaultStatusTextForActiveWorkspace();
        }

        RefreshMacroTabs();
    }

    private void RestoreDefaultStatusTextForActiveWorkspace()
    {
        if (_recorder.IsRecording && _recordingTimeline != null)
        {
            StatusText.Text = $"\u25CF Recording {_recordingTimeline.Name}";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
            return;
        }

        if (IsWorkspaceRunning(_activeWorkspace))
        {
            StatusText.Text = IsWorkspacePaused(_activeWorkspace)
                ? "Paused"
                : $"Running {_activeWorkspace.Name}";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
            return;
        }

        StatusText.Text = "Stopped";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(61, 84, 112));
    }

    private void LoadChildWindows(TargetWindowInfo target)
    {
        var children = ChildWindowFinder.GetChildWindows(target.Handle);
        if (children.Count == 0)
        {
            HandleComboBox.Visibility = Visibility.Collapsed;
            HandleComboBox.ItemsSource = null;
            return;
        }

        var handles = new List<TargetWindowInfo>
        {
            new() { Handle = target.Handle, Title = ParentWindowTitle }
        };
        handles.AddRange(children);

        HandleComboBox.ItemsSource = handles;
        HandleComboBox.SelectedIndex = 0;
        HandleComboBox.Visibility = Visibility.Visible;
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
        var builder = new System.Text.StringBuilder(256);
        NativeMethods.GetWindowText(handle, builder, builder.Capacity);
        return builder.ToString().Trim();
    }
}
