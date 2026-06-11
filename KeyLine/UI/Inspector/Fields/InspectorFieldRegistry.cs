namespace KeyLine.UI.Inspector.Fields;

/// <summary>
/// Tracks every live inspector field so the window can finish edits from a single place. Timeline fields
/// are persistent (wired once); node fields are rebuilt on every selection change, so they live in a
/// separate bucket that is cleared whenever the node panel is replaced.
/// </summary>
public sealed class InspectorFieldRegistry
{
    private readonly List<IInspectorField> _persistentFields = new();
    private readonly List<IInspectorField> _nodeFields = new();
    private bool _isFinishing;

    public void RegisterPersistent(IInspectorField field) => _persistentFields.Add(field);

    public void RegisterNode(IInspectorField field) => _nodeFields.Add(field);

    public void ClearNodeFields() => _nodeFields.Clear();

    /// <summary>
    /// Commits any field with a pending edit (only the focused field ever has one), restoring its display
    /// if nothing was written. Re-entrant calls are ignored because a successful commit rebuilds the
    /// inspector, which would otherwise re-enter through SetNodeContent.
    /// </summary>
    public void FinishAll()
    {
        if (_isFinishing)
            return;

        _isFinishing = true;
        try
        {
            // Snapshot: a commit may rebuild node fields mid-iteration.
            foreach (var field in _persistentFields.Concat(_nodeFields).ToArray())
            {
                if (!field.HasPendingEdit)
                    continue;

                if (!field.TryCommit())
                    field.CancelEdit();
            }
        }
        finally
        {
            _isFinishing = false;
        }
    }
}
