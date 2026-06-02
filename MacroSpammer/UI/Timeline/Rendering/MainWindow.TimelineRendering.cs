using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MacroSpammer.Domain;
using MacroSpammer.Services.Macro;
using MacroSpammer.State;
using MacroSpammer.UI.Config;
using MacroSpammer.UI.Timeline;
using MacroSpammer.Services.Timeline;

namespace MacroSpammer;

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

        // Row pattern:
        // 0 = top extra
        // 1 = timeline 0 row
        // 2 = gap after timeline 0
        // 3 = timeline 1 row
        // 4 = gap after timeline 1
        // ...
        // The header cells must consume the spacer rows too.
        // Otherwise the spacer rows become visible holes between T1/T2/etc.
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

        if (_selection.HasMultipleNodeSelection && _selection.IsNodeSelected(timeline, _drag.DraggedNode!))
        {
            RefreshTimelineRow(timeline);
            return;
        }
        
        // Fast path: if we already have a render state for this row, just update positions
        if (_timelineRowRenderStates.TryGetValue(timeline, out var state))
        {
            // The problem with the fast path is that the "placeholder" is only present in state.VisualItems
            // IF the row was already rendered in "dragging mode".
            // If it's the FIRST move after BeginStepDrag, state.VisualItems doesn't have the placeholder.
            // In that case, we MUST do a full row refresh once to get the placeholder element created.
            
            var hasPlaceholder = state.VisualItems.Any(v => Equals(v.AnimationKey, (timeline, "drop-placeholder")));
            if (!hasPlaceholder)
            {
                RefreshTimelineRow(timeline);
                return;
            }

            var previewSlots = GetTimelineRenderPreviewSlots(timeline);

            // Re-calculate positions for all items including the placeholder.
            var currentLeft = TimelineFirstItemLeft;
            var placeholderAdded = false;

            var visualIndex = 0;
            var orderedVisualItems = new List<TimelineVisualItem>(state.VisualItems.Count);

            foreach (var slot in previewSlots)
            {
                if (slot.IsDraggedSlot)
                {
                    if (!UpdateOrMoveVisualItem(
                            state,
                            orderedVisualItems,
                            ref visualIndex,
                            ref currentLeft,
                            GetDropPlaceholderAnimationKey(timeline)))
                        return;
                    placeholderAdded = true;
                    continue;
                }

                if (!UpdateOrMoveVisualItem(
                        state,
                        orderedVisualItems,
                        ref visualIndex,
                        ref currentLeft,
                        GetTimelineAnimationKey(timeline, slot.DisplayNode)))
                    return;
            }

            if (!placeholderAdded)
            {
                if (!UpdateOrMoveVisualItem(
                        state,
                        orderedVisualItems,
                        ref visualIndex,
                        ref currentLeft,
                        GetDropPlaceholderAnimationKey(timeline)))
                    return;
            }

            // Finally, the Add button
            if (!UpdateOrMoveVisualItem(
                    state,
                    orderedVisualItems,
                    ref visualIndex,
                    ref currentLeft,
                    (timeline, "add")))
                return;

            state.VisualItems = orderedVisualItems;
            state.VisualItemCount = orderedVisualItems.Count;
            state.NextLeft = orderedVisualItems[^1].Left;
            state.RowWidth = orderedVisualItems[^1].Left + orderedVisualItems[^1].Width + TimelineRightPadding;
            
            UpdateRowConnector(state);
            FitTimelineCanvasWidthToCurrentContent();
            return;
        }

        RefreshTimeline();
    }

    private bool UpdateOrMoveVisualItem(
        TimelineRowRenderState state,
        List<TimelineVisualItem> orderedVisualItems,
        ref int visualIndex,
        ref double currentLeft,
        object animationKey)
    {
        TimelineVisualItem? item = null;
        if (visualIndex < state.VisualItems.Count)
        {
            var candidate = state.VisualItems[visualIndex];
            if (Equals(candidate.AnimationKey, animationKey))
            {
                item = candidate;
            }
            else
            {
                // Try searching for it (it might have moved in the list)
                item = state.VisualItems.FirstOrDefault(v => Equals(v.AnimationKey, animationKey));
            }
        }
        else
        {
            item = state.VisualItems.FirstOrDefault(v => Equals(v.AnimationKey, animationKey));
        }

        if (item == null)
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
            if (Math.Abs(deltaX) > 0.5)
                TimelineAnimationService.AnimateRenderOffsetToRest(item.Element, deltaX, 0, animateY: false);

            Canvas.SetLeft(item.Element, currentLeft);
        }

        item.Left = currentLeft;
        currentLeft += item.Width + TimelineItemGap;
        
        // Update the cached position for future full refreshes
        _timelineVisualPositions[animationKey] = new Point(item.Left, TimelineLayoutCalculator.GetItemTop(TimelineConnectorY, item.Size.Height));
        
        visualIndex++;
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

    private UIElement CreateTimelineRow(MacroTimeline timeline, IReadOnlyList<MacroNode> visibleSteps, double canvasWidth, bool isFirstRow, bool isLastRow)
    {
        return CreateTimelineRow(
            timeline,
            BuildTimelineVisualItems(timeline, visibleSteps),
            canvasWidth,
            isFirstRow,
            isLastRow);
    }

    private UIElement CreateTimelineRow(MacroTimeline timeline, IReadOnlyList<TimelineVisualItem> visualItems, double canvasWidth, bool isFirstRow, bool isLastRow)
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

        var connector = CreateTimelineConnector(visualItems);
        if (connector != null)
            canvas.Children.Add(connector);

        foreach (var item in visualItems)
            AddTimelineItem(canvas, item);

        RegisterTimelineRowState(timeline, canvas, connector, visualItems);

        return row;
    }
    
    private List<TimelineVisualItem> BuildTimelineVisualItems(MacroTimeline timeline, IReadOnlyList<MacroNode> visibleSteps)
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

                var block = CreateNode(timeline, slot.DisplayNode);

                if (block is FrameworkElement element)
                    element.Tag = slot.DisplayNode;

                var size = MeasureTimelineItem(block);

                visualItems.Add(new TimelineVisualItem
                {
                    Element = block,
                    Left = currentLeft,
                    Size = size,
                    AnimationKey = GetTimelineAnimationKey(timeline, slot.DisplayNode)
                });

                currentLeft += size.Width + TimelineItemGap;
            }
        }
        else
        {
            foreach (var step in visibleSteps)
            {
                var block = CreateNode(timeline, step);

                if (block is FrameworkElement element)
                    element.Tag = step;

                var size = MeasureTimelineItem(block);

                visualItems.Add(new TimelineVisualItem
                {
                    Element = block,
                    Left = currentLeft,
                    Size = size,
                    AnimationKey = GetTimelineAnimationKey(timeline, step)
                });

                currentLeft += size.Width + TimelineItemGap;
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

    private static object GetDropPlaceholderAnimationKey(MacroTimeline timeline)
    {
        return (timeline, "drop-placeholder");
    }

    private object GetDropPlaceholderAnimationKey(MacroTimeline timeline, MacroNode node)
    {
        return (timeline, "drop-placeholder", GetTimelineAnimationKey(timeline, node));
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

        if (placeholder.RenderTransform is ScaleTransform scale)
        {
            var duration = new Duration(TimeSpan.FromMilliseconds(90));
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            var animation = new DoubleAnimation(1.0, duration) { EasingFunction = easing };

            scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
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

    private void RegisterTimelineRowState(MacroTimeline timeline, Canvas canvas, Border? connector, IReadOnlyList<TimelineVisualItem> visualItems)
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
            VisualItems = visualItems.ToList()
        };
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
                Element = block,
                Left = currentLeft,
                Size = size,
                AnimationKey = GetTimelineAnimationKey(timeline, step)
            };

            AddTimelineItem(state.Canvas, item);
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

    private void UpdateRowConnector(TimelineRowRenderState state)
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

            if (Math.Abs(deltaX) > 0.5 || Math.Abs(deltaWidth) > 0.5)
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



    private Border CreateTimelineHeader(MacroTimeline timeline, bool isActive, bool isSelected, bool isFirst, bool isLast)
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
            return new TextBlock
            {
                Text = timeline.Name,
                FontWeight = FontWeights.Black,
                FontSize = useVerticalText ? 12 : 14,
                MaxWidth = useVerticalText
                    ? Math.Max(36, TimelineRowHeight - 12)
                    : Math.Max(28, TimelineHeaderWidth - 8),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                LayoutTransform = useVerticalText ? new RotateTransform(-90) : null,
                Foreground = new SolidColorBrush(isActive
                    ? Color.FromRgb(224, 242, 254)
                    : Color.FromRgb(148, 163, 184)),
                ToolTip = timeline.Name
            };
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

            if (Math.Abs(deltaX) > 0.5 || Math.Abs(deltaY) > 0.5)
                TimelineAnimationService.AnimateRenderOffsetToRest(item.Element, deltaX, deltaY, animateY: Math.Abs(deltaY) > 0.5);
        }

        _timelineVisualPositions[item.AnimationKey] = targetPosition;
    }

    private static bool IsDropPlaceholderAnimationKey(object? animationKey)
    {
        return animationKey is ValueTuple<MacroTimeline, string> tuple &&
               tuple.Item2 == "drop-placeholder";
    }
}
