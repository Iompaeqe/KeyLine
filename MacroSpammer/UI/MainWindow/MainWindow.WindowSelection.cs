using System.Windows;
using System.Windows.Controls;
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

    private void WindowComboBox_DropDownOpened(object sender, EventArgs e) => LoadWindows();

    private void WindowComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WindowComboBox.SelectedItem is not TargetWindowInfo target)
            return;

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
        CaptureSelectedTargetWindow(_activeWorkspace);
        ScheduleSaveState();
    }

    private TargetWindowInfo? GetTargetHandle()
    {
        if (HandleComboBox.Visibility == Visibility.Visible)
            return HandleComboBox.SelectedItem as TargetWindowInfo;
        return WindowComboBox.SelectedItem as TargetWindowInfo;
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
            return null;

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
