using System.Windows.Controls;
using System.Windows.Input;
using KeyLine.Domain;
using KeyLine.UI.Common.EntryBlocks;

namespace KeyLine.UI.Inspector.Nodes;

public partial class RandomChanceConditionInspector
{
    private StackPanel ChanceHost =>
        ChanceRow.GetContent<StackPanel>()!;

    private NumberEntryBlock ChanceEntry =>
        ChanceHost.Children.OfType<NumberEntryBlock>().First();

    public RandomChanceConditionInspector()
    {
        InitializeComponent();
    }

    public void Bind(NodeInspectorContext context, MacroNode node, bool isEnabled)
    {
        BindNumberEntry(
            entry: ChanceEntry,
            context: context,
            value: Math.Clamp(node.ConditionChancePercent, 0, 100),
            commit: value => context.CommitNodeValueChange(() =>
                node.ConditionChancePercent = Math.Clamp(value, 0, 100)),
            min: 0,
            max: 100,
            tooltip: TooltipNotes.ConditionChance,
            isEnabled: isEnabled);
    }

    private static void BindNumberEntry(
        NumberEntryBlock entry,
        NodeInspectorContext context,
        int value,
        Action<int> commit,
        int min,
        int? max,
        string tooltip,
        bool isEnabled)
    {
        var canEdit = context.CanEditOption(isEnabled);

        entry.IsEnabled = canEdit;
        entry.ToolTip = tooltip;

        var textBox = entry.TextBox;
        textBox.Text = value.ToString();
        textBox.IsEnabled = canEdit;

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
                    context.IsRefreshing());
            }
            finally
            {
                isCommittingText = false;
            }
        }

        textBox.PreviewTextInput += (_, e) =>
        {
            e.Handled = !e.Text.All(char.IsDigit);
        };

        textBox.GotKeyboardFocus += (_, _) =>
        {
            textBox.SelectAll();
        };

        textBox.LostFocus += (_, _) =>
        {
            CommitText();
        };

        textBox.TextChanged += (_, _) =>
        {
            CommitText();
        };

        textBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;

            CommitText();
            Keyboard.ClearFocus();
            e.Handled = true;
        };
    }
}