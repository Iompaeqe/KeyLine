using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Threading;
using MacroSpammer.Services.Macro;

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
        MacroStateStore.Save(_workspaces, _activeWorkspaceIndex);
    }

    private int GetLoopCount() =>
        int.TryParse(LoopCountTextBox.Text, out var loops) ? Math.Max(0, loops) : 0;

    private int GetTimerMinutes() =>
        int.TryParse(TimerMinutesTextBox.Text, out var minutes) ? Math.Max(0, minutes) : 0;

    protected override void OnClosing(CancelEventArgs e)
    {
        StopAllRunners();
        _stateSaveTimer.Stop();
        SaveStateNow();

        base.OnClosing(e);
    }
}
