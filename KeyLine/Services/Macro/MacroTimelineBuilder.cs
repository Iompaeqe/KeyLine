using KeyLine.Domain;

namespace KeyLine.Services.Macro;

public static class MacroTimelineBuilder
{
    public static List<MacroNode> BuildVisibleSteps(
        IReadOnlyList<MacroNode> rawSteps,
        bool useStandardDelay,
        bool showKeyUpDown)
    {
        if (!useStandardDelay)
            return rawSteps.ToList();

        if (showKeyUpDown)
        {
            return rawSteps
                .Where(step => step.Type is not (MacroNodeType.Delay or MacroNodeType.RandomDelay))
                .ToList();
        }

        return BuildCombinationSteps(rawSteps);
    }

    private static List<MacroNode> BuildCombinationSteps(IReadOnlyList<MacroNode> rawSteps)
    {
        var result = new List<MacroNode>();

        var activeKeys = new List<MacroNode>();

        // KeyDown steps used only for the visible name: A+B+C
        var comboKeyDowns = new List<MacroNode>();

        // Full raw group: keydowns, keyups, and hidden recorded delays between them.
        var comboSourceSteps = new List<MacroNode>();

        foreach (var step in rawSteps)
        {
            if (step.Type is MacroNodeType.Delay or MacroNodeType.RandomDelay)
            {
                // Standard delay mode hides delays visually, but if a delay is inside
                // an active key group, keep it attached so drag/drop moves the real raw sequence.
                if (comboSourceSteps.Count > 0)
                    comboSourceSteps.Add(step);

                continue;
            }

            if (step.Type is MacroNodeType.Text
                or MacroNodeType.MouseClick
                or MacroNodeType.MouseScrollUp
                or MacroNodeType.MouseScrollDown
                or MacroNodeType.MouseScrollLeft
                or MacroNodeType.MouseScrollRight
                or MacroNodeType.CursorMove
                or MacroNodeType.BackgroundMouseDown
                or MacroNodeType.BackgroundMouseUp
                or MacroNodeType.BackgroundMouseClick
                or MacroNodeType.SystemOpenLaunch
                or MacroNodeType.SystemVolumeControl
                or MacroNodeType.SystemWaitUntilWindowOpens
                or MacroNodeType.SystemSelectTargetWindow
                or MacroNodeType.SystemFocusWindow
                or MacroNodeType.RunMacro
                or MacroNodeType.RepeatStart
                or MacroNodeType.RepeatEnd
                or MacroNodeType.ConditionStart
                or MacroNodeType.ConditionEnd)
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

            result.Add(new MacroNode
            {
                Type = comboKeyDowns.All(IsForegroundMouseStep) ? MacroNodeType.MouseDown : MacroNodeType.KeyDown,
                KeyName = string.Join("+", comboKeyDowns.Select(GetKeyLikeName)),
                VirtualKey = comboKeyDowns[0].VirtualKey,
                ToggleKeyMode = GetSyntheticToggleKeyMode(comboSourceSteps),
                MouseButton = comboKeyDowns.FirstOrDefault(IsForegroundMouseStep)?.MouseButton ?? 1,
                IsSyntheticDisplayNode = true,

                // IMPORTANT:
                // This must include the full raw group, not only KeyDowns.
                SourceNodes = comboSourceSteps.ToList()
            });

            comboKeyDowns.Clear();
            comboSourceSteps.Clear();
            activeKeys.Clear();
        }
    }

    private static bool IsKeyLikeDown(MacroNode node) =>
        node.Type is MacroNodeType.KeyDown or MacroNodeType.MouseDown;

    private static bool IsKeyLikeUp(MacroNode node) =>
        node.Type is MacroNodeType.KeyUp or MacroNodeType.MouseUp;

    private static bool IsForegroundMouseStep(MacroNode node) =>
        node.Type is MacroNodeType.MouseDown or MacroNodeType.MouseUp;

    private static bool HasSameKeyLikeIdentity(MacroNode downNode, MacroNode upNode)
    {
        if (IsForegroundMouseStep(downNode) || IsForegroundMouseStep(upNode))
        {
            return IsForegroundMouseStep(downNode) &&
                   IsForegroundMouseStep(upNode) &&
                   NormalizeMouseButton(downNode.MouseButton) == NormalizeMouseButton(upNode.MouseButton);
        }

        return downNode.VirtualKey == upNode.VirtualKey;
    }

    private static string GetKeyLikeName(MacroNode node) =>
        IsForegroundMouseStep(node)
            ? $"M{NormalizeMouseButton(node.MouseButton)}"
            : node.KeyName;

    private static ToggleKeyMode GetSyntheticToggleKeyMode(IEnumerable<MacroNode> sourceSteps)
    {
        return sourceSteps
            .Where(step => step.Type is MacroNodeType.KeyDown or MacroNodeType.KeyUp)
            .Select(step => step.ToggleKeyMode)
            .FirstOrDefault(mode => mode != ToggleKeyMode.Normal);
    }

    private static int NormalizeMouseButton(int mouseButton) =>
        Math.Clamp(mouseButton <= 0 ? 1 : mouseButton, 1, 5);
}

