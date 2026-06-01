using MacroSpammer.Domain;
using MacroSpammer.UI.Config;

namespace MacroSpammer.UI.Timeline;

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

    public static bool IsComboKey(MacroNode node)
    {
        return IsComboKey(GetKeyText(node));
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
}
