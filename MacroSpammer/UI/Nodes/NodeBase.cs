using System.Windows;
using System.Windows.Controls;
using MacroSpammer.Domain;

namespace MacroSpammer.UI.Nodes;

public enum InlineEditorActivationMode
{
    None,
    SuppressMouseUp,
    AllowMouseUp
}

public abstract class NodeBase : UserControl
{
    private MacroNode? _step;
    private bool _isSelected;

    public MacroNode? Step
    {
        get => _step;
        set
        {
            if (ReferenceEquals(_step, value))
                return;

            _step = value;
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
