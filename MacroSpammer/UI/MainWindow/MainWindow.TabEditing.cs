using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MacroSpammer.Domain;

namespace MacroSpammer;

public partial class MainWindow
{
    private void DeleteWorkspace(MacroWorkspace workspace)
    {
        if (_workspaces.Count <= 1)
            return;

        var index = _workspaces.IndexOf(workspace);
        if (index < 0)
            return;

        if (IsWorkspaceRunning(workspace))
        {
            _pendingDeleteWorkspace = null;
            RefreshMacroTabs();
            StatusText.Text = "Stop this macro before deleting it";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(253, 230, 138));
            return;
        }

        if (_recorder.IsRecording)
            StopRecording();

        _workspaces.RemoveAt(index);
        _pendingDeleteWorkspace = null;

        if (_activeWorkspaceIndex > index)
            _activeWorkspaceIndex--;
        else if (_activeWorkspaceIndex >= _workspaces.Count)
            _activeWorkspaceIndex = _workspaces.Count - 1;

        ActivateWorkspace(_activeWorkspaceIndex, false);
        ScheduleSaveState();
    }

    private void BeginOrConfirmWorkspaceDelete(MacroWorkspace workspace)
    {
        if (_workspaces.Count <= 1)
            return;

        if (IsWorkspaceRunning(workspace))
        {
            _pendingDeleteWorkspace = null;
            RefreshMacroTabs();
            StatusText.Text = "Stop this macro before deleting it";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(253, 230, 138));
            return;
        }

        if (ReferenceEquals(_pendingDeleteWorkspace, workspace))
        {
            DeleteWorkspace(workspace);
            return;
        }

        _pendingDeleteWorkspace = workspace;
        _renamingWorkspace = null;
        RefreshMacroTabs();
    }

    private TextBox CreateRenameTextBox(MacroWorkspace workspace, int index)
    {
        var textBox = new TextBox
        {
            Text = workspace.Name,
            Height = 22,
            MinWidth = 92,
            MaxWidth = 128,
            Width = 104,
            Padding = new Thickness(8, 1, 8, 1),
            Margin = new Thickness(index == 0 ? 0 : 4, 0, 0, 0),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Background = new SolidColorBrush(Color.FromRgb(10, 52, 84)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(14, 165, 233)),
            Foreground = new SolidColorBrush(Color.FromRgb(224, 242, 254)),
            Tag = workspace
        };

        textBox.Loaded += (_, _) =>
        {
            textBox.Focus();
            textBox.SelectAll();
        };

        textBox.LostKeyboardFocus += (_, _) => CommitWorkspaceRename(workspace, textBox.Text);
        textBox.KeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Key.Enter:
                    CommitWorkspaceRename(workspace, textBox.Text);
                    e.Handled = true;
                    break;

                case Key.Escape:
                    CancelWorkspaceRename();
                    e.Handled = true;
                    break;
            }
        };

        return textBox;
    }

    private void BeginWorkspaceRename(MacroWorkspace workspace)
    {
        if (IsWorkspaceRunning(workspace))
        {
            StatusText.Text = "Stop this macro before renaming it";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(253, 230, 138));
            return;
        }

        _pendingDeleteWorkspace = null;
        _renamingWorkspace = workspace;
        RefreshMacroTabs();
    }

    private void CommitWorkspaceRename(MacroWorkspace workspace, string name)
    {
        if (!ReferenceEquals(_renamingWorkspace, workspace))
            return;

        var normalizedName = NormalizeWorkspaceName(name);
        if (!string.IsNullOrWhiteSpace(normalizedName))
            workspace.Name = normalizedName;

        _renamingWorkspace = null;
        RefreshMacroTabs();
        ScheduleSaveState();
    }

    private void CancelWorkspaceRename()
    {
        if (_renamingWorkspace == null)
            return;

        _renamingWorkspace = null;
        RefreshMacroTabs();
    }

    private static string NormalizeWorkspaceName(string name)
    {
        var normalized = string.Join(
            " ",
            name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return normalized.Length <= 24
            ? normalized
            : normalized[..24];
    }
}
