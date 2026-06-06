using System.Windows.Input;
using KeyLine.Domain;
using KeyLine.UI.Common.EntryBlocks;

namespace KeyLine.UI.Inspector.Nodes;

public partial class MouseScrollNodeInspector
{
    private NumberEntryBlock AmountEntry =>
        AmountRow.GetContent<NumberEntryBlock>()!;

    public MouseScrollNodeInspector()
    {
        InitializeComponent();
    }

    public void Bind(NodeInspectorContext context, MacroNode node, NodeInspectorPolicy policy)
    {
        BindNumberEntry(
            entry: AmountEntry,
            context: context,
            value: Math.Clamp(node.MouseScrollAmount <= 0 ? 1 : node.MouseScrollAmount, 1, 100),
            commit: value => context.CommitNodeValueChange(() =>
                node.MouseScrollAmount = Math.Clamp(value, 1, 100)),
            min: 1,
            max: 100,
            isEnabled: policy.CanEditMouseScrollAmount);
    }

    private static void BindNumberEntry(
        NumberEntryBlock entry,
        NodeInspectorContext context,
        int value,
        Action<int> commit,
        int min,
        int? max,
        bool isEnabled)
    {
        var canEdit = context.CanEditOption(isEnabled);

        entry.IsEnabled = canEdit;
        entry.ToolTip = "How many scroll notches this node sends.";

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