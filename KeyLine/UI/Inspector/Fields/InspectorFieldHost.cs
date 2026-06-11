using System.Windows;

namespace KeyLine.UI.Inspector.Fields;

/// <summary>
/// The environment a field needs from the inspector window, abstracted so the same binder serves both
/// the persistent timeline fields and the rebuilt-per-selection node fields. The window builds two hosts:
/// one that registers persistent fields and reports the "setting timeline state" guard, and one that
/// registers node fields and reports the controller's refresh guard.
/// </summary>
public sealed class InspectorFieldHost
{
    private readonly Action<IInspectorField> _register;
    private readonly Func<bool> _isRefreshing;
    private readonly Action<DependencyObject?> _requestDefocus;
    private readonly Func<bool> _canEdit;

    public InspectorFieldHost(
        Action<IInspectorField> register,
        Func<bool> isRefreshing,
        Action<DependencyObject?> requestDefocus,
        Func<bool> canEdit)
    {
        _register = register;
        _isRefreshing = isRefreshing;
        _requestDefocus = requestDefocus;
        _canEdit = canEdit;
    }

    /// <summary>True while the UI is being populated from the model — fields must not commit then.</summary>
    public bool IsRefreshing => _isRefreshing();

    public bool CanEdit => _canEdit();

    public void Register(IInspectorField field) => _register(field);

    /// <summary>Moves keyboard focus to the inspector's focus sink, clearing the active field's focus visual.</summary>
    public void RequestDefocus(DependencyObject? source = null) => _requestDefocus(source);
}
