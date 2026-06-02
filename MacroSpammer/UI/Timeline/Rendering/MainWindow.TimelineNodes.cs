using System.Windows;
using MacroSpammer.Domain;
using MacroSpammer.UI.Nodes;

namespace MacroSpammer;

public partial class MainWindow
{
    private UIElement CreateNode(MacroTimeline timeline, MacroNode node)
    {
        return node.Type switch
        {
            MacroNodeType.Delay or MacroNodeType.RandomDelay => CreateDelayNode(timeline, node),
            MacroNodeType.Text => CreateTextNode(timeline, node),
            MacroNodeType.MouseDown or MacroNodeType.MouseUp => CreateKeyNode(timeline, node),
            MacroNodeType.MouseClick => CreateForegroundMouseNode(timeline, node),
            MacroNodeType.CursorMove or MacroNodeType.BackgroundMouseDown or MacroNodeType.BackgroundMouseUp or MacroNodeType.BackgroundMouseClick => CreateMouseNode(timeline, node),
            MacroNodeType.KeyDown or MacroNodeType.KeyUp => CreateKeyNode(timeline, node),
            _ => CreateTextNode(timeline, node)
        };
    }

    private UIElement CreateKeyNode(MacroTimeline timeline, MacroNode node)
    {
        var control = new KeyNode
        {
            Node = node,
            IsSelected = IsStepSelected(timeline, node),
            ShowKeyUpDown = timeline.ShowKeyUpDown,
            Tag = node
        };

        AttachNodeMouseHandlers(control, timeline, node);
        return control;
    }

    private UIElement CreateTextNode(MacroTimeline timeline, MacroNode node)
    {
        var control = new TextNode
        {
            Node = node,
            IsSelected = IsStepSelected(timeline, node),
            Tag = node
        };

        AttachNodeMouseHandlers(control, timeline, node);
        return control;
    }

    private UIElement CreateDelayNode(MacroTimeline timeline, MacroNode node)
    {
        var control = new DelayNode
        {
            Node = node,
            IsSelected = IsStepSelected(timeline, node),
            Tag = node
        };

        control.DelayCommitted += (_, _) =>
        {
            RefreshTimeline();
            RefreshInspector();
            ScheduleSaveState();
        };

        AttachNodeMouseHandlers(control, timeline, node);
        return control;
    }

    private UIElement CreateMouseNode(MacroTimeline timeline, MacroNode node)
    {
        var control = new BackgroundMouseNode
        {
            Node = node,
            IsSelected = IsStepSelected(timeline, node),
            Tag = node
        };

        control.CoordinateCommitted += (_, _) =>
        {
            RefreshTimeline();
            ScheduleSaveState();
        };

        control.TargetPickRequested += async (_, _) =>
        {
            SaveUndoSnapshot();
            SelectTimeline(timeline);
            _selection.SelectNode(timeline, node);
            await PickMouseCoordinatesForNodeAsync(node);
        };

        AttachNodeMouseHandlers(control, timeline, node);
        return control;
    }

    private UIElement CreateForegroundMouseNode(MacroTimeline timeline, MacroNode node)
    {
        var control = new MouseNode
        {
            Node = node,
            IsSelected = IsStepSelected(timeline, node),
            Tag = node
        };

        AttachNodeMouseHandlers(control, timeline, node);
        return control;
    }

    private UIElement CreateAddNode(MacroTimeline timeline)
    {
        var control = new AddNode
        {
            Tag = timeline
        };

        control.AddClicked += AddButton_Click;
        return control;
    }

    private bool IsStepSelected(MacroTimeline timeline, MacroNode node)
    {
        if (!_selection.HasNodeSelection || _selection.SelectedTimeline == null || _selection.SelectedNodes.Count == 0)
            return false;

        if (!ReferenceEquals(_selection.SelectedTimeline, timeline))
            return false;

        return _selection.SelectedNodes.Any(selectedStep => IsSameSelectedStep(node, selectedStep));
    }

    private static bool IsSameSelectedStep(MacroNode node, MacroNode selectedNode)
    {
        if (ReferenceEquals(node, selectedNode))
            return true;

        if (node.IsSyntheticDisplayNode)
            return node.SourceNodes.Contains(selectedNode) ||
                   (selectedNode.IsSyntheticDisplayNode && node.SourceNodes.SequenceEqual(selectedNode.SourceNodes));

        return selectedNode.IsSyntheticDisplayNode && selectedNode.SourceNodes.Contains(node);
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
}
