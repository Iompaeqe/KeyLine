using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KeyLine.Domain;
using KeyLine.Services.Playback;
using KeyLine.Services.Timeline;

namespace KeyLine.UI.Timeline;

public sealed partial class TimelineRenderer
{
    private TimelineHeader CreateTimelineHeader(MacroTimeline timeline, bool isActive, bool isSelected, bool isFirst,
        bool isLast)
    {
        var header = new TimelineHeader { Tag = timeline };

        // The context menu and mouse handlers are wired once at creation; only the visual state
        // (active/selected/disabled/status) is re-applied by the lighter header refresh tiers.
        header.ContextMenu = _context.CreateHeaderContextMenu(timeline);
        _context.AttachHeaderMouseHandlers(header, timeline);
        _timelineHeaderControls[timeline] = header;

        ApplyTimelineHeaderVisualState(timeline, header, isActive, isSelected);
        return header;
    }

    // Re-applies the active/selected/disabled card styling, body, and live status to an existing
    // header control. Does NOT rebuild the context menu (its labels depend only on disabled state
    // and timeline count, which the callers of the active-state tier never change).
    private void ApplyTimelineHeaderVisualState(MacroTimeline timeline, TimelineHeader header, bool isActive,
        bool isSelected)
    {
        var isPendingDelete = ReferenceEquals(timeline, _pendingDeleteTimeline);
        var isDisabled = timeline.IsDisabled && !isPendingDelete;

        var backgroundColor = isPendingDelete
            ? Color.FromRgb(127, 29, 29)
            : isActive
                ? Color.FromRgb(10, 52, 84)
                : Color.FromRgb(8, 17, 31);

        var borderColor = isPendingDelete
            ? Color.FromRgb(248, 113, 113)
            : isSelected
                ? Color.FromRgb(226, 232, 240)
                : isActive
                    ? Color.FromRgb(14, 165, 233)
                    : Color.FromRgb(30, 64, 100);

        // Disabled timelines stay visible but read as inactive: the whole card is dimmed.
        header.SetCard(new SolidColorBrush(backgroundColor), new SolidColorBrush(borderColor),
            isDisabled ? 0.45 : 1.0);

        if (isPendingDelete)
        {
            header.ShowPendingDelete(timeline.Name);
            return;
        }

        var isHook = IsHookTimeline(timeline);
        var isCollapsed = IsEffectivelyCollapsed(timeline);

        var tooltip = isHook
            ? $"{timeline.Name} hook\nWraps macro execution. Pinned and not reorderable."
            : $"{timeline.Name}\nLoops: {FormatTimelineHeaderLoopCount(timeline)}\nLoop Delay: {FormatTimelineHeaderDelay(timeline.BaseDelayMs)}\nMiddle-click to delete. Right-click for options.";

        header.ShowNormal(
            timeline.Name,
            isCollapsed,
            new SolidColorBrush(isActive ? HeaderAccentActiveColor : HeaderAccentIdleColor),
            new SolidColorBrush(isActive ? HeaderNameActiveColor : HeaderNameIdleColor),
            BuildHeaderMetaLine(timeline, isHook),
            tooltip);

        UpdateTimelineHeaderStatus(timeline, header);
    }

    // Visual-state tier for a timeline-selection change: re-skin the headers for the new
    // active/selected set and clear node-selection highlights from the timeline that previously
    // owned a node selection. No rows or node visuals are rebuilt.
    public void RefreshTimelineSelectionVisuals(MacroTimeline? previousNodeTimeline)
    {
        UpdateTimelineHeaderActiveStates();

        if (previousNodeTimeline != null)
            UpdateSelectionVisualsForTimeline(previousNodeTimeline);
    }

    // Resizes the header grid's content/gap rows to the current per-timeline heights, reusing the
    // existing header controls (used by the collapse tier so collapse never rebuilds headers).
    private void UpdateHeaderGridRowHeights()
    {
        var displayTimelines = GetDisplayTimelines();
        if (displayTimelines.Count <= 1)
            return;

        var rowDefs = TimelineHeaderGrid.RowDefinitions;
        for (var i = 0; i < displayTimelines.Count; i++)
        {
            var contentRow = TimelineLayoutCalculator.GetHeaderGridRow(i);
            var gapRow = contentRow + 1;

            if (contentRow < rowDefs.Count)
                rowDefs[contentRow].Height = new GridLength(GetDisplayRowHeight(displayTimelines[i]));

            if (gapRow < rowDefs.Count)
            {
                var isLast = i == displayTimelines.Count - 1;
                rowDefs[gapRow].Height = isLast
                    ? new GridLength(1, GridUnitType.Star)
                    : new GridLength(GetDisplayRowGap(displayTimelines[i]));
            }
        }
    }

    // Visual-state tier: re-apply active/selected/disabled styling + status (incl. header meta:
    // name, loop, delay, cooldown) across the existing header controls without rebuilding them.
    // Used when the active/selected timeline or a header-meta value changes but the header
    // structure and context menus do not (e.g. add/delete node, deselect, inspector header edits).
    public void UpdateTimelineHeaderActiveStates()
    {
        foreach (var (timeline, header) in _timelineHeaderControls)
            ApplyTimelineHeaderVisualState(
                timeline,
                header,
                IsActiveDisplayTimeline(timeline),
                _selection.IsTimelineSelected(timeline));
    }

    // Header-structure tier: rebuild just the header grid + header controls (fresh context menus),
    // leaving the node rows untouched. Used when something that the context menu reflects changes
    // (e.g. enable/disable toggles the menu label) but the node rows are unaffected.
    public void RefreshTimelineHeaders()
    {
        if (TimelineHeaderGrid == null)
            return;

        BuildTimelineHeaderGridRows();

        var displayTimelines = GetDisplayTimelines();
        if (displayTimelines.Count <= 1)
            return;

        for (var i = 0; i < displayTimelines.Count; i++)
        {
            var timeline = displayTimelines[i];
            AddTimelineHeaderToGrid(
                timeline,
                i,
                IsActiveDisplayTimeline(timeline),
                _selection.IsTimelineSelected(timeline));
        }
    }

    // Inner-edge accent + status colors used across the header card.
    private static readonly Color HeaderAccentActiveColor = Color.FromRgb(14, 165, 233);
    private static readonly Color HeaderAccentIdleColor = Color.FromRgb(38, 64, 96);
    private static readonly Color HeaderNameActiveColor = Color.FromRgb(224, 242, 254);
    private static readonly Color HeaderNameIdleColor = Color.FromRgb(203, 213, 225);

    private string BuildHeaderMetaLine(MacroTimeline timeline, bool isHook)
    {
        if (isHook)
            return "macro hook";

        var meta = $"↻ {FormatTimelineHeaderLoopCount(timeline)}     {FormatTimelineHeaderDelay(timeline.BaseDelayMs)}";

        // In Sequence/Random, show the configured cooldown compactly (hidden when 0 to avoid clutter).
        if (_context.IsSequenceMode(_activeWorkspace) && timeline.CooldownMs > 0)
            meta += $"     CD {FormatTimelineHeaderDelay(timeline.CooldownMs)}";

        return meta;
    }

    public void RefreshTimelineHeaderStatuses()
    {
        foreach (var (timeline, header) in _timelineHeaderControls)
            UpdateTimelineHeaderStatus(timeline, header);
    }

    public void UpdateTimelineHeaderPlaybackStatus(MacroTimeline timeline)
    {
        if (_timelineHeaderControls.TryGetValue(timeline, out var header))
            UpdateTimelineHeaderStatus(timeline, header);
    }

    // Sequence/Random "Ready" / "CD <remaining>" status shown on normal timeline headers while the
    // macro is not playing. Returns false when the default playback status should be used instead.
    private bool TryGetSequenceHeaderStatus(MacroTimeline timeline, out string text, out Color color)
    {
        text = string.Empty;
        color = Color.FromRgb(100, 116, 139);

        if (!_context.IsSequenceMode(_activeWorkspace) ||
            IsHookTimeline(timeline) ||
            _context.IsWorkspaceRunning(_activeWorkspace) ||
            !timeline.HasNodes)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        if (_sequence.IsOnCooldown(_activeWorkspace, timeline, now))
        {
            var remaining = _sequence.GetRemainingCooldown(_activeWorkspace, timeline, now);
            text = $"CD {FormatCooldownRemaining(remaining)}";
            color = Color.FromRgb(253, 230, 138);
            return true;
        }

        // Only Ordered uses the rotating pointer, so the "Next" marker only applies there.
        var normals = _activeWorkspace.Document.Timelines;
        var isNext = _activeWorkspace.SequenceMode == SequenceMode.Ordered &&
                     normals.Count > 0 &&
                     ReferenceEquals(timeline, normals[_sequence.GetNextIndex(_activeWorkspace, normals.Count)]);

        text = isNext ? "Ready\nNext" : "Ready";
        color = Color.FromRgb(52, 211, 153);
        return true;
    }

    private static string FormatCooldownRemaining(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero)
            return "0s";

        return remaining.TotalSeconds >= 10
            ? $"{(int)Math.Ceiling(remaining.TotalSeconds)}s"
            : $"{remaining.TotalSeconds:0.0}s";
    }

    private void UpdateTimelineHeaderStatus(MacroTimeline timeline, TimelineHeader header)
    {
        var (text, color, fontSize, tooltip) = ResolveHeaderStatus(timeline);
        var brush = new SolidColorBrush(color);

        // Status text is single-line in the card; flatten any multi-line status (e.g. "Ready\nNext").
        text = text.Replace("\n", " ");

        var collapsed = IsEffectivelyCollapsed(timeline);

        // Collapsed cards have a single tight line, so the name takes priority: only surface the
        // status word there for active/transient states (countdown, running, etc.). Idle "Ready"
        // and stopped states fall back to the loop count and let the colored dot carry the status.
        var showActiveWord = !string.IsNullOrEmpty(text) &&
                             (text.StartsWith("Running") || text.StartsWith("Waiting") ||
                              text.StartsWith("CD") || text.StartsWith("Warning") ||
                              text.StartsWith("Disabled"));

        // Collapsed cards keep the line for the cooldown (and other active/transient states) but omit
        // the loop count — it's low value there. Idle/empty states show nothing; the dot carries status.
        if (string.IsNullOrEmpty(text) || (collapsed && !showActiveWord))
        {
            header.SetStatus(string.Empty, brush, fontSize, FontWeights.Bold, tooltip: null, visible: false);
        }
        else
        {
            header.SetStatus(text, brush, fontSize, FontWeights.Bold, tooltip, visible: true);
        }

        header.SetDot(brush);
    }

    // Resolves the status word/color for a header, falling back to playback status when no
    // Sequence/Random status applies. An empty Text means "idle" (dot shown in the muted color).
    private (string Text, Color Color, double FontSize, string? Tooltip) ResolveHeaderStatus(MacroTimeline timeline)
    {
        if (timeline.IsDisabled && !IsHookTimeline(timeline))
            return ("Disabled", Color.FromRgb(148, 163, 184), 9.5, "This timeline is skipped by every loop mode. Right-click to enable.");

        if (TryGetSequenceHeaderStatus(timeline, out var sequenceText, out var sequenceColor))
            return (sequenceText, sequenceColor, 9.5, null);

        var status = _context.GetPlaybackStatusForHeader(timeline);

        var text = status switch
        {
            TimelinePlaybackStatus.Running => "Running…",
            TimelinePlaybackStatus.Waiting => "Waiting…",
            TimelinePlaybackStatus.Stopped => "Stopped",
            TimelinePlaybackStatus.Warning => "Warning!",
            _ => string.Empty
        };

        var color = status switch
        {
            TimelinePlaybackStatus.Running => Color.FromRgb(52, 211, 153),
            TimelinePlaybackStatus.Waiting => Color.FromRgb(253, 230, 138),
            TimelinePlaybackStatus.Stopped => Color.FromRgb(100, 116, 139),
            TimelinePlaybackStatus.Warning => Color.FromRgb(251, 113, 133),
            _ => Color.FromRgb(100, 116, 139)
        };

        var tooltip = status == TimelinePlaybackStatus.Warning
            ? "Chain mode will not reach later timelines because this timeline is infinite."
            : null;

        var fontSize = status == TimelinePlaybackStatus.Warning ? 10.5 : 9.5;

        return (text, color, fontSize, tooltip);
    }

    private static string FormatTimelineHeaderLoopCount(MacroTimeline timeline)
    {
        var loopCount = Math.Max(0, timeline.LoopCount);
        return loopCount == 0 ? "∞" : loopCount.ToString();
    }

    private static string FormatTimelineHeaderDelay(int milliseconds)
    {
        var delayMs = Math.Max(0, milliseconds);
        var formatted = DelayFormatter.Format(delayMs);
        return delayMs < 1000 ? $"{formatted}ms" : formatted;
    }

    private void UpdateSingleTimelineMetadataText()
    {
        if (SingleTimelineMetadataText == null)
            return;

        if (GetDisplayTimelines().Count != 1)
        {
            SingleTimelineMetadataText.Visibility = Visibility.Collapsed;
            return;
        }

        var timeline = _document.Timelines[0];
        SingleTimelineMetadataText.Text =
            $"Loops {FormatTimelineHeaderLoopCount(timeline)}  ·  Loop Delay {FormatTimelineHeaderDelay(timeline.BaseDelayMs)}";
        SingleTimelineMetadataText.Visibility = Visibility.Visible;
    }
}
