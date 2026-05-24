using System.Windows;
using System.Windows.Media;

namespace KeySpammer.UI.Config;

public static class GeneratedUiConfig
{
    public static TimelineUiConfig Timeline { get; } = new();
    public static KeyStepUiConfig KeyStep { get; } = new();
    public static DelayStepUiConfig DelayStep { get; } = new();
    public static TextStepUiConfig TextStep { get; } = new();
    public static AddStepUiConfig AddStep { get; } = new();
    public static DragUiConfig Drag { get; } = new();
}

public sealed class TimelineUiConfig
{
    public double RowHeight { get; init; } = 72;
    public double RowGap { get; init; } = 30;
    public double HeaderWidth { get; init; } = 56;

    public double FirstItemLeft { get; init; } = 12;
    public double ItemGap { get; init; } = 0;
    public double RightPadding { get; init; } = 32;

    public double ConnectorY { get; init; } = 36;
    public double ConnectorThickness { get; init; } = 2;

    public double HeaderTopExtra { get; init; } = 28;
    public double HeaderBottomExtra { get; init; } = 30;

    public double WindowBaseHeight { get; init; } = 276;
    public double WindowExtraTimelineHeight { get; init; } = 52;

    public CornerRadius HeaderBackplateCornerRadius { get; init; } = new(8, 0, 0, 8);
    public Thickness HeaderBackplateBorderThickness { get; init; } = new(1);
    public CornerRadius HeaderCornerRadiusFirst { get; init; } = new(8, 0, 0, 0);
    public CornerRadius HeaderCornerRadiusMiddle { get; init; } = new(0);
    public CornerRadius HeaderCornerRadiusLast { get; init; } = new(0, 0, 0, 8);
    public CornerRadius HeaderCornerRadiusSingle { get; init; } = new(8, 0, 0, 8);
    public Thickness HeaderBorderThickness { get; init; } = new(0, 1, 1, 1);
    public double HeaderFontSize { get; init; } = 14;

    public CornerRadius DropPlaceholderCornerRadius { get; init; } = new(10);
    public Thickness DropPlaceholderBorderThickness { get; init; } = new(1.5);

    public Color ConnectorColor { get; init; } = Color.FromRgb(31, 48, 66);

    public Color HeaderBackground { get; init; } = Color.FromRgb(8, 17, 31);
    public Color HeaderBackgroundActive { get; init; } = Color.FromRgb(10, 52, 84);
    public Color HeaderBorder { get; init; } = Color.FromRgb(30, 64, 100);
    public Color HeaderBorderActive { get; init; } = Color.FromRgb(14, 165, 233);
    public Color HeaderBorderSelected { get; init; } = Color.FromRgb(226, 232, 240);

    public Color HeaderText { get; init; } = Color.FromRgb(148, 163, 184);
    public Color HeaderTextActive { get; init; } = Color.FromRgb(224, 242, 254);

    public Color DropPlaceholderBorder { get; init; } = Color.FromArgb(150, 96, 165, 250);
    public Color DropPlaceholderBackground { get; init; } = Color.FromArgb(30, 96, 165, 250);
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

    public Color Background { get; init; } = Color.FromRgb(27, 45, 74);
    public Color Border { get; init; } = Color.FromRgb(96, 165, 250);
    public Color Text { get; init; } = Color.FromRgb(226, 238, 255);

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
    public double StepDragThreshold { get; init; } = 6;
    public double TimelineHeaderDragThreshold { get; init; } = 6;

    public double GhostOpacity { get; init; } = 0.86;
    public double GhostScale { get; init; } = 1.04;
    public double GhostFollowStrength { get; init; } = 0.65;

    // Adjusts where the dragged node ghost appears compared to the cursor.
    // X: positive moves the ghost right, negative moves it left.
    // Y: positive moves the ghost down, negative moves it up.
    // Default 0 keeps the previous behavior.
    public double GhostCursorOffsetX { get; init; } = 0;
    public double GhostCursorOffsetY { get; init; } = 30;
}
