using System.Collections.Generic;
using System.Linq;
using KeyLine.Domain;
using KeyLine.UI.Config;

namespace KeyLine;

public partial class MainWindow
{
    private static TimelineUiConfig TimelineUi => GeneratedUiConfig.Timeline;

    private static double TimelineRowHeight => TimelineUi.RowHeight;
    private static double TimelineRowGap => TimelineUi.RowGap;
    private static double TimelineHeaderWidth => TimelineUi.HeaderWidth;

    private static double TimelineFirstItemLeft => TimelineUi.FirstItemLeft;
    private static double TimelineItemGap => TimelineUi.ItemGap;
    private static double TimelineRightPadding => TimelineUi.RightPadding;

    private static double TimelineConnectorY => TimelineUi.ConnectorY;
    private static double TimelineConnectorThickness => TimelineUi.ConnectorThickness;

    private static double TimelineHeaderTopExtra => TimelineUi.HeaderTopExtra;
    private static double TimelineHeaderBottomExtra => TimelineUi.HeaderBottomExtra;

    // The ordered timelines shown in the strip: enabled Start hook, normal timelines, enabled End hook.
    // Rendering uses this list; logic (playback selection, reorder, delete) uses Document.Timelines.
    private List<MacroTimeline> GetDisplayTimelines() => _activeWorkspace.EnumerateDisplayTimelines().ToList();

    private bool IsHookTimeline(MacroTimeline timeline) => _activeWorkspace.IsHookTimeline(timeline);

    private const double CollapsedRowHeight = 30;
    private const double CollapsedRowGap = 6;

    // Collapse only takes visual effect when the header column (and its chevron) is shown, i.e.
    // when there is more than one display timeline — otherwise a lone timeline could not be expanded.
    private bool IsEffectivelyCollapsed(MacroTimeline timeline) =>
        timeline.IsCollapsed && GetDisplayTimelines().Count > 1;

    private double GetDisplayRowHeight(MacroTimeline timeline) =>
        IsEffectivelyCollapsed(timeline) ? CollapsedRowHeight : TimelineRowHeight;

    private double GetDisplayRowGap(MacroTimeline timeline) =>
        IsEffectivelyCollapsed(timeline) ? CollapsedRowGap : TimelineRowGap;

    private void ToggleTimelineCollapsed(MacroTimeline timeline)
    {
        timeline.IsCollapsed = !timeline.IsCollapsed;
        // Targeted collapse: reuse the row's existing node visuals (just hide/show the container)
        // instead of rebuilding every timeline and node.
        RefreshTimelineCollapse(timeline);
        ScheduleSaveState();
    }

    private void ToggleTimelineDisabled(MacroTimeline timeline)
    {
        if (!_isTimelineEditingEnabled || IsHookTimeline(timeline))
            return;

        timeline.IsDisabled = !timeline.IsDisabled;
        ResetTimelineDeleteConfirmation();
        // Disabled state only affects the header (dimming, status, and the enable/disable menu
        // label); the node rows are unchanged, so a header-only rebuild suffices.
        RefreshTimelineHeaders();
        ScheduleSaveState();
    }

    // A hook header highlights as "active" when it is the current selection; normal timelines
    // keep their existing active-timeline highlight.
    private bool IsActiveDisplayTimeline(MacroTimeline timeline) =>
        IsHookTimeline(timeline)
            ? ReferenceEquals(timeline, _selection.SelectedTimeline)
            : ReferenceEquals(timeline, _document.ActiveTimeline);
}
