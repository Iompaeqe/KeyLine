using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using KeyLine.Domain;
using KeyLine.State;
using KeyLine.UI.Nodes;
using KeyLine.UI.Timeline;

namespace KeyLine;

public partial class MainWindow
{
    private TimelineRenderer _timelineRenderer = null!;

    private void InitializeTimelineRenderer()
    {
        _timelineRenderer = new TimelineRenderer(new TimelineRenderContext
        {
            Selection = _selection,
            Drag = _drag,
            Sequence = _sequence,

            RowsPanel = TimelineRowsPanel,
            HeaderGrid = TimelineHeaderGrid,
            HeaderColumn = TimelineHeaderColumn,
            ScrollViewer = TimelineScrollViewer,
            TimelineGrid = TimelineGrid,
            EmptyTimelinePanel = EmptyTimelinePanel,
            DragOverlayCanvas = TimelineDragOverlayCanvas,
            ScrollIndicator = TimelineScrollIndicator,
            SingleTimelineMetadataText = SingleTimelineMetadataText,

            GetDocument = () => _document,
            GetActiveWorkspace = () => _activeWorkspace,
            GetPendingDeleteTimeline = () => _pendingDeleteTimeline,

            GetDisplayTimelines = GetDisplayTimelines,
            IsHookTimeline = IsHookTimeline,
            IsActiveDisplayTimeline = IsActiveDisplayTimeline,
            IsEffectivelyCollapsed = IsEffectivelyCollapsed,
            GetDisplayRowHeight = GetDisplayRowHeight,
            GetDisplayRowGap = GetDisplayRowGap,

            GetRenderRawSteps = GetTimelineRenderRawSteps,
            GetRenderPreviewSlots = GetTimelineRenderPreviewSlots,

            AttachNodeMouseHandlers = AttachNodeMouseHandlers,
            AttachHeaderMouseHandlers = AttachTimelineHeaderMouseHandlers,
            AttachBlockLabelMouseHandlers = AttachBlockLabelMouseHandlers,
            CreateHeaderContextMenu = CreateTimelineHeaderContextMenu,
            AddButtonClick = AddButton_Click,

            IsSequenceMode = IsSequenceMode,
            IsWorkspaceRunning = IsWorkspaceRunning,
            GetPlaybackStatusForHeader = GetTimelinePlaybackStatusForHeader,

            SaveUndoSnapshot = SaveDocumentUndoSnapshot,
            ScheduleSaveState = ScheduleSaveState,
            RefreshInspector = RefreshInspector,
            SyncOptionsFromActiveTimeline = SyncOptionsFromActiveTimeline,
            SelectTimeline = timeline => SelectTimeline(timeline),
            UpdateOptionsPagerVisibility = UpdateTimelineOptionsPagerVisibility,
            UpdateWindowHeight = UpdateWindowHeightForTimelineCount,
            UpdateScrollIndicator = UpdateTimelineScrollIndicator,

            ResolveMacroName = ResolveActiveProfileMacroName,
            PickMouseCoordinatesForNodeAsync = PickMouseCoordinatesForNodeAsync
        });
    }

    // --- Thin wrappers: the timeline refresh tiers, kept as MainWindow names so existing call
    //     sites are unchanged. Each delegates to the renderer's matching method. ---

    private void RefreshTimeline(object? sender = null, RoutedEventArgs? e = null) =>
        _timelineRenderer.RefreshTimeline();

    private void RefreshTimelineWithoutInspector() =>
        _timelineRenderer.RefreshTimelineWithoutInspector();

    private void RefreshTimelineRow(MacroTimeline timeline) =>
        _timelineRenderer.RefreshTimelineRow(timeline);

    private void RefreshTimelineRowAndHeaders(MacroTimeline timeline) =>
        _timelineRenderer.RefreshTimelineRowAndHeaders(timeline);

    private void RefreshTimelineNode(MacroTimeline timeline, MacroNode node) =>
        _timelineRenderer.RefreshTimelineNode(timeline, node);

    private void UpdateSelectionVisuals(MacroTimeline? previousTimeline, MacroTimeline? currentTimeline) =>
        _timelineRenderer.UpdateSelectionVisuals(previousTimeline, currentTimeline);

    private void RefreshTimelineDragPreview() =>
        _timelineRenderer.RefreshTimelineDragPreview();

    private void RefreshTimelineHeaders() =>
        _timelineRenderer.RefreshTimelineHeaders();

    private void RefreshTimelineHeaderActiveStates() =>
        _timelineRenderer.UpdateTimelineHeaderActiveStates();

    private void RefreshTimelineSelectionVisuals(MacroTimeline? previousNodeTimeline) =>
        _timelineRenderer.RefreshTimelineSelectionVisuals(previousNodeTimeline);

    private void RefreshTimelineCollapse(MacroTimeline timeline) =>
        _timelineRenderer.RefreshTimelineCollapse(timeline);

    private void RefreshTimelineHeaderStatuses() =>
        _timelineRenderer.RefreshTimelineHeaderStatuses();

    private void UpdateTimelineHeaderPlaybackStatus(MacroTimeline timeline) =>
        _timelineRenderer.UpdateTimelineHeaderPlaybackStatus(timeline);

    private void AppendRecordedStepsToTimelineRow(MacroTimeline timeline, IReadOnlyList<MacroNode> addedRawSteps) =>
        _timelineRenderer.AppendRecordedStepsToTimelineRow(timeline, addedRawSteps);

    // --- Render helpers used by interaction/recording code, kept as MainWindow names. ---

    private UIElement CreateNode(MacroTimeline timeline, MacroNode node) =>
        _timelineRenderer.CreateNode(timeline, node);

    private static Size MeasureTimelineItem(UIElement element) =>
        TimelineRenderer.MeasureTimelineItem(element);

    private object GetTimelineAnimationKey(MacroTimeline timeline, MacroNode node) =>
        _timelineRenderer.GetTimelineAnimationKey(timeline, node);

    private void RemoveDropPlaceholderAnimationKeys(MacroTimeline timeline) =>
        _timelineRenderer.RemoveDropPlaceholderAnimationKeys(timeline);

    private double GetCachedNodePreviewWidth(MacroTimeline timeline, MacroNode node) =>
        _timelineRenderer.GetCachedNodePreviewWidth(timeline, node);

    private double GetLeadingNodePreviewWidth(MacroTimeline timeline, MacroNode node) =>
        _timelineRenderer.GetLeadingNodePreviewWidth(timeline, node);

    private double GetMinimumTimelineCanvasWidth() =>
        _timelineRenderer.GetMinimumTimelineCanvasWidth();

    private void EnsureTimelineCanvasWidthForAllRows(double requiredWidth) =>
        _timelineRenderer.EnsureTimelineCanvasWidthForAllRows(requiredWidth);

    // Selection-node matching (incl. synthetic display nodes) lives in TimelineSelectionState;
    // this keeps the original call-site name for the selection/interaction code.
    private static bool IsSameSelectedStep(MacroNode node, MacroNode selectedNode) =>
        TimelineSelectionState.IsSameSelectionNode(node, selectedNode);
}
