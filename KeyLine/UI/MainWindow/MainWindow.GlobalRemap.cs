using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KeyLine.Services.Input;

namespace KeyLine;

public partial class MainWindow
{
    private void InitializeGlobalRemapToggle()
    {
        GlobalRemapPill.MouseLeftButtonDown += GlobalRemapPill_MouseLeftButtonDown;
        UpdateGlobalRemapToggle();
    }

    private void GlobalRemapPill_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        SetGlobalRemapEnabled(!_settings.GlobalRemapEnabled);
        e.Handled = true;
    }

    private void ToggleGlobalRemapFromShortcut()
    {
        if (IsShortcutCaptureActive())
            return;

        SetGlobalRemapEnabled(!_settings.GlobalRemapEnabled);
    }

    private void SetGlobalRemapEnabled(bool isEnabled)
    {
        if (_settings.GlobalRemapEnabled == isEnabled)
        {
            UpdateGlobalRemapToggle();
            return;
        }

        _settings.GlobalRemapEnabled = isEnabled;

        if (!isEnabled)
            _shortcutController?.ClearConsumedRemapKeys();

        UpdateGlobalRemapToggle();

        StatusText.Text = isEnabled
            ? "Global remap enabled"
            : "Global remap disabled";

        ScheduleSaveState();
    }

    private void UpdateGlobalRemapToggle()
    {
        if (GlobalRemapPill == null)
            return;

        var isEnabled = _settings.GlobalRemapEnabled;

        GlobalRemapPill.Text = isEnabled ? "Remaps on" : "Remaps off";
        GlobalRemapPill.ClearValue(ForegroundProperty);

        if (isEnabled)
        {
            GlobalRemapPill.TextElement.Foreground = (Brush)FindResource("Cyan");
        }
        else
        {
            GlobalRemapPill.TextElement.ClearValue(TextBlock.ForegroundProperty);
        }

        GlobalRemapPill.ToolTip =
            $"Global remap is {(isEnabled ? "enabled" : "disabled")}.\n" +
            $"Shortcut: {ShortcutGesture.Format(_settings.ToggleGlobalRemapShortcut)}.\n" +
            "When disabled, remap macros will not consume shortcut keys.";
    }
}
