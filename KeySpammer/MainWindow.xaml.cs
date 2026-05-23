using System.Collections.ObjectModel;
using System.Windows;
using KeySpammer.Domain;
using KeySpammer.Services.Playback;
using KeySpammer.Services.Recording;
using KeySpammer.State;

namespace KeySpammer;

public partial class MainWindow : Window
{
    private readonly MacroDocument _document = new();
    private readonly MacroRunner _runner = new();
    private readonly MacroRecorder _recorder = new();
    
    private readonly TimelineSelectionState _selection = new();
    private readonly TimelineDragState _drag = new();

    public MainWindow()
    {
        InitializeComponent();
        LoadWindows();
        RefreshTimeline();
    }
}