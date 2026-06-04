using KeyLine.Domain;
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
            _ => node.Type.ToString()
        };
    }
}
