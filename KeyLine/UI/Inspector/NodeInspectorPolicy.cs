using KeyLine.Domain;
using KeyLine.Services.Input;

public sealed class NodeInspectorPolicy
{
    public bool HasInspector { get; init; } = true;

    public bool CanEditDelay { get; init; }
    public bool CanEditText { get; init; }
    public bool CanEditMousePosition { get; init; }
    public bool CanPickMousePosition { get; init; }
    public bool CanEditMouseButton { get; init; }
    public bool CanEditMouseScrollAmount { get; init; }
    public bool CanEditSystemLaunch { get; init; }
    public bool CanEditVolumeControl { get; init; }
    public bool CanEditSystemWindowWait { get; init; }
    public bool CanEditSystemTargetWindow { get; init; }
    public bool CanEditSystemFocusWindow { get; init; }
    public bool CanEditRepeatCount { get; init; }
    public bool CanEditCondition { get; init; }
    public bool CanEditToggleKeyMode { get; init; }

    public static NodeInspectorPolicy For(MacroTimeline timeline, MacroNode node)
    {
        if (node.IsSyntheticDisplayNode)
        {
            if (HasToggleKey(node))
            {
                return new NodeInspectorPolicy
                {
                    CanEditToggleKeyMode = true
                };
            }

            return new NodeInspectorPolicy
            {
                HasInspector = false
            };
        }

        return node.Type switch
        {
            MacroNodeType.Delay or MacroNodeType.RandomDelay => new NodeInspectorPolicy
            {
                CanEditDelay = true
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

            MacroNodeType.KeyDown or MacroNodeType.KeyUp => HasToggleKey(node)
                ? new NodeInspectorPolicy
                {
                    CanEditToggleKeyMode = true
                }
                : new NodeInspectorPolicy
                {
                    HasInspector = false
                },

            MacroNodeType.MouseClick => new NodeInspectorPolicy
            {
                HasInspector = false
            },

            MacroNodeType.MouseScrollUp or
            MacroNodeType.MouseScrollDown or
            MacroNodeType.MouseScrollLeft or
            MacroNodeType.MouseScrollRight => new NodeInspectorPolicy
            {
                CanEditMouseScrollAmount = true
            },

            MacroNodeType.SystemOpenLaunch => new NodeInspectorPolicy
            {
                CanEditSystemLaunch = true
            },

            MacroNodeType.SystemVolumeControl => new NodeInspectorPolicy
            {
                CanEditVolumeControl = true
            },

            MacroNodeType.SystemWaitUntilWindowOpens => new NodeInspectorPolicy
            {
                CanEditSystemWindowWait = true
            },

            MacroNodeType.SystemSelectTargetWindow => new NodeInspectorPolicy
            {
                CanEditSystemTargetWindow = true
            },

            MacroNodeType.SystemFocusWindow => new NodeInspectorPolicy
            {
                CanEditSystemFocusWindow = true
            },

            MacroNodeType.RunMacro => new NodeInspectorPolicy(),

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

    private static bool HasToggleKey(MacroNode node)
    {
        if (ToggleKeyService.IsToggleKey(node.VirtualKey))
            return true;

        return node.IsSyntheticDisplayNode &&
               node.SourceNodes.Any(source => ToggleKeyService.IsToggleKey(source.VirtualKey));
    }
}


