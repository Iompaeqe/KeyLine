using System.Windows;
using System.Windows.Controls;
using KeySpammer.Domain;
using KeySpammer.Services.Windows;

namespace KeySpammer;

public partial class MainWindow
{
    private void LoadWindows()
    {
        WindowComboBox.ItemsSource = WindowEnumerator.GetVisibleWindows();
        if (WindowComboBox.Items.Count > 0 && WindowComboBox.SelectedIndex < 0)
            WindowComboBox.SelectedIndex = 0;
    }

    private void WindowComboBox_DropDownOpened(object sender, EventArgs e) => LoadWindows();

    private void WindowComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WindowComboBox.SelectedItem is not TargetWindowInfo target)
            return;

        var children = ChildWindowFinder.GetChildWindows(target.Handle);
        if (children.Count == 0)
        {
            HandleComboBox.Visibility = Visibility.Collapsed;
            HandleComboBox.ItemsSource = null;
            return;
        }

        var handles = new List<TargetWindowInfo>
        {
            new() { Handle = target.Handle, Title = "[Parent Window]" }
        };
        handles.AddRange(children);

        HandleComboBox.ItemsSource = handles;
        HandleComboBox.SelectedIndex = 0;
        HandleComboBox.Visibility = Visibility.Visible;
    }

    private TargetWindowInfo? GetTargetHandle()
    {
        if (HandleComboBox.Visibility == Visibility.Visible)
            return HandleComboBox.SelectedItem as TargetWindowInfo;
        return WindowComboBox.SelectedItem as TargetWindowInfo;
    }
}
