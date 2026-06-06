using System.Windows.Controls;
using System.Windows.Input;
using KeyLine.Domain;
using KeyLine.UI.Common.EntryBlocks;

namespace KeyLine.UI.Inspector.Nodes;

public partial class RepeatBlockInspector
{
    private NumberEntryBlock CountEntry =>
        CountRow.GetContent<NumberEntryBlock>()!;

    private TextBlock InsideValue =>
        InsideRow.GetContent<TextBlock>()!;

    public RepeatBlockInspector()
    {
        InitializeComponent();
    }

    public void Bind(NodeInspectorContext context, MacroNode repeatStart, int blockNodeCount)
    {
        BindNumberEntry(
            entry: CountEntry,
            context: context,
            value: Math.Max(1, repeatStart.RepeatCount),
            commit: value => context.CommitNodeValueChange(() =>
                repeatStart.RepeatCount = Math.Max(1, value)),
            min: 1,
            max: null,
            tooltip: TooltipNotes.RepeatCount,
            isEnabled: true);

        InsideValue.Text = Math.Max(0, blockNodeCount - 2).ToString();
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