using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using KeySpammer.Domain;
using KeySpammer.Services.Macro;
using KeySpammer.Services.Timeline;
using KeySpammer.State;
using KeySpammer.UI.Config;
using KeySpammer.UI.Controls;

namespace KeySpammer;

public partial class MainWindow
{
    private sealed class TimelineVisualItem
    {
        public required UIElement Element { get; init; }
        public required double Left { get; init; }
        public required Size Size { get; init; }
        public object? AnimationKey { get; init; }

        public double Width => Size.Width;
        public double CenterX => Left + (Size.Width / 2.0);
    }

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

    private sealed class TimelineRowRenderState
    {
        public required MacroTimeline Timeline { get; init; }
        public required Canvas Canvas { get; init; }
        public Border? Connector { get; set; }
        public FrameworkElement? AddButton { get; set; }
        public double NextLeft { get; set; }
        public double FirstCenterX { get; set; }
        public double LastCenterX { get; set; }
        public double RowWidth { get; set; }
        public int VisualItemCount { get; set; }
    }

    private readonly Dictionary<MacroTimeline, TimelineRowRenderState> _timelineRowRenderStates = new();
    

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
        
        TimelineRowsPanel.Margin = new Thickness(0, TimelineHeaderTopExtra, 0, 0);

        var visibleStepsByTimeline = _document.Timelines.ToDictionary(
            timeline => timeline,
            timeline => MacroTimelineBuilder.BuildVisibleSteps(
                GetTimelineRenderRawSteps(timeline).ToList(),
                timeline.UseStandardDelay,
                timeline.ShowKeyUpDown));

        var canvasWidth = CalculateSimpleTimelineCanvasWidth(visibleStepsByTimeline);

        for (var i = 0; i < _document.Timelines.Count; i++)
        {
            var timeline = _document.Timelines[i];
            var isLastRow = i == _document.Timelines.Count - 1;

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
                visibleStepsByTimeline[timeline],
                canvasWidth,
                i == 0,
                isLastRow));
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

        TimelineHeaderGrid.RowDefinitions.Add(new RowDefinition
        {
            Height = new GridLength(TimelineHeaderTopExtra)
        });

        for (var i = 0; i < _document.Timelines.Count; i++)
        {
            TimelineHeaderGrid.RowDefinitions.Add(new RowDefinition
            {
                Height = new GridLength(TimelineRowHeight)
            });

            TimelineHeaderGrid.RowDefinitions.Add(new RowDefinition
            {
                // Gap rows are consumed by the header above them.
                // The final row must absorb the remaining panel height so the last
                // header reaches the bottom of the timeline scroll viewer.
                Height = i < _document.Timelines.Count - 1
                    ? new GridLength(TimelineRowGap)
                    : new GridLength(1, GridUnitType.Star),
                MinHeight = i < _document.Timelines.Count - 1
                    ? 0
                    : TimelineHeaderBottomExtra
            });
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
        var row = isFirst
            ? 0
            : 1 + (timelineIndex * 2);

        var rowSpan = isFirst
            ? 3
            : 2;

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

        if (!_drag.IsDraggingStep || _drag.DraggedStepTimeline == null)
        {
            RefreshTimeline();
            return;
        }

        var timeline = _drag.DraggedStepTimeline;
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

        var requiredCanvasWidth = CalculateTimelineCanvasWidth(timeline, visibleSteps);
        var canvasWidth = GetSharedTimelineCanvasWidth(requiredCanvasWidth);

        var replacementRow = CreateTimelineRow(
            timeline,
            visibleSteps,
            canvasWidth,
            rowIndex == 0,
            rowIndex == _document.Timelines.Count - 1);

        TimelineRowsPanel.Children.RemoveAt(rowIndex);
        TimelineRowsPanel.Children.Insert(rowIndex, replacementRow);
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

        var viewportWidth = TimelineScrollViewer?.ViewportWidth > 0
            ? TimelineScrollViewer.ViewportWidth
            : TimelineScrollViewer?.ActualWidth ?? 0;

        return Math.Max(0, viewportWidth - 20);
    }

    private double CalculateSimpleTimelineCanvasWidth(Dictionary<MacroTimeline, List<MacroStep>> visibleStepsByTimeline)
    {
        var viewportWidth = TimelineScrollViewer?.ViewportWidth > 0
            ? TimelineScrollViewer.ViewportWidth
            : TimelineScrollViewer?.ActualWidth ?? 0;

        var maxContentWidth = TimelineFirstItemLeft + TimelineRightPadding;

        foreach (var (timeline, visibleSteps) in visibleStepsByTimeline)
        {
            var contentWidth = TimelineFirstItemLeft;

            foreach (var step in visibleSteps)
            {
                var block = CreateStepBlock(timeline, step);
                var size = MeasureTimelineItem(block);

                contentWidth += size.Width + TimelineItemGap;
            }

            var addBlock = CreateAddBlock(timeline);
            var addSize = MeasureTimelineItem(addBlock);

            contentWidth += addSize.Width + TimelineRightPadding;

            maxContentWidth = Math.Max(maxContentWidth, contentWidth);
        }

        return Math.Max(Math.Max(0, viewportWidth - 20), maxContentWidth);
    }

    private UIElement CreateTimelineRow(MacroTimeline timeline, IReadOnlyList<MacroStep> visibleSteps, double canvasWidth, bool isFirstRow, bool isLastRow)
    {
        var row = new Grid
        {
            Height = TimelineRowHeight,
            Margin = new Thickness(0, 0, 0, isLastRow ? 0 : TimelineRowGap),
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

        var visualItems = BuildTimelineVisualItems(timeline, visibleSteps);

        var connector = CreateTimelineConnector(visualItems);
        if (connector != null)
            canvas.Children.Add(connector);

        foreach (var item in visualItems)
            AddTimelineItem(canvas, item);

        RegisterTimelineRowState(timeline, canvas, connector, visualItems);

        return row;
    }
    
    private List<TimelineVisualItem> BuildTimelineVisualItems(MacroTimeline timeline, IReadOnlyList<MacroStep> visibleSteps)
    {
        var visualItems = new List<TimelineVisualItem>();
        var currentLeft = TimelineFirstItemLeft;

        var rawStepsForRender = GetTimelineRenderRawSteps(timeline);

        var isDraggingThisTimeline =
            _drag.IsDraggingStep &&
            ReferenceEquals(_drag.DraggedStepTimeline, timeline) &&
            _drag.DraggedStep != null;

        var draggedItems = isDraggingThisTimeline
            ? GetRawStepsForDisplayStep(timeline, _drag.DraggedStep!)
            : new List<MacroStep>();

        var placeholderAdded = false;

        foreach (var step in visibleSteps)
        {
            var rawItems = GetRawStepsForDisplayStep(rawStepsForRender, step);

            var isDraggedDisplayStep =
                isDraggingThisTimeline &&
                rawItems.Any(draggedItems.Contains);

            if (isDraggedDisplayStep)
            {
                AddPlaceholderVisualItem(visualItems, ref currentLeft, timeline, _drag.DraggedStep!);
                placeholderAdded = true;
                continue;
            }

            var block = CreateStepBlock(timeline, step);

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

        if (isDraggingThisTimeline && !placeholderAdded)
            AddPlaceholderVisualItem(visualItems, ref currentLeft, timeline, _drag.DraggedStep!);

        var addBlock = CreateAddBlock(timeline);
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

    private void AddPlaceholderVisualItem(
        List<TimelineVisualItem> visualItems,
        ref double currentLeft,
        MacroTimeline timeline,
        MacroStep draggedStep)
    {
        var draggedBlock = CreateStepBlock(timeline, draggedStep);
        var draggedSize = MeasureTimelineItem(draggedBlock);

        var placeholder = new Border
        {
            Width = draggedSize.Width,
            Height = draggedSize.Height,
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1.5),
            BorderBrush = new SolidColorBrush(Color.FromArgb(150, 96, 165, 250)),
            Background = new SolidColorBrush(Color.FromArgb(30, 96, 165, 250)),
            Opacity = 1.0,
            IsHitTestVisible = false
        };

        visualItems.Add(new TimelineVisualItem
        {
            Element = placeholder,
            Left = currentLeft,
            Size = draggedSize,
            AnimationKey = (timeline, "drop-placeholder")
        });

        currentLeft += draggedSize.Width + TimelineItemGap;
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
    
    private object GetTimelineAnimationKey(MacroTimeline timeline, MacroStep step)
    {
        var rawItems = GetRawStepsForDisplayStep(timeline, step);

        if (rawItems.Count == 0)
            return step;

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
        Canvas.SetTop(connector, TimelineConnectorY - (TimelineConnectorThickness / 2.0));
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
            VisualItemCount = visualItems.Count
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

        var requiredCanvasWidth = CalculateTimelineCanvasWidth(timeline, visibleSteps);
        var canvasWidth = GetSharedTimelineCanvasWidth(requiredCanvasWidth);

        var replacementRow = CreateTimelineRow(
            timeline,
            visibleSteps,
            canvasWidth,
            rowIndex == 0,
            rowIndex == _document.Timelines.Count - 1);

        TimelineRowsPanel.Children.RemoveAt(rowIndex);
        TimelineRowsPanel.Children.Insert(rowIndex, replacementRow);

        Dispatcher.BeginInvoke(new Action(UpdateTimelineScrollIndicator));
    }

    private void AppendRecordedStepsToTimelineRow(MacroTimeline timeline, IReadOnlyList<MacroStep> addedRawSteps)
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
            var block = CreateStepBlock(timeline, step);

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

        var addBlock = CreateAddBlock(timeline);
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
        if (state.VisualItemCount <= 1)
        {
            if (state.Connector != null)
            {
                state.Canvas.Children.Remove(state.Connector);
                state.Connector = null;
            }

            return;
        }

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

            Canvas.SetLeft(state.Connector, state.FirstCenterX);
            Canvas.SetTop(state.Connector, TimelineConnectorY - (TimelineConnectorThickness / 2.0));
            Panel.SetZIndex(state.Connector, -10);
            state.Canvas.Children.Insert(0, state.Connector);
        }

        state.Connector.Width = Math.Max(0, state.LastCenterX - state.FirstCenterX);
    }

    private void EnsureTimelineCanvasWidthForAllRows(double requiredWidth)
    {
        if (TimelineRowsPanel == null)
            return;

        var width = GetSharedTimelineCanvasWidth(requiredWidth);

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
        var viewportWidth = TimelineScrollViewer?.ViewportWidth > 0
            ? TimelineScrollViewer.ViewportWidth
            : TimelineScrollViewer?.ActualWidth ?? 0;

        return Math.Max(0, viewportWidth - 20);
    }



    private double CalculateTimelineCanvasWidth(MacroTimeline timeline, IReadOnlyList<MacroStep> visibleSteps)
    {
        var contentWidth = TimelineFirstItemLeft;

        foreach (var step in visibleSteps)
        {
            var block = CreateStepBlock(timeline, step);
            var size = MeasureTimelineItem(block);

            contentWidth += size.Width + TimelineItemGap;
        }

        var addBlock = CreateAddBlock(timeline);
        var addSize = MeasureTimelineItem(addBlock);

        contentWidth += addSize.Width + TimelineRightPadding;

        var viewportWidth = TimelineScrollViewer?.ViewportWidth > 0
            ? TimelineScrollViewer.ViewportWidth
            : TimelineScrollViewer?.ActualWidth ?? 0;

        return Math.Max(Math.Max(0, viewportWidth - 20), contentWidth);
    }

    private Border CreateTimelineHeader(MacroTimeline timeline, bool isActive, bool isSelected, bool isFirst, bool isLast)
    {
        var backgroundColor = isActive
            ? Color.FromRgb(10, 52, 84)
            : Color.FromRgb(8, 17, 31);

        var borderColor = isSelected
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

        border.Child = new TextBlock
        {
            Text = timeline.Name,
            FontWeight = FontWeights.Black,
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(isActive
                ? Color.FromRgb(224, 242, 254)
                : Color.FromRgb(148, 163, 184))
        };

        AttachTimelineHeaderMouseHandlers(border, timeline);
        return border;
    }

    private UIElement CreateStepBlock(MacroTimeline timeline, MacroStep step)
    {
        return step.Type switch
        {
            MacroStepType.Delay => CreateDelayBlock(timeline, step),
            MacroStepType.Text => CreateTextStepBlock(timeline, step),
            MacroStepType.KeyDown or MacroStepType.KeyUp => CreateKeyStepBlock(timeline, step),
            _ => CreateTextStepBlock(timeline, step)
        };
    }

    private UIElement CreateKeyStepBlock(MacroTimeline timeline, MacroStep step)
    {
        var control = new KeyStepControl
        {
            Step = step,
            IsSelected = IsStepSelected(timeline, step),
            ShowKeyUpDown = timeline.ShowKeyUpDown,
            Tag = step
        };

        AttachStepMouseHandlers(control, timeline, step);
        return control;
    }

    private UIElement CreateTextStepBlock(MacroTimeline timeline, MacroStep step)
    {
        var control = new TextStepControl
        {
            Step = step,
            IsSelected = IsStepSelected(timeline, step),
            Tag = step
        };

        AttachStepMouseHandlers(control, timeline, step);

        control.MouseRightButtonDown += (_, e) =>
        {
            EditTextStep(timeline, step);
            e.Handled = true;
        };

        return control;
    }

    private UIElement CreateDelayBlock(MacroTimeline timeline, MacroStep step)
    {
        var control = new DelayStepControl
        {
            Step = step,
            IsSelected = IsStepSelected(timeline, step),
            Tag = step
        };

        control.DelayCommitted += (_, _) =>
        {
            RefreshTimeline();
            ScheduleSaveState();
        };

        AttachStepMouseHandlers(control, timeline, step);
        return control;
    }

    private UIElement CreateAddBlock(MacroTimeline timeline)
    {
        var control = new AddStepControl
        {
            Tag = timeline
        };

        control.AddClicked += AddButton_Click;
        return control;
    }

    private bool IsStepSelected(MacroTimeline timeline, MacroStep step)
    {
        if (!_selection.HasStepSelection || _selection.SelectedTimeline == null || _selection.SelectedStep == null)
            return false;

        if (!ReferenceEquals(_selection.SelectedTimeline, timeline))
            return false;

        if (ReferenceEquals(step, _selection.SelectedStep))
            return true;

        if (step.IsSyntheticDisplayStep)
        {
            return step.SourceSteps.Contains(_selection.SelectedStep) ||
                   (_selection.SelectedStep.IsSyntheticDisplayStep &&
                    step.SourceSteps.SequenceEqual(_selection.SelectedStep.SourceSteps));
        }

        if (_selection.SelectedStep.IsSyntheticDisplayStep)
            return _selection.SelectedStep.SourceSteps.Contains(step);

        return false;
    }

    private void SelectTimeline(MacroTimeline timeline)
    {
        if (_isClearConfirmationActive && !ReferenceEquals(_pendingClearTimeline, timeline))
            ResetClearConfirmation();
        
        _document.SelectTimeline(timeline);
        SyncOptionsFromActiveTimeline();
        ScheduleSaveState();
    }

    private void SyncOptionsFromActiveTimeline()
    {
        if (UseStandardDelayCheckBox == null)
            return;

        var timeline = _document.ActiveTimeline;

        _isSyncingOptions = true;

        UseStandardDelayCheckBox.IsChecked = timeline.UseStandardDelay;
        StandardDelayTextBox.Text = timeline.StandardDelayMs.ToString();
        ShowKeyUpDownCheckBox.IsChecked = timeline.ShowKeyUpDown;

        ShowKeyUpDownCheckBox.Visibility = timeline.UseStandardDelay ? Visibility.Visible : Visibility.Collapsed;
        ShowKeyUpDownCheckSeparator.Visibility = timeline.UseStandardDelay ? Visibility.Visible : Visibility.Collapsed;

        ActiveTimelineTextBlock.Text = $"{timeline.Name}/{_document.Timelines.Count}";
        UpdateTimelineOptionsPagerVisibility();

        _isSyncingOptions = false;
    }

    private void UpdateTimelineOptionsPagerVisibility()
    {
        var visibility = _document.Timelines.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

        if (PreviousTimelineOptionsButton != null)
            PreviousTimelineOptionsButton.Visibility = visibility;

        if (NextTimelineOptionsButton != null)
            NextTimelineOptionsButton.Visibility = visibility;

        if (ActiveTimelineTextBlock != null)
            ActiveTimelineTextBlock.Visibility = visibility;
    }

    private void OptionsControl_Changed(object sender, RoutedEventArgs e)
    {
        if (_isSyncingOptions || UseStandardDelayCheckBox == null)
            return;

        ApplyOptionsToActiveTimeline();
        RefreshTimeline();
        ScheduleSaveState();
    }

    private void StandardDelayTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSyncingOptions || StandardDelayTextBox == null)
            return;

        ApplyOptionsToActiveTimeline();
        RefreshTimeline();
        ScheduleSaveState();
    }

    private void ApplyOptionsToActiveTimeline()
    {
        var timeline = _document.ActiveTimeline;

        timeline.UseStandardDelay = UseStandardDelayCheckBox.IsChecked == true;
        timeline.StandardDelayMs = GetStandardDelayMs();

        if (!timeline.UseStandardDelay)
            timeline.ShowKeyUpDown = true;
        else
            timeline.ShowKeyUpDown = ShowKeyUpDownCheckBox.IsChecked == true;
    }

    private void PreviousTimelineOptionsButton_Click(object sender, RoutedEventArgs e)
    {
        _document.SelectPreviousTimeline();
        _selection.SelectTimeline(_document.ActiveTimeline);
        RefreshTimeline();
        ScheduleSaveState();
    }

    private void NextTimelineOptionsButton_Click(object sender, RoutedEventArgs e)
    {
        _document.SelectNextTimeline();
        _selection.SelectTimeline(_document.ActiveTimeline);
        RefreshTimeline();
        ScheduleSaveState();
    }

    private void UpdateWindowHeightForTimelineCount()
    {
        var timelineCount = Math.Max(1, _document.Timelines.Count);

        var timelineAreaHeight =
            (timelineCount * TimelineRowHeight) +
            ((timelineCount - 1) * TimelineRowGap) +
            52;

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
        var targetTop = TimelineConnectorY - (item.Size.Height / 2.0);

        canvas.Children.Add(item.Element);

        Canvas.SetLeft(item.Element, targetLeft);
        Canvas.SetTop(item.Element, targetTop);

        if (item.AnimationKey == null)
            return;

        var targetPosition = new Point(targetLeft, targetTop);

        if (_timelineVisualPositions.TryGetValue(item.AnimationKey, out var previousPosition))
        {
            var deltaX = previousPosition.X - targetLeft;
            var deltaY = previousPosition.Y - targetTop;

            if (Math.Abs(deltaX) > 0.5 || Math.Abs(deltaY) > 0.5)
            {
                var transform = new TranslateTransform(deltaX, deltaY);
                item.Element.RenderTransform = transform;

                var duration = new Duration(TimeSpan.FromMilliseconds(130));
                var easing = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut
                };

                transform.BeginAnimation(
                    TranslateTransform.XProperty,
                    new DoubleAnimation(0, duration)
                    {
                        EasingFunction = easing
                    });

                if (Math.Abs(deltaY) > 0.5)
                {
                    transform.BeginAnimation(
                        TranslateTransform.YProperty,
                        new DoubleAnimation(0, duration)
                        {
                            EasingFunction = easing
                        });
                }
                else
                {
                    transform.Y = 0;
                }
            }
        }

        _timelineVisualPositions[item.AnimationKey] = targetPosition;
    }
}
