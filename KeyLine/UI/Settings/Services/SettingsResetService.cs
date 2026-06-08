using System.Windows;
using KeyLine.Domain;

namespace KeyLine;

internal sealed class SettingsResetService
{
    private readonly SettingsControllerContext _context;
    private readonly SettingsStartupService _startupService;

    public SettingsResetService(SettingsControllerContext context, SettingsStartupService startupService)
    {
        _context = context;
        _startupService = startupService;
    }

    public void ResetDefaults()
    {
        if (MessageBox.Show(
                _context.Owner,
                "Reset default macro values?",
                "Reset defaults",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        _context.Settings.DefaultStandardDelayEnabled = false;
        _context.Settings.DefaultShowKeyUpDown = true;
        _context.Settings.DefaultStandardDelayMs = 50;
        _context.Settings.DefaultBaseDelayMs = 50;
        _context.Settings.DefaultTimerMs = 0;
        _context.Settings.DefaultLoopCount = 0;
        _context.Settings.DefaultLoopMode = MacroLoopMode.Async;
        _context.Settings.DefaultTextInputMode = false;

        _context.ApplySettings();
    }

    public void ResetSettings()
    {
        if (MessageBox.Show(
                _context.Owner,
                "Reset all settings? Macros will be kept.",
                "Reset settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        _context.Settings.CopyFrom(new AppSettings());
        _startupService.SetLaunchOnStartup(_context.Settings.LaunchOnWindowsStartup);
        _context.ApplySettings();
    }

    public void ResetAllSavedData()
    {
        if (MessageBox.Show(
                _context.Owner,
                "Reset all saved data? This removes settings and all macros. This cannot be undone.",
                "Reset all saved data",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        _context.StopAllRunners();

        _context.Workspaces.Clear();
        _context.ResetProfiles();
        _context.Settings.CopyFrom(new AppSettings());
        _startupService.SetLaunchOnStartup(_context.Settings.LaunchOnWindowsStartup);

        _context.Workspaces.Add(_context.CreateWorkspace(1));

        _context.ActivateWorkspace(0, false);
        _context.SaveStateNow();
    }
}
