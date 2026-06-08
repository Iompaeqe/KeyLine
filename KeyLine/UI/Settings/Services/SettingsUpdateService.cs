using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyLine;

internal sealed class SettingsUpdateService
{
    private const string GitHubLatestReleaseApi = "https://api.github.com/repos/Iompaeqe/KeyLine/releases/latest";
    private const string GitHubReleasesPage = "https://github.com/Iompaeqe/KeyLine/releases";

    private Task<UpdateCheckResult>? _updateCheckTask;

    public string CurrentVersionText => GetCurrentVersionText();

    public UpdateCheckResult? LastUpdateCheckResult { get; private set; }

    public event EventHandler<UpdateCheckResult>? UpdateCheckCompleted;

    public Task<UpdateCheckResult> CheckForUpdatesAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && LastUpdateCheckResult != null)
            return Task.FromResult(LastUpdateCheckResult);

        if (_updateCheckTask is { IsCompleted: false })
            return _updateCheckTask;

        _updateCheckTask = CheckForUpdatesCoreAsync();
        return _updateCheckTask;
    }

    public void OpenReleasesPage(string? releaseUrl = null)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = string.IsNullOrWhiteSpace(releaseUrl)
                ? GitHubReleasesPage
                : releaseUrl,
            UseShellExecute = true
        });
    }

    private async Task<UpdateCheckResult> CheckForUpdatesCoreAsync()
    {
        UpdateCheckResult result;

        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(8)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("KeyLine");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            using var response = await client.GetAsync(GitHubLatestReleaseApi);

            if (!response.IsSuccessStatusCode)
            {
                result = new UpdateCheckResult(UpdateCheckState.Failed, "Update check failed - retry");
                return PublishUpdateCheckResult(result);
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream);

            if (release == null || string.IsNullOrWhiteSpace(release.TagName))
            {
                result = new UpdateCheckResult(UpdateCheckState.Failed, "Update check failed - retry");
                return PublishUpdateCheckResult(result);
            }

            var currentVersion = TryParseVersion(GetCurrentVersionText());
            var latestVersion = TryParseVersion(release.TagName);

            if (currentVersion == null || latestVersion == null)
            {
                result = new UpdateCheckResult(
                    UpdateCheckState.UpdateAvailable,
                    $"Latest release: {release.TagName} - install",
                    release.HtmlUrl);
                return PublishUpdateCheckResult(result);
            }

            if (latestVersion > currentVersion)
            {
                result = new UpdateCheckResult(
                    UpdateCheckState.UpdateAvailable,
                    $"Update available: {release.TagName} - install",
                    release.HtmlUrl);
                return PublishUpdateCheckResult(result);
            }

            result = new UpdateCheckResult(
                UpdateCheckState.Latest,
                "Latest version");
            return PublishUpdateCheckResult(result);
        }
        catch
        {
            result = new UpdateCheckResult(
                UpdateCheckState.Failed,
                "Update check failed - retry");
            return PublishUpdateCheckResult(result);
        }
    }

    private UpdateCheckResult PublishUpdateCheckResult(UpdateCheckResult result)
    {
        if (result.State == UpdateCheckState.Failed &&
            LastUpdateCheckResult?.State == UpdateCheckState.UpdateAvailable)
        {
            UpdateCheckCompleted?.Invoke(this, LastUpdateCheckResult);
            return LastUpdateCheckResult;
        }

        LastUpdateCheckResult = result;
        UpdateCheckCompleted?.Invoke(this, result);
        return result;
    }

    private static string GetCurrentVersionText()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(version))
            return CleanVersionText(version);

        return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static Version? TryParseVersion(string value)
    {
        value = CleanVersionText(value);

        return Version.TryParse(value, out var version)
            ? version
            : null;
    }

    private static string CleanVersionText(string value)
    {
        value = value.Trim();

        if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            value = value[1..];

        var plusIndex = value.IndexOf('+');
        if (plusIndex >= 0)
            value = value[..plusIndex];

        var dashIndex = value.IndexOf('-');
        if (dashIndex >= 0)
            value = value[..dashIndex];

        return value.Trim();
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = "";

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = "";
    }
}
