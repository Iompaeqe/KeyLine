using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MacroSpammer.Domain;
using MacroSpammer.UI;
using MacroSpammer.UI.Common.EntryBlocks;
using MacroSpammer.UI.Inspector;
using MacroSpammer.UI.Timeline;

namespace MacroSpammer;

public partial class MainWindow
{
    private bool _isRefreshingInspector;
    private bool _isCommittingTimelineName;
    private MacroTimeline? _editingTimelineName;
    private bool _isTimelineInspectorCollapsed;
    private bool _isNodeInspectorCollapsed;

    private void InitializeInspectorWindowEvents(InspectorWindow inspectorWindow)
    {
        inspectorWindow.TimelineHeaderClicked += () =>
        {
            _isTimelineInspectorCollapsed = !_isTimelineInspectorCollapsed;
            RefreshInspector();
        };
        inspectorWindow.NodeHeaderClicked += () =>
        {
            _isNodeInspectorCollapsed = !_isNodeInspectorCollapsed;
            RefreshInspector();
        };
        inspectorWindow.TimelineNameCommitted += name =>
        {
            var timeline = _editingTimelineName ?? _selection.SelectedTimeline ?? _document.ActiveTimeline;
            CommitTimelineName(timeline, name);
        };
        inspectorWindow.TimelineNameEditCancelled += () =>
        {
            _editingTimelineName = null;
            RefreshInspector();
        };
        inspectorWindow.TimelineLoopsCommitted += value =>
        {
            var timeline = _selection.SelectedTimeline ?? _document.ActiveTimeline;
            CommitTimelineChange(timeline, () => timeline.LoopCount = value);
            RefreshInspector();
        };
        inspectorWindow.TimelineLoopDelayCommitted += value =>
        {
            var timeline = _selection.SelectedTimeline ?? _document.ActiveTimeline;
            CommitTimelineChange(timeline, () => timeline.BaseDelayMs = value);
            RefreshInspector();
        };
        inspectorWindow.TimelineInputTypeChangeRequested += () =>
        {
            var timeline = _selection.SelectedTimeline ?? _document.ActiveTimeline;
            CommitTimelineChange(timeline, () => timeline.UseTextInputMode = !timeline.UseTextInputMode);
            RefreshInspector();
        };
        inspectorWindow.TimelineStandardDelayChanged += value =>
        {
            var timeline = _selection.SelectedTimeline ?? _document.ActiveTimeline;
            CommitTimelineChange(timeline, () =>
            {
                timeline.UseStandardDelay = value;
                if (!timeline.UseStandardDelay)
                    timeline.ShowKeyUpDown = true;
            });
            RefreshInspector();
        };
        inspectorWindow.TimelineStandardDelayCommitted += value =>
        {
            var timeline = _selection.SelectedTimeline ?? _document.ActiveTimeline;
            CommitTimelineChange(timeline, () => timeline.StandardDelayMs = value);
            RefreshInspector();
        };
        inspectorWindow.TimelineShowKeyUpDownChanged += value =>
        {
            var timeline = _selection.SelectedTimeline ?? _document.ActiveTimeline;
            CommitTimelineChange(timeline, () => timeline.ShowKeyUpDown = value);
            RefreshInspector();
        };
    }

    private void RefreshInspector()
    {
        if (_inspectorWindow == null)
            return;

        _isRefreshingInspector = true;
        try
        {
            var timeline = _selection.SelectedTimeline ?? _document.ActiveTimeline;
            _inspectorWindow.SetTimelineState(
                timeline.Name,
                ReferenceEquals(_editingTimelineName, timeline),
                _isTimelineInspectorCollapsed,
                _isTimelineEditingEnabled,
                Math.Max(0, timeline.LoopCount),
                Math.Max(0, timeline.BaseDelayMs),
                timeline.UseTextInputMode,
                timeline.UseStandardDelay,
                Math.Max(0, timeline.StandardDelayMs),
                timeline.ShowKeyUpDown);

            var nodeContent = BuildNodeInspectorContent(timeline);
            _inspectorWindow.SetNodeContent(nodeContent, nodeContent != null, _isNodeInspectorCollapsed);
        }
        finally
        {
            _isRefreshingInspector = false;
        }
    }

    private UIElement? BuildNodeInspectorContent(MacroTimeline timeline)
    {
        if (_selection.HasMultipleStepSelection)
            return CreateReadonlySectionContent(("Selected", _selection.SelectedSteps.Count.ToString()));

        if (_selection.HasStepSelection && _selection.SelectedStep != null &&
            (_selection.SelectedStep.IsSyntheticDisplayNode || HasEditableStepInspector(_selection.SelectedStep)))
            return CreateStepInspector(timeline, _selection.SelectedStep);

        return null;
    }

    private UIElement CreateStepInspector(MacroTimeline timeline, MacroNode node)
    {
        var section = CreateSection();

        section.Children.Add(CreateReadonlyRow("Type", GetStepTypeText(node)));

        if (node.IsSyntheticDisplayNode)
        {
            section.Children.Add(CreateReadonlyRow("Value", NodeDisplayFormatter.GetKeyText(node)));
            section.Children.Add(CreateReadonlyRow("Parts", node.SourceNodes.Count.ToString()));
            return section;
        }

        switch (node.Type)
        {
            case MacroNodeType.Delay:
                section.Children.Add(CreateDelayRow(
                    "Delay",
                    node.DelayMs,
                    value => CommitStepChange(() => node.DelayMs = value),
                    TooltipNotes.DelayStepValue));
                break;

            case MacroNodeType.RandomDelay:
                section.Children.Add(CreateDelayRow(
                    "Min",
                    node.RandomDelayMinMs,
                    value => CommitStepChange(() =>
                    {
                        node.RandomDelayMinMs = value;
                        NormalizeRandomDelay(node);
                    }),
                    TooltipNotes.RandomDelayMinimum));
                section.Children.Add(CreateDelayRow(
                    "Max",
                    node.RandomDelayMaxMs,
                    value => CommitStepChange(() =>
                    {
                        node.RandomDelayMaxMs = value;
                        NormalizeRandomDelay(node);
                    }),
                    TooltipNotes.RandomDelayMaximum));
                break;

            case MacroNodeType.Text:
                section.Children.Add(CreateTextEditRow(timeline, node));
                break;

            case MacroNodeType.CursorMove:
            case MacroNodeType.BackgroundMouseDown:
            case MacroNodeType.BackgroundMouseUp:
            case MacroNodeType.BackgroundMouseClick:
                section.Children.Add(CreateNumberRow(
                    "X",
                    node.MouseX,
                    value => CommitStepChange(() => node.MouseX = value)));
                section.Children.Add(CreateNumberRow(
                    "Y",
                    node.MouseY,
                    value => CommitStepChange(() => node.MouseY = value)));
                section.Children.Add(CreatePickPointButton(node));
                break;

            case MacroNodeType.MouseDown:
            case MacroNodeType.MouseUp:
                section.Children.Add(CreateNumberRow(
                    "Button",
                    Math.Clamp(node.MouseButton <= 0 ? 1 : node.MouseButton, 1, 5),
                    value => CommitStepChange(() => node.MouseButton = Math.Clamp(value, 1, 5)),
                    "",
                    1,
                    5));
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

    private StackPanel CreateSection()
    {
        return new StackPanel();
    }

    private UIElement WrapSection(
        string title,
        StackPanel section,
        Func<bool> isCollapsed,
        Action<bool> setCollapsed)
    {
        var collapsed = isCollapsed();
        var host = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 7)
        };

        section.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        host.Children.Add(CreateSectionHeader(title, collapsed, () =>
        {
            setCollapsed(!isCollapsed());
            RefreshInspector();
        }));
        host.Children.Add(section);
        host.Children.Add(new Border
        {
            Height = 1,
            Margin = new Thickness(0, collapsed ? 2 : 4, 0, 0),
            Background = new SolidColorBrush(Color.FromRgb(38, 50, 68)),
            Opacity = 0.8
        });

        return host;
    }

    private UIElement CreateReadonlySectionContent(params (string Label, string Value)[] rows)
    {
        var section = CreateSection();
        foreach (var row in rows)
            section.Children.Add(CreateReadonlyRow(row.Label, row.Value));

        return section;
    }

    private UIElement CreateSectionHeader(string title, bool isCollapsed, Action toggle)
    {
        var header = new Grid
        {
            MinHeight = 18,
            Margin = new Thickness(0, 0, 0, isCollapsed ? 0 : 4),
            Cursor = Cursors.Hand,
            Background = Brushes.Transparent
        };

        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var titleBlock = new TextBlock
        {
            Text = title,
            Style = TryFindResource("Cap") as Style,
            Margin = new Thickness(0)
        };
        var arrow = new TextBlock
        {
            Text = isCollapsed ? "▸" : "▾",
            Foreground = new SolidColorBrush(Color.FromRgb(142, 160, 182)),
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(5, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        Grid.SetColumn(titleBlock, 0);
        Grid.SetColumn(arrow, 1);
        header.Children.Add(titleBlock);
        header.Children.Add(arrow);

        header.MouseLeftButtonDown += (_, e) =>
        {
            toggle();
            e.Handled = true;
        };

        return header;
    }

    private UIElement CreateGroupLabel(string text)
    {
        return new TextBlock
        {
            Text = text.ToUpperInvariant(),
            Foreground = new SolidColorBrush(Color.FromRgb(94, 113, 137)),
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 4, 0, 3)
        };
    }

    private static UIElement CreateIndentedRow(UIElement row)
    {
        return new Border
        {
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(7, 0, 0, 0),
            BorderThickness = new Thickness(1, 0, 0, 0),
            BorderBrush = new SolidColorBrush(Color.FromRgb(38, 50, 68)),
            Child = row
        };
    }

    private UIElement CreateTimelineNameRow(MacroTimeline timeline)
    {
        var grid = CreateInspectorRowGrid();
        grid.ToolTip = TooltipNotes.TimelineName;
        grid.Children.Add(CreateInspectorLabel("Name", TooltipNotes.TimelineName));

        var host = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Right
        };
        host.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        host.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(host, 1);

        var isEditing = ReferenceEquals(_editingTimelineName, timeline);

        if (isEditing)
        {
            var textBox = new TextBox
            {
                Text = timeline.Name,
                Width = 126,
                Height = 20,
                Padding = new Thickness(5, 0, 5, 0),
                IsEnabled = _isTimelineEditingEnabled,
                ToolTip = TooltipNotes.EditTimelineName
            };

            textBox.Loaded += (_, _) =>
            {
                textBox.Focus();
                textBox.SelectAll();
            };
            textBox.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    CommitTimelineName(timeline, textBox.Text);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    _editingTimelineName = null;
                    RefreshInspector();
                    e.Handled = true;
                }
            };
            textBox.LostFocus += (_, _) =>
            {
                    if (ReferenceEquals(_editingTimelineName, timeline))
                        CommitTimelineName(timeline, textBox.Text);
            };

            Grid.SetColumn(textBox, 0);
            host.Children.Add(textBox);
        }
        else
        {
            var nameBlock = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(timeline.Name) ? "-" : timeline.Name,
                MaxWidth = 90,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = timeline.Name
            };
            var editIcon = CreateEditIcon(() =>
            {
                if (!_isTimelineEditingEnabled)
                    return;

                _editingTimelineName = timeline;
                RefreshInspector();
            });

            Grid.SetColumn(nameBlock, 0);
            Grid.SetColumn(editIcon, 1);
            host.Children.Add(nameBlock);
            host.Children.Add(editIcon);
        }

        grid.Children.Add(host);
        return grid;
    }

    private TextBlock CreateEditIcon(Action click)
    {
        var icon = new TextBlock
        {
            Text = "✎",
            Width = 16,
            Height = 20,
            Margin = new Thickness(5, 0, 0, 0),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(142, 160, 182)),
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Cursor = Cursors.Hand,
            Opacity = 0.65,
            ToolTip = TooltipNotes.RenameTimeline
        };

        icon.MouseEnter += (_, _) => icon.Opacity = 1;
        icon.MouseLeave += (_, _) => icon.Opacity = 0.65;
        icon.MouseLeftButtonDown += (_, e) =>
        {
            click();
            e.Handled = true;
        };

        return icon;
    }

    private UIElement CreateReadonlyRow(string label, string value)
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
        string? tooltip = null)
    {
        var grid = CreateInspectorRowGrid();
        var rowTooltip = tooltip ?? $"{label} value.";
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
            IsEnabled = _isTimelineEditingEnabled,
            ToolTip = rowTooltip
        };
        var textBox = numberEntry.TextBox;
        textBox.Text = value.ToString();
        textBox.IsEnabled = _isTimelineEditingEnabled;

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
        textBox.LostFocus += (_, _) => CommitNumberText(textBox, value, commit, min, max);
        textBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;

            CommitNumberText(textBox, value, commit, min, max);
            Keyboard.ClearFocus();
            e.Handled = true;
        };

        grid.Children.Add(host);
        return grid;
    }

    private UIElement CreateDelayRow(string label, int value, Action<int> commit, string tooltip)
    {
        var grid = CreateInspectorRowGrid();
        grid.ToolTip = tooltip;
        grid.Children.Add(CreateInspectorLabel(label, tooltip));

        var entry = new TimeEntryBlock
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            ToolTip = $"{tooltip} Click to edit in milliseconds."
        };
        entry.SetDisplay(value);
        entry.TextBox.IsEnabled = _isTimelineEditingEnabled;
        Grid.SetColumn(entry, 1);

        var textBox = entry.TextBox;
        textBox.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
        textBox.GotKeyboardFocus += (_, _) =>
        {
            textBox.Text = value.ToString();
            entry.UnitText.Text = "ms";
            textBox.SelectAll();
        };
        textBox.LostFocus += (_, _) => CommitDelayText(entry, value, commit);
        textBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;

            CommitDelayText(entry, value, commit);
            Keyboard.ClearFocus();
            e.Handled = true;
        };

        grid.Children.Add(entry);
        return grid;
    }

    private static void CommitDelayText(TimeEntryBlock entry, int originalValue, Action<int> commit)
    {
        var textBox = entry.TextBox;
        if (!int.TryParse(textBox.Text, out var value))
            value = 0;

        value = Math.Max(0, value);
        if (value != originalValue)
        {
            commit(value);
            return;
        }

        entry.SetDisplay(value);
    }

    private UIElement CreateCheckRow(string label, bool value, Action<bool> commit, bool isEnabled = true, string? tooltip = null)
    {
        var grid = CreateInspectorRowGrid();
        var rowTooltip = tooltip ?? label;
        grid.ToolTip = rowTooltip;
        grid.Children.Add(CreateInspectorLabel(label, rowTooltip));

        var checkBox = new CheckBox
        {
            IsChecked = value,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = _isTimelineEditingEnabled && isEnabled,
            ToolTip = rowTooltip
        };
        Grid.SetColumn(checkBox, 1);

        checkBox.Checked += (_, _) =>
        {
            if (!_isRefreshingInspector)
                commit(true);
        };
        checkBox.Unchecked += (_, _) =>
        {
            if (!_isRefreshingInspector)
                commit(false);
        };

        grid.Children.Add(checkBox);
        return grid;
    }

    private UIElement CreateInputTypeRow(MacroTimeline timeline)
    {
        const string tooltip = TooltipNotes.TimelineInputType;

        var grid = CreateInspectorRowGrid();
        grid.ToolTip = tooltip;
        grid.Children.Add(CreateInspectorLabel("Input type", tooltip));

        var pager = new PagerEntryBlock
        {
            Text = timeline.UseTextInputMode ? "Text" : "Key",
            IsEnabled = _isTimelineEditingEnabled,
            HorizontalAlignment = HorizontalAlignment.Right,
            ToolTip = tooltip
        };
        pager.PageRequested += (_, _) => ToggleInputType(timeline);
        Grid.SetColumn(pager, 1);

        grid.Children.Add(pager);
        return grid;
    }

    private void ToggleInputType(MacroTimeline timeline)
    {
        if (_isRefreshingInspector)
            return;

        CommitTimelineChange(timeline, () => timeline.UseTextInputMode = !timeline.UseTextInputMode);
        RefreshInspector();
    }

    private UIElement CreateTextEditRow(MacroTimeline timeline, MacroNode node)
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(0, 2, 0, 0),
            ToolTip = TooltipNotes.TextNodeValue
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
            IsEnabled = _isTimelineEditingEnabled,
            ToolTip = TooltipNotes.TextNodeEdit
        };

        textBox.LostFocus += (_, _) =>
        {
            if (!_isRefreshingInspector && textBox.Text != node.Text)
                CommitStepChange(() => node.Text = textBox.Text);
        };
        textBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                return;

            if (textBox.Text != node.Text)
                CommitStepChange(() => node.Text = textBox.Text);

            Keyboard.ClearFocus();
            e.Handled = true;
        };

        panel.Children.Add(textBox);
        return panel;
    }

    private UIElement CreatePickPointButton(MacroNode node)
    {
        var button = new Button
        {
            Content = "Pick point",
            Height = 22,
            Margin = new Thickness(0, 2, 0, 0),
            IsEnabled = _isTimelineEditingEnabled,
            ToolTip = TooltipNotes.PickMouseCoordinates
        };

        button.Click += async (_, _) =>
        {
            SaveUndoSnapshot();
            await PickMouseCoordinatesForStepAsync(node);
            RefreshInspector();
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

    private void CommitNumberText(TextBox textBox, int originalValue, Action<int> commit, int min, int? max)
    {
        if (_isRefreshingInspector)
            return;

        if (!int.TryParse(textBox.Text, out var value))
            value = min;

        value = Math.Max(min, value);
        if (max.HasValue)
            value = Math.Min(max.Value, value);

        textBox.Text = value.ToString();

        if (value != originalValue)
            commit(value);
    }

    private void CommitStepChange(Action change)
    {
        if (!_isTimelineEditingEnabled)
            return;

        SaveUndoSnapshot();
        change();
        RefreshTimeline();
        RefreshInspector();
        ScheduleSaveState();
    }

    private void CommitTimelineChange(MacroTimeline timeline, Action change)
    {
        if (!_isTimelineEditingEnabled)
            return;

        change();
        SelectTimeline(timeline);
        RefreshTimeline();
        ScheduleSaveState();
    }

    private void CommitTimelineName(MacroTimeline timeline, string name)
    {
        if (_isCommittingTimelineName)
            return;

        var trimmedName = string.IsNullOrWhiteSpace(name) ? timeline.Name : name.Trim();

        if (!_isTimelineEditingEnabled)
            return;

        _isCommittingTimelineName = true;
        try
        {
            timeline.Name = trimmedName;
            _editingTimelineName = null;
            _selection.SelectTimeline(timeline);
            RefreshTimeline();
            RefreshInspector();
            ScheduleSaveState();
        }
        finally
        {
            _isCommittingTimelineName = false;
        }
    }

    private static bool HasEditableStepInspector(MacroNode node) =>
        !node.IsSyntheticDisplayNode &&
        node.Type is MacroNodeType.Delay
            or MacroNodeType.RandomDelay
            or MacroNodeType.Text
            or MacroNodeType.CursorMove
            or MacroNodeType.BackgroundMouseDown
            or MacroNodeType.BackgroundMouseUp
            or MacroNodeType.BackgroundMouseClick;

    private static void NormalizeRandomDelay(MacroNode node)
    {
        if (node.RandomDelayMaxMs < node.RandomDelayMinMs)
            (node.RandomDelayMinMs, node.RandomDelayMaxMs) = (node.RandomDelayMaxMs, node.RandomDelayMinMs);
    }

    private static string GetStepTypeText(MacroNode node)
    {
        return node.Type switch
        {
            MacroNodeType.KeyDown => "Key down",
            MacroNodeType.KeyUp => "Key up",
            MacroNodeType.Delay => "Delay",
            MacroNodeType.RandomDelay => "Random delay",
            MacroNodeType.Text => "Text",
            MacroNodeType.MouseClick => "Foreground click",
            MacroNodeType.MouseDown => "Mouse down",
            MacroNodeType.MouseUp => "Mouse up",
            MacroNodeType.CursorMove => "Move cursor",
            MacroNodeType.BackgroundMouseDown => "BG mouse down",
            MacroNodeType.BackgroundMouseUp => "BG mouse up",
            MacroNodeType.BackgroundMouseClick => "BG mouse click",
            _ => node.Type.ToString()
        };
    }
}
