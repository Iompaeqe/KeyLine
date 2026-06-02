using System.ComponentModel;
using System.Windows.Threading;
using KeyLine.Services.Macro;

namespace KeyLine;

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
        Math.Max(0, _document.ActiveTimeline.LoopCount);

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
        ShutdownInspector();
        _stateSaveTimer.Stop();
        SaveStateNow();
        DisposeTrayIcon();

        base.OnClosing(e);
    }
}
