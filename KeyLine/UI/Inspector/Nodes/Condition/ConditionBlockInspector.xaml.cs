using System.Windows;
using System.Windows.Controls;
using KeyLine.Domain;
using KeyLine.Interop;
using KeyLine.Services.Input;
using KeyLine.Services.Macro;
using KeyLine.UI.Timeline;

namespace KeyLine.UI.Inspector.Nodes;

public partial class ConditionBlockInspector
{
    private static readonly IReadOnlyList<ConditionTypeOption> ConditionTypeOptions =
    [
        new("Key held", MacroConditionType.KeyState),
        new("Pixel matches", MacroConditionType.PixelColor),
        new("Random chance", MacroConditionType.RandomChance),
        new("Loop context", MacroConditionType.LoopContext),
        new("Window Focused", MacroConditionType.TargetWindowFocused),
        new("Window Exists", MacroConditionType.WindowExists),
        new("Macro Running", MacroConditionType.MacroRunning),
        new("Time Passed", MacroConditionType.TimePassed)
    ];

    private TextBlock OverviewTypeText =>
        OverviewRow.GetContent<StackPanel>()!
            .Children
            .OfType<TextBlock>()
            .ElementAt(0);

    private TextBlock OverviewInsideText =>
        OverviewRow.GetContent<StackPanel>()!
            .Children
            .OfType<TextBlock>()
            .ElementAt(1);

    private StackPanel ConditionTypeHost =>
        ConditionTypeRow.GetContent<StackPanel>()!;

    private ComboBox ConditionTypeCombo =>
        ConditionTypeHost.Children.OfType<ComboBox>().First();

    private CheckBox NotToggle =>
        ConditionTypeHost.Children.OfType<CheckBox>().First();

    private NodeInspectorContext? _context;
    private MacroNode? _node;
    private int _blockNodeCount;
    private Action<StackPanel, MacroNode, bool>? _buildDetails;

    public ConditionBlockInspector()
    {
        InitializeComponent();
    }

    public void Bind(
        NodeInspectorContext context,
        MacroNode node,
        int blockNodeCount,
        Action<StackPanel, MacroNode, bool> buildDetails)
    {
        _context = context;
        _node = node;
        _blockNodeCount = blockNodeCount;
        _buildDetails = buildDetails;

        BindOverview();
        BindConditionTypeRow();
        RebuildDetails();
    }

    private void BindOverview()
    {
        if (_node == null)
            return;

        OverviewRow.RowToolTip = NodeDisplayFormatter.GetConditionSummary(_node, ResolveMacroName);
        OverviewTypeText.Text = GetConditionTypeLabel(_node.ConditionType);
        OverviewInsideText.Text = $"{Math.Max(0, _blockNodeCount - 2)} inside";
    }

    private void BindConditionTypeRow()
    {
        if (_context == null || _node == null)
            return;

        var canEdit = _context.CanEditOption(true);
        var canInvert = MacroConditionDefinitions.CanInvert(_node.ConditionType);

        ConditionTypeCombo.ItemsSource = ConditionTypeOptions;
        ConditionTypeCombo.SelectedValue = _node.ConditionType;
        ConditionTypeCombo.Width = canInvert ? 136 : 176;
        ConditionTypeCombo.IsEnabled = canEdit;

        NotToggle.Visibility = canInvert
            ? Visibility.Visible
            : Visibility.Collapsed;

        NotToggle.IsChecked = _node.ConditionIsInverted;
        NotToggle.IsEnabled = canEdit;

        ConditionTypeCombo.SelectionChanged += (_, _) =>
        {
            if (_context.IsRefreshing())
                return;

            if (ConditionTypeCombo.SelectedItem is not ConditionTypeOption option)
                return;

            if (option.Value == _node.ConditionType)
                return;

            _context.CommitNodeChange(() =>
            {
                _node.ConditionType = option.Value;
                NormalizeConditionDefaults(_node);
            });

            BindOverview();
            RefreshConditionTypeRowOnly();
            RebuildDetails();
        };

        NotToggle.Checked += (_, _) => CommitConditionInversion(true);
        NotToggle.Unchecked += (_, _) => CommitConditionInversion(false);
    }

    private void RefreshConditionTypeRowOnly()
    {
        if (_context == null || _node == null)
            return;

        var canEdit = _context.CanEditOption(true);
        var canInvert = MacroConditionDefinitions.CanInvert(_node.ConditionType);

        ConditionTypeCombo.Width = canInvert ? 136 : 176;

        NotToggle.Visibility = canInvert
            ? Visibility.Visible
            : Visibility.Collapsed;

        NotToggle.IsChecked = _node.ConditionIsInverted;
        NotToggle.IsEnabled = canEdit;
    }

    private void RebuildDetails()
    {
        if (_node == null || _buildDetails == null)
            return;

        DetailsHost.Children.Clear();
        _buildDetails(DetailsHost, _node, true);
    }

    private void CommitConditionInversion(bool isInverted)
    {
        if (_context == null ||
            _node == null ||
            _context.IsRefreshing() ||
            _node.ConditionIsInverted == isInverted ||
            !MacroConditionDefinitions.CanInvert(_node.ConditionType))
        {
            return;
        }

        _context.CommitNodeChange(() => _node.ConditionIsInverted = isInverted);
    }

    private string? ResolveMacroName(string macroId)
    {
        macroId = macroId?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(macroId))
            return null;

        return _context?.GetActiveProfileWorkspaces()
            .FirstOrDefault(workspace => string.Equals(
                workspace.Id,
                macroId,
                StringComparison.OrdinalIgnoreCase))
            ?.Name;
    }

    private static string GetConditionTypeLabel(MacroConditionType type) =>
        type switch
        {
            MacroConditionType.KeyState => "Key held",
            MacroConditionType.PixelColor => "Pixel color",
            MacroConditionType.RandomChance => "Random chance",
            MacroConditionType.LoopContext => "Loop rule",
            MacroConditionType.TargetWindowFocused => "Window focused",
            MacroConditionType.WindowExists => "Window exists",
            MacroConditionType.MacroRunning => "Macro running",
            MacroConditionType.TimePassed => "Time passed",
            _ => "Condition"
        };

    private static void NormalizeConditionDefaults(MacroNode node)
    {
        if (!MacroConditionDefinitions.CanInvert(node.ConditionType))
            node.ConditionIsInverted = false;

        if (node.ConditionType == MacroConditionType.KeyState && node.ConditionVirtualKey <= 0)
        {
            node.ConditionVirtualKey = NativeMethods.VK_SHIFT;
            node.ConditionKeyName = "Shift";
            node.ConditionShortcutKeys = ConditionInputGesture.Serialize([NativeMethods.VK_SHIFT]);
        }

        node.ConditionPixelX = Math.Max(0, node.ConditionPixelX);
        node.ConditionPixelY = Math.Max(0, node.ConditionPixelY);
        node.ConditionPixelRed = Math.Clamp(node.ConditionPixelRed, 0, 255);
        node.ConditionPixelGreen = Math.Clamp(node.ConditionPixelGreen, 0, 255);
        node.ConditionPixelBlue = Math.Clamp(node.ConditionPixelBlue, 0, 255);
        node.ConditionPixelTolerance = Math.Clamp(node.ConditionPixelTolerance, 0, 255);
        node.ConditionChancePercent = Math.Clamp(node.ConditionChancePercent, 0, 100);
        node.ConditionLoopInterval = Math.Max(1, node.ConditionLoopInterval);
        node.ConditionTimePassedMs = Math.Max(1, node.ConditionTimePassedMs);

        if (node.ConditionType is MacroConditionType.WindowExists or MacroConditionType.TargetWindowFocused &&
            node.WindowReference.Type == WindowReferenceType.CustomTitle &&
            string.IsNullOrWhiteSpace(node.WindowReference.CustomTitle))
        {
            node.WindowReference = new WindowReference
            {
                Type = WindowReferenceType.SelectedTarget
            };
        }
    }

    private sealed record ConditionTypeOption(string Label, MacroConditionType Value)
    {
        public override string ToString() => Label;
    }
}