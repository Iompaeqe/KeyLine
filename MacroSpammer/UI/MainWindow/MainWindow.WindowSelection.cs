using System.Windows;
using System.Windows.Controls;
using MacroSpammer.Domain;
using MacroSpammer.Services.Windows;

namespace MacroSpammer;

public partial class MainWindow
{
    private const string SelectWindowPlaceholderTitle = "Select target window";
    private const string ParentWindowTitle = "[Parent Window]";

    private void LoadWindows()
    {
        var selectedTitle = WindowComboBox.SelectedItem is TargetWindowInfo currentTarget
            ? currentTarget.Title
            : "";

        var windows = new List<TargetWindowInfo>
        {
            new() { Handle = 0, Title = SelectWindowPlaceholderTitle }
        };
        windows.AddRange(WindowEnumerator.GetVisibleWindows());

        WindowComboBox.ItemsSource = windows;

        if (!SelectComboBoxItemByTitle(WindowComboBox, selectedTitle))
            WindowComboBox.SelectedIndex = 0;
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

    private void CaptureSelectedTargetWindow(MacroWorkspace workspace)
    {
        if (_isRestoringWindowSelection)
            return;

        workspace.TargetWindowTitle = WindowComboBox.SelectedItem is TargetWindowInfo target
                                     && target.Handle != 0
            ? target.Title
            : "";

        workspace.TargetChildWindowTitle = HandleComboBox.Visibility == Visibility.Visible &&
                                           HandleComboBox.SelectedItem is TargetWindowInfo child
            ? child.Title
            : "";
    }

    private void RestoreTargetWindowSelection(MacroWorkspace workspace)
    {
        _isRestoringWindowSelection = true;

        try
        {
            LoadWindows();

            SelectComboBoxItemByTitle(WindowComboBox, workspace.TargetWindowTitle);

            if (WindowComboBox.SelectedItem is TargetWindowInfo target)
            {
                LoadChildWindows(target);

                if (HandleComboBox.Visibility == Visibility.Visible)
                    SelectComboBoxItemByTitle(HandleComboBox, workspace.TargetChildWindowTitle);
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
}
