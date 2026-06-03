namespace KeyLine.Domain;

public sealed class MacroProfile
{
    public const string NoProfileId = "";
    public const string NoProfileName = "No Profile";

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "Profile";

    public static string NormalizeId(string? id)
    {
        return string.IsNullOrWhiteSpace(id) ? NoProfileId : id.Trim();
    }

    public static bool IsNoProfile(string? id)
    {
        return string.IsNullOrWhiteSpace(id);
    }
}
