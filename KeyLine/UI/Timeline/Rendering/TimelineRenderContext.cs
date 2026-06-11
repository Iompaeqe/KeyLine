using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using KeyLine.Domain;
using KeyLine.Services.Playback;
using KeyLine.State;
using KeyLine.UI.Nodes;

namespace KeyLine.UI.Timeline;

/// <summary>
/// The dependencies <see cref="TimelineRenderer"/> needs from the host <c>MainWindow</c>. The
/// renderer owns the visual tree of the timeline canvas + headers and the render-state caches;
/// everything it must read or call back into (selection/drag state, the document, interaction
/// wiring, playback status, window-level updates) is supplied here. Mirrors the
/// <see cref="KeyLine.UI.Inspector.NodeInspectorContext"/> pattern used by the inspector refactor.
/// </summary>
public sealed class TimelineRenderContext
{
    // --- Stable service/state objects (never reassigned on the host) ---
    public required TimelineSelectionState Selection { get; init; }
    public required TimelineDragState Drag { get; init; }
    public required SequenceController Sequence { get; init; }

    // --- Timeline visual controls owned by the host's XAML ---
    public required Panel RowsPanel { get; init; }
    public required Grid HeaderGrid { get; init; }
    public required ColumnDefinition HeaderColumn { get; init; }
    public required ScrollViewer ScrollViewer { get; init; }
    public required FrameworkElement TimelineGrid { get; init; }
    public required UIElement EmptyTimelinePanel { get; init; }
    public required UIElement DragOverlayCanvas { get; init; }
    public required UIElement ScrollIndicator { get; init; }
    public required TextBlock SingleTimelineMetadataText { get; init; }

    // --- Host state read each render (document/workspace are reassigned, so these are getters) ---
    public required Func<MacroDocument> GetDocument { get; init; }
    public required Func<MacroWorkspace> GetActiveWorkspace { get; init; }
    public required Func<MacroTimeline?> GetPendingDeleteTimeline { get; init; }

    // --- Display-list helpers (shared with interaction code; canonical copies stay on the host) ---
    public required Func<IReadOnlyList<MacroTimeline>> GetDisplayTimelines { get; init; }
    public required Func<MacroTimeline, bool> IsHookTimeline { get; init; }
    public required Func<MacroTimeline, bool> IsActiveDisplayTimeline { get; init; }
    public required Func<MacroTimeline, bool> IsEffectivelyCollapsed { get; init; }
    public required Func<MacroTimeline, double> GetDisplayRowHeight { get; init; }
    public required Func<MacroTimeline, double> GetDisplayRowGap { get; init; }

    // --- Drag preview data sourced from the host's drag model ---
    public required Func<MacroTimeline, IReadOnlyList<MacroNode>> GetRenderRawSteps { get; init; }
    public required Func<MacroTimeline, IReadOnlyList<NodePreviewSlot>> GetRenderPreviewSlots { get; init; }

    // --- Interaction wiring attached to freshly created visuals ---
    public required Action<NodeBase, MacroTimeline, MacroNode> AttachNodeMouseHandlers { get; init; }
    public required Action<FrameworkElement, MacroTimeline> AttachHeaderMouseHandlers { get; init; }
    public required Action<FrameworkElement, MacroTimeline, MacroNode> AttachBlockLabelMouseHandlers { get; init; }
    public required Func<MacroTimeline, ContextMenu> CreateHeaderContextMenu { get; init; }
    public required RoutedEventHandler AddButtonClick { get; init; }

    // --- Playback / sequence queries used for header status ---
    public required Func<MacroWorkspace, bool> IsSequenceMode { get; init; }
    public required Func<MacroWorkspace, bool> IsWorkspaceRunning { get; init; }
    public required Func<MacroTimeline, TimelinePlaybackStatus> GetPlaybackStatusForHeader { get; init; }

    // --- Commands / host-side updates triggered by rendering ---
    public required Action SaveUndoSnapshot { get; init; }
    public required Action ScheduleSaveState { get; init; }
    public required Action RefreshInspector { get; init; }
    public required Action SyncOptionsFromActiveTimeline { get; init; }
    public required Action<MacroTimeline> SelectTimeline { get; init; }
    public required Action UpdateOptionsPagerVisibility { get; init; }
    public required Action UpdateWindowHeight { get; init; }
    public required Action UpdateScrollIndicator { get; init; }

    // --- Misc callbacks ---
    public required Func<string, string?> ResolveMacroName { get; init; }
    public required Func<MacroNode, Task> PickMouseCoordinatesForNodeAsync { get; init; }
}
