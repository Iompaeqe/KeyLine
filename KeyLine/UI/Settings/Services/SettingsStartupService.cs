using System;
using Microsoft.Win32;

namespace KeyLine;

internal sealed class SettingsStartupService
{
    public void SetLaunchOnStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run",
            writable: true);

        if (key == null)
            return;

        if (enabled)
            key.SetValue("KeyLine", $"\"{Environment.ProcessPath}\"");
        else
            key.DeleteValue("KeyLine", throwOnMissingValue: false);
    }
}
