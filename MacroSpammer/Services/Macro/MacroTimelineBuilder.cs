using MacroSpammer.Domain;

namespace MacroSpammer.Services.Macro;

public static class MacroTimelineBuilder
{
    public static List<MacroStep> BuildVisibleSteps(
        IReadOnlyList<MacroStep> rawSteps,
        bool useStandardDelay,
        bool showKeyUpDown)
    {
        if (!useStandardDelay)
            return rawSteps.ToList();

        if (showKeyUpDown)
        {
            return rawSteps
                .Where(step => step.Type != MacroStepType.Delay)
                .ToList();
        }

        return BuildCombinationSteps(rawSteps);
    }

    private static List<MacroStep> BuildCombinationSteps(IReadOnlyList<MacroStep> rawSteps)
    {
        var result = new List<MacroStep>();

        var activeKeys = new List<MacroStep>();

        // KeyDown steps used only for the visible name: A+B+C
        var comboKeyDowns = new List<MacroStep>();

        // Full raw group: keydowns, keyups, and hidden recorded delays between them.
        var comboSourceSteps = new List<MacroStep>();

        foreach (var step in rawSteps)
        {
            if (step.Type is MacroStepType.Delay or MacroStepType.RandomDelay)
            {
                // Standard delay mode hides delays visually, but if a delay is inside
                // an active key group, keep it attached so drag/drop moves the real raw sequence.
                if (comboSourceSteps.Count > 0)
                    comboSourceSteps.Add(step);

                continue;
            }

            if (step.Type is MacroStepType.Text
                or MacroStepType.ForegroundMouseClick
                or MacroStepType.CursorMove
                or MacroStepType.MouseDown
                or MacroStepType.MouseUp
                or MacroStepType.MouseClick)
            {
                FlushCombo();
                result.Add(step);
                continue;
            }

            if (IsKeyLikeDown(step))
            {
                activeKeys.Add(step);
                comboKeyDowns.Add(step);
                comboSourceSteps.Add(step);
                continue;
            }

            if (IsKeyLikeUp(step))
            {
                if (comboSourceSteps.Count == 0)
                {
                    // Stray keyup. Should not normally happen, but don't lose it.
                    result.Add(step);
                    continue;
                }

                comboSourceSteps.Add(step);

                var activeIndex = activeKeys.FindIndex(k => HasSameKeyLikeIdentity(k, step));
                if (activeIndex >= 0)
                    activeKeys.RemoveAt(activeIndex);

                if (activeKeys.Count == 0)
                    FlushCombo();
            }
        }

        FlushCombo();
        return result;

        void FlushCombo()
        {
            if (comboSourceSteps.Count == 0)
                return;

            if (comboKeyDowns.Count == 0)
            {
                comboSourceSteps.Clear();
                activeKeys.Clear();
                return;
            }

            result.Add(new MacroStep
            {
                Type = comboKeyDowns.All(IsForegroundMouseStep) ? MacroStepType.ForegroundMouseDown : MacroStepType.KeyDown,
                KeyName = string.Join("+", comboKeyDowns.Select(GetKeyLikeName)),
                VirtualKey = comboKeyDowns[0].VirtualKey,
                MouseButton = comboKeyDowns.FirstOrDefault(IsForegroundMouseStep)?.MouseButton ?? 1,
                IsSyntheticDisplayStep = true,

                // IMPORTANT:
                // This must include the full raw group, not only KeyDowns.
                SourceSteps = comboSourceSteps.ToList()
            });

            comboKeyDowns.Clear();
            comboSourceSteps.Clear();
            activeKeys.Clear();
        }
    }

    private static bool IsKeyLikeDown(MacroStep step) =>
        step.Type is MacroStepType.KeyDown or MacroStepType.ForegroundMouseDown;

    private static bool IsKeyLikeUp(MacroStep step) =>
        step.Type is MacroStepType.KeyUp or MacroStepType.ForegroundMouseUp;

    private static bool IsForegroundMouseStep(MacroStep step) =>
        step.Type is MacroStepType.ForegroundMouseDown or MacroStepType.ForegroundMouseUp;

    private static bool HasSameKeyLikeIdentity(MacroStep downStep, MacroStep upStep)
    {
        if (IsForegroundMouseStep(downStep) || IsForegroundMouseStep(upStep))
        {
            return IsForegroundMouseStep(downStep) &&
                   IsForegroundMouseStep(upStep) &&
                   NormalizeMouseButton(downStep.MouseButton) == NormalizeMouseButton(upStep.MouseButton);
        }

        return downStep.VirtualKey == upStep.VirtualKey;
    }

    private static string GetKeyLikeName(MacroStep step) =>
        IsForegroundMouseStep(step)
            ? $"M{NormalizeMouseButton(step.MouseButton)}"
            : step.KeyName;

    private static int NormalizeMouseButton(int mouseButton) =>
        Math.Clamp(mouseButton <= 0 ? 1 : mouseButton, 1, 5);
}
