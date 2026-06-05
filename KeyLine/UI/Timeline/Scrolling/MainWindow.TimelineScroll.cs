using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using KeyLine.Domain;
using KeyLine.State;

namespace KeyLine;

public partial class MainWindow
{
    private const double MinimumTimelineThumbWidth = 44;
    private const double TimelineOverflowTolerance = 8;
    private const double TimelineWheelScrollAmount = 80;

    private void TimelineScrollViewer_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateTimelineScrollIndicator();
    }

    private void TimelineScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateTimelineScrollIndicator();
    }

    private void TimelineScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Width changes should reveal more timeline content, not shift/rebuild rows.
        // Keep all row canvases at least as wide as the new viewport.
        EnsureTimelineCanvasWidthForAllRows(GetMinimumTimelineCanvasWidth());
        UpdateTimelineScrollIndicator();
    }

    private void TimelineScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_recorder.IsRecording)
        {
            RecordMouseScroll(e.Delta);
            e.Handled = true;
            return;
        }

        if (!HasTimelineOverflow(TimelineScrollViewer.ExtentWidth, TimelineScrollViewer.ViewportWidth))
            return;

        var direction = e.Delta > 0 ? -1 : 1;
        TimelineScrollViewer.ScrollToHorizontalOffset(
            TimelineScrollViewer.HorizontalOffset + (direction * TimelineWheelScrollAmount));

        UpdateTimelineScrollIndicator();
        e.Handled = true;
    }

    private void UpdateTimelineScrollIndicator()
    {
        if (!CanUpdateTimelineScrollIndicator())
            return;

        var trackWidth = TimelineScrollIndicator.ActualWidth;
        if (trackWidth <= 0)
            return;

        TimelineScrollTrack.Width = trackWidth;

        var extentWidth = TimelineScrollViewer.ExtentWidth;
        var viewportWidth = TimelineScrollViewer.ViewportWidth;

        if (!HasTimelineOverflow(extentWidth, viewportWidth))
        {
            ShowFullTimelineThumb(trackWidth);
            return;
        }

        UpdateOverflowTimelineThumb(trackWidth, extentWidth, viewportWidth);
    }

    private bool CanUpdateTimelineScrollIndicator()
    {
        return TimelineScrollViewer != null &&
               TimelineScrollTrack != null &&
               TimelineScrollThumb != null &&
               TimelineScrollIndicator != null;
    }

    private static bool HasTimelineOverflow(double extentWidth, double viewportWidth)
    {
        return extentWidth > viewportWidth + TimelineOverflowTolerance;
    }

    private void ShowFullTimelineThumb(double trackWidth)
    {
        TimelineScrollThumb.Width = trackWidth;
        TimelineScrollThumb.Opacity = 0.22;
        Canvas.SetLeft(TimelineScrollThumb, 0);
    }

    private void UpdateOverflowTimelineThumb(double trackWidth, double extentWidth, double viewportWidth)
    {
        TimelineScrollThumb.Opacity = 0.95;

        var thumbWidth = CalculateTimelineThumbWidth(trackWidth, extentWidth, viewportWidth);
        var thumbLeft = CalculateTimelineThumbLeft(trackWidth, thumbWidth, extentWidth, viewportWidth);

        TimelineScrollThumb.Width = thumbWidth;
        Canvas.SetLeft(TimelineScrollThumb, thumbLeft);
    }

    private static double CalculateTimelineThumbWidth(double trackWidth, double extentWidth, double viewportWidth)
    {
        var thumbWidth = viewportWidth / extentWidth * trackWidth;
        thumbWidth = Math.Max(MinimumTimelineThumbWidth, thumbWidth);
        return Math.Min(thumbWidth, trackWidth);
    }

    private double CalculateTimelineThumbLeft(double trackWidth, double thumbWidth, double extentWidth, double viewportWidth)
    {
        var maxOffset = extentWidth - viewportWidth;
        var maxThumbLeft = trackWidth - thumbWidth;

        return maxOffset <= 0
            ? 0
            : TimelineScrollViewer.HorizontalOffset / maxOffset * maxThumbLeft;
    }

    private void TimelineGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_recorder.IsRecording)
            return;

        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        if (IsInsideInteractiveTimelineElement(e.OriginalSource as DependencyObject))
            return;

        DeselectSelectedTimelineNode();

        if (!IsInsideScrollableTimelineContent(e.GetPosition(TimelineGrid)))
            return;

        _drag.BeginTimelinePan(
            e.GetPosition(TimelineScrollViewer),
            TimelineScrollViewer.HorizontalOffset);

        TimelineGrid.CaptureMouse();
        TimelineGrid.Cursor = Cursors.SizeWE;

        e.Handled = true;
    }

    private void TimelineGrid_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_recorder.IsRecording || !IsInsideScrollableTimelineContent(e.GetPosition(TimelineGrid)))
            return;

        if (!TryGetRecordedMouseButton(e.ChangedButton, out var mouseButton))
            return;

        RecordMouseDown(mouseButton);
        e.Handled = true;
    }

    private void TimelineGrid_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_drag.IsTimelinePanning)
            return;

        var currentMouse = e.GetPosition(TimelineScrollViewer);
        var targetOffset = _drag.GetTimelinePanTargetOffset(currentMouse);

        TimelineScrollViewer.ScrollToHorizontalOffset(targetOffset);
        UpdateTimelineScrollIndicator();

        e.Handled = true;
    }

    private void TimelineGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_recorder.IsRecording)
            return;

        if (!_drag.IsTimelinePanning)
            return;

        StopTimelinePanning();
        e.Handled = true;
    }

    private void TimelineGrid_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_recorder.IsRecording || !IsInsideScrollableTimelineContent(e.GetPosition(TimelineGrid)))
            return;

        if (!TryGetRecordedMouseButton(e.ChangedButton, out var mouseButton))
            return;

        RecordMouseUp(mouseButton);
        e.Handled = true;
    }

    private static bool TryGetRecordedMouseButton(MouseButton button, out int mouseButton)
    {
        mouseButton = button switch
        {
            MouseButton.Left => 1,
            MouseButton.Right => 2,
            MouseButton.Middle => 3,
            MouseButton.XButton1 => 4,
            MouseButton.XButton2 => 5,
            _ => 0
        };

        return mouseButton != 0;
    }

    private void TimelineGrid_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_drag.IsTimelinePanning)
            return;

        if (e.LeftButton != MouseButtonState.Pressed)
            StopTimelinePanning();
    }

    private void StopTimelinePanning()
    {
        if (!_drag.IsTimelinePanning)
            return;

        _drag.EndTimelinePan();

        if (TimelineGrid.IsMouseCaptured)
            TimelineGrid.ReleaseMouseCapture();

        TimelineGrid.Cursor = Cursors.Arrow;
    }

    private void DeselectSelectedTimelineNode()
    {
        if (!_selection.HasNodeSelection)
            return;

        Keyboard.ClearFocus();
        _selection.Clear();
        RefreshTimeline();
    }
    private bool IsInsideScrollableTimelineContent(Point positionInTimelineGrid)
    {
        if (TimelineGrid == null || TimelineScrollViewer == null)
            return false;

        var scrollViewerTopLeft = TimelineScrollViewer.TranslatePoint(new Point(0, 0), TimelineGrid);

        return positionInTimelineGrid.X >= scrollViewerTopLeft.X &&
               positionInTimelineGrid.Y >= scrollViewerTopLeft.Y &&
               positionInTimelineGrid.X <= scrollViewerTopLeft.X + TimelineScrollViewer.ActualWidth &&
               positionInTimelineGrid.Y <= scrollViewerTopLeft.Y + TimelineScrollViewer.ActualHeight;
    }
    
    private static bool IsInsideInteractiveTimelineElement(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is Button ||
                source is TextBox ||
                source is ComboBox)
            {
                return true;
            }

            if (source is FrameworkElement fe &&
                (fe.Tag is MacroNode || fe.Tag is MacroTimeline))
            {
                return true;
            }

            source = GetSafeUiParent(source);
        }

        return false;
    }

    private static DependencyObject? GetSafeUiParent(DependencyObject source)
    {
        if (source is Visual or Visual3D)
            return VisualTreeHelper.GetParent(source);

        if (source is FrameworkContentElement contentElement)
            return contentElement.Parent;

        return null;
    }
}
