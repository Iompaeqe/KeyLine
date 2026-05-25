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
    }

    private void LoopCountTextBox_TextChanged(object sender, TextChangedEventArgs e) => ScheduleSaveState();

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

    protected override void OnClosing(CancelEventArgs e)
    {
        _stateSaveTimer.Stop();
        SaveStateNow();

        base.OnClosing(e);
    }
}
