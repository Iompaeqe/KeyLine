using System.Windows.Controls;
using MacroSpammer.UI.Common.EntryBlocks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using MacroSpammer.Services.Tray;
using System.Media;

namespace MacroSpammer;

public partial class MainWindow
{
    // From MainWindow.Controls.cs
        private ScrollViewer MacroTabsScrollViewer => MacroTabsBlock.TabsScrollViewer;
        private StackPanel MacroTabsPanel => MacroTabsBlock.TabsPanel;
        private Canvas MacroTabsDragOverlay => MacroTabsBlock.DragOverlay;

        private Border MacroTabsLeftEdgeFade => MacroTabsBlock.LeftEdgeFade;
        private Border MacroTabsRightEdgeFade => MacroTabsBlock.RightEdgeFade;

        private Border MacroTabsLeftEdgeLine => MacroTabsBlock.LeftEdgeLine;
        private Border MacroTabsRightEdgeLine => MacroTabsBlock.RightEdgeLine;

        private Button AddMacroTabButton => MacroTabsBlock.AddButton;


        private Button ToggleRightPanelButton => TitleCommandButtons.InspectorButton;
        private Button SettingsButton => TitleCommandButtons.SettingsButtonControl;
        private Button MinimizeButton => TitleCommandButtons.MinimizeButtonControl;
        private Button CloseWindowButton => TitleCommandButtons.CloseButtonControl;

        private Border ShortcutBorder => MacroOptionsPanel.ShortcutBorderControl;
        private OptionsPillBlock ShortcutPill => MacroOptionsPanel.ShortcutPillControl;
        private OptionsPillBlock ShortcutTogglePill => MacroOptionsPanel.ShortcutTogglePillControl;
        private TextBlock ShortcutTextBlock => MacroOptionsPanel.ShortcutText;
        private TextBlock ShortcutToggleTextBlock => MacroOptionsPanel.ShortcutToggleText;
        private PagerEntryBlock LoopTypePager => MacroOptionsPanel.LoopTypePagerControl;

        private OptionsPillBlock TargetWindowSearchPill => TargetWindowPanel.SearchPill;
        private TextBox TargetWindowSearchTextBox => TargetWindowPanel.SearchTextBox;
        private ComboBox WindowComboBox => TargetWindowPanel.WindowSelector;
        private ComboBox HandleComboBox => TargetWindowPanel.HandleSelector;


    // From MainWindow.TitleBar.cs
        private void TitleBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            // Do not drag when clicking titlebar controls.
            if (IsClickInsideTitleBarControl(e.OriginalSource as DependencyObject))
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

        private static bool IsClickInsideTitleBarControl(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is Button or TextBox or ScrollViewer)
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

    // From MainWindow.Tray.cs
        private TrayController? _trayController;

        private TrayController TrayController =>
            _trayController ??= new TrayController(this, "KeyLine", HideInspector);

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState != WindowState.Minimized)
                return;

            HideInspector();

            if (_settings.MinimizeToTray)
                HideToTray();
        }

        private void HideToTray()
        {
            TrayController.HideToTray();
        }

        private void EnsureTrayIcon()
        {
            TrayController.EnsureTrayIcon();
        }

        private void ShowFromTray()
        {
            TrayController.ShowFromTray();
        }

        private bool ShouldCancelCloseForTray()
        {
            return TrayController.ShouldCancelClose(_settings.CloseToTray);
        }

        private bool ConfirmCloseIfNeeded()
        {
            if (!_settings.ConfirmCloseWhileMacrosRunning || !AnyPlaybackRunning())
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
            _trayController?.Dispose();
            _trayController = null;
        }

    // From MainWindow.Sounds.cs
        private void PlayMacroSound()
        {
            if (!_settings.PlaySoundOnMacroStartStop)
                return;

            PlaySelectedSystemSound();
        }

        public void PreviewPlaybackSound()
        {
            PlaySelectedSystemSound();
        }

        private void PlaySelectedSystemSound()
        {
            try
            {
                var sound = _settings.PlaybackSoundName switch
                {
                    "Asterisk" => SystemSounds.Asterisk,
                    "Exclamation" => SystemSounds.Exclamation,
                    "Hand" => SystemSounds.Hand,
                    "Question" => SystemSounds.Question,
                    _ => SystemSounds.Beep
                };

                sound.Play();
            }
            catch
            {
            }
        }

}
