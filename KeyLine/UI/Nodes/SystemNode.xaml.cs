using System.Windows;
using System.Windows.Media;
using KeyLine.Domain;
using KeyLine.UI.Timeline;

namespace KeyLine.UI.Nodes;

public partial class SystemNode : NodeBase
{
    public SystemNode()
    {
        InitializeComponent();
    }

    protected override void UpdateVisual()
    {
        var step = Node;
        if (step == null)
            return;

        ActionTextBlock.Text = step.Type switch
        {
            MacroNodeType.SystemOpenLaunch => NodeDisplayFormatter.GetSystemLaunchActionText(step),
            MacroNodeType.SystemVolumeControl => NodeDisplayFormatter.GetSystemVolumeActionText(step),
            MacroNodeType.SystemWaitUntilWindowOpens => NodeDisplayFormatter.GetSystemWindowWaitActionText(step),
            MacroNodeType.SystemSelectTargetWindow => NodeDisplayFormatter.GetSystemTargetWindowActionText(step),
            MacroNodeType.SystemFocusWindow => NodeDisplayFormatter.GetSystemFocusWindowActionText(step),
            _ => "SYSTEM"
        };

        DetailTextBlock.Text = step.Type switch
        {
            MacroNodeType.SystemOpenLaunch => NodeDisplayFormatter.GetSystemLaunchTargetSummary(step),
            MacroNodeType.SystemVolumeControl => NodeDisplayFormatter.GetSystemVolumeDetailText(step),
            MacroNodeType.SystemWaitUntilWindowOpens => NodeDisplayFormatter.GetSystemWindowWaitDetailText(step),
            MacroNodeType.SystemSelectTargetWindow => NodeDisplayFormatter.GetSystemTargetWindowDetailText(step),
            MacroNodeType.SystemFocusWindow => NodeDisplayFormatter.GetSystemFocusWindowDetailText(step),
            _ => ""
        };

        RootBorder.ToolTip = NodeDisplayFormatter.GetSystemNodeTooltip(step);

        RootBorder.Background = new SolidColorBrush(IsSelected
            ? Color.FromRgb(45, 52, 69)
            : Color.FromRgb(23, 32, 51));
        RootBorder.BorderBrush = new SolidColorBrush(IsSelected
            ? Color.FromRgb(148, 163, 184)
            : Color.FromRgb(82, 98, 122));
        RootBorder.BorderThickness = IsSelected ? new Thickness(2) : new Thickness(1);
        ActionTextBlock.Foreground = new SolidColorBrush(IsSelected
            ? Color.FromRgb(248, 250, 252)
            : Color.FromRgb(226, 232, 240));
        DetailTextBlock.Foreground = new SolidColorBrush(IsSelected
            ? Color.FromRgb(203, 213, 225)
            : Color.FromRgb(148, 163, 184));
    }
}
