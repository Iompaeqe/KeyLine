using System.IO;
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
using Microsoft.Win32;

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

    private static readonly IReadOnlyList<InspectorOption<SystemLaunchKind>> SystemLaunchKindOptions =
    [
        new("Application", SystemLaunchKind.Application),
        new("File", SystemLaunchKind.File),
        new("Folder", SystemLaunchKind.Folder),
        new("URL", SystemLaunchKind.Url)
    ];

    private static readonly IReadOnlyList<InspectorOption<SystemVolumeAction>> SystemVolumeActionOptions =
    [
        new("Volume Up", SystemVolumeAction.VolumeUp),
        new("Volume Down", SystemVolumeAction.VolumeDown),
        new("Mute Toggle", SystemVolumeAction.MuteToggle),
        new("Mute", SystemVolumeAction.Mute),
        new("Unmute", SystemVolumeAction.Unmute),
        new("Set Volume %", SystemVolumeAction.SetVolumePercent)
    ];

    private readonly TimelineSelectionState _selection;
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
        _canEdit = canEdit;
        _isRefreshing = isRefreshing;
        _saveUndoSnapshot = saveDocumentUndoSnapshot;
        _commitNodeChange = commitNodeChange;
        _commitNodeValueChange = commitNodeValueChange;
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
                    () => node.DelayMs,
                    value => _commitNodeValueChange(() => node.DelayMs = value),
                    TooltipNotes.DelayNodeValue,
                    policy.CanEditDelay));
                break;

            case MacroNodeType.RandomDelay:
                section.Children.Add(CreateDelayRow(
                    "Min",
                    () => node.RandomDelayMinMs,
                    value => _commitNodeValueChange(() => node.RandomDelayMinMs = value),
                    TooltipNotes.RandomDelayMinimum,
                    policy.CanEditRandomDelay,
                    () => NormalizeRandomDelayAfterEdit(node)));

                section.Children.Add(CreateDelayRow(
                    "Max",
                    () => node.RandomDelayMaxMs,
                    value => _commitNodeValueChange(() => node.RandomDelayMaxMs = value),
                    TooltipNotes.RandomDelayMaximum,
                    policy.CanEditRandomDelay,
                    () => NormalizeRandomDelayAfterEdit(node)));
                break;

            case MacroNodeType.Text:
                section.Children.Add(CreateTextEditRow(node, policy.CanEditText));
                break;

            case MacroNodeType.RepeatStart:
                section.Children.Add(CreateNumberRow(
                    "Count",
                    Math.Max(0, node.RepeatCount),
                    value => _commitNodeValueChange(() => node.RepeatCount = Math.Max(1, value)),
                    min: 1,
                    tooltip: TooltipNotes.RepeatCount,
                    isEnabled: policy.CanEditRepeatCount));
                break;

            case MacroNodeType.RepeatEnd:
                section.Children.Add(CreateReadonlyRow("Block", GetBlockLabel(node)));
                break;

            case MacroNodeType.ConditionStart:
                section.Children.Add(CreateReadonlyRow("Runs when", NodeDisplayFormatter.GetConditionSummary(node)));
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
                    value => _commitNodeValueChange(() => node.MouseX = value),
                    isEnabled: policy.CanEditMousePosition));

                section.Children.Add(CreateNumberRow(
                    "Y",
                    node.MouseY,
                    value => _commitNodeValueChange(() => node.MouseY = value),
                    isEnabled: policy.CanEditMousePosition));

                section.Children.Add(CreatePickPointButton(node, policy.CanPickMousePosition));
                break;

            case MacroNodeType.MouseDown:
            case MacroNodeType.MouseUp:
                section.Children.Add(CreateNumberRow(
                    "Button",
                    Math.Clamp(node.MouseButton <= 0 ? 1 : node.MouseButton, 1, 5),
                    value => _commitNodeValueChange(() => node.MouseButton = Math.Clamp(value, 1, 5)),
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

            case MacroNodeType.SystemOpenLaunch:
                section.Children.Add(CreateReadonlyRow("Summary", NodeDisplayFormatter.GetSystemNodeTooltip(node)));
                AddSystemLaunchConfigurationRows(section, node, policy.CanEditSystemLaunch);
                break;

            case MacroNodeType.SystemVolumeControl:
                section.Children.Add(CreateReadonlyRow("Summary", NodeDisplayFormatter.GetSystemNodeTooltip(node)));
                AddSystemVolumeConfigurationRows(section, node, policy.CanEditVolumeControl);
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
            value => _commitNodeValueChange(() => repeatStart.RepeatCount = Math.Max(1, value)),
            min: 1,
            tooltip: TooltipNotes.RepeatCount,
            isEnabled: true));
        section.Children.Add(CreateReadonlyRow("Inside", Math.Max(0, blockNodeCount - 2).ToString()));

        return section;
    }

    private UIElement CreateConditionBlockInspector(MacroNode conditionStart, int blockNodeCount)
    {
        var section = CreateSection();

        section.Children.Add(CreateConditionOverviewRow(conditionStart, blockNodeCount));
        AddConditionConfigurationRows(section, conditionStart, isEnabled: true);

        return section;
    }

    private UIElement CreateConditionOverviewRow(MacroNode conditionStart, int blockNodeCount)
    {
        var grid = CreateInspectorRowGrid();
        grid.MinHeight = 28;
        grid.Margin = new Thickness(0, 0, 0, 6);
        grid.ToolTip = NodeDisplayFormatter.GetConditionSummary(conditionStart);

        grid.Children.Add(new TextBlock
        {
            Text = "Condition",
            Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        });

        var detailHost = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(detailHost, 1);

        detailHost.Children.Add(new TextBlock
        {
            Text = GetConditionTypeLabel(conditionStart.ConditionType),
            Foreground = new SolidColorBrush(Color.FromRgb(125, 211, 252)),
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Right
        });

        detailHost.Children.Add(new TextBlock
        {
            Text = $"{Math.Max(0, blockNodeCount - 2)} inside",
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Right
        });

        grid.Children.Add(detailHost);
        return grid;
    }

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
                section.Children.Add(CreateMultiValueRow(
                    "Position",
                    TooltipNotes.ConditionPixelPosition,
                    isEnabled,
                    new MultiValueEntryField("X", Math.Max(0, node.ConditionPixelX), value => _commitNodeChange(() => node.ConditionPixelX = Math.Max(0, value))),
                    new MultiValueEntryField("Y", Math.Max(0, node.ConditionPixelY), value => _commitNodeChange(() => node.ConditionPixelY = Math.Max(0, value)))));

                section.Children.Add(CreateMultiValueRow(
                    "Color",
                    TooltipNotes.ConditionPixelColor,
                    isEnabled,
                    new MultiValueEntryField("R", Math.Clamp(node.ConditionPixelRed, 0, 255), value => _commitNodeChange(() => node.ConditionPixelRed = Math.Clamp(value, 0, 255)), Max: 255),
                    new MultiValueEntryField("G", Math.Clamp(node.ConditionPixelGreen, 0, 255), value => _commitNodeChange(() => node.ConditionPixelGreen = Math.Clamp(value, 0, 255)), Max: 255),
                    new MultiValueEntryField("B", Math.Clamp(node.ConditionPixelBlue, 0, 255), value => _commitNodeChange(() => node.ConditionPixelBlue = Math.Clamp(value, 0, 255)), Max: 255)));

                section.Children.Add(CreateNumberRow(
                    "Tolerance",
                    Math.Clamp(node.ConditionPixelTolerance, 0, 255),
                    value => _commitNodeValueChange(() => node.ConditionPixelTolerance = Math.Clamp(value, 0, 255)),
                    max: 255,
                    tooltip: TooltipNotes.ConditionPixelTolerance,
                    isEnabled: isEnabled));
                section.Children.Add(CreatePickPixelButton(node, isEnabled));
                break;

            case MacroConditionType.RandomChance:
                section.Children.Add(CreateNumberRow(
                    "Chance",
                    Math.Clamp(node.ConditionChancePercent, 0, 100),
                    value => _commitNodeValueChange(() => node.ConditionChancePercent = Math.Clamp(value, 0, 100)),
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
                        value => _commitNodeValueChange(() => node.ConditionLoopInterval = Math.Max(1, value)),
                        suffix: node.ConditionLoopMode == MacroConditionLoopMode.EveryNRepeats ? "repeats" : "loops",
                        min: 1,
                        tooltip: TooltipNotes.ConditionLoopInterval,
                        isEnabled: isEnabled));
                }
                break;
        }
    }

    private void AddSystemLaunchConfigurationRows(StackPanel section, MacroNode node, bool isEnabled)
    {
        section.Children.Add(CreateOptionRow(
            "Kind",
            SystemLaunchKindOptions,
            node.SystemLaunchKind,
            value => _commitNodeChange(() =>
            {
                node.SystemLaunchKind = value;
                node.SystemLaunchTarget = node.SystemLaunchTarget?.Trim() ?? "";
            }),
            tooltip: "Choose what this node opens.",
            isEnabled: isEnabled));

        section.Children.Add(CreateLaunchTargetRow(node, isEnabled));
    }

    private void AddSystemVolumeConfigurationRows(StackPanel section, MacroNode node, bool isEnabled)
    {
        section.Children.Add(CreateOptionRow(
            "Action",
            SystemVolumeActionOptions,
            node.SystemVolumeAction,
            value => _commitNodeChange(() =>
            {
                node.SystemVolumeAction = value;
                node.SystemVolumePercent = Math.Clamp(node.SystemVolumePercent, 0, 100);
            }),
            tooltip: "Choose the volume operation to run.",
            isEnabled: isEnabled));

        if (node.SystemVolumeAction == SystemVolumeAction.SetVolumePercent)
        {
            section.Children.Add(CreateNumberRow(
                "Volume",
                Math.Clamp(node.SystemVolumePercent, 0, 100),
                value => _commitNodeValueChange(() => node.SystemVolumePercent = Math.Clamp(value, 0, 100)),
                suffix: "%",
                max: 100,
                tooltip: "Set the system output volume percentage.",
                isEnabled: isEnabled));
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
            Padding = new Thickness(-5,0,-5,0),
            Width = 118,
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
            HorizontalAlignment = HorizontalAlignment.Right,
            IsEnabled = canEdit,
            ToolTip = rowTooltip
        };
        StyleConditionInputPill(pill);

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

            if (IsShortcutCaptureStopMouseButton(e.ChangedButton))
            {
                e.Handled = true;
                FinishCaptureWithoutMouseButton();
                return;
            }

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
            pill.FocusInput();
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

        void FinishCaptureWithoutMouseButton()
        {
            if (capturedKeys.Count > 0)
                CommitCapture(capturedKeys);
            else
                CancelCapture();
        }

        void EndCapture()
        {
            isCapturing = false;
            capturedKeys.Clear();
            downKeys.Clear();
            pill.TextElement.Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240));
            if (ReferenceEquals(Mouse.Captured, pill))
                Mouse.Capture(null);
        }
    }

    private UIElement CreateMultiValueRow(
        string label,
        string tooltip,
        bool isEnabled,
        params MultiValueEntryField[] fields)
    {
        var grid = CreateInspectorRowGrid();

        grid.ToolTip = tooltip;
        grid.Children.Add(CreateInspectorLabel(label, tooltip));

        var entry = new MultiValueEntryBlock
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            ToolTip = tooltip,
            IsEnabled = CanEditOption(isEnabled)
        };

        entry.SetFields(fields, _isRefreshing, CanEditOption(isEnabled), tooltip);

        Grid.SetColumn(entry, 1);
        grid.Children.Add(entry);
        return grid;
    }

    private UIElement CreateLaunchTargetRow(MacroNode node, bool isEnabled)
    {
        var canEdit = CanEditOption(isEnabled);
        var panel = new StackPanel
        {
            Margin = new Thickness(0, 4, 0, 0),
            ToolTip = "Target path, executable, folder, or URL."
        };

        panel.Children.Add(new TextBlock
        {
            Text = "Target",
            Foreground = new SolidColorBrush(Color.FromRgb(142, 160, 182)),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 5)
        });

        var textBox = new TextBox
        {
            Text = node.SystemLaunchTarget,
            Height = 24,
            IsEnabled = canEdit,
            ToolTip = GetLaunchTargetTooltip(node.SystemLaunchKind)
        };

        void CommitTargetText()
        {
            if (_isRefreshing())
                return;

            var target = textBox.Text.Trim();
            if (!string.Equals(target, node.SystemLaunchTarget, StringComparison.Ordinal))
                _commitNodeChange(() => node.SystemLaunchTarget = target);
        }

        textBox.LostFocus += (_, _) => CommitTargetText();
        textBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;

            CommitTargetText();
            Keyboard.ClearFocus();
            e.Handled = true;
        };

        panel.Children.Add(textBox);

        if (node.SystemLaunchKind != SystemLaunchKind.Url)
        {
            var browseButton = new Button
            {
                Content = "Browse",
                Height = 22,
                Margin = new Thickness(0, 5, 0, 0),
                IsEnabled = canEdit,
                ToolTip = "Pick a local application, file, or folder."
            };

            browseButton.Click += (_, _) =>
            {
                var target = BrowseSystemLaunchTarget(node);
                if (string.IsNullOrWhiteSpace(target))
                    return;

                _commitNodeChange(() => node.SystemLaunchTarget = target);
            };

            panel.Children.Add(browseButton);
        }

        return panel;
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

        var committedValue = value;
        var isCommittingText = false;

        void CommitText()
        {
            if (isCommittingText)
                return;

            isCommittingText = true;
            try
            {
                committedValue = InspectorCommitService.CommitNumberText(
                    textBox,
                    committedValue,
                    commit,
                    min,
                    max,
                    _isRefreshing());
            }
            finally
            {
                isCommittingText = false;
            }
        }

        textBox.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
        textBox.GotKeyboardFocus += (_, _) => textBox.SelectAll();
        textBox.LostFocus += (_, _) => CommitText();
        textBox.TextChanged += (_, _) => CommitText();
        textBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;

            CommitText();
            Keyboard.ClearFocus();
            e.Handled = true;
        };

        grid.Children.Add(host);
        return grid;
    }

    private UIElement CreateDelayRow(
        string label,
        Func<int> currentValue,
        Action<int> commit,
        string tooltip,
        bool isEnabled = true,
        Action? normalizeAfterEdit = null)
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

        entry.SetDisplay(DelayFormatter.ClampMilliseconds(currentValue()));
        entry.TextBox.IsEnabled = canEdit;

        Grid.SetColumn(entry, 1);

        var textBox = entry.TextBox;
        var isEditing = false;
        var isSettingText = false;

        void SetEditText(int editValue)
        {
            isSettingText = true;
            try
            {
                textBox.Text = DelayFormatter.ClampMilliseconds(editValue).ToString();
                entry.UnitText.Text = "ms";
            }
            finally
            {
                isSettingText = false;
            }
        }

        void SetDisplayText(int displayValue)
        {
            isSettingText = true;
            try
            {
                entry.SetDisplay(DelayFormatter.ClampMilliseconds(displayValue));
            }
            finally
            {
                isSettingText = false;
            }
        }

        textBox.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
        textBox.GotKeyboardFocus += (_, _) =>
        {
            isEditing = true;
            SetEditText(currentValue());
            textBox.SelectAll();
        };
        textBox.LostFocus += (_, _) =>
        {
            InspectorCommitService.CommitDelayText(entry, currentValue(), commit);
            isEditing = false;
            normalizeAfterEdit?.Invoke();
            SetDisplayText(currentValue());
        };
        textBox.TextChanged += (_, _) =>
        {
            if (!isEditing || isSettingText || _isRefreshing())
                return;

            InspectorCommitService.CommitDelayText(entry, currentValue(), commit);
        };
        textBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;

            InspectorCommitService.CommitDelayText(entry, currentValue(), commit);
            isEditing = false;
            normalizeAfterEdit?.Invoke();
            SetDisplayText(currentValue());
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

    private static void StyleConditionInputPill(OptionsPillBlock pill)
    {
        pill.BorderElement.Margin = new Thickness(0);
        pill.BorderElement.Background = new SolidColorBrush(Color.FromRgb(21, 34, 53));
        pill.BorderElement.BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85));
        pill.BorderElement.BorderThickness = new Thickness(1);
        pill.BorderElement.CornerRadius = new CornerRadius(6);

        pill.TextElement.Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240));
        pill.TextElement.FontSize = 11;
        pill.TextElement.FontWeight = FontWeights.SemiBold;
        pill.TextElement.Padding = new Thickness(7, 1, 7, 2);
        pill.TextElement.TextAlignment = TextAlignment.Right;
        pill.TextElement.TextTrimming = TextTrimming.CharacterEllipsis;
        pill.TextElement.VerticalAlignment = VerticalAlignment.Center;
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

    private static string GetConditionTypeLabel(MacroConditionType type) =>
        type switch
        {
            MacroConditionType.KeyState => "Key held",
            MacroConditionType.PixelColor => "Pixel color",
            MacroConditionType.RandomChance => "Random chance",
            MacroConditionType.LoopContext => "Loop rule",
            _ => "Condition"
        };

    private static string GetLaunchTargetTooltip(SystemLaunchKind kind) =>
        kind switch
        {
            SystemLaunchKind.Application => "Executable path or application command.",
            SystemLaunchKind.File => "File path to open with the default application.",
            SystemLaunchKind.Folder => "Folder path to open in Explorer.",
            SystemLaunchKind.Url => "URL to open in the default browser.",
            _ => "Target to open."
        };

    private static string? BrowseSystemLaunchTarget(MacroNode node)
    {
        if (node.SystemLaunchKind == SystemLaunchKind.Folder)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select folder to open",
                UseDescriptionForTitle = true,
                SelectedPath = Directory.Exists(node.SystemLaunchTarget) ? node.SystemLaunchTarget : ""
            };

            return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK
                ? dialog.SelectedPath
                : null;
        }

        var fileDialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Multiselect = false,
            FileName = File.Exists(node.SystemLaunchTarget) ? node.SystemLaunchTarget : ""
        };

        fileDialog.Filter = node.SystemLaunchKind == SystemLaunchKind.Application
            ? "Applications (*.exe)|*.exe|All files (*.*)|*.*"
            : "All files (*.*)|*.*";

        return fileDialog.ShowDialog() == true
            ? fileDialog.FileName
            : null;
    }

    private static void NormalizeRandomDelay(MacroNode node)
    {
        if (node.RandomDelayMaxMs < node.RandomDelayMinMs)
            (node.RandomDelayMinMs, node.RandomDelayMaxMs) = (node.RandomDelayMaxMs, node.RandomDelayMinMs);
    }

    private void NormalizeRandomDelayAfterEdit(MacroNode node)
    {
        if (node.RandomDelayMaxMs >= node.RandomDelayMinMs)
            return;

        _commitNodeChange(() => NormalizeRandomDelay(node));
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

    private static bool IsShortcutCaptureStopMouseButton(MouseButton button) =>
        button is MouseButton.Left or MouseButton.Right or MouseButton.Middle;

    private bool CanEditOption(bool optionEnabled)
    {
        return _canEdit() && optionEnabled;
    }

    private sealed record InspectorOption<T>(string Label, T Value)
    {
        public override string ToString() => Label;
    }

}
