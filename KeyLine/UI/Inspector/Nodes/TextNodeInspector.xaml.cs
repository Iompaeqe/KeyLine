using KeyLine.Domain;
using KeyLine.UI.Inspector.Fields;

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

        InspectorFieldBinder.BindText(
            context.FieldHost,
            TextEditor,
            read: InspectorFieldBinder.SingleText(() => node.Text),
            apply: value => context.CommitNodeChange(() => node.Text = value),
            isEnabled: policy.CanEditText,
            multiline: true,
            selectAllOnFocus: false);
    }
}
