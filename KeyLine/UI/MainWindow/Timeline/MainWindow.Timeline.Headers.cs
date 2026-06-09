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

        var border = new Border
        {
            Width = TimelineHeaderWidth,
            Margin = new Thickness(0),
            CornerRadius = new CornerRadius(
                isFirst ? 8 : 0,
                0,
                0,
                isLast ? 8 : 0),
            Background = new SolidColorBrush(backgroundColor),
            BorderBrush = new SolidColorBrush(borderColor),
            BorderThickness = new Thickness(0, 1, 1, 1),
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

    private UIElement CreateTimelineHeaderContent(MacroTimeline timeline, bool isActive, bool isPendingDelete)
    {
        if (!isPendingDelete)
        {
            var isHook = IsHookTimeline(timeline);
            var useVerticalText = timeline.Name.Length > 4;
            var content = new Grid
            {
                Margin = new Thickness(2, 4, 2, 4),
                ToolTip = isHook
                    ? $"{timeline.Name} hook\nWraps macro execution. Pinned and not reorderable."
                    : $"{timeline.Name}\nLoops: {FormatTimelineHeaderLoopCount(timeline)}\nLoop Delay: {FormatTimelineHeaderDelay(timeline.BaseDelayMs)}\nMiddle-click to delete. Right-click for options."
            };

            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });

            var statusText = new TextBlock
            {
                FontSize = 7.5,
                FontWeight = FontWeights.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = Math.Max(28, TimelineHeaderWidth - 6)
            };

            _timelineHeaderStatusTextBlocks[timeline] = statusText;
            UpdateTimelineHeaderStatusTextBlock(timeline, statusText);
            Grid.SetRow(statusText, 0);
            content.Children.Add(statusText);

            var nameText = new TextBlock
            {
                Text = timeline.Name,
                FontWeight = FontWeights.Black,
                FontSize = useVerticalText ? 11 : 13,
                MaxWidth = useVerticalText
                    ? Math.Max(26, TimelineRowHeight - 34)
                    : Math.Max(28, TimelineHeaderWidth - 8),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                LayoutTransform = useVerticalText ? new RotateTransform(-90) : null,
                Foreground = new SolidColorBrush(isActive
                    ? Color.FromRgb(224, 242, 254)
                    : Color.FromRgb(148, 163, 184))
            };

            Grid.SetRow(nameText, 1);
            content.Children.Add(nameText);

            var detailText = new TextBlock
            {
                Text = isHook
                    ? string.Empty
                    : $"L{FormatTimelineHeaderLoopCount(timeline)} \nD{FormatTimelineHeaderDelay(timeline.BaseDelayMs)}",
                FontSize = 10,
                Height = 50,
                Width = 45,
                Padding = new Thickness(4, 0, 0, 0),
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextAlignment = TextAlignment.Left,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                MaxWidth = Math.Max(28, TimelineHeaderWidth - 6),
                Foreground = new SolidColorBrush(Color.FromRgb(120, 136, 149))
            };

            Grid.SetRow(detailText, 2);
            content.Children.Add(detailText);

            return content;
        }

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
                    TextAlignment = TextAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(254, 202, 202))
                },
                new TextBlock
                {
                    Text = "Confirm",
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 8,
                    TextAlignment = TextAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(254, 202, 202))
                }
            }
        };
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

    private void UpdateTimelineHeaderStatusTextBlock(MacroTimeline timeline, TextBlock statusTextBlock)
    {
        var status = GetTimelinePlaybackStatusForHeader(timeline);

        statusTextBlock.Text = status switch
        {
            TimelinePlaybackStatus.Running => "Running...",
            TimelinePlaybackStatus.Waiting => "Waiting...",
            TimelinePlaybackStatus.Stopped => "Stopped",
            TimelinePlaybackStatus.Warning => "Warning!",
            _ => string.Empty
        };

        statusTextBlock.ToolTip = status == TimelinePlaybackStatus.Warning
            ? "Chain mode will not reach later timelines because this timeline is infinite."
            : null;

        statusTextBlock.Foreground = new SolidColorBrush(status switch
        {
            TimelinePlaybackStatus.Running => Color.FromRgb(52, 211, 153),
            TimelinePlaybackStatus.Waiting => Color.FromRgb(253, 230, 138),
            TimelinePlaybackStatus.Stopped => Color.FromRgb(100, 116, 139),
            TimelinePlaybackStatus.Warning => Color.FromRgb(251, 113, 133),
            _ => Color.FromRgb(100, 116, 139)
        });

        statusTextBlock.FontSize = status switch
        {
            TimelinePlaybackStatus.Warning => 11,
            _ => 9
        };
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
        var timelineCount = Math.Max(1, GetDisplayTimelines().Count);

        var timelineAreaHeight = TimelineLayoutCalculator.GetTimelineAreaHeight(
            timelineCount,
            TimelineRowHeight,
            TimelineRowGap,
            52);

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
