using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace MacroSpammer.Services.AppWindow;

public static class CloseConfirmationService
{
    public static (bool ShouldClose, bool DontAskAgain) Show(Window owner)
    {
        var bgBrush = owner.TryFindResource("Bg") as Brush
                      ?? Application.Current.TryFindResource("Bg") as Brush
                      ?? new SolidColorBrush(Color.FromRgb(0x10, 0x15, 0x1D));

        var textBrush = owner.TryFindResource("Text") as Brush
                        ?? owner.TryFindResource("Fg") as Brush
                        ?? Application.Current.TryFindResource("Text") as Brush
                        ?? Application.Current.TryFindResource("Fg") as Brush
                        ?? Brushes.White;

        var dialog = new Window
        {
            Owner = owner,
            Title = "Close while running",
            Width = 360,
            Height = 170,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = bgBrush
        };

        ApplyDarkTitleBar(dialog);

        var dontAskAgain = new CheckBox
        {
            Content = "Don't ask again",
            Margin = new Thickness(0, 10, 0, 0),
            Foreground = textBrush
        };

        var shouldClose = false;

        var panel = new StackPanel
        {
            Margin = new Thickness(16)
        };

        panel.Children.Add(new TextBlock
        {
            Text = "Macros are still running. Close KeyLine and stop them?",
            TextWrapping = TextWrapping.Wrap,
            Foreground = textBrush
        });

        panel.Children.Add(dontAskAgain);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 82,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var closeButton = new Button
        {
            Content = "Close",
            Width = 82
        };

        cancelButton.Click += (_, _) => dialog.Close();
        closeButton.Click += (_, _) =>
        {
            shouldClose = true;
            dialog.Close();
        };

        buttons.Children.Add(cancelButton);
        buttons.Children.Add(closeButton);
        panel.Children.Add(buttons);

        dialog.Content = panel;
        dialog.ShowDialog();

        return (shouldClose, dontAskAgain.IsChecked == true);
    }

    private static void ApplyDarkTitleBar(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(window).Handle;

            var useDarkMode = 1;
            _ = DwmSetWindowAttribute(handle, 20, ref useDarkMode, sizeof(int));

            // #10151D converted to COLORREF format: 0x00BBGGRR
            var captionColor = 0x001D1510;
            _ = DwmSetWindowAttribute(handle, 35, ref captionColor, sizeof(int));

            var textColor = 0x00FFFFFF;
            _ = DwmSetWindowAttribute(handle, 36, ref textColor, sizeof(int));
        };
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);
}