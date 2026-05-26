using System.Configuration;
using System.Data;
using System.Windows;
using MacroSpammer.Interop;

namespace MacroSpammer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        base.OnStartup(e);
    }
}
