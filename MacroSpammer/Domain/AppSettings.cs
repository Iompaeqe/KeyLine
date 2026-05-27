namespace MacroSpammer.Domain;

public sealed class AppSettings
{
    public bool LaunchOnWindowsStartup { get; set; }
    public bool StartMinimized { get; set; }
    public bool MinimizeToTray { get; set; }
    public bool CloseToTray { get; set; }
    public bool ConfirmCloseWhileMacrosRunning { get; set; } = true;

    public bool DefaultStandardDelayEnabled { get; set; }
    public int DefaultStandardDelayMs { get; set; } = 50;
    public bool DefaultShowKeyUpDown { get; set; } = true;
    public int DefaultBaseDelayMs { get; set; } = 50;
    public int DefaultTimerMs { get; set; }
    public int DefaultLoopCount { get; set; }
    public bool DefaultTextInputMode { get; set; }

    public string UndoShortcut { get; set; } = "17,90";
    public string RedoShortcut { get; set; } = "17,16,90";
    public string SelectAllShortcut { get; set; } = "17,65";
    public string CopyShortcut { get; set; } = "17,67";
    public string PasteShortcut { get; set; } = "17,86";
    public string DuplicateShortcut { get; set; } = "17,68";

    public bool MergeRepeatedDelayNodes { get; set; }

    public bool PlaySoundOnMacroStartStop { get; set; }
    public string PlaybackSoundName { get; set; } = "Beep";
    public string EmergencyStopShortcut { get; set; } = "17,18,27";
    public string StopAllMacrosShortcut { get; set; } = "";
    public string PauseResumeAllMacrosShortcut { get; set; } = "";

    public bool ExperimentalFeaturesEnabled { get; set; }

    public AppSettings Clone()
    {
        return (AppSettings)MemberwiseClone();
    }

    public void CopyFrom(AppSettings source)
    {
        LaunchOnWindowsStartup = source.LaunchOnWindowsStartup;
        StartMinimized = source.StartMinimized;
        MinimizeToTray = source.MinimizeToTray;
        CloseToTray = source.CloseToTray;
        ConfirmCloseWhileMacrosRunning = source.ConfirmCloseWhileMacrosRunning;
        DefaultStandardDelayEnabled = source.DefaultStandardDelayEnabled;
        DefaultStandardDelayMs = source.DefaultStandardDelayMs;
        DefaultShowKeyUpDown = source.DefaultShowKeyUpDown;
        DefaultBaseDelayMs = source.DefaultBaseDelayMs;
        DefaultTimerMs = source.DefaultTimerMs;
        DefaultLoopCount = source.DefaultLoopCount;
        DefaultTextInputMode = source.DefaultTextInputMode;
        UndoShortcut = source.UndoShortcut;
        RedoShortcut = source.RedoShortcut;
        SelectAllShortcut = source.SelectAllShortcut;
        CopyShortcut = source.CopyShortcut;
        PasteShortcut = source.PasteShortcut;
        DuplicateShortcut = source.DuplicateShortcut;
        MergeRepeatedDelayNodes = source.MergeRepeatedDelayNodes;
        PlaySoundOnMacroStartStop = source.PlaySoundOnMacroStartStop;
        PlaybackSoundName = source.PlaybackSoundName;
        EmergencyStopShortcut = source.EmergencyStopShortcut;
        StopAllMacrosShortcut = source.StopAllMacrosShortcut;
        PauseResumeAllMacrosShortcut = source.PauseResumeAllMacrosShortcut;
        ExperimentalFeaturesEnabled = source.ExperimentalFeaturesEnabled;
    }
}
