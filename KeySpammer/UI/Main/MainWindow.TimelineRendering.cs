using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using KeySpammer.Domain;
using KeySpammer.Services.Timeline;
using KeySpammer.State;
using KeySpammer.UI.Controls;
using KeySpammer.UI.Config;
using KeySpammer.UI.Timeline;

namespace KeySpammer;

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


    private void RefreshTimeline(object? sender = null, RoutedEventArgs? e = null)
    {
        if (TimelineRowsPanel == null)
            return;

        TimelineRowsPanel.Children.Clear();
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
            CornerRadius = TimelineUi.HeaderBackplateCornerRadius,
            Background = new SolidColorBrush(TimelineUi.HeaderBackground),
            BorderBrush = new SolidColorBrush(TimelineUi.HeaderBorder),
            BorderThickness = TimelineUi.HeaderBackplateBorderThickness,
            IsHitTestVisible = false
        };

        Grid.SetRow(headerColumnBackplate, 0);
        Grid.SetRowSpan(headerColumnBackplate, TimelineHeaderGrid.RowDefinitions.Count);
        TimelineHeaderGrid.Children.Add(headerColumnBackplate);
    }
    
    private void AddTimelineHeaderToGrid(
        MacroTimeline timeline,
        int timelineIndex,
        bool isActive,
        bool isSelected)
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

        var existingCanvasWidth = GetExistingTimelineCanvasWidth(rowIndex);

        var visibleSteps = MacroTimelineBuilder.BuildVisibleSteps(
            GetTimelineRenderRawSteps(timeline).ToList(),
            timeline.UseStandardDelay,
            timeline.ShowKeyUpDown);

        var replacementRow = CreateTimelineRow(
            timeline,
            visibleSteps,
            existingCanvasWidth,
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

    private double CalculateSimpleTimelineCanvasWidth(
        Dictionary<MacroTimeline, List<MacroStep>> visibleStepsByTimeline)
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

    private UIElement CreateTimelineRow(
        MacroTimeline timeline,
        IReadOnlyList<MacroStep> visibleSteps,
        double canvasWidth,
        bool isFirstRow,
        bool isLastRow)
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

        var visualItems = BuildTimelineVisualItems(timeline, visibleSteps);

        AddTimelineConnector(canvas, visualItems);

        foreach (var item in visualItems)
            AddTimelineItem(canvas, item);

        return row;
    }
    
    private List<TimelineVisualItem> BuildTimelineVisualItems(
        MacroTimeline timeline,
        IReadOnlyList<MacroStep> visibleSteps)
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
        var placeholder = TimelineElementFactory.CreateDropPlaceholder(draggedSize);

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

    private static void AddTimelineConnector(Canvas canvas, IReadOnlyList<TimelineVisualItem> visualItems)
    {
        if (visualItems.Count <= 1)
            return;

        var firstCenterX = visualItems[0].CenterX;
        var lastCenterX = visualItems[^1].CenterX;

        var connector = TimelineElementFactory.CreateConnector(lastCenterX - firstCenterX);

        Canvas.SetLeft(connector, firstCenterX);
        Canvas.SetTop(connector, TimelineLayoutCalculator.GetConnectorTop(TimelineConnectorY, TimelineConnectorThickness));
        Panel.SetZIndex(connector, -10);

        canvas.Children.Add(connector);
    }

    private Border CreateTimelineHeader(
        MacroTimeline timeline,
        bool isActive,
        bool isSelected,
        bool isFirst,
        bool isLast)
    {
        var border = TimelineElementFactory.CreateTimelineHeader(
            timeline,
            isActive,
            isSelected,
            isFirst,
            isLast);

        AttachTimelineHeaderMouseHandlers(border, timeline);
        return border;
    }

    private UIElement CreateStepBlock(MacroTimeline timeline, MacroStep step)
    {
        var control = TimelineElementFactory.CreateStepBlock(
            timeline,
            step,
            IsStepSelected(timeline, step));

        AttachStepMouseHandlers(control, timeline, step);

        if (control is DelayStepControl delayControl)
            delayControl.DelayCommitted += (_, _) => RefreshTimeline();

        if (control is TextStepControl textControl)
        {
            textControl.MouseRightButtonDown += (_, e) =>
            {
                EditTextStep(timeline, step);
                e.Handled = true;
            };
        }

        return control;
    }

    private UIElement CreateAddBlock(MacroTimeline timeline)
    {
        var control = TimelineElementFactory.CreateAddStep(timeline);
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
    }

    private void StandardDelayTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSyncingOptions || StandardDelayTextBox == null)
            return;

        ApplyOptionsToActiveTimeline();
        RefreshTimeline();
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
    }

    private void NextTimelineOptionsButton_Click(object sender, RoutedEventArgs e)
    {
        _document.SelectNextTimeline();
        _selection.SelectTimeline(_document.ActiveTimeline);
        RefreshTimeline();
    }

    private void UpdateWindowHeightForTimelineCount()
    {
        var timelineAreaHeight = TimelineLayoutCalculator.GetTimelineAreaHeight(
            _document.Timelines.Count,
            TimelineRowHeight,
            TimelineRowGap,
            TimelineUi.WindowExtraTimelineHeight);

        var wantedHeight = TimelineUi.WindowBaseHeight + timelineAreaHeight;

        Height = Math.Max(MinHeight, wantedHeight);
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

    private static double GetElementDesiredHeight(UIElement element)
    {
        if (element is FrameworkElement frameworkElement)
        {
            if (!double.IsNaN(frameworkElement.Height) && frameworkElement.Height > 0)
                return frameworkElement.Height;

            if (frameworkElement.ActualHeight > 0)
                return frameworkElement.ActualHeight;
        }

        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        if (element.DesiredSize.Height > 0)
            return element.DesiredSize.Height;

        return 48;
    }
}