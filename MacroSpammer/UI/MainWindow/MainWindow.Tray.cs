using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Forms = System.Windows.Forms;

namespace MacroSpammer;

public partial class MainWindow
{
    private Forms.NotifyIcon? _trayIcon;
    private bool _isClosingForExit;

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _settings.MinimizeToTray)
            HideToTray();
    }

    private void HideToTray()
    {
        EnsureTrayIcon();
        Hide();
    }

    private void EnsureTrayIcon()
    {
        if (_trayIcon != null)
            return;

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show", null, (_, _) => ShowFromTray());
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _isClosingForExit = true;
            Close();
        });

        _trayIcon = new Forms.NotifyIcon
        {
            Text = "KeyLine",
            Visible = true,
            ContextMenuStrip = menu,
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? "")
        };
        _trayIcon.DoubleClick += (_, _) => ShowFromTray();
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private bool ShouldCancelCloseForTray()
    {
        if (_isClosingForExit || !_settings.CloseToTray)
            return false;

        HideToTray();
        return true;
    }

    private bool ConfirmCloseIfNeeded()
    {
        if (!_settings.ConfirmCloseWhileMacrosRunning || !_runners.Values.Any(runner => runner.IsRunning))
            return true;

        var result = ShowCloseRunningConfirmation();
        if (result.DontAskAgain)
        {
            _settings.ConfirmCloseWhileMacrosRunning = false;
            ScheduleSaveState();
        }

        return result.ShouldClose;
    }

    private (bool ShouldClose, bool DontAskAgain) ShowCloseRunningConfirmation()
    {
        var dialog = new Window
        {
            Owner = this,
            Title = "Close while running",
            Width = 360,
            Height = 170,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = System.Windows.Media.Brushes.White
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

    private void DisposeTrayIcon()
    {
        if (_trayIcon == null)
            return;

        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _trayIcon = null;
    }
}
