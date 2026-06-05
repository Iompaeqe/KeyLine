using KeyLine.Domain;
using KeyLine.Services.Input;
using KeyLine.UI.Config;
using System.IO;

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
            MacroNodeType.MouseScrollUp => "Mouse Scroll Up",
            MacroNodeType.MouseScrollDown => "Mouse Scroll Down",
            MacroNodeType.MouseScrollLeft => "Mouse Scroll Left",
            MacroNodeType.MouseScrollRight => "Mouse Scroll Right",
            MacroNodeType.SystemOpenLaunch => "Open/Launch",
            MacroNodeType.SystemVolumeControl => "Volume Control",
            MacroNodeType.SystemWaitUntilWindowOpens => "Wait Until Window Opens",
            MacroNodeType.SystemSelectTargetWindow => "Set Target Window",
            MacroNodeType.SystemFocusWindow => "Focus Window",
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

    public static string GetSystemLaunchActionText(MacroNode node) =>
        node.SystemLaunchKind switch
        {
            SystemLaunchKind.File => "OPEN FILE",
            SystemLaunchKind.Folder => "OPEN FOLDER",
            SystemLaunchKind.Url => "OPEN URL",
            _ => "LAUNCH APP"
        };

    public static string GetSystemLaunchKindText(SystemLaunchKind kind) =>
        kind switch
        {
            SystemLaunchKind.Application => "Application",
            SystemLaunchKind.File => "File",
            SystemLaunchKind.Folder => "Folder",
            SystemLaunchKind.Url => "URL",
            _ => kind.ToString()
        };

    public static string GetSystemLaunchTargetSummary(MacroNode node)
    {
        var target = node.SystemLaunchTarget?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(target))
            return "set target";

        if (node.SystemLaunchKind == SystemLaunchKind.Url &&
            Uri.TryCreate(target, UriKind.Absolute, out var uri) &&
            !string.IsNullOrWhiteSpace(uri.Host))
        {
            return uri.Host;
        }

        try
        {
            if (node.SystemLaunchKind is SystemLaunchKind.Application or SystemLaunchKind.File)
            {
                var fileName = Path.GetFileName(target);
                if (!string.IsNullOrWhiteSpace(fileName))
                    return fileName;
            }

            if (node.SystemLaunchKind == SystemLaunchKind.Folder)
            {
                var folderName = new DirectoryInfo(target).Name;
                if (!string.IsNullOrWhiteSpace(folderName))
                    return folderName;
            }
        }
        catch
        {
        }

        return target;
    }

    public static string GetSystemVolumeActionText(MacroNode node) =>
        node.SystemVolumeAction switch
        {
            SystemVolumeAction.VolumeDown => "VOL DOWN",
            SystemVolumeAction.MuteToggle => "MUTE TOG",
            SystemVolumeAction.Mute => "MUTE",
            SystemVolumeAction.Unmute => "UNMUTE",
            SystemVolumeAction.SetVolumePercent => "SET VOL",
            _ => "VOL UP"
        };

    public static string GetSystemVolumeActionLabel(SystemVolumeAction action) =>
        action switch
        {
            SystemVolumeAction.VolumeUp => "Volume Up",
            SystemVolumeAction.VolumeDown => "Volume Down",
            SystemVolumeAction.MuteToggle => "Mute Toggle",
            SystemVolumeAction.Mute => "Mute",
            SystemVolumeAction.Unmute => "Unmute",
            SystemVolumeAction.SetVolumePercent => "Set Volume %",
            _ => action.ToString()
        };

    public static string GetSystemVolumeDetailText(MacroNode node) =>
        node.SystemVolumeAction == SystemVolumeAction.SetVolumePercent
            ? $"{Math.Clamp(node.SystemVolumePercent, 0, 100)}%"
            : "system volume";

    public static string GetSystemWindowWaitActionText(MacroNode node) => "WAIT WINDOW";

    public static string GetSystemWindowWaitDetailText(MacroNode node) =>
        GetWindowReferenceSummary(node);

    public static string GetSystemFocusWindowActionText(MacroNode node) => "FOCUS WINDOW";

    public static string GetSystemFocusWindowDetailText(MacroNode node) =>
        GetWindowReferenceSummary(node);

    public static string GetSystemTargetWindowActionText(MacroNode node) => "SET TARGET";

    public static string GetSystemTargetWindowDetailText(MacroNode node) =>
        GetWindowReferenceSummary(node);

    public static string GetWindowReferenceSummary(MacroNode node)
    {
        var reference = node.GetEffectiveWindowReference();
        return reference.Type switch
        {
            WindowReferenceType.SelectedTarget => "Selected Target Window",
            WindowReferenceType.FocusedWindow => "Focused Window",
            WindowReferenceType.LastLaunchedWindow => "Last Launched Window",
            WindowReferenceType.LastFoundWindow => "Last Found Window",
            WindowReferenceType.CustomTitle => GetCustomWindowTitleSummary(reference.CustomTitle),
            _ => "Window"
        };
    }

    private static string GetCustomWindowTitleSummary(string title)
    {
        title = title?.Trim() ?? "";
        return string.IsNullOrWhiteSpace(title)
            ? "Custom title"
            : $"Custom \"{title}\"";
    }

    public static string GetSystemNodeTooltip(MacroNode node)
    {
        return node.Type switch
        {
            MacroNodeType.SystemOpenLaunch =>
                $"{GetSystemLaunchKindText(node.SystemLaunchKind)}: {GetSystemLaunchTargetSummary(node)}",
            MacroNodeType.SystemVolumeControl =>
                node.SystemVolumeAction == SystemVolumeAction.SetVolumePercent
                    ? $"Set system volume to {Math.Clamp(node.SystemVolumePercent, 0, 100)}%"
                    : GetSystemVolumeActionLabel(node.SystemVolumeAction),
            MacroNodeType.SystemWaitUntilWindowOpens =>
                $"Wait for: {GetSystemWindowWaitDetailText(node)}",
            MacroNodeType.SystemFocusWindow =>
                $"Focus: {GetSystemFocusWindowDetailText(node)}",
            MacroNodeType.SystemSelectTargetWindow =>
                $"Set target: {GetSystemTargetWindowDetailText(node)}",
            _ => GetNodeTypeText(node)
        };
    }
}
