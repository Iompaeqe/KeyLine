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
    private TimelineHeader CreateTimelineHeader(MacroTimeline timeline, bool isActive, bool isSelected, bool isFirst,
        bool isLast)
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

        var header = new TimelineHeader { Tag = timeline };

        // Disabled timelines stay visible but read as inactive: the whole card is dimmed.
        header.SetCard(new SolidColorBrush(backgroundColor), new SolidColorBrush(borderColor),
            isDisabled ? 0.45 : 1.0);

        if (isPendingDelete)
        {
            header.ShowPendingDelete(timeline.Name);
        }
        else
        {
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
        }

        header.ContextMenu = CreateTimelineHeaderContextMenu(timeline);
        AttachTimelineHeaderMouseHandlers(header, timeline);

        _timelineHeaderControls[timeline] = header;
        if (!isPendingDelete)
            UpdateTimelineHeaderStatus(timeline, header);

        return header;
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
        if (IsSequenceMode(_activeWorkspace) && timeline.CooldownMs > 0)
            meta += $"     CD {FormatTimelineHeaderDelay(timeline.CooldownMs)}";

        return meta;
    }

    // Tag carried by the collapse chevron in TimelineHeader.xaml so the header's mouse handler can
    // tell a chevron click apart from a card click/drag.
    private const string CollapseToggleTag = "collapse-toggle";

    private static bool IsCollapseToggleSource(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is FrameworkElement element && (element.Tag as string) == CollapseToggleTag)
                return true;

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private ContextMenu CreateTimelineHeaderContextMenu(MacroTimeline timeline)
    {
        var contextMenu = new ContextMenu();
        contextMenu.SetResourceReference(FrameworkElement.StyleProperty, "KeyLineContextMenu");

        // Hook timelines (Start/End) are pinned and fixed: no duplicate/rename/delete.
        var isHook = IsHookTimeline(timeline);

        var duplicateItem = new MenuItem
        {
            Header = "Duplicate",
            IsEnabled = _isTimelineEditingEnabled && !isHook
        };
        duplicateItem.Click += (_, _) =>
        {
            ResetTimelineDeleteConfirmation();
            DuplicateTimeline(timeline);
        };

        var renameItem = new MenuItem
        {
            Header = "Rename",
            IsEnabled = _isTimelineEditingEnabled && !isHook
        };
        renameItem.Click += (_, _) => BeginTimelineHeaderRename(timeline);

        // Disable/Enable lets a timeline stay authored but be skipped by every loop mode at run time.
        var disableItem = new MenuItem
        {
            Header = timeline.IsDisabled ? "Enable" : "Disable",
            IsEnabled = _isTimelineEditingEnabled && !isHook
        };
        disableItem.Click += (_, _) => ToggleTimelineDisabled(timeline);

        var deleteItem = new MenuItem
        {
            Header = "Delete",
            IsEnabled = _isTimelineEditingEnabled && !isHook && _document.Timelines.Count > 1
        };
        deleteItem.Click += (_, _) => BeginTimelineDeleteConfirmation(timeline);

        contextMenu.Items.Add(duplicateItem);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(renameItem);
        contextMenu.Items.Add(disableItem);
        contextMenu.Items.Add(deleteItem);

        return contextMenu;
    }

    private void RefreshTimelineHeaderStatuses()
    {
        foreach (var (timeline, header) in _timelineHeaderControls)
            UpdateTimelineHeaderStatus(timeline, header);
    }

    private void UpdateTimelineHeaderPlaybackStatus(MacroTimeline timeline)
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

        if (!IsSequenceMode(_activeWorkspace) ||
            IsHookTimeline(timeline) ||
            IsWorkspaceRunning(_activeWorkspace) ||
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

        // For Sequence, mark the timeline the pointer will check next.
        var normals = _activeWorkspace.Document.Timelines;
        var isNext = _activeWorkspace.LoopMode == MacroLoopMode.Sequence &&
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

        var status = GetTimelinePlaybackStatusForHeader(timeline);

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
        return loopCount == 0 ? "\u221E" : loopCount.ToString();
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
            $"Loops {FormatTimelineHeaderLoopCount(timeline)}  \u00b7  Loop Delay {FormatTimelineHeaderDelay(timeline.BaseDelayMs)}";
        SingleTimelineMetadataText.Visibility = Visibility.Visible;
    }

    private void UpdateWindowHeightForTimelineCount()
    {
        var displayTimelines = GetDisplayTimelines();

        // Sum per-row heights and gaps so collapsed rows shrink the window accordingly.
        var timelineAreaHeight = 28.0;
        for (var i = 0; i < displayTimelines.Count; i++)
        {
            timelineAreaHeight += GetDisplayRowHeight(displayTimelines[i]);
            if (i < displayTimelines.Count - 1)
                timelineAreaHeight += GetDisplayRowGap(displayTimelines[i]);
        }

        if (displayTimelines.Count == 0)
            timelineAreaHeight += TimelineRowHeight;

        var wantedHeight = 252 + timelineAreaHeight;

        LockWindowHeight(Math.Max(420, wantedHeight));
    }

    private void LockWindowHeight(double height)
    {
        height = Math.Ceiling(height);

        // The window is intentionally horizontally resizable only.
        // MinHeight == MaxHeight blocks manual vertical resizing, while this
        // method still lets the app grow/shrink vertically when timeline count changes.
        MinHeight = height;
        MaxHeight = height;
        Height = height;
    }
}
