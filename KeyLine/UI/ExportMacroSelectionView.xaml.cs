using System.Windows;
using System.Windows.Controls;
using KeyLine.Domain;

namespace KeyLine;

public partial class ExportMacroSelectionView : UserControl
{
    private readonly Action<IReadOnlyList<MacroWorkspace>> _exportRequested;
    private readonly Action _cancelRequested;

    public ExportMacroSelectionView(
        IReadOnlyList<MacroWorkspace> workspaces,
        MacroWorkspace activeWorkspace,
        Action<IReadOnlyList<MacroWorkspace>> exportRequested,
        Action cancelRequested)
    {
        _exportRequested = exportRequested;
        _cancelRequested = cancelRequested;

        InitializeComponent();

        MacroListBox.ItemsSource = workspaces;
        MacroListBox.SelectedItems.Add(activeWorkspace);
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        _exportRequested(MacroListBox.SelectedItems.OfType<MacroWorkspace>().ToList());
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cancelRequested();
    }
}
