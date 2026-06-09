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
    private Border CreateTimelineHeader(MacroTimeline timeline, bool isActive, bool isSelected, bool isFirst,
        bool isLast)
    {
        var isPendingDelete = ReferenceEquals(timeline, _pendingDeleteTimeline);

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

        // Each header is now its own rounded card aligned to a single row, so it carries a full
        // border on all sides and uniform rounding (no longer a connected top/bottom strip).
        var border = new Border
        {
            // Stretch within the header column (with a small rail inset on both sides) rather than a
            // fixed width, so the card's right edge stays inside the column instead of being clipped.
            Margin = new Thickness(4, 0, 4, 0),
            CornerRadius = new CornerRadius(9),
            Background = new SolidColorBrush(backgroundColor),
            BorderBrush = new SolidColorBrush(borderColor),
            BorderThickness = new Thickness(1),
            Cursor = System.Windows.Input.Cursors.SizeAll,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Tag = timeline
        };

        border.Child = CreateTimelineHeaderContent(timeline, isActive, isPendingDelete);
        border.ContextMenu = CreateTimelineHeaderContextMenu(timeline);

        AttachTimelineHeaderMouseHandlers(border, timeline);
        return border;
    }

    // Inner-edge accent + status colors used across the header card.
    private static readonly Color HeaderAccentActiveColor = Color.FromRgb(14, 165, 233);
    private static readonly Color HeaderAccentIdleColor = Color.FromRgb(38, 64, 96);
    private static readonly Color HeaderNameActiveColor = Color.FromRgb(224, 242, 254);
    private static readonly Color HeaderNameIdleColor = Color.FromRgb(203, 213, 225);
    private static readonly Color HeaderMetaColor = Color.FromRgb(122, 138, 156);

    private UIElement CreateTimelineHeaderContent(MacroTimeline timeline, bool isActive, bool isPendingDelete)
    {
        if (isPendingDelete)
            return CreatePendingDeleteHeaderContent(timeline);

        var isHook = IsHookTimeline(timeline);
        var isCollapsed = IsEffectivelyCollapsed(timeline);

        var root = new Grid
        {
            ToolTip = isHook
                ? $"{timeline.Name} hook\nWraps macro execution. Pinned and not reorderable."
                : $"{timeline.Name}\nLoops: {FormatTimelineHeaderLoopCount(timeline)}\nLoop Delay: {FormatTimelineHeaderDelay(timeline.BaseDelayMs)}\nMiddle-click to delete. Right-click for options."
        };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Status-colored accent spine on the inner edge ties the card to its row visually.
        var accent = new Border
        {
            Width = 3,
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, isCollapsed ? 6 : 13, 0, isCollapsed ? 6 : 13),
            Background = new SolidColorBrush(isActive ? HeaderAccentActiveColor : HeaderAccentIdleColor),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = false
        };
        Grid.SetColumn(accent, 0);
        root.Children.Add(accent);

        var body = isCollapsed
            ? BuildCollapsedHeaderBody(timeline, isHook, isActive)
            : BuildExpandedHeaderBody(timeline, isHook, isActive);
        Grid.SetColumn(body, 1);
        root.Children.Add(body);

        return root;
    }

    // Expanded card: name line (chevron + name + status dot), a horizontal metadata line, and a
    // live status word underneath. Laid out left-to-right so growing metadata stays readable.
    private UIElement BuildExpandedHeaderBody(MacroTimeline timeline, bool isHook, bool isActive)
    {
        var stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 9, 0)
        };

        var nameLine = new Grid();
        nameLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        nameLine.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        nameLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var chevron = CreateCollapseChevron(timeline, false);
        Grid.SetColumn(chevron, 0);
        nameLine.Children.Add(chevron);

        var nameText = new TextBlock
        {
            Text = timeline.Name,
            FontWeight = FontWeights.Bold,
            FontSize = 12.5,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(isActive ? HeaderNameActiveColor : HeaderNameIdleColor)
        };
        Grid.SetColumn(nameText, 1);
        nameLine.Children.Add(nameText);

        var dot = CreateHeaderStatusDot();
        Grid.SetColumn(dot, 2);
        nameLine.Children.Add(dot);

        stack.Children.Add(nameLine);

        var metaText = new TextBlock
        {
            Text = BuildHeaderMetaLine(timeline, isHook),
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(2, 3, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            Foreground = new SolidColorBrush(HeaderMetaColor)
        };
        stack.Children.Add(metaText);

        var statusText = new TextBlock
        {
            Margin = new Thickness(2, 2, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap
        };
        RegisterHeaderStatus(timeline, statusText, dot, collapsed: false);
        stack.Children.Add(statusText);

        return stack;
    }

    // Collapsed card: one compact horizontal line that still surfaces status (dot) and name.
    private UIElement BuildCollapsedHeaderBody(MacroTimeline timeline, bool isHook, bool isActive)
    {
        var grid = new Grid
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 9, 0)
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var chevron = CreateCollapseChevron(timeline, true);
        Grid.SetColumn(chevron, 0);
        grid.Children.Add(chevron);

        var dot = CreateHeaderStatusDot();
        dot.Margin = new Thickness(2, 0, 6, 0);
        Grid.SetColumn(dot, 1);
        grid.Children.Add(dot);

        var nameText = new TextBlock
        {
            Text = timeline.Name,
            FontWeight = FontWeights.Bold,
            FontSize = 11.5,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(isActive ? HeaderNameActiveColor : HeaderNameIdleColor)
        };
        Grid.SetColumn(nameText, 2);
        grid.Children.Add(nameText);

        var statusText = new TextBlock
        {
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap
        };
        RegisterHeaderStatus(timeline, statusText, dot, collapsed: true);
        Grid.SetColumn(statusText, 3);
        grid.Children.Add(statusText);

        return grid;
    }

    private Border CreateHeaderStatusDot()
    {
        return new Border
        {
            Width = 7,
            Height = 7,
            CornerRadius = new CornerRadius(3.5),
            Margin = new Thickness(5, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            IsHitTestVisible = false
        };
    }

    private void RegisterHeaderStatus(MacroTimeline timeline, TextBlock statusText, Border dot, bool collapsed)
    {
        statusText.Tag = collapsed ? CollapsedStatusTag : null;
        _timelineHeaderStatusTextBlocks[timeline] = statusText;
        _timelineHeaderStatusDots[timeline] = dot;
        UpdateTimelineHeaderStatusTextBlock(timeline, statusText);
    }

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

    private UIElement CreatePendingDeleteHeaderContent(MacroTimeline timeline)
    {
        return new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Left-click or middle-click to confirm delete.",
            Children =
            {
                new TextBlock
                {
                    Text = timeline.Name,
                    FontWeight = FontWeights.Black,
                    FontSize = 13,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextAlignment = TextAlignment.Center,
                    MaxWidth = Math.Max(40, TimelineHeaderWidth - 14),
                    Foreground = new SolidColorBrush(Color.FromRgb(254, 202, 202))
                },
                new TextBlock
                {
                    Text = "Confirm delete",
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 8,
                    TextAlignment = TextAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(254, 202, 202))
                }
            }
        };
    }

    private const string CollapseToggleTag = "collapse-toggle";
    private const string CollapsedStatusTag = "collapsed-status";

    private UIElement CreateCollapseChevron(MacroTimeline timeline, bool isCollapsed)
    {
        return new Border
        {
            Tag = CollapseToggleTag,
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0, 0, 4, 0),
            ToolTip = isCollapsed ? "Expand timeline" : "Collapse timeline",
            Child = new TextBlock
            {
                Text = isCollapsed ? "▸" : "▾",
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                IsHitTestVisible = false,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 160, 182))
            }
        };
    }

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

        var deleteItem = new MenuItem
        {
            Header = "Delete",
            IsEnabled = _isTimelineEditingEnabled && !isHook && _document.Timelines.Count > 1
        };
        deleteItem.Click += (_, _) => BeginTimelineDeleteConfirmation(timeline);

        contextMenu.Items.Add(duplicateItem);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(renameItem);
        contextMenu.Items.Add(deleteItem);

        return contextMenu;
    }

    private void RefreshTimelineHeaderStatuses()
    {
        foreach (var (timeline, statusTextBlock) in _timelineHeaderStatusTextBlocks)
            UpdateTimelineHeaderStatusTextBlock(timeline, statusTextBlock);
    }

    private void UpdateTimelineHeaderPlaybackStatus(MacroTimeline timeline)
    {
        if (_timelineHeaderStatusTextBlocks.TryGetValue(timeline, out var statusTextBlock))
            UpdateTimelineHeaderStatusTextBlock(timeline, statusTextBlock);
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

    private void UpdateTimelineHeaderStatusTextBlock(MacroTimeline timeline, TextBlock statusTextBlock)
    {
        var (text, color, fontSize, tooltip) = ResolveHeaderStatus(timeline);
        var isCollapsedStatus = (statusTextBlock.Tag as string) == CollapsedStatusTag;

        // Status text is single-line in the new card; flatten any multi-line status (e.g. "Ready\nNext").
        text = text.Replace("\n", " ");

        // Collapsed cards have a single tight line, so the name takes priority: only surface the
        // status word there for active/transient states (countdown, running, etc.). Idle "Ready"
        // and stopped states fall back to the loop count and let the colored dot carry the status.
        var showActiveWord = !string.IsNullOrEmpty(text) &&
                             (text.StartsWith("Running") || text.StartsWith("Waiting") ||
                              text.StartsWith("CD") || text.StartsWith("Warning"));

        if (isCollapsedStatus && !showActiveWord)
        {
            statusTextBlock.Text = $"↻ {FormatTimelineHeaderLoopCount(timeline)}";
            statusTextBlock.Foreground = new SolidColorBrush(HeaderMetaColor);
            statusTextBlock.FontWeight = FontWeights.SemiBold;
            statusTextBlock.FontSize = 9.5;
            statusTextBlock.ToolTip = null;
            statusTextBlock.Visibility = Visibility.Visible;
        }
        else if (string.IsNullOrEmpty(text))
        {
            statusTextBlock.Text = string.Empty;
            statusTextBlock.Visibility = Visibility.Collapsed;
        }
        else
        {
            statusTextBlock.Text = text;
            statusTextBlock.Foreground = new SolidColorBrush(color);
            statusTextBlock.FontWeight = FontWeights.Bold;
            statusTextBlock.FontSize = fontSize;
            statusTextBlock.ToolTip = tooltip;
            statusTextBlock.Visibility = Visibility.Visible;
        }

        if (_timelineHeaderStatusDots.TryGetValue(timeline, out var dot))
            dot.Background = new SolidColorBrush(color);
    }

    // Resolves the status word/color for a header, falling back to playback status when no
    // Sequence/Random status applies. An empty Text means "idle" (dot shown in the muted color).
    private (string Text, Color Color, double FontSize, string? Tooltip) ResolveHeaderStatus(MacroTimeline timeline)
    {
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
        var timelineAreaHeight = 52.0;
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
