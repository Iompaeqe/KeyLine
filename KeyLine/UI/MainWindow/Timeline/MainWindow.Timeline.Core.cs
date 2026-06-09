using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KeyLine.Domain;
using KeyLine.Services.Macro;
using KeyLine.Services.Playback;
using KeyLine.Services.Timeline;
using KeyLine.State;
using KeyLine.UI.Config;
using KeyLine.UI.Nodes;
using KeyLine.UI.Timeline;

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

    private readonly Dictionary<MacroTimeline, TimelineRowRenderState> _timelineRowRenderStates = new();
    private readonly Dictionary<MacroTimeline, TextBlock> _timelineHeaderStatusTextBlocks = new();
    private readonly Dictionary<MacroTimeline, Border> _timelineHeaderStatusDots = new();

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
        RefreshTimeline();
        ScheduleSaveState();
    }

    // A hook header highlights as "active" when it is the current selection; normal timelines
    // keep their existing active-timeline highlight.
    private bool IsActiveDisplayTimeline(MacroTimeline timeline) =>
        IsHookTimeline(timeline)
            ? ReferenceEquals(timeline, _selection.SelectedTimeline)
            : ReferenceEquals(timeline, _document.ActiveTimeline);

    private sealed class TimelineRowVisualModel
    {
        public required MacroTimeline Timeline { get; init; }
        public required IReadOnlyList<TimelineVisualItem> VisualItems { get; init; }
        public required bool IsFirstRow { get; init; }
        public required bool IsLastRow { get; init; }

        public double RowWidth
        {
            get
            {
                if (VisualItems.Count == 0)
                    return TimelineFirstItemLeft + TimelineRightPadding;

                var lastItem = VisualItems[^1];
                return lastItem.Left + lastItem.Width + TimelineRightPadding;
            }
        }
    }

    private void RefreshTimeline(object? sender = null, RoutedEventArgs? e = null)
    {
        RefreshTimelineCore(refreshInspector: true);
    }

    private void RefreshTimelineWithoutInspector()
    {
        RefreshTimelineCore(refreshInspector: false);
    }

    private void RefreshTimelineCore(bool refreshInspector)
    {
        if (TimelineRowsPanel == null)
            return;

        TimelineRowsPanel.Children.Clear();
        _timelineRowRenderStates.Clear();
        BuildTimelineHeaderGridRows();
        UpdateSingleTimelineMetadataText();

        if (_document.Timelines.Count == 0)
        {
            ShowEmptyTimelineState();
            UpdateTimelineOptionsPagerVisibility();
            UpdateWindowHeightForTimelineCount();
            Dispatcher.BeginInvoke(new Action(UpdateTimelineScrollIndicator));
            return;
        }

        HideEmptyTimelineState();
        if (refreshInspector)
            SyncOptionsFromActiveTimeline();

        TimelineRowsPanel.Margin = TimelineLayoutCalculator.GetRowsPanelMargin(TimelineHeaderTopExtra);

        var rowModels = BuildTimelineRowVisualModels();

        var canvasWidth = GetTimelineCanvasWidth(rowModels);
        SetTimelineCanvasWidthForAllRows(canvasWidth);

        for (var i = 0; i < rowModels.Count; i++)
        {
            var rowModel = rowModels[i];
            var timeline = rowModel.Timeline;

            if (rowModels.Count > 1)
            {
                var isActive = IsActiveDisplayTimeline(timeline);
                var isSelected = _selection.IsTimelineSelected(timeline);

                AddTimelineHeaderToGrid(
                    timeline,
                    i,
                    isActive,
                    isSelected);
            }

            TimelineRowsPanel.Children.Add(CreateTimelineRow(
                timeline,
                rowModel.VisualItems,
                canvasWidth,
                rowModel.IsFirstRow,
                rowModel.IsLastRow));
        }

        UpdateTimelineOptionsPagerVisibility();
        UpdateWindowHeightForTimelineCount();

        Dispatcher.BeginInvoke(new Action(UpdateTimelineScrollIndicator));
    }

    private void BuildTimelineHeaderGridRows()
    {
        TimelineHeaderGrid.Children.Clear();
        TimelineHeaderGrid.RowDefinitions.Clear();
        _timelineHeaderStatusTextBlocks.Clear();
        _timelineHeaderStatusDots.Clear();

        var displayTimelines = GetDisplayTimelines();
        var displayCount = displayTimelines.Count;
        var showHeaderColumn = displayCount > 1;

        TimelineHeaderColumn.Width = showHeaderColumn
            ? new GridLength(TimelineHeaderWidth)
            : new GridLength(0);

        if (!showHeaderColumn)
        {
            TimelineHeaderGrid.Visibility = Visibility.Collapsed;
            return;
        }

        TimelineHeaderGrid.Visibility = Visibility.Visible;

        TimelineHeaderGrid.RowDefinitions.Add(
            TimelineLayoutCalculator.CreateHeaderTopExtraRow(TimelineHeaderTopExtra));

        for (var i = 0; i < displayCount; i++)
        {
            var timeline = displayTimelines[i];

            TimelineHeaderGrid.RowDefinitions.Add(
                TimelineLayoutCalculator.CreateTimelineHeaderContentRow(GetDisplayRowHeight(timeline)));

            TimelineHeaderGrid.RowDefinitions.Add(
                TimelineLayoutCalculator.CreateTimelineHeaderGapRow(
                    i,
                    displayCount,
                    GetDisplayRowGap(timeline),
                    TimelineHeaderBottomExtra));
        }
    }

    private void AddTimelineHeaderToGrid(MacroTimeline timeline, int timelineIndex, bool isActive, bool isSelected)
    {
        var isFirst = timelineIndex == 0;
        var isLast = timelineIndex == GetDisplayTimelines().Count - 1;
        var header = CreateTimelineHeader(timeline, isActive, isSelected, isFirst, isLast);

        var row = TimelineLayoutCalculator.GetHeaderGridRow(timelineIndex);
        var rowSpan = TimelineLayoutCalculator.GetHeaderGridRowSpan(timelineIndex);

        Grid.SetRow(header, row);
        Grid.SetRowSpan(header, rowSpan);

        TimelineHeaderGrid.Children.Add(header);
    }

    private void ShowEmptyTimelineState()
    {
        TimelineRowsPanel.Margin = new Thickness(0);
        TimelineHeaderGrid.Visibility = Visibility.Collapsed;
        TimelineHeaderColumn.Width = new GridLength(0);
        SingleTimelineMetadataText.Visibility = Visibility.Collapsed;

        EmptyTimelinePanel.Visibility = Visibility.Visible;
        TimelineScrollViewer.Visibility = Visibility.Hidden;
        TimelineDragOverlayCanvas.Visibility = Visibility.Collapsed;
        TimelineScrollIndicator.Visibility = Visibility.Collapsed;
    }

    private void HideEmptyTimelineState()
    {
        EmptyTimelinePanel.Visibility = Visibility.Collapsed;
        TimelineScrollViewer.Visibility = Visibility.Visible;
        TimelineDragOverlayCanvas.Visibility = Visibility.Visible;
        TimelineScrollIndicator.Visibility = Visibility.Visible;
        UpdateSingleTimelineMetadataText();
    }

}
