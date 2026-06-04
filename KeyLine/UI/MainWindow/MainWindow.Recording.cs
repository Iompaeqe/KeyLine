using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using KeyLine.Domain;
using KeyLine.Services.Timeline;

namespace KeyLine;

public partial class MainWindow
{
    private const double RecordingFollowRightOverscan = 96;

    private void StartRecording(MacroTimeline timeline, MacroNode? rawInsertAnchor = null)
    {
        SelectTimeline(timeline);

        _recordingTimeline = timeline;
        _recordingRawInsertAnchor = rawInsertAnchor != null && timeline.Nodes.Contains(rawInsertAnchor)
            ? rawInsertAnchor
            : null;
        _recorder.Start();

        RecordStopButtonHost.IsHitTestVisible = true;
        RecordStopButtonHost.Visibility = Visibility.Visible;
        RecordingMouseNotice.Visibility = Visibility.Visible;
        StatusText.Text = $"● Recording {timeline.Name}";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));

        Focus();
    }

    private void StopRecording()
    {
        var timeline = _recordingTimeline;

        _recorder.Stop();
        _recordingTimeline = null;
        _recordingRawInsertAnchor = null;

        RecordStopButtonHost.Visibility = Visibility.Collapsed;
        RecordStopButtonHost.IsHitTestVisible = false;
        RecordingMouseNotice.Visibility = Visibility.Collapsed;
        StatusText.Text = "Stopped";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(61, 84, 112));

        if (timeline != null && MergeAdjacentDelayNodesIfEnabled(timeline))
            ScheduleSaveState();

        RefreshTimeline();
    }

    private void RecordStopButton_Click(object sender, RoutedEventArgs e) => StopRecording();

    private void RecordMouseDown(int mouseButton)
    {
        if (!_recorder.IsRecording)
            return;

        var timeline = _recordingTimeline ?? _document.ActiveTimeline;
        var addedSteps = _recorder.RecordMouseDown(mouseButton, ShouldIncludeRecordedDelay(timeline)).ToList();

        AppendRecordedInputSteps(timeline, addedSteps);
    }

    private void RecordMouseUp(int mouseButton)
    {
        if (!_recorder.IsRecording)
            return;

        var timeline = _recordingTimeline ?? _document.ActiveTimeline;
        var addedSteps = _recorder.RecordMouseUp(mouseButton, ShouldIncludeRecordedDelay(timeline)).ToList();

        AppendRecordedInputSteps(timeline, addedSteps);
    }

    private void AppendRecordedInputSteps(MacroTimeline timeline, List<MacroNode> addedSteps)
    {
        if (addedSteps.Count == 0)
            return;

        SaveUndoSnapshot();

        var insertIndex = GetRecordingInsertIndex(timeline);
        for (var i = 0; i < addedSteps.Count; i++)
            timeline.Nodes.Insert(insertIndex + i, addedSteps[i]);

        if (_recordingRawInsertAnchor == null)
            AppendRecordedStepsToTimelineRow(timeline, addedSteps);
        else
            RefreshTimelineRow(timeline);

        ScrollAfterRecordingAppend(timeline, addedSteps);

        ScheduleSaveState();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_recorder.IsRecording)
            CancelTimelineDragState();

        if (IsShortcutCaptureActive())
            return;

        if (!_recorder.IsRecording && TryHandleEditingShortcut(e))
            return;

        if (!_recorder.IsRecording && e.Key == Key.Delete)
        {
            DeleteSelectedItem();
            e.Handled = true;
            return;
        }

        if (!_recorder.IsRecording)
            return;

        var timeline = _recordingTimeline ?? _document.ActiveTimeline;
        var addedSteps = _recorder.RecordKeyDown(e, ShouldIncludeRecordedDelay(timeline)).ToList();

        e.Handled = true;
        AppendRecordedInputSteps(timeline, addedSteps);
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (!_recorder.IsRecording)
            return;

        var timeline = _recordingTimeline ?? _document.ActiveTimeline;
        var addedSteps = _recorder.RecordKeyUp(e, ShouldIncludeRecordedDelay(timeline)).ToList();

        e.Handled = true;
        AppendRecordedInputSteps(timeline, addedSteps);
    }

    private void ScrollAfterRecordingAppend(MacroTimeline timeline, IReadOnlyList<MacroNode> addedSteps)
    {
        var scrollToTimelineEnd = _recordingRawInsertAnchor == null;

        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            new Action(() =>
            {
                TimelineScrollViewer.ScrollToHorizontalOffset(scrollToTimelineEnd
                    ? GetTimelineRecordingFollowOffset(timeline)
                    : GetTimelineRecordingInsertedNodeOffset(timeline, addedSteps));
                UpdateTimelineScrollIndicator();
            }));
    }

    private double GetTimelineRecordingFollowOffset(MacroTimeline timeline)
    {
        if (TimelineScrollViewer == null)
            return 0;

        var viewportWidth = TimelineScrollViewer.ViewportWidth > 0
            ? TimelineScrollViewer.ViewportWidth
            : TimelineScrollViewer.ActualWidth;

        if (viewportWidth <= 0)
            return TimelineScrollViewer.HorizontalOffset;

        var rowWidth = _timelineRowRenderStates.TryGetValue(timeline, out var state)
            ? state.RowWidth
            : GetMinimumTimelineCanvasWidth();

        var contentEnd = TimelineScrollViewer.Padding.Left + rowWidth + RecordingFollowRightOverscan;
        var targetOffset = Math.Max(0, contentEnd - viewportWidth);
        var maxOffset = Math.Max(0, TimelineScrollViewer.ExtentWidth - viewportWidth);

        return Math.Clamp(targetOffset, 0, maxOffset);
    }

    private double GetTimelineRecordingInsertedNodeOffset(MacroTimeline timeline, IReadOnlyList<MacroNode> addedSteps)
    {
        if (TimelineScrollViewer == null)
            return 0;

        var viewportWidth = TimelineScrollViewer.ViewportWidth > 0
            ? TimelineScrollViewer.ViewportWidth
            : TimelineScrollViewer.ActualWidth;

        if (viewportWidth <= 0)
            return TimelineScrollViewer.HorizontalOffset;

        if (!_timelineRowRenderStates.TryGetValue(timeline, out var state))
            return TimelineScrollViewer.HorizontalOffset;

        foreach (var step in addedSteps.Reverse())
        {
            var animationKey = GetTimelineAnimationKey(timeline, step);
            if (!state.VisualItemsByAnimationKey.TryGetValue(animationKey, out var item))
                continue;

            var targetOffset = TimelineScrollViewer.Padding.Left + item.Left - (viewportWidth * 0.35);
            var maxOffset = Math.Max(0, TimelineScrollViewer.ExtentWidth - viewportWidth);
            return Math.Clamp(targetOffset, 0, maxOffset);
        }

        return TimelineScrollViewer.HorizontalOffset;
    }

    private int GetRecordingInsertIndex(MacroTimeline timeline)
    {
        if (_recordingRawInsertAnchor == null || !timeline.Nodes.Contains(_recordingRawInsertAnchor))
            return timeline.Nodes.Count;

        var anchorIndex = timeline.Nodes.IndexOf(_recordingRawInsertAnchor);
        return anchorIndex >= 0 ? anchorIndex : timeline.Nodes.Count;
    }

    private bool ShouldIncludeRecordedDelay(MacroTimeline timeline)
    {
        var insertIndex = GetRecordingInsertIndex(timeline);
        return timeline.Nodes
            .Take(insertIndex)
            .Any(node => !TimelineBlockService.IsControlNode(node));
    }
}
