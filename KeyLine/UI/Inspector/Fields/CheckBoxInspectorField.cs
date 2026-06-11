using System.Windows.Controls;
using KeyLine.UI.Inspector.Batch;

namespace KeyLine.UI.Inspector.Fields;

/// <summary>
/// Shared behavior for checkbox inspector fields. Toggling commits immediately; a mixed batch shows the
/// indeterminate (dot) state. Like the dropdown field it shares the refresh-suppression rule so populating
/// the UI never writes back to the model.
/// </summary>
internal sealed class CheckBoxInspectorField : IInspectorField
{
    private readonly CheckBox _checkBox;
    private readonly InspectorFieldHost _host;
    private readonly Func<BatchValue<bool>> _read;
    private readonly Action<bool> _apply;
    private readonly object? _baseToolTip;

    private bool _isSettingState;

    public CheckBoxInspectorField(
        CheckBox checkBox,
        InspectorFieldHost host,
        Func<BatchValue<bool>> read,
        Action<bool> apply)
    {
        _checkBox = checkBox;
        _host = host;
        _read = read;
        _apply = apply;
        _baseToolTip = checkBox.ToolTip;

        _checkBox.Checked += OnToggled;
        _checkBox.Unchecked += OnToggled;

        LoadFromModel();
    }

    public bool HasPendingEdit => false;

    public void LoadFromModel()
    {
        var batch = _read();
        _isSettingState = true;
        try
        {
            _checkBox.IsThreeState = batch.HasMixedValue;
            _checkBox.IsChecked = batch.HasMixedValue ? null : batch.Value;
            _checkBox.ToolTip = batch.HasMixedValue ? "Mixed values" : _baseToolTip;
        }
        finally
        {
            _isSettingState = false;
        }
    }

    public bool TryCommit() => false;

    public void CancelEdit()
    {
    }

    private void OnToggled(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_isSettingState || _host.IsRefreshing)
            return;

        _apply(_checkBox.IsChecked ?? false);
    }
}
