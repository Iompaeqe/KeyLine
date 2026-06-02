using System.Windows;
using System.Windows.Controls;
using KeyLine.Domain;

namespace KeyLine.UI.Nodes;

public enum InlineEditorActivationMode
{
    None,
    SuppressMouseUp,
    AllowMouseUp
}

public abstract class NodeBase : UserControl
{
    private MacroNode? _node;
    private bool _isSelected;

    public MacroNode? Node
    {
        get => _node;
        set
        {
            if (ReferenceEquals(_node, value))
                return;

            _node = value;
            Tag = value;
            UpdateVisual();
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            UpdateVisual();
        }
    }

    public virtual InlineEditorActivationMode GetInlineEditorActivationMode(DependencyObject? source) => InlineEditorActivationMode.None;

    public virtual void FocusInlineEditor(DependencyObject? source = null)
    {
    }

    protected abstract void UpdateVisual();
}
