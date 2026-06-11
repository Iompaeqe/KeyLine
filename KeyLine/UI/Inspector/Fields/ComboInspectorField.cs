using System.Windows.Controls;
using KeyLine.UI.Inspector.Batch;

namespace KeyLine.UI.Inspector.Fields;

/// <summary>
/// Shared behavior for dropdown inspector fields. Dropdowns commit immediately on selection (they have no
/// pending text edit), but they share the same refresh-suppression and "don't write an unchanged value"
/// rules as every other field, and show a blank selection for a mixed batch.
/// </summary>
internal sealed class ComboInspectorField<TValue> : IInspectorField
{
    private readonly ComboBox _combo;
    private readonly InspectorFieldHost _host;
    private readonly Func<BatchValue<TValue>> _read;
    private readonly Action<TValue> _apply;
    private readonly IEqualityComparer<TValue> _comparer;

    private bool _isSettingSelection;

    public ComboInspectorField(
        ComboBox combo,
        InspectorFieldHost host,
        Func<BatchValue<TValue>> read,
        Action<TValue> apply,
        IEqualityComparer<TValue>? comparer = null)
    {
        _combo = combo;
        _host = host;
        _read = read;
        _apply = apply;
        _comparer = comparer ?? EqualityComparer<TValue>.Default;

        _combo.SelectionChanged += OnSelectionChanged;

        LoadFromModel();
    }

    public bool HasPendingEdit => false;

    public void LoadFromModel()
    {
        var batch = _read();
        _isSettingSelection = true;
        try
        {
            if (batch.HasMixedValue)
                _combo.SelectedIndex = -1;
            else
                _combo.SelectedValue = batch.Value;
        }
        finally
        {
            _isSettingSelection = false;
        }
    }

    public bool TryCommit() => false;

    public void CancelEdit()
    {
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSettingSelection || _host.IsRefreshing)
            return;

        if (_combo.SelectedValue is not TValue selected)
            return;

        var batch = _read();
        if (!batch.HasMixedValue && _comparer.Equals(batch.Value, selected))
            return;

        _apply(selected);
    }
}
