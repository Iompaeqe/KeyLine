using MacroSpammer.Domain;
using MacroSpammer.UI.Config;

namespace MacroSpammer.UI.Timeline;

public static class StepDisplayFormatter
{
    public static string GetKeyText(MacroStep step)
    {
        return step.KeyName;
    }

    public static bool IsComboKey(MacroStep step)
    {
        return IsComboKey(GetKeyText(step));
    }

    public static bool IsComboKey(string keyText)
    {
        return keyText.Contains('+');
    }

    public static string GetTextPreview(MacroStep step)
    {
        return GetTextPreview(step.Text);
    }

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