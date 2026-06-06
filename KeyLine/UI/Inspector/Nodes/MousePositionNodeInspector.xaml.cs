using System.Windows.Input;
using KeyLine.Domain;
using KeyLine.UI.Common.EntryBlocks;

namespace KeyLine.UI.Inspector.Nodes;

public partial class MousePositionNodeInspector
{
    private NumberEntryBlock XEntry =>
        XRow.GetContent<NumberEntryBlock>()!;

    private NumberEntryBlock YEntry =>
        YRow.GetContent<NumberEntryBlock>()!;

    public MousePositionNodeInspector()
    {
        InitializeComponent();
    }

    public void Bind(NodeInspectorContext context, MacroNode node, NodeInspectorPolicy policy)
    {
        BindNumberEntry(
            entry: XEntry,
            context: context,
            value: node.MouseX,
            commit: value => context.CommitNodeValueChange(() => node.MouseX = value),
            min: 0,
            max: null,
            isEnabled: policy.CanEditMousePosition);

        BindNumberEntry(
            entry: YEntry,
            context: context,
            value: node.MouseY,
            commit: value => context.CommitNodeValueChange(() => node.MouseY = value),
            min: 0,
            max: null,
            isEnabled: policy.CanEditMousePosition);

        PickPointButton.IsEnabled = context.CanEditOption(policy.CanPickMousePosition);
        PickPointButton.Click += async (_, _) =>
        {
            context.SaveUndoSnapshot();
            await context.PickMouseCoordinatesForNodeAsync(node);
            context.RefreshInspector();
        };
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
        entry.ToolTip = "Coordinate value.";

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