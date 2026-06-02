using MacroSpammer.Domain;

namespace MacroSpammer.Services.Macro;

public static class WorkspaceNameService
{
    public static string GetUniqueName(
        IEnumerable<MacroWorkspace> existingWorkspaces,
        string preferredName,
        string fallbackName = "Imported Macro")
    {
        var baseName = string.IsNullOrWhiteSpace(preferredName)
            ? fallbackName
            : preferredName.Trim();

        if (existingWorkspaces.All(workspace =>
                !string.Equals(workspace.Name, baseName, StringComparison.OrdinalIgnoreCase)))
        {
            return baseName;
        }

        for (var i = 2;; i++)
        {
            var candidate = $"{baseName} {i}";

            if (existingWorkspaces.All(workspace =>
                    !string.Equals(workspace.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }
    }

    public static string GetUniqueDuplicateName(
        IEnumerable<MacroWorkspace> existingWorkspaces,
        string sourceName)
    {
        var baseName = string.IsNullOrWhiteSpace(sourceName)
            ? "Macro"
            : sourceName.Trim();

        var preferredName = $"{baseName} - dub";

        if (existingWorkspaces.All(workspace =>
                !string.Equals(workspace.Name, preferredName, StringComparison.OrdinalIgnoreCase)))
        {
            return preferredName;
        }

        for (var i = 2;; i++)
        {
            var candidate = $"{preferredName} {i}";

            if (existingWorkspaces.All(workspace =>
                    !string.Equals(workspace.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }
    }
}