using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using KeySpammer.Domain;
using KeySpammer.Services.Timeline;
using KeySpammer.State;
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

    private const double TimelineRowHeight = 88;
    private const double TimelineRowGap = 8;
    private const double TimelineHeaderWidth = 48;

    private const double TimelineFirstItemLeft = 12;
    private const double TimelineItemGap = 0;
    private const double TimelineRightPadding = 32;

    private const double TimelineConnectorY = 28;
    private const double TimelineConnectorThickness = 2;

    private void RefreshTimeline(object? sender = null, RoutedEventArgs? e = null)
    {
        if (TimelineRowsPanel == null)
            return;

        _document.EnsureTimeline();
        SyncOptionsFromActiveTimeline();

        TimelineRowsPanel.Children.Clear();

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

            TimelineRowsPanel.Children.Add(CreateTimelineRow(
                timeline,
                visibleStepsByTimeline[timeline],
                canvasWidth,
                i == 0));
        }

        UpdateTimelineOptionsPagerVisibility();
        UpdateWindowHeightForTimelineCount();

        Dispatcher.BeginInvoke(new Action(UpdateTimelineScrollIndicator));
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
            rowIndex == 0);

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

    private UIElement CreateTimelineRow(MacroTimeline timeline, IReadOnlyList<MacroStep> visibleSteps, double canvasWidth, bool isFirstRow)
    {
        var showTimelineHeaders = _document.Timelines.Count > 1;

        var row = new Grid
        {
            Height = TimelineRowHeight,
            Margin = new Thickness(0, isFirstRow ? 14 : 0, 0, showTimelineHeaders ? TimelineRowGap : 0),
            Tag = timeline,
            VerticalAlignment = VerticalAlignment.Top
        };

        row.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = showTimelineHeaders ? new GridLength(TimelineHeaderWidth) : new GridLength(0)
        });

        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        if (showTimelineHeaders)
        {
            var isActive = ReferenceEquals(timeline, _document.ActiveTimeline);
            var isSelected = _selection.IsTimelineSelected(timeline);

            var header = CreateTimelineHeader(timeline, isActive, isSelected);
            Grid.SetColumn(header, 0);
            row.Children.Add(header);
        }

        var canvas = new Canvas
        {
            Width = canvasWidth,
            Height = TimelineRowHeight,
            Background = Brushes.Transparent,
            Tag = timeline,
            VerticalAlignment = VerticalAlignment.Top
        };

        Grid.SetColumn(canvas, 1);
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

    private static void AddTimelineConnector(Canvas canvas, IReadOnlyList<TimelineVisualItem> visualItems)
    {
        if (visualItems.Count <= 1)
            return;

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

        canvas.Children.Add(connector);
    }

    private Border CreateTimelineHeader(MacroTimeline timeline, bool isActive, bool isSelected)
    {
        var border = new Border
        {
            Width = 36,
            Height = 44,
            Margin = new Thickness(0, 6, 8, 6),
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(isActive ? Color.FromRgb(18, 58, 90) : Color.FromRgb(15, 23, 42)),
            BorderBrush = new SolidColorBrush(isSelected ? Color.FromRgb(248, 250, 252) : Color.FromRgb(37, 99, 235)),
            BorderThickness = new Thickness(isSelected ? 2 : 1),
            Cursor = System.Windows.Input.Cursors.SizeAll,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Tag = timeline
        };

        border.Child = new TextBlock
        {
            Text = timeline.Name,
            FontWeight = FontWeights.Bold,
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(isActive ? Color.FromRgb(186, 230, 253) : Color.FromRgb(148, 163, 184))
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

        control.DelayCommitted += (_, _) => RefreshTimeline();

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
        var timelineCount = Math.Max(1, _document.Timelines.Count);

        var timelineAreaHeight =
            (timelineCount * TimelineRowHeight) +
            ((timelineCount - 1) * TimelineRowGap) +
            36; // header/footer/padding inside the timeline card

        var wantedHeight = 356 + (_document.Timelines.Count * 100);
        Height = Math.Max(MinHeight, wantedHeight);
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