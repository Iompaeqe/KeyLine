using System.Windows;
using System.Windows.Controls;
using KeyLine.Domain;

namespace KeyLine;

public partial class ExportMacroSelectionView : UserControl
{
    private readonly Action<IReadOnlyList<object>> _exportRequested;
    private readonly Action _cancelRequested;

    public ExportMacroSelectionView(
        IReadOnlyList<MacroWorkspace> workspaces,
        MacroWorkspace activeWorkspace,
        Action<IReadOnlyList<MacroWorkspace>> exportRequested,
        Action cancelRequested)
        : this(
            "Export macros",
            workspaces.Cast<object>().ToList(),
            new object[] { activeWorkspace },
            selected => exportRequested(selected.OfType<MacroWorkspace>().ToList()),
            cancelRequested)
    {
    }

    public ExportMacroSelectionView(
        string title,
        IReadOnlyList<object> items,
        IReadOnlyList<object> selectedItems,
        Action<IReadOnlyList<object>> exportRequested,
        Action cancelRequested)
    {
        _exportRequested = exportRequested;
        _cancelRequested = cancelRequested;

        InitializeComponent();

        TitleTextBlock.Text = title;
        MacroListBox.ItemsSource = items;

        foreach (var selectedItem in selectedItems)
            MacroListBox.SelectedItems.Add(selectedItem);
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        _exportRequested(MacroListBox.SelectedItems.Cast<object>().ToList());
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cancelRequested();
    }
}
