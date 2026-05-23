using KeySpammer.Domain;

namespace KeySpammer.Services.Timeline;

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
            if (step.Type == MacroStepType.Delay)
            {
                // Standard delay mode hides delays visually, but if a delay is inside
                // an active key group, keep it attached so drag/drop moves the real raw sequence.
                if (comboSourceSteps.Count > 0)
                    comboSourceSteps.Add(step);

                continue;
            }

            if (step.Type == MacroStepType.Text)
            {
                FlushCombo();
                result.Add(step);
                continue;
            }

            if (step.Type == MacroStepType.KeyDown)
            {
                activeKeys.Add(step);
                comboKeyDowns.Add(step);
                comboSourceSteps.Add(step);
                continue;
            }

            if (step.Type == MacroStepType.KeyUp)
            {
                if (comboSourceSteps.Count == 0)
                {
                    // Stray keyup. Should not normally happen, but don't lose it.
                    result.Add(step);
                    continue;
                }

                comboSourceSteps.Add(step);

                var activeIndex = activeKeys.FindIndex(k => k.VirtualKey == step.VirtualKey);
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
                Type = MacroStepType.KeyDown,
                KeyName = string.Join("+", comboKeyDowns.Select(k => k.KeyName)),
                VirtualKey = comboKeyDowns[0].VirtualKey,
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
}