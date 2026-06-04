namespace KeyLine.Services.Features;

public sealed class FeatureConfig
{
    public FeatureState Profiles { get; init; } = FeatureState.Enabled;
    public FeatureState RepeatBlocks { get; init; } = FeatureState.Enabled;
    public FeatureState ConditionBlocks { get; init; } = FeatureState.Enabled;
}
