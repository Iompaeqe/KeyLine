using System.Windows.Controls;
using System.Windows.Input;
using KeyLine.UI.Inspector.Batch;

namespace KeyLine.UI.Inspector.Fields;

/// <summary>
/// Shared behavior for free-text inspector fields (launch target, window title, text-node payload). Same
/// focus/commit contract as the numeric fields: commit on Enter (Ctrl+Enter for multi-line) and on focus
/// loss, restore on Escape, and never write an unchanged value.
/// </summary>
internal sealed class TextInspectorField : IInspectorField
{
    private readonly TextBox _textBox;
    private readonly InspectorFieldHost _host;
    private readonly Func<BatchValue<string>> _read;
    private readonly Action<string> _apply;
    private readonly bool _multiline;
    private readonly bool _selectAllOnFocus;

    private bool _isEditing;
    private bool _isSettingText;

    public TextInspectorField(
        TextBox textBox,
        InspectorFieldHost host,
        Func<BatchValue<string>> read,
        Action<string> apply,
        bool multiline,
        bool selectAllOnFocus)
    {
        _textBox = textBox;
        _host = host;
        _read = read;
        _apply = apply;
        _multiline = multiline;
        _selectAllOnFocus = selectAllOnFocus;

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
                BatchUi.ShowMixed(_textBox);
            else
            {
                BatchUi.ClearMixedStyle(_textBox);
                _textBox.Text = batch.Value ?? string.Empty;
            }
        }
        finally
        {
            _isSettingText = false;
        }
    }

    public bool TryCommit()
    {
        if (!_isEditing)
            return false;

        _isEditing = false;

        if (_isSettingText || _host.IsRefreshing)
            return false;

        var batch = _read();
        var text = _textBox.Text ?? string.Empty;

        if (batch.HasMixedValue)
        {
            // Leave a mixed field untouched unless the user actually typed something.
            if (string.IsNullOrEmpty(text))
                return false;
        }
        else if (string.Equals(text, batch.Value ?? string.Empty, StringComparison.Ordinal))
        {
            return false;
        }

        _apply(text);
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
        if (_read().HasMixedValue)
        {
            _isSettingText = true;
            try
            {
                BatchUi.ClearMixedStyle(_textBox);
                _textBox.Text = string.Empty;
            }
            finally
            {
                _isSettingText = false;
            }
        }

        if (_selectAllOnFocus)
            _textBox.SelectAll();
    }

    private void OnLostFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        if (!TryCommit())
            LoadFromModel();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                // Multi-line fields keep Enter for newlines and commit on Ctrl+Enter.
                if (_multiline && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                    return;
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
