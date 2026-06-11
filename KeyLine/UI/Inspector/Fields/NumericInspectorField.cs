using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using KeyLine.UI.Inspector.Batch;

namespace KeyLine.UI.Inspector.Fields;

/// <summary>
/// The single implementation of integer/delay field behavior, shared by every numeric inspector field
/// (Delay, Loops, Cooldown, Mouse X/Y, Scroll amount, Volume %, Repeat count, Random chance, Tolerance,
/// Loop interval, …) in both single and batch mode. Display/edit/mixed rendering and the parse decision
/// are injected so this class owns focus, commit timing and the Enter/Escape/LostFocus contract once.
/// </summary>
internal sealed class NumericInspectorField : IInspectorField
{
    private readonly TextBox _textBox;
    private readonly InspectorFieldHost _host;
    private readonly Func<BatchValue<int>> _read;
    private readonly Action<int> _apply;
    private readonly Func<string, BatchValue<int>, NumberCommitResult> _decide;
    private readonly Action<int> _renderDisplay;
    private readonly Action<int> _renderEdit;
    private readonly Action _renderMixedDisplay;
    private readonly Action _renderEditEmpty;

    private bool _isEditing;
    private bool _isSettingText;

    public NumericInspectorField(
        TextBox textBox,
        InspectorFieldHost host,
        Func<BatchValue<int>> read,
        Action<int> apply,
        Func<string, BatchValue<int>, NumberCommitResult> decide,
        Action<int> renderDisplay,
        Action<int> renderEdit,
        Action renderMixedDisplay,
        Action renderEditEmpty)
    {
        _textBox = textBox;
        _host = host;
        _read = read;
        _apply = apply;
        _decide = decide;
        _renderDisplay = renderDisplay;
        _renderEdit = renderEdit;
        _renderMixedDisplay = renderMixedDisplay;
        _renderEditEmpty = renderEditEmpty;

        _textBox.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
        _textBox.GotKeyboardFocus += OnGotKeyboardFocus;
        _textBox.LostFocus += OnLostFocus;
        _textBox.KeyDown += OnKeyDown;

        LoadFromModel();
    }

    public bool HasPendingEdit => _isEditing;

    public void LoadFromModel()
    {
        var batch = _read();
        _isSettingText = true;
        try
        {
            if (batch.HasMixedValue)
                _renderMixedDisplay();
            else
                _renderDisplay(batch.Value);
        }
        finally
        {
            _isSettingText = false;
        }
    }

    public bool TryCommit()
    {
        // No active edit (e.g. Enter already handled it, then LostFocus fired): nothing to write.
        if (!_isEditing)
            return false;

        _isEditing = false;

        if (_isSettingText || _host.IsRefreshing)
            return false;

        var result = _decide(_textBox.Text, _read());
        if (!result.ShouldWrite)
            return false;

        _apply(result.Value);
        return true;
    }

    public void CancelEdit()
    {
        _isEditing = false;
        LoadFromModel();
    }

    private void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _isEditing = true;
        var batch = _read();
        _isSettingText = true;
        try
        {
            if (batch.HasMixedValue)
                _renderEditEmpty();
            else
                _renderEdit(batch.Value);
        }
        finally
        {
            _isSettingText = false;
        }

        _textBox.SelectAll();
    }

    private void OnLostFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        // A successful commit rebuilds the inspector and replaces this control; otherwise restore display.
        if (!TryCommit())
            LoadFromModel();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                if (!TryCommit())
                    LoadFromModel();
                _host.RequestDefocus(_textBox);
                e.Handled = true;
                break;

            case Key.Escape:
                CancelEdit();
                _host.RequestDefocus(_textBox);
                e.Handled = true;
                break;
        }
    }
}
