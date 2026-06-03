using KeyLine.Domain;
using KeyLine.Services.Macro;
using KeyLine.UI.Profiles;

namespace KeyLine;

public partial class MainWindow
{
    private void InitializeProfileDropdown()
    {
        _profileDropdown = new ProfileDropdownController(
            selectorButton: ProfileSelectorButton,
            selectedProfileText: SelectedProfileTextBlock,
            popup: ProfilesPopup,
            scrollViewer: ProfileScrollViewer,
            profilesPanel: ProfileRowsPanel,
            dragOverlay: ProfileDragOverlay,
            addButton: AddProfileButton,
            dispatcher: Dispatcher,
            getProfiles: () => _profiles,
            getActiveProfileId: () => _activeProfileId,
            addProfile: AddProfile,
            activateProfile: ActivateProfile,
            renameProfile: RenameProfile,
            deleteProfile: DeleteProfile,
            reorderProfile: ReorderProfile);

        _profileDropdown.Refresh();
    }

    private void NormalizeProfileState()
    {
        var safeProfiles = new List<MacroProfile>();
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in _profiles.ToList())
        {
            var id = MacroProfile.NormalizeId(profile.Id);
            if (MacroProfile.IsNoProfile(id) || !usedIds.Add(id))
                continue;

            profile.Id = id;
            profile.Name = NormalizeProfileName(profile.Name, "Profile");
            safeProfiles.Add(profile);
        }

        _profiles.Clear();
        _profiles.AddRange(safeProfiles);

        var profileIds = _profiles
            .Select(profile => profile.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var workspace in _workspaces)
        {
            var profileId = MacroProfile.NormalizeId(workspace.ProfileId);
            workspace.ProfileId = MacroProfile.IsNoProfile(profileId) || profileIds.Contains(profileId)
                ? profileId
                : MacroProfile.NoProfileId;
        }

        _activeProfileId = MacroProfile.NormalizeId(_activeProfileId);
        if (!MacroProfile.IsNoProfile(_activeProfileId) && !profileIds.Contains(_activeProfileId))
            _activeProfileId = MacroProfile.NoProfileId;
    }

    private int ResolveInitialWorkspaceIndex(int preferredIndex)
    {
        if (preferredIndex >= 0 &&
            preferredIndex < _workspaces.Count &&
            IsWorkspaceInProfile(_workspaces[preferredIndex], _activeProfileId))
        {
            return preferredIndex;
        }

        var profileWorkspaceIndex = IndexOfFirstWorkspaceInProfile(_activeProfileId);
        if (profileWorkspaceIndex >= 0)
            return profileWorkspaceIndex;

        EnsureWorkspaceForProfile(_activeProfileId, _settings);
        return Math.Max(0, IndexOfFirstWorkspaceInProfile(_activeProfileId));
    }

    private void EnsureWorkspaceForProfile(string profileId, AppSettings settings)
    {
        profileId = MacroProfile.NormalizeId(profileId);
        if (_workspaces.Any(workspace => IsWorkspaceInProfile(workspace, profileId)))
            return;

        _workspaces.Add(CreateWorkspace(GetNextWorkspaceNumber(profileId), settings, profileId));
    }

    private MacroProfile AddProfile()
    {
        CaptureActiveWorkspaceState();

        var profile = new MacroProfile
        {
            Name = GetNextProfileName()
        };

        _profiles.Add(profile);
        _activeProfileId = profile.Id;

        var workspace = CreateWorkspace(GetNextWorkspaceNumber(profile.Id), _settings, profile.Id);
        _workspaces.Add(workspace);

        ActivateWorkspace(_workspaces.IndexOf(workspace), saveCurrent: false);
        RefreshProfileDropdown();
        ScheduleSaveState();

        return profile;
    }

    private void ActivateProfile(string profileId)
    {
        profileId = MacroProfile.NormalizeId(profileId);

        if (string.Equals(_activeProfileId, profileId, StringComparison.OrdinalIgnoreCase) &&
            GetActiveProfileWorkspaces().Count > 0)
        {
            RefreshProfileDropdown();
            return;
        }

        CaptureActiveWorkspaceState();
        _activeProfileId = profileId;
        EnsureWorkspaceForProfile(_activeProfileId, _settings);

        var workspaceIndex = IndexOfFirstWorkspaceInProfile(_activeProfileId);
        if (workspaceIndex >= 0)
            ActivateWorkspace(workspaceIndex, saveCurrent: false);

        RefreshProfileDropdown();
        ScheduleSaveState();
    }

    private void RenameProfile(MacroProfile profile, string name)
    {
        if (!_profiles.Contains(profile))
            return;

        profile.Name = GetUniqueProfileName(name, profile);
        RefreshProfileDropdown();
        ScheduleSaveState();
    }

    private void DeleteProfile(MacroProfile profile)
    {
        if (!_profiles.Contains(profile))
            return;

        CaptureActiveWorkspaceState();

        var deletedProfileId = profile.Id;
        foreach (var workspace in _workspaces.Where(workspace => IsWorkspaceInProfile(workspace, deletedProfileId)))
            workspace.ProfileId = MacroProfile.NoProfileId;

        _profiles.Remove(profile);

        if (string.Equals(_activeProfileId, deletedProfileId, StringComparison.OrdinalIgnoreCase))
        {
            _activeProfileId = MacroProfile.NoProfileId;
            EnsureWorkspaceForProfile(_activeProfileId, _settings);

            var workspaceIndex = IndexOfFirstWorkspaceInProfile(_activeProfileId);
            if (workspaceIndex >= 0)
                ActivateWorkspace(workspaceIndex, saveCurrent: false);
        }
        else
        {
            RefreshMacroTabs();
            RefreshProfileDropdown();
        }

        ScheduleSaveState();
    }

    private void ReorderProfile(int sourceIndex, int targetIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= _profiles.Count)
            return;

        if (targetIndex < 0 || targetIndex >= _profiles.Count || sourceIndex == targetIndex)
            return;

        var profile = _profiles[sourceIndex];
        _profiles.RemoveAt(sourceIndex);
        _profiles.Insert(targetIndex, profile);

        RefreshProfileDropdown();
        ScheduleSaveState();
    }

    private void RefreshProfileDropdown()
    {
        _profileDropdown?.Refresh();
    }

    private void ResetProfiles()
    {
        _profiles.Clear();
        _activeProfileId = MacroProfile.NoProfileId;

        foreach (var workspace in _workspaces)
            workspace.ProfileId = MacroProfile.NoProfileId;

        RefreshProfileDropdown();
    }

    private IReadOnlyList<MacroWorkspace> GetActiveProfileWorkspaces()
    {
        return _workspaces
            .Where(workspace => IsWorkspaceInProfile(workspace, _activeProfileId))
            .ToList();
    }

    private IReadOnlyList<MacroWorkspace> GetWorkspacesForProfile(string profileId)
    {
        profileId = MacroProfile.NormalizeId(profileId);

        return _workspaces
            .Where(workspace => IsWorkspaceInProfile(workspace, profileId))
            .ToList();
    }

    private int GetActiveProfileWorkspaceIndex()
    {
        var profileWorkspaces = GetActiveProfileWorkspaces();

        for (var i = 0; i < profileWorkspaces.Count; i++)
        {
            if (ReferenceEquals(profileWorkspaces[i], _activeWorkspace))
                return i;
        }

        return -1;
    }

    private int GetGlobalWorkspaceIndexFromActiveProfileIndex(int profileWorkspaceIndex)
    {
        var profileWorkspaces = GetActiveProfileWorkspaces();
        if (profileWorkspaceIndex < 0 || profileWorkspaceIndex >= profileWorkspaces.Count)
            return -1;

        return _workspaces.IndexOf(profileWorkspaces[profileWorkspaceIndex]);
    }

    private int IndexOfFirstWorkspaceInProfile(string profileId)
    {
        profileId = MacroProfile.NormalizeId(profileId);

        for (var i = 0; i < _workspaces.Count; i++)
        {
            if (IsWorkspaceInProfile(_workspaces[i], profileId))
                return i;
        }

        return -1;
    }

    private static bool IsWorkspaceInProfile(MacroWorkspace workspace, string profileId)
    {
        return string.Equals(
            MacroProfile.NormalizeId(workspace.ProfileId),
            MacroProfile.NormalizeId(profileId),
            StringComparison.OrdinalIgnoreCase);
    }

    private string GetNextProfileName(MacroProfile? excludedProfile = null)
    {
        var usedNames = _profiles
            .Where(profile => !ReferenceEquals(profile, excludedProfile))
            .Select(profile => profile.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidate = 1;
        while (usedNames.Contains($"Profile {candidate}"))
            candidate++;

        return $"Profile {candidate}";
    }

    private string GetUniqueProfileName(string name, MacroProfile profile)
    {
        var normalizedName = NormalizeProfileName(name, GetNextProfileName(profile));
        var usedNames = _profiles
            .Where(existing => !ReferenceEquals(existing, profile))
            .Select(existing => existing.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!usedNames.Contains(normalizedName))
            return normalizedName;

        for (var i = 2;; i++)
        {
            var candidate = $"{normalizedName} {i}";
            if (!usedNames.Contains(candidate))
                return candidate;
        }
    }

    private static string NormalizeProfileName(string name, string fallback)
    {
        var normalized = string.Join(
            " ",
            name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        if (string.IsNullOrWhiteSpace(normalized))
            normalized = fallback;

        return normalized.Length <= 24
            ? normalized
            : normalized[..24];
    }
}
