using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KeySpammer.Domain;
using KeySpammer.UI.Config;
using KeySpammer.UI.Controls;

namespace KeySpammer.UI.Timeline;

public static class TimelineElementFactory
{
    private static TimelineUiConfig TimelineUi => GeneratedUiConfig.Timeline;

    public static FrameworkElement CreateStepBlock(
        MacroTimeline timeline,
        MacroStep step,
        bool isSelected)
    {
        return step.Type switch
        {
            MacroStepType.Delay => CreateDelayStep(step, isSelected),
            MacroStepType.Text => CreateTextStep(step, isSelected),
            MacroStepType.KeyDown or MacroStepType.KeyUp => CreateKeyStep(timeline, step, isSelected),
            _ => CreateTextStep(step, isSelected)
        };
    }

    public static KeyStepControl CreateKeyStep(
        MacroTimeline timeline,
        MacroStep step,
        bool isSelected)
    {
        return new KeyStepControl
        {
            Step = step,
            IsSelected = isSelected,
            ShowKeyUpDown = timeline.ShowKeyUpDown,
            Tag = step
        };
    }

    public static TextStepControl CreateTextStep(
        MacroStep step,
        bool isSelected)
    {
        return new TextStepControl
        {
            Step = step,
            IsSelected = isSelected,
            Tag = step
        };
    }

    public static DelayStepControl CreateDelayStep(
        MacroStep step,
        bool isSelected)
    {
        return new DelayStepControl
        {
            Step = step,
            IsSelected = isSelected,
            Tag = step
        };
    }

    public static AddStepControl CreateAddStep(MacroTimeline timeline)
    {
        return new AddStepControl
        {
            Tag = timeline
        };
    }

    public static Border CreateDropPlaceholder(Size size)
    {
        return new Border
        {
            Width = size.Width,
            Height = size.Height,
            CornerRadius = TimelineUi.DropPlaceholderCornerRadius,
            BorderThickness = TimelineUi.DropPlaceholderBorderThickness,
            BorderBrush = UiBrushes.Get(TimelineUi.DropPlaceholderBorder),
            Background = UiBrushes.Get(TimelineUi.DropPlaceholderBackground),
            Opacity = 1.0,
            IsHitTestVisible = false
        };
    }

    public static Border CreateConnector(double width)
    {
        return new Border
        {
            Width = Math.Max(0, width),
            Height = TimelineUi.ConnectorThickness,
            CornerRadius = new CornerRadius(TimelineUi.ConnectorThickness / 2.0),
            Background = UiBrushes.Get(TimelineUi.ConnectorColor),
            Opacity = 0.85,
            IsHitTestVisible = false
        };
    }

    public static Border CreateTimelineHeader(
        MacroTimeline timeline,
        bool isActive,
        bool isSelected,
        bool isFirst,
        bool isLast)
    {
        var backgroundColor = isActive
            ? TimelineUi.HeaderBackgroundActive
            : TimelineUi.HeaderBackground;

        var borderColor = isSelected
            ? TimelineUi.HeaderBorderSelected
            : isActive
                ? TimelineUi.HeaderBorderActive
                : TimelineUi.HeaderBorder;

        var border = new Border
        {
            Width = TimelineUi.HeaderWidth,
            Margin = new Thickness(0),
            CornerRadius = GetHeaderCornerRadius(isFirst, isLast),
            Background = UiBrushes.Get(backgroundColor),
            BorderBrush = UiBrushes.Get(borderColor),
            BorderThickness = TimelineUi.HeaderBorderThickness,
            Cursor = Cursors.SizeAll,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Tag = timeline
        };

        border.Child = new TextBlock
        {
            Text = timeline.Name,
            FontWeight = FontWeights.Black,
            FontSize = TimelineUi.HeaderFontSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = UiBrushes.Get(isActive
                ? TimelineUi.HeaderTextActive
                : TimelineUi.HeaderText)
        };

        return border;
    }

    private static CornerRadius GetHeaderCornerRadius(bool isFirst, bool isLast)
    {
        if (isFirst && isLast)
            return TimelineUi.HeaderCornerRadiusSingle;

        if (isFirst)
            return TimelineUi.HeaderCornerRadiusFirst;

        return isLast
            ? TimelineUi.HeaderCornerRadiusLast
            : TimelineUi.HeaderCornerRadiusMiddle;
    }
}
