using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KeyLine.Domain;
using KeyLine.Services.Timeline;
using KeyLine.State;
using KeyLine.UI.Common.EntryBlocks;
using KeyLine.UI.Timeline;

namespace KeyLine.UI.Inspector;

public sealed class NodeInspectorBuilder
{
    private readonly TimelineSelectionState _selection;
    private readonly Func<bool> _canEdit;
    private readonly Func<bool> _isRefreshing;
    private readonly Action _saveUndoSnapshot;
    private readonly Action<Action> _commitNodeChange;
    private readonly Action _refreshInspector;
    private readonly Func<MacroNode, Task> _pickMouseCoordinatesForNodeAsync;

    public NodeInspectorBuilder(
        TimelineSelectionState selection,
        Func<bool> canEdit,
        Func<bool> isRefreshing,
        Action saveUndoSnapshot,
        Action<Action> commitNodeChange,
        Action refreshInspector,
        Func<MacroNode, Task> pickMouseCoordinatesForNodeAsync)
    {
        _selection = selection;
        _canEdit = canEdit;
        _isRefreshing = isRefreshing;
        _saveUndoSnapshot = saveUndoSnapshot;
        _commitNodeChange = commitNodeChange;
        _refreshInspector = refreshInspector;
        _pickMouseCoordinatesForNodeAsync = pickMouseCoordinatesForNodeAsync;
    }

    public UIElement? Build(MacroTimeline timeline)
    {
        if (_selection.HasMultipleNodeSelection)
        {
            if (TryGetSelectedRepeatBlock(timeline, out var repeatStart, out var blockNodeCount))
                return CreateRepeatBlockInspector(repeatStart, blockNodeCount);

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
                section.Children.Add(CreateDelayRow(
                    "Delay",
                    node.DelayMs,
                    value => _commitNodeChange(() => node.DelayMs = value),
                    TooltipNotes.DelayNodeValue,
                    policy.CanEditDelay));
                break;

            case MacroNodeType.RandomDelay:
                section.Children.Add(CreateDelayRow(
                    "Min",
                    node.RandomDelayMinMs,
                    value => _commitNodeChange(() =>
                    {
                        node.RandomDelayMinMs = value;
                        NormalizeRandomDelay(node);
                    }),
                    TooltipNotes.RandomDelayMinimum,
                    policy.CanEditRandomDelay));

                section.Children.Add(CreateDelayRow(
                    "Max",
                    node.RandomDelayMaxMs,
                    value => _commitNodeChange(() =>
                    {
                        node.RandomDelayMaxMs = value;
                        NormalizeRandomDelay(node);
                    }),
                    TooltipNotes.RandomDelayMaximum,
                    policy.CanEditRandomDelay));
                break;

            case MacroNodeType.Text:
                section.Children.Add(CreateTextEditRow(node, policy.CanEditText));
                break;

            case MacroNodeType.RepeatStart:
                section.Children.Add(CreateNumberRow(
                    "Count",
                    Math.Max(0, node.RepeatCount),
                    value => _commitNodeChange(() => node.RepeatCount = Math.Max(1, value)),
                    min: 1,
                    tooltip: TooltipNotes.RepeatCount,
                    isEnabled: policy.CanEditRepeatCount));
                break;

            case MacroNodeType.RepeatEnd:
                section.Children.Add(CreateReadonlyRow("Block", GetBlockLabel(node)));
                break;

            case MacroNodeType.CursorMove:
            case MacroNodeType.BackgroundMouseDown:
            case MacroNodeType.BackgroundMouseUp:
            case MacroNodeType.BackgroundMouseClick:
                section.Children.Add(CreateNumberRow(
                    "X",
                    node.MouseX,
                    value => _commitNodeChange(() => node.MouseX = value),
                    isEnabled: policy.CanEditMousePosition));

                section.Children.Add(CreateNumberRow(
                    "Y",
                    node.MouseY,
                    value => _commitNodeChange(() => node.MouseY = value),
                    isEnabled: policy.CanEditMousePosition));

                section.Children.Add(CreatePickPointButton(node, policy.CanPickMousePosition));
                break;

            case MacroNodeType.MouseDown:
            case MacroNodeType.MouseUp:
                section.Children.Add(CreateNumberRow(
                    "Button",
                    Math.Clamp(node.MouseButton <= 0 ? 1 : node.MouseButton, 1, 5),
                    value => _commitNodeChange(() => node.MouseButton = Math.Clamp(value, 1, 5)),
                    min: 1,
                    max: 5,
                    isEnabled: policy.CanEditMouseButton));
                break;

            case MacroNodeType.KeyDown:
            case MacroNodeType.KeyUp:
                section.Children.Add(CreateReadonlyRow("Key", node.KeyName));
                break;

            case MacroNodeType.MouseClick:
                section.Children.Add(CreateReadonlyRow("Button", "Left"));
                break;
        }

        return section;
    }

    private UIElement CreateRepeatBlockInspector(MacroNode repeatStart, int blockNodeCount)
    {
        var section = CreateSection();

        section.Children.Add(CreateReadonlyRow("Type", "Repeat Block"));
        section.Children.Add(CreateNumberRow(
            "Count",
            Math.Max(1, repeatStart.RepeatCount),
            value => _commitNodeChange(() => repeatStart.RepeatCount = Math.Max(1, value)),
            min: 1,
            tooltip: TooltipNotes.RepeatCount,
            isEnabled: true));
        section.Children.Add(CreateReadonlyRow("Inside", Math.Max(0, blockNodeCount - 2).ToString()));

        return section;
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

    private static string GetBlockLabel(MacroNode node)
    {
        return string.IsNullOrWhiteSpace(node.RepeatBlockId)
            ? "-"
            : node.RepeatBlockId[..Math.Min(8, node.RepeatBlockId.Length)];
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

    private UIElement CreateNumberRow(
        string label,
        int value,
        Action<int> commit,
        string suffix = "",
        int min = 0,
        int? max = null,
        string? tooltip = null,
        bool isEnabled = true)
    {
        var grid = CreateInspectorRowGrid();
        var rowTooltip = tooltip ?? $"{label} value.";
        var canEdit = CanEditOption(isEnabled);

        grid.ToolTip = rowTooltip;
        grid.Children.Add(CreateInspectorLabel(label, rowTooltip));

        var host = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(host, 1);

        var numberEntry = new NumberEntryBlock
        {
            IsEnabled = canEdit,
            ToolTip = rowTooltip
        };

        var textBox = numberEntry.TextBox;
        textBox.Text = value.ToString();
        textBox.IsEnabled = canEdit;

        host.Children.Add(numberEntry);

        if (!string.IsNullOrWhiteSpace(suffix))
        {
            host.Children.Add(new TextBlock
            {
                Text = suffix,
                Margin = new Thickness(6, 0, 0, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(142, 160, 182)),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = rowTooltip
            });
        }

        textBox.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
        textBox.GotKeyboardFocus += (_, _) => textBox.SelectAll();
        textBox.LostFocus += (_, _) =>
            InspectorCommitService.CommitNumberText(textBox, value, commit, min, max, _isRefreshing());
        textBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;

            InspectorCommitService.CommitNumberText(textBox, value, commit, min, max, _isRefreshing());
            Keyboard.ClearFocus();
            e.Handled = true;
        };

        grid.Children.Add(host);
        return grid;
    }

    private UIElement CreateDelayRow(
        string label,
        int value,
        Action<int> commit,
        string tooltip,
        bool isEnabled = true)
    {
        var grid = CreateInspectorRowGrid();
        var canEdit = CanEditOption(isEnabled);

        grid.ToolTip = tooltip;
        grid.Children.Add(CreateInspectorLabel(label, tooltip));

        var entry = new TimeEntryBlock
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            ToolTip = $"{tooltip} Click to edit in milliseconds.",
            IsEnabled = canEdit
        };

        entry.SetDisplay(value);
        entry.TextBox.IsEnabled = canEdit;

        Grid.SetColumn(entry, 1);

        var textBox = entry.TextBox;
        textBox.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
        textBox.GotKeyboardFocus += (_, _) =>
        {
            textBox.Text = value.ToString();
            entry.UnitText.Text = "ms";
            textBox.SelectAll();
        };
        textBox.LostFocus += (_, _) => InspectorCommitService.CommitDelayText(entry, value, commit);
        textBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;

            InspectorCommitService.CommitDelayText(entry, value, commit);
            Keyboard.ClearFocus();
            e.Handled = true;
        };

        grid.Children.Add(entry);
        return grid;
    }

    private UIElement CreateTextEditRow(MacroNode node, bool isEnabled)
    {
        var canEdit = CanEditOption(isEnabled);

        var panel = new StackPanel
        {
            Margin = new Thickness(0, 2, 0, 0),
            ToolTip = TooltipNotes.TextNodeValue,
            IsEnabled = canEdit
        };

        panel.Children.Add(new TextBlock
        {
            Text = "Text",
            Foreground = new SolidColorBrush(Color.FromRgb(142, 160, 182)),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 5)
        });

        var textBox = new TextBox
        {
            Text = node.Text,
            Height = 58,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            IsEnabled = canEdit,
            ToolTip = TooltipNotes.TextNodeEdit
        };

        textBox.LostFocus += (_, _) =>
        {
            if (!_isRefreshing() && textBox.Text != node.Text)
                _commitNodeChange(() => node.Text = textBox.Text);
        };
        textBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                return;

            if (textBox.Text != node.Text)
                _commitNodeChange(() => node.Text = textBox.Text);

            Keyboard.ClearFocus();
            e.Handled = true;
        };

        panel.Children.Add(textBox);
        return panel;
    }

    private UIElement CreatePickPointButton(MacroNode node, bool isEnabled)
    {
        var button = new Button
        {
            Content = "Pick point",
            Height = 22,
            Margin = new Thickness(0, 2, 0, 0),
            IsEnabled = CanEditOption(isEnabled),
            ToolTip = TooltipNotes.PickMouseCoordinates
        };

        button.Click += async (_, _) =>
        {
            _saveUndoSnapshot();
            await _pickMouseCoordinatesForNodeAsync(node);
            _refreshInspector();
        };

        return button;
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

    private static void NormalizeRandomDelay(MacroNode node)
    {
        if (node.RandomDelayMaxMs < node.RandomDelayMinMs)
            (node.RandomDelayMinMs, node.RandomDelayMaxMs) = (node.RandomDelayMaxMs, node.RandomDelayMinMs);
    }

    private bool CanEditOption(bool optionEnabled)
    {
        return _canEdit() && optionEnabled;
    }
}
