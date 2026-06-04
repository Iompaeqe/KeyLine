using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KeyLine.Domain;
using KeyLine.Interop;
using KeyLine.Services.Input;
using KeyLine.Services.Timeline;
using KeyLine.State;
using KeyLine.UI.Common.EntryBlocks;
using KeyLine.UI.Timeline;

namespace KeyLine.UI.Inspector;

public sealed class NodeInspectorBuilder
{
    private static readonly IReadOnlyList<InspectorOption<MacroConditionType>> ConditionTypeOptions =
    [
        new("Key held", MacroConditionType.KeyState),
        new("Pixel matches", MacroConditionType.PixelColor),
        new("Random chance", MacroConditionType.RandomChance),
        new("Loop context", MacroConditionType.LoopContext)
    ];

    private static readonly IReadOnlyList<InspectorOption<MacroConditionLoopMode>> ConditionLoopModeOptions =
    [
        new("First loop", MacroConditionLoopMode.FirstLoop),
        new("Last loop", MacroConditionLoopMode.LastLoop),
        new("Every N loops", MacroConditionLoopMode.EveryNLoops),
        new("First repeat", MacroConditionLoopMode.FirstRepeat),
        new("Last repeat", MacroConditionLoopMode.LastRepeat),
        new("Every N repeats", MacroConditionLoopMode.EveryNRepeats)
    ];

    private readonly TimelineSelectionState _selection;
    private readonly Func<bool> _canEdit;
    private readonly Func<bool> _isRefreshing;
    private readonly Action _saveUndoSnapshot;
    private readonly Action<Action> _commitNodeChange;
    private readonly Action _refreshInspector;
    private readonly Func<MacroNode, Task> _pickMouseCoordinatesForNodeAsync;
    private readonly Func<MacroNode, Task> _pickConditionPixelAsync;

    public NodeInspectorBuilder(
        TimelineSelectionState selection,
        Func<bool> canEdit,
        Func<bool> isRefreshing,
        Action saveUndoSnapshot,
        Action<Action> commitNodeChange,
        Action refreshInspector,
        Func<MacroNode, Task> pickMouseCoordinatesForNodeAsync,
        Func<MacroNode, Task> pickConditionPixelAsync)
    {
        _selection = selection;
        _canEdit = canEdit;
        _isRefreshing = isRefreshing;
        _saveUndoSnapshot = saveUndoSnapshot;
        _commitNodeChange = commitNodeChange;
        _refreshInspector = refreshInspector;
        _pickMouseCoordinatesForNodeAsync = pickMouseCoordinatesForNodeAsync;
        _pickConditionPixelAsync = pickConditionPixelAsync;
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

            case MacroNodeType.ConditionStart:
                section.Children.Add(CreateConditionSummaryRow(node));
                AddConditionConfigurationRows(section, node, policy.CanEditCondition);
                break;

            case MacroNodeType.ConditionEnd:
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

    private UIElement CreateConditionBlockInspector(MacroNode conditionStart, int blockNodeCount)
    {
        var section = CreateSection();

        section.Children.Add(CreateReadonlyRow("Type", "Condition Block"));
        section.Children.Add(CreateConditionSummaryRow(conditionStart));
        AddConditionConfigurationRows(section, conditionStart, isEnabled: true);
        section.Children.Add(CreateReadonlyRow("Inside", Math.Max(0, blockNodeCount - 2).ToString()));

        return section;
    }

    private UIElement CreateConditionSummaryRow(MacroNode conditionStart) =>
        CreateReadonlyRow("Summary", NodeDisplayFormatter.GetConditionSummary(conditionStart));

    private void AddConditionConfigurationRows(StackPanel section, MacroNode node, bool isEnabled)
    {
        section.Children.Add(CreateOptionRow(
            "Condition",
            ConditionTypeOptions,
            node.ConditionType,
            value => _commitNodeChange(() =>
            {
                node.ConditionType = value;
                NormalizeConditionDefaults(node);
            }),
            tooltip: TooltipNotes.ConditionType,
            isEnabled: isEnabled));

        switch (node.ConditionType)
        {
            case MacroConditionType.KeyState:
                section.Children.Add(CreateConditionShortcutCaptureRow(node, isEnabled));
                break;

            case MacroConditionType.PixelColor:
                section.Children.Add(CreateReadonlyRow(
                    "Picked",
                    $"{Math.Max(0, node.ConditionPixelX)}, {Math.Max(0, node.ConditionPixelY)}"));
                section.Children.Add(CreateReadonlyRow(
                    "Color",
                    $"RGB {Math.Clamp(node.ConditionPixelRed, 0, 255)}, {Math.Clamp(node.ConditionPixelGreen, 0, 255)}, {Math.Clamp(node.ConditionPixelBlue, 0, 255)}"));
                section.Children.Add(CreatePickPixelButton(node, isEnabled));
                section.Children.Add(CreateNumberRow(
                    "X",
                    Math.Max(0, node.ConditionPixelX),
                    value => _commitNodeChange(() => node.ConditionPixelX = Math.Max(0, value)),
                    tooltip: TooltipNotes.ConditionPixelPosition,
                    isEnabled: isEnabled));
                section.Children.Add(CreateNumberRow(
                    "Y",
                    Math.Max(0, node.ConditionPixelY),
                    value => _commitNodeChange(() => node.ConditionPixelY = Math.Max(0, value)),
                    tooltip: TooltipNotes.ConditionPixelPosition,
                    isEnabled: isEnabled));
                section.Children.Add(CreateNumberRow(
                    "R",
                    Math.Clamp(node.ConditionPixelRed, 0, 255),
                    value => _commitNodeChange(() => node.ConditionPixelRed = Math.Clamp(value, 0, 255)),
                    max: 255,
                    tooltip: TooltipNotes.ConditionPixelColor,
                    isEnabled: isEnabled));
                section.Children.Add(CreateNumberRow(
                    "G",
                    Math.Clamp(node.ConditionPixelGreen, 0, 255),
                    value => _commitNodeChange(() => node.ConditionPixelGreen = Math.Clamp(value, 0, 255)),
                    max: 255,
                    tooltip: TooltipNotes.ConditionPixelColor,
                    isEnabled: isEnabled));
                section.Children.Add(CreateNumberRow(
                    "B",
                    Math.Clamp(node.ConditionPixelBlue, 0, 255),
                    value => _commitNodeChange(() => node.ConditionPixelBlue = Math.Clamp(value, 0, 255)),
                    max: 255,
                    tooltip: TooltipNotes.ConditionPixelColor,
                    isEnabled: isEnabled));
                section.Children.Add(CreateNumberRow(
                    "Tolerance",
                    Math.Clamp(node.ConditionPixelTolerance, 0, 255),
                    value => _commitNodeChange(() => node.ConditionPixelTolerance = Math.Clamp(value, 0, 255)),
                    max: 255,
                    tooltip: TooltipNotes.ConditionPixelTolerance,
                    isEnabled: isEnabled));
                break;

            case MacroConditionType.RandomChance:
                section.Children.Add(CreateNumberRow(
                    "Chance",
                    Math.Clamp(node.ConditionChancePercent, 0, 100),
                    value => _commitNodeChange(() => node.ConditionChancePercent = Math.Clamp(value, 0, 100)),
                    suffix: "%",
                    max: 100,
                    tooltip: TooltipNotes.ConditionChance,
                    isEnabled: isEnabled));
                break;

            case MacroConditionType.LoopContext:
                section.Children.Add(CreateOptionRow(
                    "Mode",
                    ConditionLoopModeOptions,
                    node.ConditionLoopMode,
                    value => _commitNodeChange(() =>
                    {
                        node.ConditionLoopMode = value;
                        node.ConditionLoopInterval = Math.Max(1, node.ConditionLoopInterval);
                    }),
                    tooltip: TooltipNotes.ConditionLoopContext,
                    isEnabled: isEnabled));

                if (RequiresLoopInterval(node.ConditionLoopMode))
                {
                    section.Children.Add(CreateNumberRow(
                        "Every",
                        Math.Max(1, node.ConditionLoopInterval),
                        value => _commitNodeChange(() => node.ConditionLoopInterval = Math.Max(1, value)),
                        suffix: node.ConditionLoopMode == MacroConditionLoopMode.EveryNRepeats ? "repeats" : "loops",
                        min: 1,
                        tooltip: TooltipNotes.ConditionLoopInterval,
                        isEnabled: isEnabled));
                }
                break;
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

    private static string GetBlockLabel(MacroNode node)
    {
        var blockId = node.Type is MacroNodeType.ConditionStart or MacroNodeType.ConditionEnd
            ? node.ConditionBlockId
            : node.RepeatBlockId;

        return string.IsNullOrWhiteSpace(blockId)
            ? "-"
            : blockId[..Math.Min(8, blockId.Length)];
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

    private UIElement CreateOptionRow<T>(
        string label,
        IReadOnlyList<InspectorOption<T>> options,
        T currentValue,
        Action<T> commit,
        string? tooltip = null,
        bool isEnabled = true)
    {
        var grid = CreateInspectorRowGrid();
        var rowTooltip = tooltip ?? $"{label} option.";
        var canEdit = CanEditOption(isEnabled);

        grid.ToolTip = rowTooltip;
        grid.Children.Add(CreateInspectorLabel(label, rowTooltip));

        var comboBox = new ComboBox
        {
            ItemsSource = options,
            DisplayMemberPath = nameof(InspectorOption<T>.Label),
            SelectedValuePath = nameof(InspectorOption<T>.Value),
            SelectedValue = currentValue,
            Width = 138,
            Height = 24,
            IsEnabled = canEdit,
            ToolTip = rowTooltip
        };

        comboBox.SelectionChanged += (_, _) =>
        {
            if (_isRefreshing() || comboBox.SelectedItem is not InspectorOption<T> option)
                return;

            if (EqualityComparer<T>.Default.Equals(option.Value, currentValue))
                return;

            commit(option.Value);
        };

        Grid.SetColumn(comboBox, 1);
        grid.Children.Add(comboBox);
        return grid;
    }

    private UIElement CreateConditionShortcutCaptureRow(MacroNode node, bool isEnabled)
    {
        var grid = CreateInspectorRowGrid();
        var rowTooltip = TooltipNotes.ConditionKeyState;
        var canEdit = CanEditOption(isEnabled);

        grid.ToolTip = rowTooltip;
        grid.Children.Add(CreateInspectorLabel("Input", rowTooltip));

        var pill = new OptionsPillBlock
        {
            Text = ConditionInputGesture.Format(node),
            PlaceholderText = "press input",
            Width = 138,
            InputWidth = 138,
            IsEnabled = canEdit,
            ToolTip = rowTooltip
        };

        var capturedKeys = new List<int>();
        var downKeys = new HashSet<int>();
        var isCapturing = false;

        pill.MouseLeftButtonDown += (_, e) =>
        {
            if (!canEdit)
                return;

            BeginCapture();
            e.Handled = true;
        };

        pill.PreviewKeyDown += (_, e) =>
        {
            if (!isCapturing)
                return;

            e.Handled = true;

            if (e.Key == Key.Escape)
            {
                CancelCapture();
                return;
            }

            if (e.Key is Key.Back or Key.Delete)
            {
                CommitCapture(Array.Empty<int>());
                return;
            }

            if (e.Key == Key.Enter)
            {
                CommitCapture(capturedKeys);
                return;
            }

            var virtualKey = GetVirtualKeyFromKeyEvent(e);
            AddCapturedKey(virtualKey);
            UpdateCaptureText();
        };

        pill.PreviewKeyUp += (_, e) =>
        {
            if (!isCapturing)
                return;

            e.Handled = true;

            var virtualKey = GetVirtualKeyFromKeyEvent(e);
            if (virtualKey > 0)
                downKeys.Remove(virtualKey);

            if (capturedKeys.Count > 0 && downKeys.Count == 0)
                CommitCapture(capturedKeys);
        };

        pill.PreviewMouseDown += (_, e) =>
        {
            if (!isCapturing)
                return;

            var virtualKey = GetVirtualKeyFromMouseButton(e.ChangedButton);
            if (virtualKey <= 0)
                return;

            e.Handled = true;
            AddCapturedKey(virtualKey);
            UpdateCaptureText();
        };

        pill.PreviewMouseUp += (_, e) =>
        {
            if (!isCapturing)
                return;

            var virtualKey = GetVirtualKeyFromMouseButton(e.ChangedButton);
            if (virtualKey <= 0)
                return;

            e.Handled = true;
            downKeys.Remove(virtualKey);

            if (capturedKeys.Count > 0 && downKeys.Count == 0)
                CommitCapture(capturedKeys);
        };

        pill.LostKeyboardFocus += (_, _) =>
        {
            if (!isCapturing)
                return;

            if (capturedKeys.Count > 0)
                CommitCapture(capturedKeys);
            else
                CancelCapture();
        };

        Grid.SetColumn(pill, 1);
        grid.Children.Add(pill);
        return grid;

        void BeginCapture()
        {
            isCapturing = true;
            capturedKeys.Clear();
            downKeys.Clear();
            pill.Text = "press input";
            pill.TextElement.Foreground = new SolidColorBrush(Color.FromRgb(125, 211, 252));
            pill.Focus();
            Mouse.Capture(pill);
        }

        void AddCapturedKey(int virtualKey)
        {
            virtualKey = ShortcutGesture.NormalizeVirtualKey(virtualKey);
            if (virtualKey <= 0)
                return;

            downKeys.Add(virtualKey);
            if (!capturedKeys.Contains(virtualKey) &&
                capturedKeys.Count < ConditionInputGesture.MaxKeyCount)
            {
                capturedKeys.Add(virtualKey);
            }
        }

        void UpdateCaptureText()
        {
            pill.Text = capturedKeys.Count == 0
                ? "press input"
                : ConditionInputGesture.Format(ConditionInputGesture.Serialize(capturedKeys));
        }

        void CommitCapture(IEnumerable<int> virtualKeys)
        {
            var serialized = ConditionInputGesture.Serialize(virtualKeys);
            var keys = ConditionInputGesture.Parse(serialized);

            EndCapture();

            _commitNodeChange(() =>
            {
                node.ConditionShortcutKeys = serialized;
                node.ConditionVirtualKey = keys.FirstOrDefault();
                node.ConditionKeyName = keys.Length == 0 ? "" : ShortcutGesture.Format(keys);
            });

            Keyboard.ClearFocus();
        }

        void CancelCapture()
        {
            EndCapture();
            pill.Text = ConditionInputGesture.Format(node);
            Keyboard.ClearFocus();
        }

        void EndCapture()
        {
            isCapturing = false;
            capturedKeys.Clear();
            downKeys.Clear();
            pill.TextElement.ClearValue(TextBlock.ForegroundProperty);
            if (ReferenceEquals(Mouse.Captured, pill))
                Mouse.Capture(null);
        }
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

    private UIElement CreatePickPixelButton(MacroNode node, bool isEnabled)
    {
        var button = new Button
        {
            Content = "Pick Pixel",
            Height = 22,
            Margin = new Thickness(0, 2, 0, 4),
            IsEnabled = CanEditOption(isEnabled),
            ToolTip = TooltipNotes.ConditionPixelPick
        };

        button.Click += async (_, _) =>
        {
            _saveUndoSnapshot();
            await _pickConditionPixelAsync(node);
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

    private static void NormalizeConditionDefaults(MacroNode node)
    {
        if (node.ConditionType == MacroConditionType.KeyState && node.ConditionVirtualKey <= 0)
        {
            node.ConditionVirtualKey = NativeMethods.VK_SHIFT;
            node.ConditionKeyName = "Shift";
            node.ConditionShortcutKeys = ConditionInputGesture.Serialize(new[] { NativeMethods.VK_SHIFT });
        }

        node.ConditionPixelX = Math.Max(0, node.ConditionPixelX);
        node.ConditionPixelY = Math.Max(0, node.ConditionPixelY);
        node.ConditionPixelRed = Math.Clamp(node.ConditionPixelRed, 0, 255);
        node.ConditionPixelGreen = Math.Clamp(node.ConditionPixelGreen, 0, 255);
        node.ConditionPixelBlue = Math.Clamp(node.ConditionPixelBlue, 0, 255);
        node.ConditionPixelTolerance = Math.Clamp(node.ConditionPixelTolerance, 0, 255);
        node.ConditionChancePercent = Math.Clamp(node.ConditionChancePercent, 0, 100);
        node.ConditionLoopInterval = Math.Max(1, node.ConditionLoopInterval);
    }

    private static bool RequiresLoopInterval(MacroConditionLoopMode mode) =>
        mode is MacroConditionLoopMode.EveryNLoops or MacroConditionLoopMode.EveryNRepeats;

    private static int GetVirtualKeyFromKeyEvent(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        return ShortcutGesture.NormalizeVirtualKey(KeyInterop.VirtualKeyFromKey(key));
    }

    private static int GetVirtualKeyFromMouseButton(MouseButton button)
    {
        return button switch
        {
            MouseButton.XButton1 => NativeMethods.VK_XBUTTON1,
            MouseButton.XButton2 => NativeMethods.VK_XBUTTON2,
            _ => 0
        };
    }

    private bool CanEditOption(bool optionEnabled)
    {
        return _canEdit() && optionEnabled;
    }

    private sealed record InspectorOption<T>(string Label, T Value)
    {
        public override string ToString() => Label;
    }

}
