using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KeyLine.State;
using System.Windows.Input;
using System.Runtime.CompilerServices;
using KeyLine.Domain;
using KeyLine.Services.Macro;
using KeyLine.Services.Playback;
using KeyLine.Services.Timeline;
using KeyLine.UI.Config;
using KeyLine.UI.Nodes;
using KeyLine.UI.Timeline;

namespace KeyLine;

public partial class MainWindow
{
    // From MainWindow.TimelineRendering.cs
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
        SyncOptionsFromActiveTimeline();

        TimelineRowsPanel.Margin = TimelineLayoutCalculator.GetRowsPanelMargin(TimelineHeaderTopExtra);

        var rowModels = BuildTimelineRowVisualModels();

        var canvasWidth = GetTimelineCanvasWidth(rowModels);
        SetTimelineCanvasWidthForAllRows(canvasWidth);

        for (var i = 0; i < rowModels.Count; i++)
        {
            var rowModel = rowModels[i];
            var timeline = rowModel.Timeline;

            if (_document.Timelines.Count > 1)
            {
                var isActive = ReferenceEquals(timeline, _document.ActiveTimeline);
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

        var showHeaderColumn = _document.Timelines.Count > 1;

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

        for (var i = 0; i < _document.Timelines.Count; i++)
        {
            TimelineHeaderGrid.RowDefinitions.Add(
                TimelineLayoutCalculator.CreateTimelineHeaderContentRow(TimelineRowHeight));

            TimelineHeaderGrid.RowDefinitions.Add(
                TimelineLayoutCalculator.CreateTimelineHeaderGapRow(
                    i,
                    _document.Timelines.Count,
                    TimelineRowGap,
                    TimelineHeaderBottomExtra));
        }

        var headerColumnBackplate = new Border
        {
            CornerRadius = new CornerRadius(8, 0, 0, 8),
            Background = new SolidColorBrush(Color.FromRgb(8, 17, 31)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(30, 64, 100)),
            BorderThickness = new Thickness(1, 1, 1, 1),
            IsHitTestVisible = false
        };

        Grid.SetRow(headerColumnBackplate, 0);
        Grid.SetRowSpan(headerColumnBackplate, TimelineHeaderGrid.RowDefinitions.Count);
        TimelineHeaderGrid.Children.Add(headerColumnBackplate);
    }

    private void AddTimelineHeaderToGrid(MacroTimeline timeline, int timelineIndex, bool isActive, bool isSelected)
    {
        var isFirst = timelineIndex == 0;
        var isLast = timelineIndex == _document.Timelines.Count - 1;
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


    private void RefreshTimelineDragPreview()
    {
        if (TimelineRowsPanel == null)
            return;

        if (!_drag.IsDraggingNode || _drag.DraggedNodeTimeline == null)
        {
            RefreshTimeline();
            return;
        }

        var timeline = _drag.DraggedNodeTimeline;

        // Fast path: if we already have a render state for this row, just update positions
        if (_timelineRowRenderStates.TryGetValue(timeline, out var state))
        {
            // The problem with the fast path is that the "placeholder" is only present in state.VisualItems
            // IF the row was already rendered in "dragging mode".
            // If it's the FIRST move after BeginStepDrag, state.VisualItems doesn't have the placeholder.
            // In that case, we MUST do a full row refresh once to get the placeholder element created.

            var hasPlaceholder = state.VisualItems.Any(item => IsDropPlaceholderAnimationKey(item.AnimationKey));
            if (!hasPlaceholder)
            {
                RefreshTimelineRow(timeline);
                return;
            }

            if (state.VisualItems.Any(item => IsBlockAddAnimationKey(item.AnimationKey)))
            {
                RefreshTimelineRow(timeline);
                return;
            }

            var previewSlots = GetTimelineRenderPreviewSlots(timeline);

            // Re-calculate positions for all items including the placeholder.
            var currentLeft = TimelineFirstItemLeft;
            var placeholderAdded = false;

            var orderedVisualItems = new List<TimelineVisualItem>(state.VisualItems.Count);

            foreach (var slot in previewSlots)
            {
                if (slot.IsDraggedSlot)
                {
                    var placeholderKey = GetDropPlaceholderAnimationKey(timeline, slot.DisplayNode);
                    if (!UpdateOrMoveVisualItem(
                            state,
                            orderedVisualItems,
                            ref currentLeft,
                            placeholderKey))
                        return;
                    placeholderAdded = true;
                    continue;
                }

                if (!UpdateOrMoveVisualItem(
                        state,
                        orderedVisualItems,
                        ref currentLeft,
                        GetTimelineAnimationKey(timeline, slot.DisplayNode)))
                    return;
            }

            if (!placeholderAdded)
            {
                var placeholderKey = GetDropPlaceholderAnimationKey(timeline, _drag.DraggedNode!);
                if (!UpdateOrMoveVisualItem(
                        state,
                        orderedVisualItems,
                        ref currentLeft,
                        placeholderKey))
                    return;
            }

            // Finally, the Add button
            if (!UpdateOrMoveVisualItem(
                    state,
                    orderedVisualItems,
                    ref currentLeft,
                    (timeline, "add")))
                return;

            state.VisualItems = orderedVisualItems;
            state.VisualItemsByAnimationKey = BuildVisualItemLookup(orderedVisualItems);
            state.VisualItemCount = orderedVisualItems.Count;
            state.NextLeft = orderedVisualItems[^1].Left;
            state.RowWidth = orderedVisualItems[^1].Left + orderedVisualItems[^1].Width + TimelineRightPadding;

            if (state.RowWidth > state.Canvas.Width + 1)
                EnsureTimelineCanvasWidthForAllRows(state.RowWidth);

            UpdateRowConnector(state, animate: false);
            return;
        }

        RefreshTimeline();
    }

    private bool UpdateOrMoveVisualItem(
        TimelineRowRenderState state,
        List<TimelineVisualItem> orderedVisualItems,
        ref double currentLeft,
        object animationKey)
    {
        if (!state.VisualItemsByAnimationKey.TryGetValue(animationKey, out var item))
        {
            RefreshTimelineRow(state.Timeline);
            return false;
        }

        if (!orderedVisualItems.Contains(item))
            orderedVisualItems.Add(item);

        var previousLeft = Canvas.GetLeft(item.Element);
        if (Math.Abs(previousLeft - currentLeft) > 0.1)
        {
            // Animate smooth movement if it moved significantly
            var deltaX = previousLeft - currentLeft;
            if (Math.Abs(deltaX) > 0.5 && IsTimelineItemNearViewport(previousLeft, currentLeft, item.Width))
                TimelineAnimationService.AnimateRenderOffsetToRest(item.Element, deltaX, 0, animateY: false);

            Canvas.SetLeft(item.Element, currentLeft);
        }

        item.Left = currentLeft;
        currentLeft += item.Width + TimelineItemGap;

        // Update the cached position for future full refreshes
        _timelineVisualPositions[animationKey] = new Point(item.Left,
            TimelineLayoutCalculator.GetItemTop(TimelineConnectorY, item.Size.Height));
        return true;
    }

    private double GetExistingTimelineCanvasWidth(int rowIndex)
    {
        if (rowIndex >= 0 && rowIndex < TimelineRowsPanel.Children.Count)
        {
            if (TimelineRowsPanel.Children[rowIndex] is Grid row)
            {
                foreach (var canvas in row.Children.OfType<Canvas>())
                {
                    if (canvas.Width > 0)
                        return canvas.Width;
                }
            }
        }

        return GetMinimumTimelineCanvasWidth();
    }

    private List<TimelineRowVisualModel> BuildTimelineRowVisualModels()
    {
        var rows = new List<TimelineRowVisualModel>(_document.Timelines.Count);

        for (var i = 0; i < _document.Timelines.Count; i++)
        {
            var timeline = _document.Timelines[i];
            var visibleSteps = MacroTimelineBuilder.BuildVisibleSteps(
                GetTimelineRenderRawSteps(timeline).ToList(),
                timeline.UseStandardDelay,
                timeline.ShowKeyUpDown);

            rows.Add(new TimelineRowVisualModel
            {
                Timeline = timeline,
                VisualItems = BuildTimelineVisualItems(timeline, visibleSteps),
                IsFirstRow = i == 0,
                IsLastRow = i == _document.Timelines.Count - 1
            });
        }

        return rows;
    }

    private double GetTimelineCanvasWidth(IEnumerable<TimelineRowVisualModel> rows)
    {
        var maxContentWidth = rows.Aggregate(
            TimelineFirstItemLeft + TimelineRightPadding,
            (maxWidth, row) => Math.Max(maxWidth, row.RowWidth));

        return Math.Max(GetMinimumTimelineCanvasWidth(), maxContentWidth);
    }

    private UIElement CreateTimelineRow(MacroTimeline timeline, IReadOnlyList<MacroNode> visibleSteps,
        double canvasWidth, bool isFirstRow, bool isLastRow)
    {
        return CreateTimelineRow(
            timeline,
            BuildTimelineVisualItems(timeline, visibleSteps),
            canvasWidth,
            isFirstRow,
            isLastRow);
    }

    private UIElement CreateTimelineRow(MacroTimeline timeline, IReadOnlyList<TimelineVisualItem> visualItems,
        double canvasWidth, bool isFirstRow, bool isLastRow)
    {
        var row = new Grid
        {
            Height = TimelineRowHeight,
            Margin = new Thickness(
                0,
                0,
                0,
                TimelineLayoutCalculator.GetRowBottomMargin(isLastRow, TimelineRowGap)),
            Tag = timeline,
            VerticalAlignment = VerticalAlignment.Top
        };

        var canvas = new Canvas
        {
            Width = canvasWidth,
            Height = TimelineRowHeight,
            Background = Brushes.Transparent,
            Tag = timeline,
            VerticalAlignment = VerticalAlignment.Top
        };

        row.Children.Add(canvas);

        AddBlockBackgrounds(canvas, timeline, visualItems, isFirstRow);

        var connector = CreateTimelineConnector(visualItems);
        if (connector != null)
            canvas.Children.Add(connector);

        foreach (var item in visualItems)
            AddTimelineItem(canvas, item);

        RegisterTimelineRowState(timeline, canvas, connector, visualItems);

        return row;
    }

    private List<TimelineVisualItem> BuildTimelineVisualItems(MacroTimeline timeline,
        IReadOnlyList<MacroNode> visibleSteps)
    {
        var visualItems = new List<TimelineVisualItem>();
        var currentLeft = TimelineFirstItemLeft;

        var isDraggingThisTimeline =
            _drag.IsDraggingNode &&
            ReferenceEquals(_drag.DraggedNodeTimeline, timeline) &&
            _drag.DraggedNode != null;

        var placeholderCount = 0;
        var previewSlots = isDraggingThisTimeline
            ? GetTimelineRenderPreviewSlots(timeline)
            : Array.Empty<NodePreviewSlot>();

        if (isDraggingThisTimeline && previewSlots.Count > 0)
        {
            foreach (var slot in previewSlots)
            {
                if (slot.IsDraggedSlot)
                {
                    AddPlaceholderVisualItem(visualItems, ref currentLeft, timeline, slot.DisplayNode, slot.Width);
                    placeholderCount++;
                    continue;
                }

                if (ShouldShowBlockAddButton(timeline, slot.DisplayNode))
                    AddBlockAddVisualItem(visualItems, ref currentLeft, timeline, slot.DisplayNode);

                AddNodeVisualItem(visualItems, ref currentLeft, timeline, slot.DisplayNode);
            }
        }
        else
        {
            foreach (var step in visibleSteps)
            {
                if (ShouldShowBlockAddButton(timeline, step))
                    AddBlockAddVisualItem(visualItems, ref currentLeft, timeline, step);

                AddNodeVisualItem(visualItems, ref currentLeft, timeline, step);
            }
        }

        if (isDraggingThisTimeline && placeholderCount == 0)
            AddPlaceholderVisualItem(visualItems, ref currentLeft, timeline, _drag.DraggedNode!);

        var addBlock = CreateAddNode(timeline);
        var addSize = MeasureTimelineItem(addBlock);

        visualItems.Add(new TimelineVisualItem
        {
            Element = addBlock,
            Left = currentLeft,
            Size = addSize,
            AnimationKey = (timeline, "add")
        });

        return visualItems;
    }

    private void AddNodeVisualItem(
        List<TimelineVisualItem> visualItems,
        ref double currentLeft,
        MacroTimeline timeline,
        MacroNode step)
    {
        var block = CreateNode(timeline, step);

        if (block is FrameworkElement element)
            element.Tag = step;

        var size = MeasureTimelineItem(block);

        visualItems.Add(new TimelineVisualItem
        {
            Node = step,
            Element = block,
            Left = currentLeft,
            Size = size,
            AnimationKey = GetTimelineAnimationKey(timeline, step)
        });

        currentLeft += size.Width + TimelineItemGap;
    }

    private void AddBlockAddVisualItem(
        List<TimelineVisualItem> visualItems,
        ref double currentLeft,
        MacroTimeline timeline,
        MacroNode rawInsertAnchor)
    {
        var addBlock = CreateAddNode(timeline, rawInsertAnchor);
        var addSize = MeasureTimelineItem(addBlock);

        visualItems.Add(new TimelineVisualItem
        {
            Element = addBlock,
            Left = currentLeft,
            Size = addSize,
            AnimationKey = GetBlockAddAnimationKey(timeline, rawInsertAnchor)
        });

        currentLeft += addSize.Width + TimelineItemGap;
    }

    private static bool ShouldShowBlockAddButton(MacroTimeline timeline, MacroNode node)
    {
        return TimelineBlockService.IsBlockEnd(node) &&
               TimelineBlockService.TryGetBlockRange(timeline, node, out _);
    }

    private object GetDropPlaceholderAnimationKey(MacroTimeline timeline, MacroNode node)
    {
        return (timeline, "drop-placeholder", GetTimelineAnimationKey(timeline, node));
    }

    private object GetBlockAddAnimationKey(MacroTimeline timeline, MacroNode rawInsertAnchor)
    {
        return (timeline, "block-add", rawInsertAnchor);
    }

    private void RemoveDropPlaceholderAnimationKeys(MacroTimeline timeline)
    {
        var keysToRemove = _timelineVisualPositions.Keys
            .Where(key => IsDropPlaceholderAnimationKeyForTimeline(key, timeline))
            .ToList();

        foreach (var key in keysToRemove)
            _timelineVisualPositions.Remove(key);
    }

    private void AddPlaceholderVisualItem(
        List<TimelineVisualItem> visualItems,
        ref double currentLeft,
        MacroTimeline timeline,
        MacroNode draggedNode,
        double? widthOverride = null)
    {
        var draggedBlock = CreateNode(timeline, draggedNode);
        var draggedSize = MeasureTimelineItem(draggedBlock);
        if (widthOverride.HasValue)
            draggedSize = new Size(widthOverride.Value, draggedSize.Height);

        var placeholder = new Border
        {
            Width = draggedSize.Width,
            Height = draggedSize.Height,
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1.5),
            BorderBrush = new SolidColorBrush(Color.FromArgb(150, 96, 165, 250)),
            Background = new SolidColorBrush(Color.FromArgb(30, 96, 165, 250)),
            Opacity = 1.0,
            IsHitTestVisible = false,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(0.96, 0.96)
        };

        var shouldAnimatePlaceholder = IsTimelineItemNearViewport(currentLeft, currentLeft, draggedSize.Width);
        if (shouldAnimatePlaceholder && placeholder.RenderTransform is ScaleTransform scale)
        {
            var duration = new Duration(TimeSpan.FromMilliseconds(90));
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            var animation = new DoubleAnimation(1.0, duration) { EasingFunction = easing };

            scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }
        else if (placeholder.RenderTransform is ScaleTransform restingScale)
        {
            restingScale.ScaleX = 1.0;
            restingScale.ScaleY = 1.0;
        }

        visualItems.Add(new TimelineVisualItem
        {
            Element = placeholder,
            Left = currentLeft,
            Size = draggedSize,
            AnimationKey = GetDropPlaceholderAnimationKey(timeline, draggedNode)
        });

        currentLeft += draggedSize.Width + TimelineItemGap;
    }

    private object GetTimelineAnimationKey(MacroTimeline timeline, MacroNode node)
    {
        var rawItems = TimelineNodeMutationService.GetRawStepsForDisplayStep(timeline, node);

        if (rawItems.Count == 0)
            return node;

        return rawItems[0];
    }

    private double GetCachedNodePreviewWidth(MacroTimeline timeline, MacroNode node)
    {
        var animationKey = GetTimelineAnimationKey(timeline, node);
        if (_timelineRowRenderStates.TryGetValue(timeline, out var state) &&
            state.VisualItemsByAnimationKey.TryGetValue(animationKey, out var item))
        {
            return Math.Max(1, item.Width);
        }

        return Math.Max(1, MeasureTimelineItem(CreateNode(timeline, node)).Width);
    }

    private void AddBlockBackgrounds(
        Canvas canvas,
        MacroTimeline timeline,
        IReadOnlyList<TimelineVisualItem> visualItems,
        bool isFirstRow)
    {
        var itemsByNode = visualItems
            .Where(item => item.Node != null)
            .ToDictionary(item => item.Node!, item => item);

        if (itemsByNode.Count == 0)
            return;

        var ranges = TimelineBlockService.BuildBlockRanges(timeline.Nodes)
            .Where(range => itemsByNode.ContainsKey(range.Start) && itemsByNode.ContainsKey(range.End))
            .OrderBy(range => range.StartIndex)
            .ToList();

        var rangesWithDepth = ranges
            .Select(range => new
            {
                Range = range,
                Depth = ranges.Count(other =>
                    other.StartIndex < range.StartIndex &&
                    other.EndIndex > range.EndIndex)
            })
            .ToList();

        var maxDepth = rangesWithDepth.Count == 0
            ? 0
            : rangesWithDepth.Max(item => item.Depth);

        const double naturalLabelStep = 17;

        var availableUpwardLabelBand = Math.Max(
            0,
            (isFirstRow ? TimelineHeaderTopExtra : TimelineRowGap) - 4);

        availableUpwardLabelBand = Math.Min(availableUpwardLabelBand, 42);

        var labelStep = maxDepth <= 0
            ? 0
            : Math.Min(naturalLabelStep, availableUpwardLabelBand / maxDepth);

        foreach (var item in rangesWithDepth)
        {
            var range = item.Range;
            var depth = item.Depth;

            var startItem = itemsByNode[range.Start];
            var endItem = itemsByNode[range.End];

            var left = startItem.Left - 4;
            var right = endItem.Left + endItem.Width + 4;

            var labelSlot = maxDepth - depth;
            var labelTop = -(labelSlot * labelStep);

            var top = labelTop + 7;
            var bottom = TimelineRowHeight - 4;
            var height = Math.Max(12, bottom - top);

            var background = new Border
            {
                Width = Math.Max(1, right - left),
                Height = height,
                CornerRadius = new CornerRadius(13),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(120, 168, 85, 247)),
                Background = new SolidColorBrush(Color.FromArgb(34, 86, 66, 120)),
                IsHitTestVisible = false
            };

            Canvas.SetLeft(background, left);
            Canvas.SetTop(background, top);
            Panel.SetZIndex(background, -30 - depth);
            canvas.Children.Add(background);

            var startLabel = CreateBlockLabel(
                timeline,
                range.Start,
                NodeDisplayFormatter.GetBlockTimelineLabel(range.Start),
                horizontalPadding: 7);
            Canvas.SetLeft(startLabel, left + 12);
            Canvas.SetTop(startLabel, labelTop);
            Panel.SetZIndex(startLabel, 20 + depth);
            canvas.Children.Add(startLabel);

            var endLabel = CreateBlockLabel(
                timeline,
                range.End,
                "End",
                horizontalPadding: 6);
            endLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var endLabelLeft = right - endLabel.DesiredSize.Width - 12;
            Canvas.SetLeft(endLabel, Math.Max(left + 12, endLabelLeft));
            Canvas.SetTop(endLabel, top + height - 10);
            Panel.SetZIndex(endLabel, 20 + depth);
            canvas.Children.Add(endLabel);
        }
    }

    private Border CreateBlockLabel(
        MacroTimeline timeline,
        MacroNode node,
        string text,
        double horizontalPadding)
    {
        var isSelected = IsStepSelected(timeline, node);

        var label = new Border
        {
            Padding = new Thickness(horizontalPadding, 1, horizontalPadding, 1),
            Margin = new Thickness(-10, -5, -10, -3),
            CornerRadius = new CornerRadius(7),
            Background = new SolidColorBrush(isSelected
                ? Color.FromRgb(76, 29, 149)
                : Color.FromRgb(15, 20, 29)),
            BorderBrush = new SolidColorBrush(isSelected
                ? Color.FromRgb(226, 232, 240)
                : Color.FromArgb(150, 168, 85, 247)),
            BorderThickness = new Thickness(isSelected ? 1.5 : 1),
            Cursor = Cursors.Hand,
            IsHitTestVisible = true,
            Tag = node,
            ToolTip = "Click to select block. Double-click to inspect.",
            Child = new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontWeight = FontWeights.Black,
                FontFamily = new FontFamily("Segoe UI"),
                Foreground = new SolidColorBrush(isSelected
                    ? Color.FromRgb(255, 255, 255)
                    : Color.FromRgb(233, 213, 255)),
                VerticalAlignment = VerticalAlignment.Center,

                // Important: let the Border receive the click, not the TextBlock.
                IsHitTestVisible = false
            }
        };

        AttachBlockLabelMouseHandlers(label, timeline, node);

        return label;
    }

    private static Border? CreateTimelineConnector(IReadOnlyList<TimelineVisualItem> visualItems)
    {
        if (visualItems.Count <= 1)
            return null;

        var firstCenterX = visualItems[0].CenterX;
        var lastCenterX = visualItems[^1].CenterX;

        var connector = new Border
        {
            Width = Math.Max(0, lastCenterX - firstCenterX),
            Height = TimelineConnectorThickness,
            CornerRadius = new CornerRadius(TimelineConnectorThickness / 2.0),
            Background = new SolidColorBrush(Color.FromRgb(31, 48, 66)),
            Opacity = 0.85,
            IsHitTestVisible = false
        };

        Canvas.SetLeft(connector, firstCenterX);
        Canvas.SetTop(
            connector,
            TimelineLayoutCalculator.GetConnectorTop(TimelineConnectorY, TimelineConnectorThickness));
        Panel.SetZIndex(connector, -10);

        return connector;
    }

    private void RegisterTimelineRowState(MacroTimeline timeline, Canvas canvas, Border? connector,
        IReadOnlyList<TimelineVisualItem> visualItems)
    {
        if (visualItems.Count == 0)
            return;

        var addItem = visualItems[^1];

        _timelineRowRenderStates[timeline] = new TimelineRowRenderState
        {
            Timeline = timeline,
            Canvas = canvas,
            Connector = connector,
            AddButton = addItem.Element as FrameworkElement,
            NextLeft = addItem.Left,
            FirstCenterX = visualItems[0].CenterX,
            LastCenterX = visualItems[^1].CenterX,
            RowWidth = addItem.Left + addItem.Width + TimelineRightPadding,
            VisualItemCount = visualItems.Count,
            VisualItems = visualItems.ToList(),
            VisualItemsByAnimationKey = BuildVisualItemLookup(visualItems)
        };
    }

    private static Dictionary<object, TimelineVisualItem> BuildVisualItemLookup(
        IReadOnlyList<TimelineVisualItem> visualItems)
    {
        var lookup = new Dictionary<object, TimelineVisualItem>();

        foreach (var item in visualItems)
        {
            if (item.AnimationKey != null)
                lookup[item.AnimationKey] = item;
        }

        return lookup;
    }

    private void RefreshTimelineRow(MacroTimeline timeline)
    {
        if (TimelineRowsPanel == null)
            return;

        var rowIndex = _document.Timelines.IndexOf(timeline);
        if (rowIndex < 0 || rowIndex >= TimelineRowsPanel.Children.Count)
        {
            RefreshTimeline();
            return;
        }

        var visibleSteps = MacroTimelineBuilder.BuildVisibleSteps(
            GetTimelineRenderRawSteps(timeline).ToList(),
            timeline.UseStandardDelay,
            timeline.ShowKeyUpDown);

        var canvasWidth = GetExistingTimelineCanvasWidth(rowIndex);

        var replacementRow = CreateTimelineRow(
            timeline,
            visibleSteps,
            canvasWidth,
            rowIndex == 0,
            rowIndex == _document.Timelines.Count - 1);

        TimelineRowsPanel.Children.RemoveAt(rowIndex);
        TimelineRowsPanel.Children.Insert(rowIndex, replacementRow);

        FitTimelineCanvasWidthToCurrentContent();
        Dispatcher.BeginInvoke(new Action(UpdateTimelineScrollIndicator));
    }

    private void UpdateSelectionVisuals(MacroTimeline? previousTimeline, MacroTimeline? currentTimeline)
    {
        UpdateSelectionVisualsForTimeline(previousTimeline);

        if (currentTimeline != null && !ReferenceEquals(currentTimeline, previousTimeline))
            UpdateSelectionVisualsForTimeline(currentTimeline);
    }

    private void UpdateSelectionVisualsForTimeline(MacroTimeline? timeline)
    {
        if (timeline == null)
            return;

        if (!_timelineRowRenderStates.TryGetValue(timeline, out var state))
            return;

        foreach (var item in state.VisualItems)
        {
            if (item.Element is not NodeBase nodeControl || nodeControl.Node == null)
                continue;

            nodeControl.IsSelected = IsStepSelected(timeline, nodeControl.Node);
        }
    }

    private void AppendRecordedStepsToTimelineRow(MacroTimeline timeline, IReadOnlyList<MacroNode> addedRawSteps)
    {
        if (addedRawSteps.Count == 0)
            return;

        // Combo display mode can retroactively change the previous visible item,
        // so append-only rendering is unsafe there. Fall back to row-only rebuild.
        if (timeline.UseStandardDelay && !timeline.ShowKeyUpDown)
        {
            RefreshTimelineRow(timeline);
            return;
        }

        if (!_timelineRowRenderStates.TryGetValue(timeline, out var state))
        {
            RefreshTimelineRow(timeline);
            return;
        }

        var visibleSteps = MacroTimelineBuilder.BuildVisibleSteps(
            addedRawSteps,
            timeline.UseStandardDelay,
            timeline.ShowKeyUpDown);

        if (visibleSteps.Count == 0)
            return;

        if (state.AddButton != null)
        {
            state.Canvas.Children.Remove(state.AddButton);
            state.VisualItems.RemoveAll(item => ReferenceEquals(item.Element, state.AddButton));
            state.VisualItemsByAnimationKey.Remove((timeline, "add"));
            state.VisualItemCount = Math.Max(0, state.VisualItemCount - 1);
        }

        var currentLeft = state.NextLeft;

        foreach (var step in visibleSteps)
        {
            var block = CreateNode(timeline, step);

            if (block is FrameworkElement element)
                element.Tag = step;

            var size = MeasureTimelineItem(block);

            var item = new TimelineVisualItem
            {
                Node = step,
                Element = block,
                Left = currentLeft,
                Size = size,
                AnimationKey = GetTimelineAnimationKey(timeline, step)
            };

            AddTimelineItem(state.Canvas, item);
            state.VisualItems.Add(item);
            if (item.AnimationKey != null)
                state.VisualItemsByAnimationKey[item.AnimationKey] = item;
            UpdateRowConnectorBounds(state, item);

            currentLeft += size.Width + TimelineItemGap;
        }

        var addBlock = CreateAddNode(timeline);
        var addSize = MeasureTimelineItem(addBlock);

        var addItem = new TimelineVisualItem
        {
            Element = addBlock,
            Left = currentLeft,
            Size = addSize,
            AnimationKey = (timeline, "add")
        };

        AddTimelineItem(state.Canvas, addItem);
        state.VisualItems.Add(addItem);
        state.VisualItemsByAnimationKey[(timeline, "add")] = addItem;
        UpdateRowConnectorBounds(state, addItem);

        state.AddButton = addBlock as FrameworkElement;
        state.NextLeft = currentLeft;

        var requiredWidth = currentLeft + addSize.Width + TimelineRightPadding;
        state.RowWidth = requiredWidth;
        EnsureTimelineCanvasWidthForAllRows(requiredWidth);

        UpdateRowConnector(state);

        Dispatcher.BeginInvoke(new Action(UpdateTimelineScrollIndicator));
    }

    private void UpdateRowConnectorBounds(TimelineRowRenderState state, TimelineVisualItem item)
    {
        if (state.VisualItemCount == 0)
            state.FirstCenterX = item.CenterX;

        state.LastCenterX = item.CenterX;
        state.VisualItemCount++;
    }

    private void UpdateRowConnector(TimelineRowRenderState state, bool animate = true)
    {
        if (state.VisualItems.Count <= 1)
        {
            if (state.Connector != null)
            {
                state.Canvas.Children.Remove(state.Connector);
                state.Connector = null;
            }

            return;
        }

        var firstItem = state.VisualItems[0];
        var lastItem = state.VisualItems[^1];

        var firstCenterX = Canvas.GetLeft(firstItem.Element) + (firstItem.Width / 2.0);
        var lastCenterX = Canvas.GetLeft(lastItem.Element) + (lastItem.Width / 2.0);

        if (state.Connector == null)
        {
            state.Connector = new Border
            {
                Height = TimelineConnectorThickness,
                CornerRadius = new CornerRadius(TimelineConnectorThickness / 2.0),
                Background = new SolidColorBrush(Color.FromRgb(31, 48, 66)),
                Opacity = 0.85,
                IsHitTestVisible = false
            };

            Canvas.SetLeft(state.Connector, firstCenterX);
            Canvas.SetTop(
                state.Connector,
                TimelineLayoutCalculator.GetConnectorTop(TimelineConnectorY, TimelineConnectorThickness));
            Panel.SetZIndex(state.Connector, -10);
            state.Canvas.Children.Insert(0, state.Connector);
        }

        // Animate connector smoothly if items moved
        var previousLeft = Canvas.GetLeft(state.Connector);
        var previousWidth = state.Connector.Width;
        var targetWidth = Math.Max(0, lastCenterX - firstCenterX);

        if (Math.Abs(previousLeft - firstCenterX) > 0.1 || Math.Abs(previousWidth - targetWidth) > 0.1)
        {
            Canvas.SetLeft(state.Connector, firstCenterX);
            state.Connector.Width = targetWidth;

            var deltaX = previousLeft - firstCenterX;
            var deltaWidth = previousWidth - targetWidth;

            if (animate &&
                (Math.Abs(deltaX) > 0.5 || Math.Abs(deltaWidth) > 0.5) &&
                IsTimelineItemNearViewport(previousLeft, firstCenterX, Math.Max(previousWidth, targetWidth)))
            {
                TimelineAnimationService.AnimateRenderOffsetToRest(state.Connector, deltaX, 0, animateY: false);

                var duration = new Duration(TimeSpan.FromMilliseconds(130));
                var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

                // Width animation is more expensive as it triggers layout, but it's needed for the connector
                var widthAnim = new DoubleAnimation(previousWidth, targetWidth, duration) { EasingFunction = easing };
                state.Connector.BeginAnimation(FrameworkElement.WidthProperty, widthAnim);
            }
        }
    }

    private void EnsureTimelineCanvasWidthForAllRows(double requiredWidth)
    {
        if (TimelineRowsPanel == null)
            return;

        var width = GetSharedTimelineCanvasWidth(requiredWidth);
        SetTimelineCanvasWidthForAllRows(width);
    }

    private void FitTimelineCanvasWidthToCurrentContent()
    {
        if (TimelineRowsPanel == null)
            return;

        SetTimelineCanvasWidthForAllRows(GetSharedTimelineCanvasWidth());
    }

    private void SetTimelineCanvasWidthForAllRows(double width)
    {
        TimelineRowsPanel.Width = width;

        foreach (var row in TimelineRowsPanel.Children.OfType<Grid>())
        {
            foreach (var canvas in row.Children.OfType<Canvas>())
                canvas.Width = width;
        }

        foreach (var state in _timelineRowRenderStates.Values)
            state.Canvas.Width = width;
    }

    private double GetSharedTimelineCanvasWidth(double requiredWidth = 0)
    {
        var contentWidth = Math.Max(requiredWidth, GetRequiredTimelineContentWidth());
        return Math.Max(contentWidth, GetMinimumTimelineCanvasWidth());
    }

    private double GetRequiredTimelineContentWidth()
    {
        var maxWidth = TimelineFirstItemLeft + TimelineRightPadding;

        foreach (var state in _timelineRowRenderStates.Values)
            maxWidth = Math.Max(maxWidth, state.RowWidth);

        return maxWidth;
    }

    private double GetMinimumTimelineCanvasWidth()
    {
        var viewportWidth = GetTimelineLayoutViewportWidth();
        var horizontalPadding = GetTimelineScrollViewerHorizontalPadding();

        return Math.Max(0, viewportWidth - horizontalPadding);
    }

    private double GetTimelineScrollViewerHorizontalPadding()
    {
        return TimelineScrollViewer == null
            ? 0
            : TimelineScrollViewer.Padding.Left + TimelineScrollViewer.Padding.Right;
    }

    private double GetTimelineLayoutViewportWidth()
    {
        var measuredViewportWidth = TimelineScrollViewer?.ViewportWidth > 0
            ? TimelineScrollViewer.ViewportWidth
            : TimelineScrollViewer?.ActualWidth ?? 0;

        if (TimelineGrid == null || TimelineGrid.ActualWidth <= 0)
            return measuredViewportWidth;

        var headerWidth = _document.Timelines.Count > 1
            ? TimelineHeaderWidth
            : 0;

        var expectedViewportWidth = Math.Max(0, TimelineGrid.ActualWidth - headerWidth);

        if (measuredViewportWidth <= 0)
            return expectedViewportWidth;

        if (expectedViewportWidth <= 0)
            return measuredViewportWidth;

        return Math.Min(measuredViewportWidth, expectedViewportWidth);
    }


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

        AttachTimelineHeaderMouseHandlers(border, timeline);
        return border;
    }

    private UIElement CreateTimelineHeaderContent(MacroTimeline timeline, bool isActive, bool isPendingDelete)
    {
        if (!isPendingDelete)
        {
            var useVerticalText = timeline.Name.Length > 4;
            var content = new Grid
            {
                Margin = new Thickness(2, 4, 2, 4),
                ToolTip =
                    $"{timeline.Name}\nLoops: {FormatTimelineHeaderLoopCount(timeline)}\nLoop Delay: {FormatTimelineHeaderDelay(timeline.BaseDelayMs)}"
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
                Text =
                    $"L{FormatTimelineHeaderLoopCount(timeline)} \nD{FormatTimelineHeaderDelay(timeline.BaseDelayMs)}",
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

        if (_document.Timelines.Count != 1)
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
        var timelineCount = Math.Max(1, _document.Timelines.Count);

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

    private void AddTimelineItem(Canvas canvas, TimelineVisualItem item)
    {
        var targetLeft = item.Left;
        var targetTop = TimelineLayoutCalculator.GetItemTop(TimelineConnectorY, item.Size.Height);

        canvas.Children.Add(item.Element);

        Canvas.SetLeft(item.Element, targetLeft);
        Canvas.SetTop(item.Element, targetTop);

        if (item.AnimationKey == null)
            return;

        var targetPosition = new Point(targetLeft, targetTop);

        if (!IsDropPlaceholderAnimationKey(item.AnimationKey) &&
            _timelineVisualPositions.TryGetValue(item.AnimationKey, out var previousPosition))
        {
            var deltaX = previousPosition.X - targetLeft;
            var deltaY = previousPosition.Y - targetTop;

            if ((Math.Abs(deltaX) > 0.5 || Math.Abs(deltaY) > 0.5) &&
                IsTimelineItemNearViewport(previousPosition.X, targetLeft, item.Width))
            {
                TimelineAnimationService.AnimateRenderOffsetToRest(item.Element, deltaX, deltaY,
                    animateY: Math.Abs(deltaY) > 0.5);
            }
        }

        _timelineVisualPositions[item.AnimationKey] = targetPosition;
    }

    private bool IsTimelineItemNearViewport(double previousLeft, double targetLeft, double width)
    {
        if (TimelineScrollViewer == null)
            return true;

        var viewportWidth = TimelineScrollViewer.ViewportWidth > 0
            ? TimelineScrollViewer.ViewportWidth
            : TimelineScrollViewer.ActualWidth;

        if (viewportWidth <= 0)
            return true;

        const double animationOverscan = 96;
        var viewportLeft = TimelineScrollViewer.HorizontalOffset - animationOverscan;
        var viewportRight = TimelineScrollViewer.HorizontalOffset + viewportWidth + animationOverscan;

        return IntersectsHorizontalRange(previousLeft, width, viewportLeft, viewportRight) ||
               IntersectsHorizontalRange(targetLeft, width, viewportLeft, viewportRight);
    }

    private static bool IntersectsHorizontalRange(double left, double width, double viewportLeft, double viewportRight)
    {
        var right = left + Math.Max(0, width);
        return right >= viewportLeft && left <= viewportRight;
    }

    private static bool IsDropPlaceholderAnimationKey(object? animationKey)
    {
        return animationKey is ITuple tuple &&
               tuple.Length >= 2 &&
               tuple[1] is string marker &&
               marker == "drop-placeholder";
    }

    private static bool IsDropPlaceholderAnimationKeyForTimeline(object? animationKey, MacroTimeline timeline)
    {
        return animationKey is ITuple tuple &&
               tuple.Length >= 2 &&
               ReferenceEquals(tuple[0], timeline) &&
               tuple[1] is string marker &&
               marker == "drop-placeholder";
    }

    private static bool IsBlockAddAnimationKey(object? animationKey)
    {
        return animationKey is ITuple tuple &&
               tuple.Length >= 2 &&
               tuple[1] is string marker &&
               marker == "block-add";
    }

    // From MainWindow.TimelineNodes.cs
    private UIElement CreateNode(MacroTimeline timeline, MacroNode node)
    {
        return node.Type switch
        {
            MacroNodeType.Delay or MacroNodeType.RandomDelay => CreateDelayNode(timeline, node),
            MacroNodeType.Text => CreateTextNode(timeline, node),
            MacroNodeType.MouseDown or MacroNodeType.MouseUp => CreateKeyNode(timeline, node),
            MacroNodeType.MouseClick => CreateForegroundMouseNode(timeline, node),
            MacroNodeType.CursorMove or MacroNodeType.BackgroundMouseDown or MacroNodeType.BackgroundMouseUp
                or MacroNodeType.BackgroundMouseClick => CreateMouseNode(timeline, node),
            MacroNodeType.KeyDown or MacroNodeType.KeyUp => CreateKeyNode(timeline, node),
            MacroNodeType.RepeatStart or MacroNodeType.RepeatEnd or
                MacroNodeType.ConditionStart or MacroNodeType.ConditionEnd => CreateBlockNode(timeline, node),
            _ => CreateTextNode(timeline, node)
        };
    }

    private UIElement CreateKeyNode(MacroTimeline timeline, MacroNode node)
    {
        var control = new KeyNode
        {
            Node = node,
            IsSelected = IsStepSelected(timeline, node),
            ShowKeyUpDown = timeline.ShowKeyUpDown,
            Tag = node
        };

        AttachNodeMouseHandlers(control, timeline, node);
        return control;
    }

    private UIElement CreateTextNode(MacroTimeline timeline, MacroNode node)
    {
        var control = new TextNode
        {
            Node = node,
            IsSelected = IsStepSelected(timeline, node),
            Tag = node
        };

        AttachNodeMouseHandlers(control, timeline, node);
        return control;
    }

    private UIElement CreateDelayNode(MacroTimeline timeline, MacroNode node)
    {
        var control = new DelayNode
        {
            Node = node,
            IsSelected = IsStepSelected(timeline, node),
            Tag = node
        };

        control.DelayCommitted += (_, _) =>
        {
            RefreshTimeline();
            RefreshInspector();
            ScheduleSaveState();
        };

        AttachNodeMouseHandlers(control, timeline, node);
        return control;
    }

    private UIElement CreateBlockNode(MacroTimeline timeline, MacroNode node)
    {
        var control = new BlockNode
        {
            Node = node,
            IsSelected = IsStepSelected(timeline, node),
            Tag = node
        };

        AttachNodeMouseHandlers(control, timeline, node);
        return control;
    }

    private UIElement CreateMouseNode(MacroTimeline timeline, MacroNode node)
    {
        var control = new BackgroundMouseNode
        {
            Node = node,
            IsSelected = IsStepSelected(timeline, node),
            Tag = node
        };

        control.CoordinateCommitted += (_, _) =>
        {
            RefreshTimeline();
            ScheduleSaveState();
        };

        control.TargetPickRequested += async (_, _) =>
        {
            SaveUndoSnapshot();
            SelectTimeline(timeline);
            _selection.SelectNode(timeline, node);
            await PickMouseCoordinatesForNodeAsync(node);
        };

        AttachNodeMouseHandlers(control, timeline, node);
        return control;
    }

    private UIElement CreateForegroundMouseNode(MacroTimeline timeline, MacroNode node)
    {
        var control = new MouseNode
        {
            Node = node,
            IsSelected = IsStepSelected(timeline, node),
            Tag = node
        };

        AttachNodeMouseHandlers(control, timeline, node);
        return control;
    }

    private UIElement CreateAddNode(MacroTimeline timeline, MacroNode? rawInsertAnchor = null)
    {
        var control = new AddNode
        {
            Tag = rawInsertAnchor == null
                ? timeline
                : new AddNodeContext
                {
                    Timeline = timeline,
                    RawInsertAnchor = rawInsertAnchor
                }
        };

        control.AddClicked += AddButton_Click;
        return control;
    }

    private bool IsStepSelected(MacroTimeline timeline, MacroNode node)
    {
        if (!_selection.HasNodeSelection || _selection.SelectedTimeline == null || _selection.SelectedNodes.Count == 0)
            return false;

        if (!ReferenceEquals(_selection.SelectedTimeline, timeline))
            return false;

        if (!node.IsSyntheticDisplayNode)
            return _selection.IsNodeSelected(timeline, node);

        return _selection.SelectedNodes.Any(selectedStep => IsSameSelectedStep(node, selectedStep));
    }

    private static bool IsSameSelectedStep(MacroNode node, MacroNode selectedNode)
    {
        if (ReferenceEquals(node, selectedNode))
            return true;

        if (node.IsSyntheticDisplayNode)
            return node.SourceNodes.Contains(selectedNode) ||
                   (selectedNode.IsSyntheticDisplayNode && node.SourceNodes.SequenceEqual(selectedNode.SourceNodes));

        return selectedNode.IsSyntheticDisplayNode && selectedNode.SourceNodes.Contains(node);
    }

    private static Size MeasureTimelineItem(UIElement element)
    {
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var width = element.DesiredSize.Width;
        var height = element.DesiredSize.Height;

        if (element is FrameworkElement frameworkElement)
        {
            if (!double.IsNaN(frameworkElement.Width) && frameworkElement.Width > 0)
                width = frameworkElement.Width;

            if (!double.IsNaN(frameworkElement.Height) && frameworkElement.Height > 0)
                height = frameworkElement.Height;
        }

        if (width <= 0)
            width = 72;

        if (height <= 0)
            height = 48;

        return new Size(width, height);
    }

    // From MainWindow.TimelineHeaderDragging.cs
    private void AttachTimelineHeaderMouseHandlers(FrameworkElement element, MacroTimeline timeline)
    {
        element.PreviewMouseLeftButtonDown += (_, e) =>
        {
            EnsureTimelineDragGlobalHandlers();
            ResetTimelineDeleteConfirmation();

            SelectTimeline(timeline);
            _selection.SelectTimeline(timeline);
            RefreshInspector();

            if (e.ClickCount >= 2)
            {
                OpenInspectorFromSelection();
                e.Handled = true;
                return;
            }

            if (!_isTimelineEditingEnabled)
            {
                e.Handled = true;
                return;
            }

            _drag.BeginTimelineHeaderDrag(
                timeline,
                e.GetPosition(TimelineHeaderGrid),
                _document.Timelines.IndexOf(timeline));

            element.CaptureMouse();

            e.Handled = true;
        };

        element.PreviewMouseMove += (_, e) =>
        {
            if (!_isTimelineEditingEnabled)
                return;

            if (_drag.DraggedTimelineHeader == null ||
                e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var currentPoint = e.GetPosition(TimelineHeaderGrid);

            if (!_drag.IsDraggingTimelineHeader)
            {
                if (!_drag.ShouldStartTimelineHeaderDrag(currentPoint, TimelineHeaderDragThreshold))
                    return;

                SaveUndoSnapshot();
                _drag.MarkTimelineHeaderDragging();
            }

            MoveTimelineHeaderByMouseY(_drag.DraggedTimelineHeader, currentPoint.Y);

            e.Handled = true;
        };

        element.PreviewMouseLeftButtonUp += (_, e) =>
        {
            EndTimelineHeaderDrag(element);
            RefreshTimeline();

            e.Handled = true;
        };

        element.LostMouseCapture += (_, _) =>
        {
            if (Mouse.LeftButton != MouseButtonState.Pressed)
                EndTimelineHeaderDrag(element);
        };

        element.PreviewMouseRightButtonDown += (_, e) =>
        {
            CancelTimelineDragState();

            if (!_isTimelineEditingEnabled)
            {
                SelectTimeline(timeline);
                _selection.SelectTimeline(timeline);
                RefreshInspector();
                e.Handled = true;
                return;
            }

            BeginOrConfirmTimelineDelete(timeline);
            e.Handled = true;
        };
    }

    private void MoveTimelineHeaderByMouseY(MacroTimeline draggedTimeline, double mouseY)
    {
        var currentIndex = _document.Timelines.IndexOf(draggedTimeline);
        if (currentIndex < 0)
            return;

        var rowStride = TimelineRowHeight + TimelineRowGap;
        var deltaY = mouseY - _drag.HeaderDragStartPoint.Y;
        var targetIndex = _drag.HeaderDragStartIndex + (int)Math.Round(deltaY / rowStride);
        targetIndex = Math.Clamp(targetIndex, 0, _document.Timelines.Count - 1);

        if (targetIndex == currentIndex)
            return;

        _document.MoveTimeline(draggedTimeline, targetIndex);
        _selection.SelectTimeline(draggedTimeline);
        RefreshInspector();
        RefreshTimeline();
        ScheduleSaveState();
    }

    private void EndTimelineHeaderDrag(FrameworkElement element)
    {
        _drag.EndTimelineHeaderDrag();

        if (element.IsMouseCaptured)
            element.ReleaseMouseCapture();
    }

    private void BeginOrConfirmTimelineDelete(MacroTimeline timeline)
    {
        if (_document.Timelines.Count <= 1)
            return;

        SelectTimeline(timeline);
        _selection.SelectTimeline(timeline);
        RefreshInspector();

        if (ReferenceEquals(_pendingDeleteTimeline, timeline))
        {
            DeleteSelectedTimeline(timeline);
            return;
        }

        _pendingDeleteTimeline = timeline;
        RefreshTimeline();
    }

    private void ResetTimelineDeleteConfirmation()
    {
        if (_pendingDeleteTimeline == null)
            return;

        _pendingDeleteTimeline = null;
    }
}