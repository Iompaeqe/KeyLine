using System.Windows.Input;
using KeyLine.Domain;

namespace KeyLine.UI.Inspector.Nodes;

public partial class TextNodeInspector
{
    public TextNodeInspector()
    {
        InitializeComponent();
    }

    public void Bind(NodeInspectorContext context, MacroNode node, NodeInspectorPolicy policy)
    {
        var canEdit = context.CanEditOption(policy.CanEditText);

        RootPanel.IsEnabled = canEdit;
        TextEditor.IsEnabled = canEdit;
        TextEditor.Text = node.Text;

        TextEditor.LostFocus += (_, _) => CommitText(context, node);

        TextEditor.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                return;

            CommitText(context, node);
            Keyboard.ClearFocus();
            e.Handled = true;
        };
    }

    private void CommitText(NodeInspectorContext context, MacroNode node)
    {
        if (context.IsRefreshing())
            return;

        if (TextEditor.Text == node.Text)
            return;

        context.CommitNodeChange(() => node.Text = TextEditor.Text);
    }
}