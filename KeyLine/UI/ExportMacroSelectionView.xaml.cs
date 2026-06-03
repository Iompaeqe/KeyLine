using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KeyLine.Domain;

namespace KeyLine;

public partial class ExportMacroSelectionView : UserControl
{
    private readonly Action<IReadOnlyList<object>> _exportRequested;
    private readonly Action _cancelRequested;
    private readonly HashSet<MacroWorkspace> _selectedWorkspaces = new();
    private bool _usesGroupedMacros;

    public ExportMacroSelectionView(
        IReadOnlyList<MacroWorkspace> workspaces,
        IReadOnlyList<MacroProfile> profiles,
        MacroWorkspace activeWorkspace,
        Action<IReadOnlyList<MacroWorkspace>> exportRequested,
        Action cancelRequested)
    {
        _exportRequested = selected => exportRequested(selected.OfType<MacroWorkspace>().ToList());
        _cancelRequested = cancelRequested;
        _selectedWorkspaces.Add(activeWorkspace);
        _usesGroupedMacros = true;

        InitializeComponent();

        TitleTextBlock.Text = "Export macros";
        MacroListBox.Visibility = Visibility.Collapsed;
        GroupedMacroScrollViewer.Visibility = Visibility.Visible;
        RenderGroupedMacros(workspaces, profiles);
    }

    public ExportMacroSelectionView(
        IReadOnlyList<MacroWorkspace> workspaces,
        MacroWorkspace activeWorkspace,
        Action<IReadOnlyList<MacroWorkspace>> exportRequested,
        Action cancelRequested)
        : this(
            workspaces,
            Array.Empty<MacroProfile>(),
            activeWorkspace,
            exportRequested,
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

    private void RenderGroupedMacros(
        IReadOnlyList<MacroWorkspace> workspaces,
        IReadOnlyList<MacroProfile> profiles)
    {
        GroupedMacroPanel.Children.Clear();

        foreach (var group in BuildProfileGroups(workspaces, profiles))
            GroupedMacroPanel.Children.Add(CreateProfileGroup(group));
    }

    private Expander CreateProfileGroup(ProfileMacroGroup group)
    {
        var expander = new Expander
        {
            IsExpanded = true,
            Margin = new Thickness(0, 0, 0, 5),
            Foreground = new SolidColorBrush(Color.FromRgb(229, 238, 248)),
            Header = CreateProfileHeader(group)
        };

        var panel = new StackPanel { Margin = new Thickness(8, 4, 0, 2) };
        foreach (var workspace in group.Workspaces)
            panel.Children.Add(CreateMacroCheckBox(workspace));

        expander.Content = panel;
        return expander;
    }

    private static TextBlock CreateProfileHeader(ProfileMacroGroup group)
    {
        return new TextBlock
        {
            Text = $"{group.Name} ({group.Workspaces.Count})",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(186, 230, 253))
        };
    }

    private CheckBox CreateMacroCheckBox(MacroWorkspace workspace)
    {
        var checkBox = new CheckBox
        {
            Content = workspace.Name,
            IsChecked = _selectedWorkspaces.Contains(workspace),
            Margin = new Thickness(0, 0, 0, 4),
            Foreground = new SolidColorBrush(Color.FromRgb(229, 238, 248)),
            FontSize = 12
        };

        checkBox.Checked += (_, _) => _selectedWorkspaces.Add(workspace);
        checkBox.Unchecked += (_, _) => _selectedWorkspaces.Remove(workspace);

        return checkBox;
    }

    private static IReadOnlyList<ProfileMacroGroup> BuildProfileGroups(
        IReadOnlyList<MacroWorkspace> workspaces,
        IReadOnlyList<MacroProfile> profiles)
    {
        var result = new List<ProfileMacroGroup>();

        var noProfileWorkspaces = workspaces
            .Where(workspace => MacroProfile.IsNoProfile(MacroProfile.NormalizeId(workspace.ProfileId)))
            .ToList();
        if (noProfileWorkspaces.Count > 0)
            result.Add(new ProfileMacroGroup(MacroProfile.NoProfileName, noProfileWorkspaces));

        foreach (var profile in profiles)
        {
            var profileId = MacroProfile.NormalizeId(profile.Id);
            if (MacroProfile.IsNoProfile(profileId))
                continue;

            var profileWorkspaces = workspaces
                .Where(workspace => string.Equals(
                    MacroProfile.NormalizeId(workspace.ProfileId),
                    profileId,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (profileWorkspaces.Count == 0)
                continue;

            result.Add(new ProfileMacroGroup(
                string.IsNullOrWhiteSpace(profile.Name) ? "Profile" : profile.Name,
                profileWorkspaces));
        }

        var knownProfileIds = profiles
            .Select(profile => MacroProfile.NormalizeId(profile.Id))
            .Where(id => !MacroProfile.IsNoProfile(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var orphanedWorkspaces = workspaces
            .Where(workspace =>
            {
                var profileId = MacroProfile.NormalizeId(workspace.ProfileId);
                return !MacroProfile.IsNoProfile(profileId) && !knownProfileIds.Contains(profileId);
            })
            .ToList();
        if (orphanedWorkspaces.Count > 0)
            result.Add(new ProfileMacroGroup("Unknown Profile", orphanedWorkspaces));

        return result;
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_usesGroupedMacros)
        {
            _exportRequested(_selectedWorkspaces.Cast<object>().ToList());
            return;
        }

        _exportRequested(MacroListBox.SelectedItems.Cast<object>().ToList());
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cancelRequested();
    }

    private sealed record ProfileMacroGroup(string Name, IReadOnlyList<MacroWorkspace> Workspaces);
}
