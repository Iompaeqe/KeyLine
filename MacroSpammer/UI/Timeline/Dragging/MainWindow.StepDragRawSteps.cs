using MacroSpammer.Domain;

namespace MacroSpammer;

public partial class MainWindow
{
    private List<MacroStep> GetRawStepsForDisplayStep(IReadOnlyCollection<MacroStep> rawSteps, MacroStep step)
    {
        if (step.IsSyntheticDisplayStep)
        {
            return step.SourceSteps
                .Where(rawSteps.Contains)
                .ToList();
        }

        return rawSteps.Contains(step)
            ? new List<MacroStep> { step }
            : new List<MacroStep>();
    }

    private List<MacroStep> GetRawStepsForPreviewDisplayStep(MacroTimeline timeline, MacroStep step)
    {
        var rawSteps = GetRawStepsForDisplayStep(_stepDragPreviewRawSteps, step);
        if (!timeline.UseStandardDelay || step.Type is MacroStepType.Delay or MacroStepType.RandomDelay)
            return rawSteps;

        var firstIndex = rawSteps.Count > 0
            ? _stepDragPreviewRawSteps.IndexOf(rawSteps[0])
            : _stepDragPreviewRawSteps.IndexOf(step);

        if (firstIndex < 0)
            return rawSteps;

        var attachedDelays = GetContiguousPreviewDelayStepsBefore(firstIndex);
        if (attachedDelays.Count == 0)
            attachedDelays = GetContiguousPreviewDelayStepsAfter(firstIndex + Math.Max(0, rawSteps.Count - 1));

        foreach (var delayStep in attachedDelays)
        {
            if (!rawSteps.Contains(delayStep))
                rawSteps.Add(delayStep);
        }

        return rawSteps
            .Distinct()
            .OrderBy(stepItem => _stepDragPreviewRawSteps.IndexOf(stepItem))
            .ToList();
    }

    private List<MacroStep> GetRawStepsForOriginalDisplayStep(MacroTimeline timeline, MacroStep step)
    {
        var rawSteps = GetRawStepsForDisplayStep(_stepDragOriginalRawSteps, step);
        if (!timeline.UseStandardDelay || step.Type is MacroStepType.Delay or MacroStepType.RandomDelay)
            return rawSteps;

        var firstIndex = rawSteps.Count > 0
            ? _stepDragOriginalRawSteps.IndexOf(rawSteps[0])
            : _stepDragOriginalRawSteps.IndexOf(step);

        if (firstIndex < 0)
            return rawSteps;

        var attachedDelays = GetContiguousOriginalDelayStepsBefore(firstIndex);
        if (attachedDelays.Count == 0)
            attachedDelays = GetContiguousOriginalDelayStepsAfter(firstIndex + Math.Max(0, rawSteps.Count - 1));

        foreach (var delayStep in attachedDelays)
        {
            if (!rawSteps.Contains(delayStep))
                rawSteps.Add(delayStep);
        }

        return rawSteps
            .Distinct()
            .OrderBy(stepItem => _stepDragOriginalRawSteps.IndexOf(stepItem))
            .ToList();
    }

    private List<MacroStep> GetContiguousPreviewDelayStepsBefore(int stepIndex)
    {
        var result = new List<MacroStep>();

        for (var i = stepIndex - 1; i >= 0 && IsDelayCleanupStep(_stepDragPreviewRawSteps[i]); i--)
            result.Add(_stepDragPreviewRawSteps[i]);

        return result;
    }

    private List<MacroStep> GetContiguousPreviewDelayStepsAfter(int stepIndex)
    {
        var result = new List<MacroStep>();

        for (var i = stepIndex + 1; i < _stepDragPreviewRawSteps.Count && IsDelayCleanupStep(_stepDragPreviewRawSteps[i]); i++)
            result.Add(_stepDragPreviewRawSteps[i]);

        return result;
    }

    private List<MacroStep> GetContiguousOriginalDelayStepsBefore(int stepIndex)
    {
        var result = new List<MacroStep>();

        for (var i = stepIndex - 1; i >= 0 && IsDelayCleanupStep(_stepDragOriginalRawSteps[i]); i--)
            result.Add(_stepDragOriginalRawSteps[i]);

        return result;
    }

    private List<MacroStep> GetContiguousOriginalDelayStepsAfter(int stepIndex)
    {
        var result = new List<MacroStep>();

        for (var i = stepIndex + 1; i < _stepDragOriginalRawSteps.Count && IsDelayCleanupStep(_stepDragOriginalRawSteps[i]); i++)
            result.Add(_stepDragOriginalRawSteps[i]);

        return result;
    }
}
