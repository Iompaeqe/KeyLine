using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using System.Media;
using KeyLine.Services.AppWindow;
using KeyLine.Services.Tray;
using KeyLine.UI.Common.EntryBlocks;

namespace KeyLine;

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
        private ComboBox LoopModeComboBox => MacroOptionsPanel.LoopModeSelector;

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

            var result = CloseConfirmationService.Show(this);
            if (result.DontAskAgain)
            {
                _settings.ConfirmCloseWhileMacrosRunning = false;
                ScheduleSaveState();
            }

            return result.ShouldClose;
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

