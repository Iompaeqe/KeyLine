using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KeySpammer;

public partial class MainWindow
{
    private void TitleBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;

        // Do not drag when clicking titlebar buttons.
        if (IsClickInsideButton(e.OriginalSource as DependencyObject))
            return;

        try
        {
            DragMove();
        }
        catch
        {
            // DragMove can throw if mouse state changes during click.
        }
    }

    private static bool IsClickInsideButton(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is Button)
                return true;

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => 
        this.WindowState = this.WindowState == System.Windows.WindowState.Maximized
            ? System.Windows.WindowState.Normal
            : System.Windows.WindowState.Minimized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
