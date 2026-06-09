using System.ComponentModel;
using System.Windows;
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
        MacroStateStore.Save(
            _workspaces,
            _activeWorkspaceIndex,
            AnyMacroShortcutEnabled(),
            _settings,
            GetPersistedMainWindowWidth(),
            _profiles,
            _activeProfileId);
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged)
            ScheduleSaveState();
    }

    private double GetPersistedMainWindowWidth()
    {
        var width = WindowState == WindowState.Minimized && RestoreBounds.Width > 0
            ? RestoreBounds.Width
            : ActualWidth > 0
                ? ActualWidth
                : Width;

        if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
            return MinWidth;

        return Math.Max(MinWidth, width);
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

        // Closing KeyLine is a hard stop: End hooks are skipped.
        _appIsClosing = true;
        StopAllRunners();
        StopGlobalShortcutHook();
        ShutdownInspector();
        _stateSaveTimer.Stop();
        SaveStateNow();
        DisposeTrayIcon();

        base.OnClosing(e);
    }
}
