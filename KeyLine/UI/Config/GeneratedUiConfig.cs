using System.Windows;
using System.Windows.Media;

namespace KeyLine.UI.Config;

public static class GeneratedUiConfig
{
    public static TimelineUiConfig Timeline { get; } = new();
    public static KeyStepUiConfig KeyStep { get; } = new();
    public static DelayStepUiConfig DelayStep { get; } = new();
    public static TextStepUiConfig TextStep { get; } = new();
    public static AddStepUiConfig AddStep { get; } = new();
    public static DragUiConfig Drag { get; } = new();
    public static PerformanceUiConfig Performance { get; } = new();
}

public sealed class TimelineUiConfig
{
    public double RowHeight { get; init; } = 72;
    public double RowGap { get; init; } = 16;
    public double HeaderWidth { get; init; } = 140;

    public double FirstItemLeft { get; init; } = 12;
    public double ItemGap { get; init; } = 0;
    public double RightPadding { get; init; } = 32;

    public double ConnectorY { get; init; } = 36;
    public double ConnectorThickness { get; init; } = 2;

    public double HeaderTopExtra { get; init; } = 8;
    public double HeaderBottomExtra { get; init; } = 8;
}

public sealed class KeyStepUiConfig
{
    public double NormalFontSize { get; init; } = 22;
    public double ComboFontSize { get; init; } = 16;

    public double NormalMinWidth { get; init; } = 62;
    public double ComboMinWidth { get; init; } = 110;

    public Thickness NormalPadding { get; init; } = new(10, 0, 10, 0);
    public Thickness ComboPadding { get; init; } = new(12, 0, 12, 0);

    public Thickness NormalArrowMargin { get; init; } = new(8, 0, 8, 0);
    public Thickness ComboArrowMargin { get; init; } = new(7, 0, 7, 0);
    public Thickness NoArrowMargin { get; init; } = new(8, 12, 8, 12);

    public Thickness NormalBorderThickness { get; init; } = new(1);
    public Thickness SelectedBorderThickness { get; init; } = new(2);

    public Color KeyDownBackground { get; init; } = Color.FromRgb(20, 52, 96);
    public Color KeyDownBorder { get; init; } = Color.FromRgb(80, 150, 255);
    public Color KeyDownText { get; init; } = Color.FromRgb(230, 243, 255);

    public Color KeyUpBackground { get; init; } = Color.FromRgb(35, 45, 98);
    public Color KeyUpBorder { get; init; } = Color.FromRgb(125, 115, 255);
    public Color KeyUpText { get; init; } = Color.FromRgb(238, 236, 255);

    public Color FallbackBackground { get; init; } = Color.FromRgb(30, 41, 59);
    public Color FallbackBorder { get; init; } = Color.FromRgb(100, 116, 139);
    public Color FallbackText { get; init; } = Color.FromRgb(226, 232, 240);

    public Color SelectedBorder { get; init; } = Color.FromRgb(248, 250, 252);
    public Color SelectedText { get; init; } = Color.FromRgb(255, 255, 255);
}

public sealed class DelayStepUiConfig
{
    public Color Background { get; init; } = Color.FromRgb(20, 28, 40);
    public Color BackgroundSelected { get; init; } = Color.FromRgb(30, 41, 59);

    public Color Border { get; init; } = Color.FromRgb(71, 85, 105);
    public Color BorderSelected { get; init; } = Color.FromRgb(248, 250, 252);

    public Color ValueText { get; init; } = Color.FromRgb(125, 211, 252);
    public Color ValueTextSelected { get; init; } = Color.FromRgb(255, 251, 235);

    public Color UnitText { get; init; } = Color.FromRgb(148, 163, 184);
    public Color UnitTextSelected { get; init; } = Color.FromRgb(253, 230, 138);

    public double DividerOpacity { get; init; } = 0.65;
    public double DividerOpacitySelected { get; init; } = 0.9;
}

public sealed class TextStepUiConfig
{
    public double MinWidth { get; init; } = 110;
    public double MaxWidth { get; init; } = 220;
    public double WidthPerCharacter { get; init; } = 8;
    public double WidthPadding { get; init; } = 34;
    public int MaxPreviewCharacters { get; init; } = 18;

    public Color Background { get; init; } = Color.FromRgb(80, 50, 20);
    public Color Border { get; init; } = Color.FromRgb(251, 146, 60);
    public Color Text { get; init; } = Color.FromRgb(255, 237, 213);

    public Color BorderSelected { get; init; } = Color.FromRgb(248, 250, 252);
    public Color TextSelected { get; init; } = Color.FromRgb(255, 255, 255);

    public Thickness NormalBorderThickness { get; init; } = new(1);
    public Thickness SelectedBorderThickness { get; init; } = new(2);
}

public sealed class AddStepUiConfig
{
    public double Width { get; init; } = 32;
    public double Height { get; init; } = 32;
    public double FontSize { get; init; } = 18;
    public Thickness Margin { get; init; } = new(5, 0, 0, 0);
    public Color Text { get; init; } = Color.FromRgb(61, 84, 112);
}

public sealed class DragUiConfig
{
    public double NodeDragThreshold { get; init; } = 6;
    public double TimelineHeaderDragThreshold { get; init; } = 6;

    public double GhostOpacity { get; init; } = 0.86;
    public double GhostScale { get; init; } = 1.04;
    public double GhostFollowStrength { get; init; } = 0.65;

    public double GhostCursorOffsetX { get; init; } = 0;
    public double GhostCursorOffsetY { get; init; } = 25;

    public double AutoScrollEdgeSize { get; init; } = 64;
    public double AutoScrollMaxStep { get; init; } = 7;

    // Drag preview does not need to recalculate insertion slots for every single mouse pixel.
    // The ghost still follows the cursor every frame; this only throttles expensive preview math.
    public double PreviewMouseMoveEpsilon { get; init; } = 2.0;
}

public sealed class PerformanceUiConfig
{
    // Recording can generate many key events per second. Rebuilding the whole timeline for
    // every keydown/keyup makes WPF choke once there are 100+ nodes. This caps visual refresh.
    public int RecordingTimelineRefreshIntervalMs { get; init; } = 50;
}
