using MacroSpammer.Domain;
using MacroSpammer.UI.Config;

namespace MacroSpammer.UI.Timeline;

public static class StepDisplayFormatter
{
    public static string GetKeyText(MacroStep step)
    {
        if (step.IsSyntheticDisplayStep && !string.IsNullOrWhiteSpace(step.KeyName))
            return step.KeyName;

        if (step.Type is MacroStepType.ForegroundMouseDown or MacroStepType.ForegroundMouseUp)
            return $"M{NormalizeMouseButton(step.MouseButton)}";

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
