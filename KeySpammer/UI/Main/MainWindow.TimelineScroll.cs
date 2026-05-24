using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KeySpammer.Domain;
using KeySpammer.State;

namespace KeySpammer;

public partial class MainWindow
{
    private const double MinimumTimelineThumbWidth = 44;
    private const double TimelineOverflowTolerance = 1;

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
        UpdateTimelineScrollIndicator();
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

    private void TimelineScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsInsideInteractiveTimelineElement(e.OriginalSource as DependencyObject))
            return;

        _drag.BeginTimelinePan(
            e.GetPosition(TimelineScrollViewer),
            TimelineScrollViewer.HorizontalOffset);

        TimelineScrollViewer.CaptureMouse();
        TimelineScrollViewer.Cursor = Cursors.SizeWE;

        e.Handled = true;
    }

    private void TimelineScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_drag.IsTimelinePanning)
            return;

        var currentMouse = e.GetPosition(TimelineScrollViewer);
        var targetOffset = _drag.GetTimelinePanTargetOffset(currentMouse);

        TimelineScrollViewer.ScrollToHorizontalOffset(targetOffset);
        UpdateTimelineScrollIndicator();

        e.Handled = true;
    }

    private void TimelineScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_drag.IsTimelinePanning)
            return;

        StopTimelinePanning();
        e.Handled = true;
    }

    private void TimelineScrollViewer_MouseLeave(object sender, MouseEventArgs e)
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

        if (TimelineScrollViewer.IsMouseCaptured)
            TimelineScrollViewer.ReleaseMouseCapture();

        TimelineScrollViewer.Cursor = Cursors.Arrow;
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

            if (source is FrameworkElement fe && (fe.Tag is MacroStep || fe.Tag is MacroTimeline))
                return true;

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }
}