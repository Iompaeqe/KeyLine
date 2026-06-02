using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MacroSpammer.Services.AppWindow;

public static class CloseConfirmationService
{
    public static (bool ShouldClose, bool DontAskAgain) Show(Window owner)
    {
        var dialog = new Window
        {
            Owner = owner,
            Title = "Close while running",
            Width = 360,
            Height = 170,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brushes.White
        };

        var dontAskAgain = new CheckBox
        {
            Content = "Don't ask again",
            Margin = new Thickness(0, 10, 0, 0)
        };

        var shouldClose = false;
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock
        {
            Text = "Macros are still running. Close KeyLine and stop them?",
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(dontAskAgain);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };

        var cancelButton = new Button { Content = "Cancel", Width = 82, Margin = new Thickness(0, 0, 8, 0) };
        var closeButton = new Button { Content = "Close", Width = 82 };

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
}
