namespace KeyLine.Services.Macro;

public sealed class MacroFeatureValidationResult
{
    public bool CanRun => Errors.Count == 0;
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
}
