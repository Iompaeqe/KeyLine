using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using KeyLine.Domain;

namespace KeyLine.UI.Timeline;

public sealed partial class TimelineRenderer
{
    private void AddTimelineItem(Canvas canvas, TimelineVisualItem item)
    {
        var targetLeft = item.Left;
        var targetTop = TimelineLayoutCalculator.GetItemTop(TimelineConnectorY, item.Size.Height);

        canvas.Children.Add(item.Element);

        Canvas.SetLeft(item.Element, targetLeft);
        Canvas.SetTop(item.Element, targetTop);

        if (item.AnimationKey == null)
            return;

        var targetPosition = new Point(targetLeft, targetTop);

        if (!IsDropPlaceholderAnimationKey(item.AnimationKey) &&
            _timelineVisualPositions.TryGetValue(item.AnimationKey, out var previousPosition))
        {
            var deltaX = previousPosition.X - targetLeft;
            var deltaY = previousPosition.Y - targetTop;

            if ((Math.Abs(deltaX) > 0.5 || Math.Abs(deltaY) > 0.5) &&
                IsTimelineItemNearViewport(previousPosition.X, targetLeft, item.Width))
            {
                TimelineAnimationService.AnimateRenderOffsetToRest(item.Element, deltaX, deltaY,
                    animateY: Math.Abs(deltaY) > 0.5);
            }
        }

        _timelineVisualPositions[item.AnimationKey] = targetPosition;
    }

    private bool IsTimelineItemNearViewport(double previousLeft, double targetLeft, double width)
    {
        if (TimelineScrollViewer == null)
            return true;

        var viewportWidth = TimelineScrollViewer.ViewportWidth > 0
            ? TimelineScrollViewer.ViewportWidth
            : TimelineScrollViewer.ActualWidth;

        if (viewportWidth <= 0)
            return true;

        const double animationOverscan = 96;
        var viewportLeft = TimelineScrollViewer.HorizontalOffset - animationOverscan;
        var viewportRight = TimelineScrollViewer.HorizontalOffset + viewportWidth + animationOverscan;

        return IntersectsHorizontalRange(previousLeft, width, viewportLeft, viewportRight) ||
               IntersectsHorizontalRange(targetLeft, width, viewportLeft, viewportRight);
    }

    private static bool IntersectsHorizontalRange(double left, double width, double viewportLeft, double viewportRight)
    {
        var right = left + Math.Max(0, width);
        return right >= viewportLeft && left <= viewportRight;
    }

    private static bool IsDropPlaceholderAnimationKey(object? animationKey)
    {
        return animationKey is ITuple tuple &&
               tuple.Length >= 2 &&
               tuple[1] is string marker &&
               marker == "drop-placeholder";
    }

    private static bool IsDropPlaceholderAnimationKeyForTimeline(object? animationKey, MacroTimeline timeline)
    {
        return animationKey is ITuple tuple &&
               tuple.Length >= 2 &&
               ReferenceEquals(tuple[0], timeline) &&
               tuple[1] is string marker &&
               marker == "drop-placeholder";
    }

    private static bool IsBlockAddAnimationKey(object? animationKey)
    {
        return animationKey is ITuple tuple &&
               tuple.Length >= 2 &&
               tuple[1] is string marker &&
               marker == "block-add";
    }
}
