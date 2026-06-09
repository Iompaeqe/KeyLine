using System.Windows;
using System.Windows.Controls;

namespace KeyLine.UI.Timeline;

public static class TimelineLayoutCalculator
{
    public static Thickness GetRowsPanelMargin(double headerTopExtra)
    {
        return new Thickness(0, headerTopExtra, 0, 0);
    }

    public static double GetRowBottomMargin(bool isLastRow, double rowGap)
    {
        return isLastRow ? 0 : rowGap;
    }

    public static double GetItemTop(double connectorY, double itemHeight)
    {
        return connectorY - (itemHeight / 2.0);
    }

    public static double GetConnectorTop(double connectorY, double connectorThickness)
    {
        return connectorY - (connectorThickness / 2.0);
    }

    // Each header is its own card aligned to exactly its timeline content row (row 0 is the top
    // spacer, then content/gap rows alternate). Mapping to the content row only — rather than
    // spanning the spacer or the trailing stretch row — lets every header, including the first and
    // last, collapse to its row height instead of staying tall.
    public static int GetHeaderGridRow(int timelineIndex)
    {
        return 1 + (timelineIndex * 2);
    }

    public static int GetHeaderGridRowSpan(int timelineIndex)
    {
        return 1;
    }

    public static double GetTimelineAreaHeight(
        int timelineCount,
        double rowHeight,
        double rowGap,
        double extraHeight)
    {
        var safeTimelineCount = Math.Max(1, timelineCount);

        return (safeTimelineCount * rowHeight) +
               ((safeTimelineCount - 1) * rowGap) +
               extraHeight;
    }

    public static RowDefinition CreateHeaderTopExtraRow(double headerTopExtra)
    {
        return new RowDefinition
        {
            Height = new GridLength(headerTopExtra)
        };
    }

    public static RowDefinition CreateTimelineHeaderContentRow(double rowHeight)
    {
        return new RowDefinition
        {
            Height = new GridLength(rowHeight)
        };
    }

    public static RowDefinition CreateTimelineHeaderGapRow(
        int rowIndex,
        int timelineCount,
        double rowGap,
        double headerBottomExtra)
    {
        var isLast = rowIndex == timelineCount - 1;

        return new RowDefinition
        {
            // Gap rows are consumed by the header above them.
            // The final row absorbs remaining panel height so the last header
            // reaches the bottom of the timeline scroll viewer.
            Height = isLast
                ? new GridLength(1, GridUnitType.Star)
                : new GridLength(rowGap),
            MinHeight = isLast ? headerBottomExtra : 0
        };
    }
}
