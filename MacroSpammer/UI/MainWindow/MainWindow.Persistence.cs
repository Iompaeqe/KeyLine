using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MacroSpammer.Services.Macro;
using MacroSpammer.Services.Timeline;

namespace MacroSpammer;

public partial class MainWindow
{
    private readonly DispatcherTimer _stateSaveTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(300)
    };

    private void InitializeStatePersistence()
    {
        _stateSaveTimer.Tick += (_, _) =>
        {
            _stateSaveTimer.Stop();
            SaveStateNow();
        };

        LoopCountTextBox.TextChanged += LoopCountTextBox_TextChanged;
        TimerMinutesTextBox.TextChanged += TimerMinutesTextBox_TextChanged;
        BaseDelayTextBox.TextChanged += BaseDelayTextBox_TextChanged;
    }

    private void LoopCountTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isUpdatingPlaybackCounters)
            ScheduleSaveState();
    }

    private void TimerMinutesTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isUpdatingPlaybackCounters)
            ScheduleSaveState();
    }

    private void BaseDelayTextBox_TextChanged(object sender, TextChangedEventArgs e) => ScheduleSaveState();

    private void DelayInputTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsDigit);
    }

    private void DelayInputTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_runners.Values.Any(runner => runner.IsRunning))
            return;

        if (ReferenceEquals(sender, TimerMinutesTextBox))
        {
            var timerMs = GetTimerMs();
            TimerUnitTextBlock.Text = "ms";
            TimerMinutesTextBox.Text = timerMs.ToString();
        }
        else if (ReferenceEquals(sender, StandardDelayTextBox))
        {
            var standardDelayMs = GetStandardDelayMs();
            StandardDelayUnitTextBlock.Text = "ms";
            StandardDelayTextBox.Text = standardDelayMs.ToString();
        }
        else if (ReferenceEquals(sender, BaseDelayTextBox))
        {
            var baseDelayMs = GetBaseDelayMs();
            BaseDelayUnitTextBlock.Text = "ms";
            BaseDelayTextBox.Text = baseDelayMs.ToString();
        }

        if (sender is TextBox textBox)
            textBox.SelectAll();
    }

    private void DelayInputTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        FormatDelayInputTextBox(sender);
    }

    private void DelayInputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        FormatDelayInputTextBox(sender);
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void FormatDelayInputTextBox(object sender)
    {
        if (_runners.Values.Any(runner => runner.IsRunning))
            return;

        if (ReferenceEquals(sender, TimerMinutesTextBox))
        {
            SetFormattedDelayInput(TimerMinutesTextBox, TimerUnitTextBlock, GetTimerMs());
        }
        else if (ReferenceEquals(sender, StandardDelayTextBox))
        {
            var standardDelayMs = GetStandardDelayMs();
            ApplyStandardDelayToActiveTimeline(standardDelayMs);
            RefreshTimeline();
            SetFormattedDelayInput(StandardDelayTextBox, StandardDelayUnitTextBlock, standardDelayMs);
            ScheduleSaveState();
        }
        else if (ReferenceEquals(sender, BaseDelayTextBox))
        {
            SetFormattedDelayInput(BaseDelayTextBox, BaseDelayUnitTextBlock, GetBaseDelayMs());
        }
    }

    private static void SetFormattedDelayInput(TextBox textBox, TextBlock unitTextBlock, int milliseconds)
    {
        var (value, unit) = DelayFormatter.Split(milliseconds);
        textBox.Text = value;
        unitTextBlock.Text = unit;
    }

    private void ScheduleSaveState()
    {
        if (!IsInitialized || _isSwitchingWorkspace || _isRestoringWindowSelection)
            return;

        _stateSaveTimer.Stop();
        _stateSaveTimer.Start();
    }

    private void SaveStateNow()
    {
        if (!IsInitialized)
            return;

        CaptureActiveWorkspaceState();
        MacroStateStore.Save(_workspaces, _activeWorkspaceIndex, _shortcutsEnabled, _settings);
    }

    private int GetLoopCount() =>
        int.TryParse(LoopCountTextBox.Text, out var loops) ? Math.Max(0, loops) : 0;

    private int GetTimerMs() => ParseDelayInput(TimerMinutesTextBox.Text, TimerUnitTextBlock.Text);

    private int ParseDelayInput(string valueText, string unitText)
    {
        if (!double.TryParse(valueText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
            return 0;

        var multiplier = unitText switch
        {
            "sec" => 1_000,
            "min" => 60_000,
            "hours" => 3_600_000,
            _ => 1
        };

        return Math.Max(0, (int)Math.Round(value * multiplier));
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (ShouldCancelCloseForTray())
        {
            e.Cancel = true;
            return;
        }

        if (!ConfirmCloseIfNeeded())
        {
            e.Cancel = true;
            return;
        }

        StopAllRunners();
        StopGlobalShortcutHook();
        _stateSaveTimer.Stop();
        SaveStateNow();
        DisposeTrayIcon();

        base.OnClosing(e);
    }
}
