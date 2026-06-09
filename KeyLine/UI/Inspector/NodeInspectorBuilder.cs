using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KeyLine.Domain;
using KeyLine.Services.Timeline;
using KeyLine.State;
using KeyLine.UI.Common.EntryBlocks;
using KeyLine.UI.Inspector.Nodes;
using KeyLine.UI.Timeline;

namespace KeyLine.UI.Inspector;

public sealed class NodeInspectorBuilder
{
    private readonly NodeInspectorContext _context;
    private readonly TimelineSelectionState _selection;
    private readonly Func<MacroWorkspace> _getActiveWorkspace;
    private readonly Func<IReadOnlyList<MacroWorkspace>> _getActiveProfileWorkspaces;
    private readonly Func<bool> _canEdit;
    private readonly Func<bool> _isRefreshing;
    private readonly Action _saveUndoSnapshot;
    private readonly Action<Action> _commitNodeChange;
    private readonly Action<Action> _commitNodeValueChange;
    private readonly Action _refreshInspector;
    private readonly Func<MacroNode, Task> _pickMouseCoordinatesForNodeAsync;
    private readonly Func<MacroNode, Task> _pickConditionPixelAsync;

    public NodeInspectorBuilder(
        TimelineSelectionState selection,
        Func<MacroWorkspace> getActiveWorkspace,
        Func<IReadOnlyList<MacroWorkspace>> getActiveProfileWorkspaces,
        Func<bool> canEdit,
        Func<bool> isRefreshing,
        Action saveDocumentUndoSnapshot,
        Action<Action> commitNodeChange,
        Action<Action> commitNodeValueChange,
        Action refreshInspector,
        Func<MacroNode, Task> pickMouseCoordinatesForNodeAsync,
        Func<MacroNode, Task> pickConditionPixelAsync)
    {
        _selection = selection;
        _getActiveWorkspace = getActiveWorkspace;
        _getActiveProfileWorkspaces = getActiveProfileWorkspaces;
        _canEdit = canEdit;
        _isRefreshing = isRefreshing;
        _saveUndoSnapshot = saveDocumentUndoSnapshot;
        _commitNodeChange = commitNodeChange;
        _commitNodeValueChange = commitNodeValueChange;
        _refreshInspector = refreshInspector;
        _pickMouseCoordinatesForNodeAsync = pickMouseCoordinatesForNodeAsync;
        _pickConditionPixelAsync = pickConditionPixelAsync;
        _context = new NodeInspectorContext(
            getActiveWorkspace: _getActiveWorkspace,
            getActiveProfileWorkspaces: _getActiveProfileWorkspaces,
            canEdit: _canEdit,
            isRefreshing: _isRefreshing,
            saveUndoSnapshot: _saveUndoSnapshot,
            commitNodeChange: _commitNodeChange,
            commitNodeValueChange: _commitNodeValueChange,
            refreshInspector: _refreshInspector,
            pickMouseCoordinatesForNodeAsync: _pickMouseCoordinatesForNodeAsync,
            pickConditionPixelAsync: _pickConditionPixelAsync);
    }

    public UIElement? Build(MacroTimeline timeline)
    {
        if (_selection.HasMultipleNodeSelection)
        {
            if (TryGetSelectedRepeatBlock(timeline, out var repeatStart, out var blockNodeCount))
                return CreateRepeatBlockInspector(repeatStart, blockNodeCount);

            if (TryGetSelectedConditionBlock(timeline, out var conditionStart, out blockNodeCount))
                return CreateConditionBlockInspector(conditionStart, blockNodeCount);

            return CreateReadonlySectionContent(("Selected", _selection.SelectedNodes.Count.ToString()));
        }

        if (_selection.HasNodeSelection && _selection.SelectedNode != null)
        {
            var policy = NodeInspectorPolicy.For(timeline, _selection.SelectedNode);

            if (!policy.HasInspector)
                return null;

            return CreateNodeInspector(timeline, _selection.SelectedNode, policy);
        }

        return null;
    }

    private UIElement CreateNodeInspector(MacroTimeline timeline, MacroNode node, NodeInspectorPolicy policy)
    {
        var section = CreateSection();

        section.Children.Add(CreateReadonlyRow("Type", NodeDisplayFormatter.GetNodeTypeText(node)));

        switch (node.Type)
        {
            case MacroNodeType.Delay:
            case MacroNodeType.RandomDelay:
            {
                var inspector = new DelayNodeInspector();
                inspector.Bind(_context, node, policy);
                section.Children.Add(inspector);
                break;
            }

            case MacroNodeType.Text:
            {
                var inspector = new TextNodeInspector();
                inspector.Bind(_context, node, policy);
                section.Children.Add(inspector);
                break;
            }
            
            case MacroNodeType.CursorMove:
            case MacroNodeType.BackgroundMouseDown:
            case MacroNodeType.BackgroundMouseUp:
            case MacroNodeType.BackgroundMouseClick:
            {
                var inspector = new MousePositionNodeInspector();
                inspector.Bind(_context, node, policy);
                section.Children.Add(inspector);
                break;
            }

            case MacroNodeType.MouseDown:
            case MacroNodeType.MouseUp:
            case MacroNodeType.KeyDown:
            case MacroNodeType.KeyUp:
            {
                var inspector = new KeyToggleNodeInspector();
                inspector.Bind(_context, node, policy);
                section.Children.Add(inspector);
                break;
            }

            case MacroNodeType.MouseScrollUp:
            case MacroNodeType.MouseScrollDown:
            case MacroNodeType.MouseScrollLeft:
            case MacroNodeType.MouseScrollRight:
            {
                var inspector = new MouseScrollNodeInspector();
                inspector.Bind(_context, node, policy);
                section.Children.Add(inspector);
                break;
            }

            case MacroNodeType.SystemOpenLaunch:
            {
                var inspector = new SystemLaunchNodeInspector();
                inspector.Bind(_context, node, policy);
                section.Children.Add(inspector);
                break;
            }

            case MacroNodeType.SystemVolumeControl:
            {
                var inspector = new SystemVolumeNodeInspector();
                inspector.Bind(_context, node, policy);
                section.Children.Add(inspector);
                break;
            }

            case MacroNodeType.SystemWaitUntilWindowOpens:
            {
                var inspector = new SystemWindowReferenceNodeInspector();
                inspector.Bind(_context, node, policy, SystemWindowReferenceInspectorMode.WaitUntilWindowOpens);
                section.Children.Add(inspector);
                break;
            }

            case MacroNodeType.SystemSelectTargetWindow:
            {
                var inspector = new SystemWindowReferenceNodeInspector();
                inspector.Bind(_context, node, policy, SystemWindowReferenceInspectorMode.SelectTargetWindow);
                section.Children.Add(inspector);
                break;
            }

            case MacroNodeType.SystemFocusWindow:
            {
                var inspector = new SystemWindowReferenceNodeInspector();
                inspector.Bind(_context, node, policy, SystemWindowReferenceInspectorMode.FocusWindow);
                section.Children.Add(inspector);
                break;
            }

            case MacroNodeType.RunMacro:
            {
                var inspector = new RunMacroNodeInspector();
                inspector.Bind(_context, node);
                section.Children.Add(inspector);
                break;
            }
        }

        return section;
    }

    private UIElement CreateRepeatBlockInspector(MacroNode repeatStart, int blockNodeCount)
    {
        var inspector = new RepeatBlockInspector();
        inspector.Bind(_context, repeatStart, blockNodeCount);
        return inspector;
    }

    private UIElement CreateConditionBlockInspector(MacroNode conditionStart, int blockNodeCount)
    {
        var inspector = new ConditionBlockInspector();
        inspector.Bind(_context, conditionStart, blockNodeCount, AddConditionDetailRows);
        return inspector;
    }

    private void AddConditionDetailRows(StackPanel section, MacroNode node, bool isEnabled)
    {
        switch (node.ConditionType)
        {
            case MacroConditionType.KeyState:
            {
                var inspector = new KeyStateConditionInspector();
                inspector.Bind(_context, node, isEnabled);
                section.Children.Add(inspector);
                break;
            }

            case MacroConditionType.PixelColor:
            {
                var inspector = new PixelColorConditionInspector();
                inspector.Bind(_context, node, isEnabled);
                section.Children.Add(inspector);
                break;
            }

            case MacroConditionType.RandomChance:
            {
                var inspector = new RandomChanceConditionInspector();
                inspector.Bind(_context, node, isEnabled);
                section.Children.Add(inspector);
                break;
            }

            case MacroConditionType.LoopContext:
            {
                var inspector = new LoopContextConditionInspector();
                inspector.Bind(_context, node, isEnabled);
                section.Children.Add(inspector);
                break;
            }

            case MacroConditionType.TargetWindowFocused:
            case MacroConditionType.WindowExists:
            {
                var inspector = new WindowReferenceConditionInspector();
                inspector.Bind(_context, node, isEnabled);
                section.Children.Add(inspector);
                break;
            }

            case MacroConditionType.MacroRunning:
            {
                var inspector = new MacroRunningConditionInspector();
                inspector.Bind(_context, node, isEnabled);
                section.Children.Add(inspector);
                break;
            }

            case MacroConditionType.TimePassed:
            {
                var inspector = new TimePassedConditionInspector();
                inspector.Bind(_context, node, isEnabled);
                section.Children.Add(inspector);
                break;
            }
        }
    }

    private bool TryGetSelectedRepeatBlock(
        MacroTimeline timeline,
        out MacroNode repeatStart,
        out int blockNodeCount)
    {
        repeatStart = null!;
        blockNodeCount = 0;

        if (!ReferenceEquals(_selection.SelectedTimeline, timeline))
            return false;

        var selectedSet = _selection.SelectedNodes.ToHashSet();
        foreach (var selectedStart in _selection.SelectedNodes.Where(node => node.Type == MacroNodeType.RepeatStart))
        {
            if (!TimelineBlockService.TryGetRepeatBlockRange(timeline, selectedStart, out var range))
                continue;

            if (range.Count != selectedSet.Count || range.Any(node => !selectedSet.Contains(node)))
                continue;

            repeatStart = selectedStart;
            blockNodeCount = range.Count;
            return true;
        }

        return false;
    }

    private bool TryGetSelectedConditionBlock(
        MacroTimeline timeline,
        out MacroNode conditionStart,
        out int blockNodeCount)
    {
        conditionStart = null!;
        blockNodeCount = 0;

        if (!ReferenceEquals(_selection.SelectedTimeline, timeline))
            return false;

        var selectedSet = _selection.SelectedNodes.ToHashSet();
        foreach (var selectedStart in _selection.SelectedNodes.Where(node => node.Type == MacroNodeType.ConditionStart))
        {
            if (!TimelineBlockService.TryGetConditionBlockRange(timeline, selectedStart, out var range))
                continue;

            if (range.Count != selectedSet.Count || range.Any(node => !selectedSet.Contains(node)))
                continue;

            conditionStart = selectedStart;
            blockNodeCount = range.Count;
            return true;
        }

        return false;
    }

    private static StackPanel CreateSection()
    {
        return new StackPanel();
    }

    private UIElement CreateReadonlySectionContent(params (string Label, string Value)[] rows)
    {
        var section = CreateSection();
        foreach (var row in rows)
            section.Children.Add(CreateReadonlyRow(row.Label, row.Value));

        return section;
    }

    private static UIElement CreateReadonlyRow(string label, string value)
    {
        var grid = CreateInspectorRowGrid();
        var rowTooltip = $"{label}: {(string.IsNullOrWhiteSpace(value) ? "-" : value)}";
        grid.ToolTip = rowTooltip;
        grid.Children.Add(CreateInspectorLabel(label, rowTooltip));

        var valueBlock = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(value) ? "-" : value,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = rowTooltip
        };

        Grid.SetColumn(valueBlock, 1);
        grid.Children.Add(valueBlock);
        return grid;
    }

    private static Grid CreateInspectorRowGrid()
    {
        var grid = new Grid
        {
            MinHeight = 22,
            Margin = new Thickness(0, 0, 0, 2)
        };

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        return grid;
    }

    private static TextBlock CreateInspectorLabel(string label, string? tooltip = null)
    {
        return new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush(Color.FromRgb(142, 160, 182)),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            ToolTip = tooltip ?? label
        };
    }
}
