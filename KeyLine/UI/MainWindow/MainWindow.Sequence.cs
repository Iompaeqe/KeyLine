using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Threading;
using KeyLine.Domain;
using KeyLine.Services.Playback;

namespace KeyLine;

public partial class MainWindow
{
    private readonly SequenceController _sequence = new();
    private DispatcherTimer? _sequenceHeaderTimer;

    private static bool IsSequenceMode(MacroWorkspace workspace) =>
        workspace.LoopMode is MacroLoopMode.Sequence;

    // Refreshes timeline header status (CD countdown / Ready) while in Sequence and idle, shows/hides
    // the Reset option, and shows the Sequence Mode selector only when Loop Mode is Sequence.
    // Called on workspace apply, loop-mode change, and stop/start.
    private void UpdateSequenceModeUi()
    {
        _sequenceHeaderTimer ??= CreateSequenceHeaderTimer();

        var isSequence = IsSequenceMode(_activeWorkspace);

        var showsSequenceStatus = isSequence && !IsWorkspaceRunning(_activeWorkspace);
        if (showsSequenceStatus)
            _sequenceHeaderTimer.Start();
        else
            _sequenceHeaderTimer.Stop();

        SequenceModePanel.Visibility = isSequence
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;
        SetSequenceModeSelection(_activeWorkspace.SequenceMode);

        UpdateResetOptionVisibility();
    }

    private DispatcherTimer CreateSequenceHeaderTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) =>
        {
            if (IsSequenceMode(_activeWorkspace) && !IsWorkspaceRunning(_activeWorkspace))
                RefreshTimelineHeaderStatuses();
            else
                _sequenceHeaderTimer?.Stop();
        };
        return timer;
    }

    /// <summary>
    /// The timelines to run for a single trigger. For Sequence/Random this is the one selected
    /// timeline (or empty if none is ready). For the other loop modes it is all runnable normals.
    /// </summary>
    private List<MacroTimeline> GetTriggerRunnableTimelines(MacroWorkspace workspace, out MacroTimeline? sequenceSelected)
    {
        sequenceSelected = null;

        if (IsSequenceMode(workspace))
        {
            sequenceSelected = _sequence.SelectNext(
                workspace,
                workspace.Document.Timelines.ToList(),
                workspace.SequenceMode,
                System.DateTime.UtcNow);

            return sequenceSelected == null
                ? new List<MacroTimeline>()
                : new List<MacroTimeline> { sequenceSelected };
        }

        return workspace.Document.Timelines
            .Where(timeline => timeline.Nodes.Count > 0 && !timeline.IsDisabled)
            .ToList();
    }

    private void OnSequenceRunCompleted(MacroWorkspace workspace, MacroTimeline? sequenceSelected)
    {
        if (sequenceSelected == null)
            return;

        _sequence.MarkPlayed(
            workspace,
            workspace.Document.Timelines.ToList(),
            sequenceSelected,
            System.DateTime.UtcNow,
            workspace.SequenceMode);

        if (ReferenceEquals(workspace, _activeWorkspace))
            RefreshTimelineHeaderStatuses();
    }

    private void ResetSequenceState(MacroWorkspace workspace)
    {
        _sequence.Reset(workspace);

        if (ReferenceEquals(workspace, _activeWorkspace))
        {
            RefreshTimelineHeaderStatuses();
            SetWarningStatus("Sequence reset.");
        }
    }

    // --- Reset option UI (button + reset shortcut recorder), shown only for Sequence/Random ---
    private bool _isCapturingResetShortcut;
    private readonly List<int> _capturedResetKeys = new();
    private readonly HashSet<int> _resetCaptureDownKeys = new();

    private void InitializeResetUi()
    {
        // One merged control: left-click resets now, right-click assigns a reset shortcut, and
        // middle-click clears it. The bound key is shown inline in the pill text.
        ResetPill.MouseLeftButtonDown += (_, e) =>
        {
            if (_isCapturingResetShortcut)
                return;

            ResetSequenceState(_activeWorkspace);
            e.Handled = true;
        };

        ResetPill.MouseRightButtonDown += (_, e) =>
        {
            BeginResetShortcutCapture();
            e.Handled = true;
        };

        ResetPill.MouseDown += (_, e) =>
        {
            if (e.ChangedButton != System.Windows.Input.MouseButton.Middle)
                return;

            CommitResetShortcut(System.Array.Empty<int>());
            e.Handled = true;
        };

        ResetPill.PreviewKeyDown += ResetShortcutPill_PreviewKeyDown;
        ResetPill.PreviewKeyUp += ResetShortcutPill_PreviewKeyUp;
        ResetPill.LostKeyboardFocus += (_, _) =>
        {
            if (_isCapturingResetShortcut)
                CancelResetShortcutCapture();
        };
    }

    private void UpdateResetOptionVisibility()
    {
        var show = IsSequenceMode(_activeWorkspace);
        ResetPill.Visibility = show ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        if (!show && _isCapturingResetShortcut)
            CancelResetShortcutCapture();

        UpdateResetShortcutText();
    }

    private void UpdateResetShortcutText()
    {
        if (_isCapturingResetShortcut)
            return;

        // Capture sets a local Cyan foreground on the text; clear it so the OptionsShortcutText style
        // (and its hover/focus triggers) drives the color again once recording is over.
        ResetPill.DisplayTextBlock.ClearValue(System.Windows.Controls.TextBlock.ForegroundProperty);

        var keys = _activeWorkspace.ResetShortcutKeys;
        ResetPill.Text = string.IsNullOrWhiteSpace(keys)
            ? "Reset"
            : $"{KeyLine.Services.Input.ShortcutGesture.Format(keys)}  ·  Reset";
    }

    private void BeginResetShortcutCapture()
    {
        _isCapturingResetShortcut = true;
        _capturedResetKeys.Clear();
        _resetCaptureDownKeys.Clear();
        ResetPill.Text = "press reset key…";
        ResetPill.DisplayTextBlock.Foreground = (SolidColorBrush)FindResource("Cyan");
        ResetPill.Focus();
        System.Windows.Input.Keyboard.Focus(ResetPill);

        // LostKeyboardFocus only fires when the click lands on a focusable element (e.g. the timeline).
        // Watch all mouse-downs at the window level so clicking anywhere outside the pill also stops it.
        // Remove first so re-entering capture never double-subscribes.
        PreviewMouseDown -= ResetCaptureWindowMouseDown;
        PreviewMouseDown += ResetCaptureWindowMouseDown;
    }

    // Stops reset-shortcut recording when the user clicks anywhere outside the ResetPill.
    private void ResetCaptureWindowMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_isCapturingResetShortcut || ResetPill.IsMouseOver)
            return;

        CancelResetShortcutCapture();
    }

    private void CancelResetShortcutCapture()
    {
        _isCapturingResetShortcut = false;
        _capturedResetKeys.Clear();
        _resetCaptureDownKeys.Clear();
        PreviewMouseDown -= ResetCaptureWindowMouseDown;
        UpdateResetShortcutText();
    }

    private void ResetShortcutPill_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_isCapturingResetShortcut)
            return;

        e.Handled = true;

        if (e.Key == System.Windows.Input.Key.Escape)
        {
            CancelResetShortcutCapture();
            return;
        }

        if (e.Key is System.Windows.Input.Key.Back or System.Windows.Input.Key.Delete)
        {
            CommitResetShortcut(System.Array.Empty<int>());
            return;
        }

        var virtualKey = GetVirtualKeyFromKeyEvent(e);
        if (virtualKey <= 0)
            return;

        _resetCaptureDownKeys.Add(virtualKey);
        if (!_capturedResetKeys.Contains(virtualKey) &&
            _capturedResetKeys.Count < KeyLine.Services.Input.ShortcutGesture.MaxKeyCount)
        {
            _capturedResetKeys.Add(virtualKey);
        }

        ResetPill.Text = $"{KeyLine.Services.Input.ShortcutGesture.Format(_capturedResetKeys)} * Reset";
    }

    private void ResetShortcutPill_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_isCapturingResetShortcut)
            return;

        e.Handled = true;

        var virtualKey = GetVirtualKeyFromKeyEvent(e);
        if (virtualKey > 0)
            _resetCaptureDownKeys.Remove(virtualKey);

        if (_capturedResetKeys.Count > 0 && _resetCaptureDownKeys.Count == 0)
            CommitResetShortcut(_capturedResetKeys);
    }

    private void CommitResetShortcut(IEnumerable<int> virtualKeys)
    {
        var keys = virtualKeys
            .Select(KeyLine.Services.Input.ShortcutGesture.NormalizeVirtualKey)
            .Where(key => key > 0)
            .Distinct()
            .Take(KeyLine.Services.Input.ShortcutGesture.MaxKeyCount)
            .ToArray();

        _isCapturingResetShortcut = false;
        _capturedResetKeys.Clear();
        _resetCaptureDownKeys.Clear();
        PreviewMouseDown -= ResetCaptureWindowMouseDown;

        _activeWorkspace.ResetShortcutKeys = keys.Length > 0
            ? KeyLine.Services.Input.ShortcutGesture.Serialize(keys)
            : "";

        UpdateResetShortcutText();
        ApplyShortcutHookState();
        System.Windows.Input.Keyboard.ClearFocus();
        ScheduleSaveState();
    }
}
