using KeyLine.Domain;
using KeyLine.Services.Input;
using KeyLine.UI.Config;

namespace KeyLine.UI.Timeline;

public static class NodeDisplayFormatter
{
    public static string GetKeyText(MacroNode node)
    {
        if (node.IsSyntheticDisplayNode && !string.IsNullOrWhiteSpace(node.KeyName))
            return node.KeyName;

        if (node.Type is MacroNodeType.MouseDown or MacroNodeType.MouseUp)
            return $"M{NormalizeMouseButton(node.MouseButton)}";

        return node.KeyName;
    }

    public static bool IsComboKey(string keyText)
    {
        return keyText.Contains('+');
    }

    public static string GetTextPreview(MacroNode node)
    {
        return GetTextPreview(node.Text);
    }

    private static int NormalizeMouseButton(int mouseButton) =>
        Math.Clamp(mouseButton <= 0 ? 1 : mouseButton, 1, 5);

    public static string GetTextPreview(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "TXT";

        var ui = GeneratedUiConfig.TextStep;
        var normalizedText = text.Replace("\r", " ").Replace("\n", " ");

        return normalizedText.Length <= ui.MaxPreviewCharacters
            ? normalizedText
            : normalizedText[..ui.MaxPreviewCharacters] + "…";
    }

    public static string GetNodeTypeText(MacroNode node)
    {
        return node.Type switch
        {
            MacroNodeType.KeyDown => "Key Down",
            MacroNodeType.KeyUp => "Key Up",
            MacroNodeType.Delay => "Delay",
            MacroNodeType.RandomDelay => "Random Delay",
            MacroNodeType.Text => "Text",
            MacroNodeType.MouseClick => "Mouse Click",
            MacroNodeType.MouseDown => "Mouse Down",
            MacroNodeType.MouseUp => "Mouse Up",
            MacroNodeType.CursorMove => "Move Cursor",
            MacroNodeType.BackgroundMouseDown => "BG Mouse Down",
            MacroNodeType.BackgroundMouseUp => "BG Mouse Up",
            MacroNodeType.BackgroundMouseClick => "BG Mouse Click",
            MacroNodeType.RepeatStart => "Repeat Start",
            MacroNodeType.RepeatEnd => "Repeat End",
            MacroNodeType.ConditionStart => "Condition Start",
            MacroNodeType.ConditionEnd => "Condition End",
            _ => node.Type.ToString()
        };
    }

    public static string GetBlockTimelineLabel(MacroNode startNode)
    {
        return startNode.Type switch
        {
            MacroNodeType.RepeatStart => $"Repeat \u00d7{Math.Max(1, startNode.RepeatCount)}",
            MacroNodeType.ConditionStart => GetConditionSummary(startNode),
            _ => GetNodeTypeText(startNode)
        };
    }

    public static string GetConditionSummary(MacroNode node)
    {
        return node.ConditionType switch
        {
            MacroConditionType.KeyState => GetKeyStateConditionSummary(node),
            MacroConditionType.PixelColor => "If Pixel Matches",
            MacroConditionType.RandomChance => $"If Random {Math.Clamp(node.ConditionChancePercent, 0, 100)}%",
            MacroConditionType.LoopContext => GetLoopConditionSummary(node),
            _ => "If Condition"
        };
    }

    public static string GetConditionTypeText(MacroConditionType type)
    {
        return type switch
        {
            MacroConditionType.KeyState => "Key held",
            MacroConditionType.PixelColor => "Pixel matches",
            MacroConditionType.RandomChance => "Random chance",
            MacroConditionType.LoopContext => "Loop context",
            _ => type.ToString()
        };
    }

    public static string GetConditionLoopModeText(MacroConditionLoopMode mode)
    {
        return mode switch
        {
            MacroConditionLoopMode.FirstLoop => "First timeline loop",
            MacroConditionLoopMode.LastLoop => "Last timeline loop",
            MacroConditionLoopMode.EveryNLoops => "Every N timeline loops",
            MacroConditionLoopMode.FirstRepeat => "First repeat iteration",
            MacroConditionLoopMode.LastRepeat => "Last repeat iteration",
            MacroConditionLoopMode.EveryNRepeats => "Every N repeat iterations",
            _ => mode.ToString()
        };
    }

    private static string GetLoopConditionSummary(MacroNode node)
    {
        var interval = Math.Max(1, node.ConditionLoopInterval);
        return node.ConditionLoopMode switch
        {
            MacroConditionLoopMode.FirstLoop => "If First Loop",
            MacroConditionLoopMode.LastLoop => "If Last Loop",
            MacroConditionLoopMode.EveryNLoops => $"If Every {interval} Loops",
            MacroConditionLoopMode.FirstRepeat => "If First Repeat",
            MacroConditionLoopMode.LastRepeat => "If Last Repeat",
            MacroConditionLoopMode.EveryNRepeats => $"If Every {interval} Repeats",
            _ => "If Loop Context"
        };
    }

    private static string GetKeyStateConditionSummary(MacroNode node)
    {
        var inputText = ConditionInputGesture.Format(node);
        return string.Equals(inputText, "no input", StringComparison.Ordinal)
            ? "If Input Held"
            : $"If {inputText} Held";
    }
}
