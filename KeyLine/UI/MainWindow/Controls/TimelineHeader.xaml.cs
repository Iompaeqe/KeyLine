using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KeyLine.UI.Timeline;

/// <summary>
/// One timeline's header card. This control owns only the visual tree (defined in the matching XAML);
/// all the decisions about what to show live in MainWindow, which calls these setters. Keeping the
/// markup in XAML (instead of building Borders/TextBlocks in code) matches the UI/Nodes and
/// UI/Inspector pattern.
/// </summary>
public partial class TimelineHeader : UserControl
{
    private TextBlock? _statusTarget;
    private Border? _dotTarget;

    public TimelineHeader()
    {
        InitializeComponent();
    }

    /// <summary>Card background, border, and overall opacity (disabled timelines are dimmed).</summary>
    public void SetCard(Brush background, Brush borderBrush, double opacity)
    {
        CardBorder.Background = background;
        CardBorder.BorderBrush = borderBrush;
        CardBorder.Opacity = opacity;
    }

    /// <summary>Shows the expanded or collapsed body and points the status/dot setters at it.</summary>
    public void ShowNormal(string name, bool collapsed, Brush accentBrush, Brush nameBrush, string metaText,
        string tooltip)
    {
        PendingDeleteBody.Visibility = Visibility.Collapsed;
        Accent.Visibility = Visibility.Visible;
        Accent.Background = accentBrush;
        Accent.Margin = new Thickness(0, collapsed ? 6 : 13, 0, collapsed ? 6 : 13);
        CardBorder.ToolTip = tooltip;

        if (collapsed)
        {
            ExpandedBody.Visibility = Visibility.Collapsed;
            CollapsedBody.Visibility = Visibility.Visible;
            CollapsedName.Text = name;
            CollapsedName.Foreground = nameBrush;
            _statusTarget = CollapsedStatus;
            _dotTarget = CollapsedDot;
        }
        else
        {
            CollapsedBody.Visibility = Visibility.Collapsed;
            ExpandedBody.Visibility = Visibility.Visible;
            ExpandedName.Text = name;
            ExpandedName.Foreground = nameBrush;
            MetaText.Text = metaText;
            _statusTarget = ExpandedStatus;
            _dotTarget = ExpandedDot;
        }
    }

    /// <summary>Shows the delete-confirmation body. No live status/dot in this state.</summary>
    public void ShowPendingDelete(string name)
    {
        ExpandedBody.Visibility = Visibility.Collapsed;
        CollapsedBody.Visibility = Visibility.Collapsed;
        Accent.Visibility = Visibility.Collapsed;
        PendingDeleteBody.Visibility = Visibility.Visible;
        PendingDeleteName.Text = name;
        CardBorder.ToolTip = null;
        _statusTarget = null;
        _dotTarget = null;
    }

    /// <summary>Sets the live status word on the currently-shown body.</summary>
    public void SetStatus(string text, Brush foreground, double fontSize, FontWeight weight, string? tooltip,
        bool visible)
    {
        if (_statusTarget == null)
            return;

        _statusTarget.Text = text;
        _statusTarget.Foreground = foreground;
        _statusTarget.FontSize = fontSize;
        _statusTarget.FontWeight = weight;
        _statusTarget.ToolTip = tooltip;
        _statusTarget.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Sets the status dot color on the currently-shown body.</summary>
    public void SetDot(Brush brush)
    {
        if (_dotTarget != null)
            _dotTarget.Background = brush;
    }
}
