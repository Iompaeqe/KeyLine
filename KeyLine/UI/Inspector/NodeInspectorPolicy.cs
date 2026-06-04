using KeyLine.Domain;

public sealed class NodeInspectorPolicy
{
    public bool HasInspector { get; init; } = true;

    public bool CanEditDelay { get; init; }
    public bool CanEditRandomDelay { get; init; }
    public bool CanEditText { get; init; }
    public bool CanEditMousePosition { get; init; }
    public bool CanPickMousePosition { get; init; }
    public bool CanEditMouseButton { get; init; }
    public bool CanEditRepeatCount { get; init; }
    public bool CanEditCondition { get; init; }

    public static NodeInspectorPolicy For(MacroTimeline timeline, MacroNode node)
    {
        if (node.IsSyntheticDisplayNode)
        {
            return new NodeInspectorPolicy
            {
                HasInspector = false
            };
        }

        return node.Type switch
        {
            MacroNodeType.Delay => new NodeInspectorPolicy
            {
                CanEditDelay = true
            },

            MacroNodeType.RandomDelay => new NodeInspectorPolicy
            {
                CanEditRandomDelay = true
            },

            MacroNodeType.Text => new NodeInspectorPolicy
            {
                CanEditText = true
            },

            MacroNodeType.CursorMove => new NodeInspectorPolicy
            {
                CanEditMousePosition = true,
                CanPickMousePosition = true
            },

            MacroNodeType.BackgroundMouseDown or
            MacroNodeType.BackgroundMouseUp or
            MacroNodeType.BackgroundMouseClick => new NodeInspectorPolicy
            {
                CanEditMousePosition = true,
                CanPickMousePosition = true,
                CanEditMouseButton = true
            },

            MacroNodeType.MouseDown or MacroNodeType.MouseUp => new NodeInspectorPolicy
            {
                HasInspector = false
            },

            MacroNodeType.KeyDown or MacroNodeType.KeyUp => new NodeInspectorPolicy
            {
                HasInspector = false
            },

            MacroNodeType.MouseClick => new NodeInspectorPolicy
            {
                HasInspector = false
            },

            MacroNodeType.RepeatStart => new NodeInspectorPolicy
            {
                CanEditRepeatCount = true
            },

            MacroNodeType.RepeatEnd => new NodeInspectorPolicy(),

            MacroNodeType.ConditionStart => new NodeInspectorPolicy
            {
                CanEditCondition = true
            },

            MacroNodeType.ConditionEnd => new NodeInspectorPolicy(),

            _ => new NodeInspectorPolicy
            {
                HasInspector = false
            }
        };
    }
}
