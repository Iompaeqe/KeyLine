using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KeySpammer.Core;

namespace KeySpammer;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<MacroStep> _rawSteps = new();
    private readonly MacroRunner _runner = new();
    private readonly MacroRecorder _recorder = new();

    private bool _isTimelinePanning;
    private Point _timelinePanStartMouse;
    private double _timelinePanStartOffset;

    private MacroStep? _selectedStep;
    private MacroStep? _draggedStep;
    private Point _stepDragStartPoint;
    private bool _isDraggingStep;

    public MainWindow()
    {
        InitializeComponent();
        LoadWindows();
        RefreshTimeline();
    }

    private void LoadWindows()
    {
        WindowComboBox.ItemsSource = WindowEnumerator.GetVisibleWindows();
        if (WindowComboBox.Items.Count > 0 && WindowComboBox.SelectedIndex < 0)
            WindowComboBox.SelectedIndex = 0;
    }

    private void WindowComboBox_DropDownOpened(object sender, EventArgs e) => LoadWindows();

    private void WindowComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WindowComboBox.SelectedItem is not TargetWindowInfo target)
            return;

        var children = ChildWindowFinder.GetChildWindows(target.Handle);
        if (children.Count == 0)
        {
            HandleComboBox.Visibility = Visibility.Collapsed;
            HandleComboBox.ItemsSource = null;
            return;
        }

        var handles = new List<TargetWindowInfo>
        {
            new() { Handle = target.Handle, Title = "[Parent Window]" }
        };
        handles.AddRange(children);

        HandleComboBox.ItemsSource = handles;
        HandleComboBox.SelectedIndex = 0;
        HandleComboBox.Visibility = Visibility.Visible;
    }

    private TargetWindowInfo? GetTargetHandle()
    {
        if (HandleComboBox.Visibility == Visibility.Visible)
            return HandleComboBox.SelectedItem as TargetWindowInfo;
        return WindowComboBox.SelectedItem as TargetWindowInfo;
    }

    private void AddButton_Click(object sender, RoutedEventArgs e) => AddPopup.IsOpen = true;

    private void RecordMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddPopup.IsOpen = false;
        StartRecording();
    }

    private void DelayMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddPopup.IsOpen = false;
        _rawSteps.Add(new MacroStep { Type = MacroStepType.Delay, DelayMs = 100, IsRecordedDelay = false });
        UseStandardDelayCheckBox.IsChecked = false;
        RefreshTimeline();
    }

    private void TextMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddPopup.IsOpen = false;
        var dialog = new TextInputWindow { Owner = this };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.ResultText))
            return;
        _rawSteps.Add(new MacroStep { Type = MacroStepType.Text, Text = dialog.ResultText });
        RefreshTimeline();
    }

    private void StartRecording()
    {
        _recorder.Start();
        RecordStopButton.Visibility = Visibility.Visible;
        StatusText.Text = "● Recording";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
        Focus();
    }

    private void StopRecording()
    {
        _recorder.Stop();
        RecordStopButton.Visibility = Visibility.Collapsed;
        StatusText.Text = "Stopped";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(61, 84, 112));
        RefreshTimeline();
    }

    private void RecordStopButton_Click(object sender, RoutedEventArgs e) => StopRecording();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_recorder.IsRecording && e.Key == Key.Delete && _selectedStep != null)
        {
            DeleteSelectedStep();
            e.Handled = true;
            return;
        }

        if (!_recorder.IsRecording)
            return;

        if (e.Key == Key.Escape)
        {
            StopRecording();
            e.Handled = true;
            return;
        }

        foreach (var step in _recorder.RecordKeyDown(e, _rawSteps.Count > 0))
            _rawSteps.Add(step);

        e.Handled = true;
        RefreshTimeline();
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (!_recorder.IsRecording)
            return;

        foreach (var step in _recorder.RecordKeyUp(e, _rawSteps.Count > 0))
            _rawSteps.Add(step);

        e.Handled = true;
        RefreshTimeline();
    }

    private void DeleteSelectedStep()
    {
        if (_selectedStep == null)
            return;

        if (_selectedStep.IsSyntheticDisplayStep)
            foreach (var sourceStep in _selectedStep.SourceSteps)
                _rawSteps.Remove(sourceStep);
        else
            _rawSteps.Remove(_selectedStep);

        _selectedStep = null;
        RefreshTimeline();
    }

    private void RefreshTimeline(object? sender = null, RoutedEventArgs? e = null)
    {
        if (TimelinePanel == null)
            return;

        var useStandardDelay = UseStandardDelayCheckBox?.IsChecked == true;

        ShowKeyUpDownCheckBox.Visibility = useStandardDelay ? Visibility.Visible : Visibility.Collapsed;
        ShowKeyUpDownCheckSeparator.Visibility = useStandardDelay ? Visibility.Visible : Visibility.Collapsed;
        if (!useStandardDelay)
            ShowKeyUpDownCheckBox.IsChecked = true;

        TimelinePanel.Children.Clear();

        var visibleSteps = MacroTimelineBuilder.BuildVisibleSteps(
            _rawSteps.ToList(),
            useStandardDelay,
            ShowKeyUpDownCheckBox.IsChecked == true);

        foreach (var step in visibleSteps)
            TimelinePanel.Children.Add(CreateStepBlock(step));

        TimelinePanel.Children.Add(CreateAddBlock());

        Dispatcher.BeginInvoke(new Action(UpdateTimelineScrollIndicator));
    }

    private UIElement CreateStepBlock(MacroStep step)
    {
        if (step.Type == MacroStepType.Delay)
            return CreateDelayBlock(step);

        var showKeyUpDown = ShowKeyUpDownCheckBox?.IsChecked == true;
        var isSelected = IsStepSelected(step);
        var isKeyStep = step.Type is MacroStepType.KeyDown or MacroStepType.KeyUp;

        var blockText = step.Type switch
        {
            MacroStepType.Text => GetTextPreview(step.Text),
            _ => step.KeyName
        };

        var (bg, border, fg) = step.Type switch
        {
            MacroStepType.Text =>
                (Color.FromRgb(27, 45, 74), Color.FromRgb(96, 165, 250), Color.FromRgb(226, 238, 255)),

            MacroStepType.KeyDown =>
                (Color.FromRgb(20, 52, 96), Color.FromRgb(80, 150, 255), Color.FromRgb(230, 243, 255)),

            MacroStepType.KeyUp =>
                (Color.FromRgb(35, 45, 98), Color.FromRgb(125, 115, 255), Color.FromRgb(238, 236, 255)),

            _ =>
                (Color.FromRgb(30, 41, 59), Color.FromRgb(100, 116, 139), Color.FromRgb(226, 232, 240))
        };

        if (isSelected)
        {
            border = Color.FromRgb(248, 250, 252);
            fg = Color.FromRgb(255, 255, 255);
        }

        if (isKeyStep)
        {
            return CreateKeyStepBlock(step, showKeyUpDown, isSelected, bg, border, fg);
        }

        var width = step.Type == MacroStepType.Text
            ? Math.Max(110, Math.Min(220, (blockText.Length * 8) + 34))
            : 96;

        var borderElement = new Border
        {
            Width = width,
            Height = 50,
            Margin = new Thickness(5, 0, 5, 0),
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(bg),
            BorderBrush = new SolidColorBrush(border),
            BorderThickness = isSelected ? new Thickness(2) : new Thickness(1),
            ToolTip = step.Type == MacroStepType.Text ? step.Text : null,
            Tag = step,
            Child = new TextBlock
            {
                Text = blockText,
                Foreground = new SolidColorBrush(fg),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Segoe UI"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            }
        };

        AttachStepMouseHandlers(borderElement, step);

        if (step.Type == MacroStepType.Text)
        {
            borderElement.MouseRightButtonDown += (_, e) =>
            {
                EditTextStep(step);
                e.Handled = true;
            };
        }

        return borderElement;
    }

    private UIElement CreateKeyStepBlock(
        MacroStep step,
        bool showKeyUpDown,
        bool isSelected,
        Color bg,
        Color border,
        Color fg)
    {
        var keyText = step.KeyName;
        var isComboKey = keyText.Contains('+');

        var boxHeight = 52;

        var box = new Border
        {
            // IMPORTANT:
            // No fixed Width.
            // MinWidth only. This lets long combo keys expand naturally.
            MinWidth = isComboKey ? 110 : 62,
            Height = boxHeight,

            Padding = isComboKey
                ? new Thickness(12, 0, 12, 0)
                : new Thickness(10, 0, 10, 0),

            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(bg),
            BorderBrush = new SolidColorBrush(border),
            BorderThickness = isSelected ? new Thickness(2) : new Thickness(1),
            Tag = step,

            Child = new TextBlock
            {
                Text = keyText,
                Foreground = new SolidColorBrush(fg),
                FontSize = isComboKey ? 16 : 22,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Segoe UI"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,

                // IMPORTANT:
                // No ellipsis, no wrapping.
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.None
            }
        };

        if (!showKeyUpDown)
        {
            box.Margin = new Thickness(8, 12, 8, 12);
            AttachStepMouseHandlers(box, step);
            return box;
        }

        var outer = new Grid
        {
            Margin = isComboKey
                ? new Thickness(7, 0, 7, 0)
                : new Thickness(8, 0, 8, 0),

            Tag = step
        };

        outer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
        outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        outer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });

        var arrow = new TextBlock
        {
            Text = step.Type == MacroStepType.KeyUp ? "▲" : "▼",
            Foreground = new SolidColorBrush(fg),
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Segoe UI"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.95
        };

        Grid.SetRow(box, 1);
        outer.Children.Add(box);

        Grid.SetRow(arrow, step.Type == MacroStepType.KeyUp ? 0 : 2);
        outer.Children.Add(arrow);

        AttachStepMouseHandlers(outer, step);

        return outer;
    }

    private UIElement CreateDelayBlock(MacroStep step)
    {
        var isSelected = IsStepSelected(step);
        var (topText, bottomText) = DelayFormatter.Split(step.DelayMs);

        var bg = isSelected
            ? Color.FromRgb(30, 41, 59)
            : Color.FromRgb(20, 28, 40);

        var border = isSelected
            ? Color.FromRgb(248, 250, 252)
            : Color.FromRgb(71, 85, 105);

        var valueColor = isSelected
            ? Color.FromRgb(255, 251, 235)
            : Color.FromRgb(125, 211, 252);

        var unitColor = isSelected
            ? Color.FromRgb(253, 230, 138)
            : Color.FromRgb(148, 163, 184);

        var outer = new Border
        {
            Width = 54,
            Height = 46,
            Margin = new Thickness(7, 10, 7, 10),
            CornerRadius = new CornerRadius(9),
            Background = new SolidColorBrush(bg),
            BorderBrush = new SolidColorBrush(border),
            BorderThickness = new Thickness(1),
            Tag = step
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(27) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
        outer.Child = grid;

        var valueBox = new TextBox
        {
            Text = topText,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(valueColor),
            BorderBrush = Brushes.Transparent,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Segoe UI"),
            TextAlignment = TextAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0),
            Margin = new Thickness(3, 0, 3, 0)
        };

        valueBox.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);

        valueBox.GotKeyboardFocus += (_, _) =>
        {
            valueBox.Text = step.DelayMs.ToString();
            valueBox.SelectAll();
        };

        valueBox.LostFocus += (_, _) => ApplyDelayText(valueBox, step);

        valueBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                ApplyDelayText(valueBox, step);
                Keyboard.ClearFocus();
                e.Handled = true;
            }
        };

        Grid.SetRow(valueBox, 0);
        grid.Children.Add(valueBox);

        var divider = new Border
        {
            Height = 1,
            Margin = new Thickness(9, 0, 9, 0),
            Background = new SolidColorBrush(isSelected
                ? Color.FromRgb(248, 250, 252)
                : Color.FromRgb(71, 85, 105)),
            Opacity = isSelected ? 0.9 : 0.65
        };

        Grid.SetRow(divider, 1);
        grid.Children.Add(divider);

        var unitText = new TextBlock
        {
            Text = bottomText,
            Foreground = new SolidColorBrush(unitColor),
            FontSize = 10,
            FontFamily = new FontFamily("Segoe UI"),
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        Grid.SetRow(unitText, 2);
        grid.Children.Add(unitText);

        AttachStepMouseHandlers(outer, step);

        return outer;
    }

    private UIElement CreateAddBlock()
    {
        var button = new Button
        {
            Content = "+",
            Width = 32,
            Height = 32,
            FontSize = 18,
            Foreground = new SolidColorBrush(Color.FromRgb(61, 84, 112)),
            Margin = new Thickness(5, 0, 0, 0)
        };
        button.Click += AddButton_Click;
        return button;
    }

    private void AttachStepMouseHandlers(FrameworkElement element, MacroStep step)
    {
        element.PreviewMouseLeftButtonDown += (_, e) =>
        {
            SelectStep(step);

            _draggedStep = step;
            _stepDragStartPoint = e.GetPosition(TimelinePanel);
            _isDraggingStep = false;

            element.CaptureMouse();

            e.Handled = true;
        };

        element.PreviewMouseMove += (_, e) =>
        {
            if (_draggedStep == null || e.LeftButton != MouseButtonState.Pressed)
                return;

            var currentPoint = e.GetPosition(TimelinePanel);
            var dragDistance = Math.Abs(currentPoint.X - _stepDragStartPoint.X);

            if (!_isDraggingStep)
            {
                if (dragDistance < 6)
                    return;

                _isDraggingStep = true;
            }

            MoveStepByMouseX(_draggedStep, currentPoint.X);

            e.Handled = true;
        };

        element.PreviewMouseLeftButtonUp += (_, e) =>
        {
            _draggedStep = null;
            _isDraggingStep = false;

            if (element.IsMouseCaptured)
                element.ReleaseMouseCapture();

            e.Handled = true;
        };

        element.LostMouseCapture += (_, _) =>
        {
            _draggedStep = null;
            _isDraggingStep = false;
        };
    }
    
    private void MoveStepByMouseX(MacroStep draggedStep, double mouseX)
    {
        var draggedItems = GetRawStepsForDisplayStep(draggedStep);
        if (draggedItems.Count == 0)
            return;

        MacroStep? insertBeforeDisplayStep = null;

        foreach (var child in TimelinePanel.Children.OfType<FrameworkElement>())
        {
            if (child.Tag is not MacroStep childStep)
                continue;

            var childItems = GetRawStepsForDisplayStep(childStep);
            if (childItems.Count == 0)
                continue;

            // Ignore the dragged node/group itself.
            if (draggedItems.Any(childItems.Contains))
                continue;

            var childPos = child.TransformToAncestor(TimelinePanel).Transform(new Point(0, 0));
            var midpoint = childPos.X + child.ActualWidth * 0.5;

            if (mouseX < midpoint)
            {
                insertBeforeDisplayStep = childStep;
                break;
            }
        }

        MoveStepBeforeDisplayStep(draggedStep, insertBeforeDisplayStep);
    }
    
    private void MoveStepBeforeDisplayStep(MacroStep draggedStep, MacroStep? insertBeforeDisplayStep)
    {
        var draggedItems = GetRawStepsForDisplayStep(draggedStep);
        if (draggedItems.Count == 0)
            return;

        MacroStep? rawInsertAnchor = null;

        if (insertBeforeDisplayStep != null)
        {
            var targetItems = GetRawStepsForDisplayStep(insertBeforeDisplayStep);

            if (targetItems.Count > 0 && !draggedItems.Any(targetItems.Contains))
                rawInsertAnchor = targetItems[0];
        }

        var oldFirstIndex = _rawSteps.IndexOf(draggedItems[0]);
        var newIndexBeforeRemoval = rawInsertAnchor == null
            ? _rawSteps.Count
            : _rawSteps.IndexOf(rawInsertAnchor);

        if (newIndexBeforeRemoval < 0)
            newIndexBeforeRemoval = _rawSteps.Count;

        // Avoid rebuilding when the group is already in that position.
        if (newIndexBeforeRemoval == oldFirstIndex ||
            newIndexBeforeRemoval == oldFirstIndex + draggedItems.Count)
        {
            return;
        }

        foreach (var item in draggedItems)
            _rawSteps.Remove(item);

        var insertIndex = rawInsertAnchor == null
            ? _rawSteps.Count
            : _rawSteps.IndexOf(rawInsertAnchor);

        if (insertIndex < 0)
            insertIndex = _rawSteps.Count;

        for (var i = 0; i < draggedItems.Count; i++)
            _rawSteps.Insert(insertIndex + i, draggedItems[i]);

        _selectedStep = draggedStep;
        RefreshTimeline();
    }

    private void SelectStep(MacroStep step)
    {
        _selectedStep = step;
        Keyboard.Focus(this);
        Focus();
        RefreshTimeline();
    }

    private void MoveStep(MacroStep draggedStep, MacroStep targetStep)
    {
        if (draggedStep == targetStep)
            return;

        var draggedItems = GetRawStepsForDisplayStep(draggedStep);
        var targetItems = GetRawStepsForDisplayStep(targetStep);

        if (draggedItems.Count == 0 || targetItems.Count == 0)
            return;

        // Do not move a group onto itself.
        if (draggedItems.Any(targetItems.Contains))
            return;

        var targetAnchor = targetItems[0];

        // Remove the full dragged raw group.
        foreach (var item in draggedItems)
            _rawSteps.Remove(item);

        var insertIndex = _rawSteps.IndexOf(targetAnchor);
        if (insertIndex < 0)
            insertIndex = _rawSteps.Count;

        // Insert the group in original order.
        for (var i = 0; i < draggedItems.Count; i++)
            _rawSteps.Insert(insertIndex + i, draggedItems[i]);

        _selectedStep = draggedStep;
        RefreshTimeline();
    }
    
    private List<MacroStep> GetRawStepsForDisplayStep(MacroStep step)
    {
        if (step.IsSyntheticDisplayStep)
        {
            return step.SourceSteps
                .Where(sourceStep => _rawSteps.Contains(sourceStep))
                .ToList();
        }

        return _rawSteps.Contains(step)
            ? new List<MacroStep> { step }
            : new List<MacroStep>();
    }

    private MacroStep? FindStepFromElement(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is FrameworkElement element && element.Tag is MacroStep step)
                return step;
            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private void EditTextStep(MacroStep step)
    {
        var dialog = new TextInputWindow { Owner = this };
        dialog.SetText(step.Text);
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.ResultText))
            return;
        step.Text = dialog.ResultText;
        RefreshTimeline();
    }

    private void ApplyDelayText(TextBox box, MacroStep step)
    {
        if (int.TryParse(box.Text, out var value))
            step.DelayMs = Math.Max(0, value);
        box.Text = DelayFormatter.Format(step.DelayMs);
        RefreshTimeline();
    }

    private int GetStandardDelayMs() =>
        int.TryParse(StandardDelayTextBox.Text, out var ms) ? Math.Max(0, ms) : 50;

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _rawSteps.Clear();
        _selectedStep = null;
        RefreshTimeline();
    }

    private async void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_runner.IsRunning)
        {
            _runner.Stop();
            StartStopButton.Content = "▶  Start";
            StatusText.Text = "Stopped";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(61, 84, 112));
            return;
        }

        var target = GetTargetHandle();
        if (target == null || _rawSteps.Count == 0)
            return;

        StartStopButton.Content = "■  Stop";
        StatusText.Text = "Running";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));

        var loopCount = int.TryParse(LoopCountTextBox.Text, out var loops) ? loops : 0;

        await _runner.StartAsync(
            target.Handle,
            _rawSteps.ToList(),
            loopCount,
            UseStandardDelayCheckBox.IsChecked == true,
            GetStandardDelayMs());

        StartStopButton.Content = "▶  Start";
        StatusText.Text = "Stopped";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(61, 84, 112));
    }

    private static string GetTextPreview(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "TXT";

        text = text.Replace("\r", " ").Replace("\n", " ");
        return text.Length <= 18 ? text : text[..18] + "…";
    }

    private void TitleBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;

        // Do not drag when clicking titlebar buttons.
        if (IsClickInsideButton(e.OriginalSource as DependencyObject))
            return;

        try
        {
            DragMove();
        }
        catch
        {
            // DragMove can throw if mouse state changes during click.
        }
    }

    private static bool IsClickInsideButton(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is Button)
                return true;

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void TimelineScrollViewer_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateTimelineScrollIndicator();
    }

    private void TimelineScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateTimelineScrollIndicator();
    }

    private void TimelineScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateTimelineScrollIndicator();
    }

    private void UpdateTimelineScrollIndicator()
    {
        if (TimelineScrollViewer == null ||
            TimelineScrollTrack == null ||
            TimelineScrollThumb == null ||
            TimelineScrollIndicator == null)
        {
            return;
        }

        var extentWidth = TimelineScrollViewer.ExtentWidth;
        var viewportWidth = TimelineScrollViewer.ViewportWidth;
        var offset = TimelineScrollViewer.HorizontalOffset;

        var trackWidth = TimelineScrollIndicator.ActualWidth;

        if (trackWidth <= 0)
            return;

        TimelineScrollTrack.Width = trackWidth;

        var hasOverflow = extentWidth > viewportWidth + 1;

        if (!hasOverflow)
        {
            TimelineScrollThumb.Width = trackWidth;
            TimelineScrollThumb.Opacity = 0.22;
            Canvas.SetLeft(TimelineScrollThumb, 0);
            return;
        }

        TimelineScrollThumb.Opacity = 0.95;

        var thumbWidth = Math.Max(44, viewportWidth / extentWidth * trackWidth);
        thumbWidth = Math.Min(thumbWidth, trackWidth);

        var maxOffset = extentWidth - viewportWidth;
        var maxThumbLeft = trackWidth - thumbWidth;

        var thumbLeft = maxOffset <= 0
            ? 0
            : offset / maxOffset * maxThumbLeft;

        TimelineScrollThumb.Width = thumbWidth;
        Canvas.SetLeft(TimelineScrollThumb, thumbLeft);
    }

    private void TimelineScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsInsideInteractiveTimelineElement(e.OriginalSource as DependencyObject))
            return;

        _isTimelinePanning = true;
        _timelinePanStartMouse = e.GetPosition(TimelineScrollViewer);
        _timelinePanStartOffset = TimelineScrollViewer.HorizontalOffset;

        TimelineScrollViewer.CaptureMouse();
        TimelineScrollViewer.Cursor = Cursors.SizeWE;

        e.Handled = true;
    }

    private void TimelineScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isTimelinePanning)
            return;

        var currentMouse = e.GetPosition(TimelineScrollViewer);
        var deltaX = currentMouse.X - _timelinePanStartMouse.X;

        TimelineScrollViewer.ScrollToHorizontalOffset(_timelinePanStartOffset - deltaX);
        UpdateTimelineScrollIndicator();

        e.Handled = true;
    }

    private void TimelineScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isTimelinePanning)
            return;

        StopTimelinePanning();
        e.Handled = true;
    }

    private void TimelineScrollViewer_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isTimelinePanning)
            return;

        if (e.LeftButton != MouseButtonState.Pressed)
            StopTimelinePanning();
    }

    private void StopTimelinePanning()
    {
        if (!_isTimelinePanning)
            return;

        _isTimelinePanning = false;

        if (TimelineScrollViewer.IsMouseCaptured)
            TimelineScrollViewer.ReleaseMouseCapture();

        TimelineScrollViewer.Cursor = Cursors.Arrow;
    }

    private static bool IsInsideInteractiveTimelineElement(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is Button ||
                source is TextBox ||
                source is ComboBox)
            {
                return true;
            }

            if (source is FrameworkElement fe && fe.Tag is MacroStep)
                return true;

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }
    
    private bool IsStepSelected(MacroStep step)
    {
        if (_selectedStep == null)
            return false;

        if (ReferenceEquals(step, _selectedStep))
            return true;

        if (step.IsSyntheticDisplayStep)
            return step.SourceSteps.Contains(_selectedStep) ||
                   (_selectedStep.IsSyntheticDisplayStep &&
                    step.SourceSteps.SequenceEqual(_selectedStep.SourceSteps));

        if (_selectedStep.IsSyntheticDisplayStep)
            return _selectedStep.SourceSteps.Contains(step);

        return false;
    }
}