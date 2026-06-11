using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KeyLine.Domain;
using KeyLine.Services.Macro;
using KeyLine.Services.Playback;
using KeyLine.State;
using KeyLine.UI.Config;
using KeyLine.UI.Nodes;

namespace KeyLine.UI.Timeline;

/// <summary>
/// Owns the timeline canvas + header visuals and the render-state caches, and exposes the tiered
/// refresh API the host calls: full rebuild (<see cref="RefreshTimeline"/> /
/// <see cref="RefreshTimelineCore"/>), per-timeline (<see cref="RefreshTimelineRow"/>), per-node
/// (<see cref="RefreshTimelineNode"/>), and visual-state only (<see cref="UpdateSelectionVisuals"/>,
/// header status). Everything it needs from the host is supplied through <see cref="TimelineRenderContext"/>.
/// </summary>
public sealed partial class TimelineRenderer
{
    private readonly TimelineRenderContext _context;

    // Render-state caches owned by the renderer.
    private readonly Dictionary<MacroTimeline, TimelineRowRenderState> _timelineRowRenderStates = new();
    private readonly Dictionary<MacroTimeline, TimelineHeader> _timelineHeaderControls = new();
    private readonly Dictionary<object, Point> _timelineVisualPositions = new();

    public TimelineRenderer(TimelineRenderContext context)
    {
        _context = context;
    }

    // --- Context-backed accessors so moved method bodies read host state unchanged ---
    private TimelineSelectionState _selection => _context.Selection;
    private TimelineDragState _drag => _context.Drag;
    private SequenceController _sequence => _context.Sequence;
    private MacroDocument _document => _context.GetDocument();
    private MacroWorkspace _activeWorkspace => _context.GetActiveWorkspace();
    private MacroTimeline? _pendingDeleteTimeline => _context.GetPendingDeleteTimeline();

    private Panel TimelineRowsPanel => _context.RowsPanel;
    private Grid TimelineHeaderGrid => _context.HeaderGrid;
    private ColumnDefinition TimelineHeaderColumn => _context.HeaderColumn;
    private ScrollViewer TimelineScrollViewer => _context.ScrollViewer;
    private FrameworkElement TimelineGrid => _context.TimelineGrid;
    private UIElement EmptyTimelinePanel => _context.EmptyTimelinePanel;
    private UIElement TimelineDragOverlayCanvas => _context.DragOverlayCanvas;
    private UIElement TimelineScrollIndicator => _context.ScrollIndicator;
    private TextBlock SingleTimelineMetadataText => _context.SingleTimelineMetadataText;

    private Dispatcher Dispatcher => _context.RowsPanel.Dispatcher;

    private List<MacroTimeline> GetDisplayTimelines() => _context.GetDisplayTimelines().ToList();
    private bool IsHookTimeline(MacroTimeline timeline) => _context.IsHookTimeline(timeline);
    private bool IsActiveDisplayTimeline(MacroTimeline timeline) => _context.IsActiveDisplayTimeline(timeline);
    private bool IsEffectivelyCollapsed(MacroTimeline timeline) => _context.IsEffectivelyCollapsed(timeline);
    private double GetDisplayRowHeight(MacroTimeline timeline) => _context.GetDisplayRowHeight(timeline);
    private double GetDisplayRowGap(MacroTimeline timeline) => _context.GetDisplayRowGap(timeline);

    // --- Layout constants (same global config the host reads; duplicated here to stay decoupled) ---
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

    public void RefreshTimeline()
    {
        RefreshTimelineCore(refreshInspector: true);
    }

    public void RefreshTimelineWithoutInspector()
    {
        RefreshTimelineCore(refreshInspector: false);
    }

    public void RefreshTimelineCore(bool refreshInspector)
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
            _context.UpdateOptionsPagerVisibility();
            _context.UpdateWindowHeight();
            Dispatcher.BeginInvoke(new Action(_context.UpdateScrollIndicator));
            return;
        }

        HideEmptyTimelineState();
        if (refreshInspector)
            _context.SyncOptionsFromActiveTimeline();

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

        _context.UpdateOptionsPagerVisibility();
        _context.UpdateWindowHeight();

        Dispatcher.BeginInvoke(new Action(_context.UpdateScrollIndicator));
    }

    private void BuildTimelineHeaderGridRows()
    {
        TimelineHeaderGrid.Children.Clear();
        TimelineHeaderGrid.RowDefinitions.Clear();
        _timelineHeaderControls.Clear();

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

    // --- API consumed by interaction/recording code on the host ---

    public TimelineRowRenderState? GetRowRenderState(MacroTimeline timeline) =>
        _timelineRowRenderStates.TryGetValue(timeline, out var state) ? state : null;

    public void ClearAnimationCache() => _timelineVisualPositions.Clear();
}
