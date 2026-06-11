namespace KeyLine.UI.Inspector.Fields;

/// <summary>
/// One inspector input field (text, number, delay, combo, checkbox) with a uniform commit lifecycle.
/// Every editable control in the inspector is wired through <see cref="InspectorFieldBinder"/> as an
/// <see cref="IInspectorField"/> so focus loss, click-outside, window-deactivation and selection change
/// all flow through the same commit/restore path instead of per-control ad-hoc handlers.
/// </summary>
public interface IInspectorField
{
    /// <summary>True while the user is actively editing and a value may need committing.</summary>
    bool HasPendingEdit { get; }

    /// <summary>Renders the current model/batch value into the control (no commit).</summary>
    void LoadFromModel();

    /// <summary>Commits the pending edit if the value actually changed. Returns true when a value was written.</summary>
    bool TryCommit();

    /// <summary>Abandons the pending edit and restores the displayed value (Escape / restore).</summary>
    void CancelEdit();
}
