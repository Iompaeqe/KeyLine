using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KeyLine;

public partial class LicenseView : UserControl
{
    public LicenseView()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Window.GetWindow(this)?.Close();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(LicenseTextBlock.Text);
        CopyButton.Content = "Copied";
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
            return;

        Window.GetWindow(this)?.DragMove();
    }
}