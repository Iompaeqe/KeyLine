namespace KeyLine.Services.Features;

public sealed class FeatureGate
{
    private readonly FeatureConfig _config;

    public FeatureGate(FeatureConfig config)
    {
        _config = config;
    }

    public FeatureState GetState(FeatureId feature)
    {
        return feature switch
        {
            FeatureId.Profiles => _config.Profiles,
            FeatureId.RepeatBlocks => _config.RepeatBlocks,
            FeatureId.ConditionBlocks => _config.ConditionBlocks,
            FeatureId.ShortcutRemap => _config.ShortcutRemap,
            FeatureId.AutoWindow => _config.AutoWindow,
            FeatureId.SystemNodes => _config.SystemNodes,
            _ => FeatureState.DisabledHidden
        };
    }

    public bool IsVisible(FeatureId feature)
    {
        return GetState(feature) is FeatureState.Enabled or FeatureState.DisabledVisible;
    }

    public bool IsEnabled(FeatureId feature)
    {
        return GetState(feature) == FeatureState.Enabled;
    }

    public bool IsLocked(FeatureId feature)
    {
        return GetState(feature) == FeatureState.DisabledVisible;
    }

    public bool IsHidden(FeatureId feature)
    {
        return GetState(feature) == FeatureState.DisabledHidden;
    }

    public string GetLockedFeatureMessage(FeatureId feature)
    {
        return feature switch
        {
            FeatureId.Profiles => "Profiles are not available.",
            FeatureId.RepeatBlocks => "Repeat Blocks are not available.",
            FeatureId.ConditionBlocks => "Condition Blocks are not available.",
            FeatureId.ShortcutRemap => "Shortcut remap is not available.",
            FeatureId.AutoWindow => "Auto window targeting is not available.",
            FeatureId.SystemNodes => "System nodes are not available.",
            _ => "This feature is not available."
        };
    }
}
