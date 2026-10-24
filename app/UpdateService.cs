using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gta6Countdown;

public sealed class UpdateCheckResult
{
    public static UpdateCheckResult Unknown => new() { State = "unknown" };

    [JsonPropertyName("state")]
    public string State { get; set; } = "unknown";

    [JsonPropertyName("latestVersion")]
    public string? LatestVersion { get; set; }

    [JsonPropertyName("releaseUrl")]
    public string? ReleaseUrl { get; set; }

    [JsonPropertyName("publishedAt")]
    public string? PublishedAt { get; set; }

    [JsonPropertyName("checkedAt")]
    public string? CheckedAt { get; set; }
}

public sealed class ReleaseInfo
{
    public string? TagName { get; set; }
    public string? HtmlUrl { get; set; }
    public bool Draft { get; set; }
    public string? PublishedAt { get; set; }
}

internal static class UpdateService
{
    // Change these two values after publishing the repository.
    private const string GitHubOwner = "your-github-name";
    private const string GitHubRepository = "gta6-desktop-countdown";

    private const string CheckedStateFile = "update-state.json";
    private const string ResultCacheFile = "update-result.json";
    private const string PendingVersionFile = "pending-update-version.txt";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(12);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);

    private static UpdateCheckResult _cached = LoadCachedResult();
    private static DateTimeOffset? _lastCheck = LoadLastCheck();

    public static string CurrentVersion { get; } = ReadCurrentVersion();

    public static string RepositoryUrl => $"https://github.com/{GitHubOwner}/{GitHubRepository}";

    public static string ReleasesPageUrl => $"{RepositoryUrl}/releases/latest";

    public static string LatestReleaseUrl { get; private set; } = ReleasesPageUrl;

    public static bool CachedResultKnown => _cached.State != "unknown";

    /// <summary>
    /// Set once per process when a check discovers a newer release and the
    /// update notice has not been shown yet in this session.
    /// </summary>
    public static string? PendingAnnouncement { get; set; }

    private static string ReadCurrentVersion()
    {
        var version = typeof(UpdateService).Assembly.GetName().Version;
        if (version is null) return "1.2.0";
        return $"{version.Major}.{Math.Max(0, version.Minor)}.{Math.Max(0, version.Build)}";
    }

    public static bool IsNewer(string? candidate, string baseline)
    {
        if (!TryParseVersion(candidate, out var candidateVersion)) return false;
        if (!TryParseVersion(baseline, out var baselineVersion)) return false;
        return candidateVersion > baselineVersion;
    }

    private static bool TryParseVersion(string? value, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(value)) return false;

        var text = value.Trim().TrimStart('v', 'V');
        var cut = text.IndexOfAny(new[] { '-', '+' });
        if (cut >= 0)
        {
            text = text[..cut];
        }

        if (!Version.TryParse(text, out var parsed)) return false;
        version = parsed;
        return true;
    }

    public static async Task<UpdateCheckResult> CheckAsync(bool force = false)
    {
        if (!force && CachedResultKnown && _lastCheck is { } last && DateTimeOffset.UtcNow - last < CheckInterval)
        {
            return _cached;
        }

        if (GitHubOwner.StartsWith("your-", StringComparison.OrdinalIgnoreCase))
        {
            _cached = new UpdateCheckResult { State = "unconfigured", CheckedAt = DateTimeOffset.UtcNow.ToString("O") };
            _lastCheck = DateTimeOffset.UtcNow;
            return _cached;
        }

        var stamp = DateTimeOffset.UtcNow;
        UpdateCheckResult result;

        try
        {
            using var client = new HttpClient { Timeout = Timeout };
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"GTA6Countdown/{CurrentVersion}");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");

            var apiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepository}/releases/latest";
            using var apiResponse = await client.GetAsync(apiUrl).ConfigureAwait(false);
            if (!apiResponse.IsSuccessStatusCode)
            {
                result = new UpdateCheckResult { State = "unknown", CheckedAt = stamp.ToString("O") };
            }
            else
            {
                var json = await apiResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                var release = JsonSerializer.Deserialize<ReleaseInfo>(json, SettingsService.JsonOptions);
                var latest = release?.TagName;
                var isNewer = release is { Draft: false } && IsNewer(latest, CurrentVersion);

                if (release?.HtmlUrl is { Length: > 0 } releaseUrl)
                {
                    LatestReleaseUrl = releaseUrl;
                }

                result = new UpdateCheckResult
                {
                    State = isNewer ? "available" : "up-to-date",
                    LatestVersion = latest?.TrimStart('v', 'V'),
                    ReleaseUrl = release?.HtmlUrl ?? ReleasesPageUrl,
                    PublishedAt = release?.PublishedAt,
                    CheckedAt = stamp.ToString("O")
                };
            }
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Update check failed: {ex.Message}");
            result = new UpdateCheckResult { State = "unknown", CheckedAt = stamp.ToString("O") };
        }

        _cached = result;
        _lastCheck = stamp;
        PersistResult(result);

        if (result.State == "available")
        {
            TryWriteText(Path.Combine(WidgetPaths.DataDirectory, PendingVersionFile), result.LatestVersion ?? string.Empty);
            PendingAnnouncement ??= result.LatestVersion;
        }

        WidgetLog.Write($"Update check: state={result.State}, latest={result.LatestVersion ?? "-"}, current={CurrentVersion}.");
        return result;
    }

    public static bool TryTakePendingVersion(out string version)
    {
        version = string.Empty;
        try
        {
            var path = Path.Combine(WidgetPaths.DataDirectory, PendingVersionFile);
            if (!File.Exists(path)) return false;

            var text = File.ReadAllText(path).Trim();
            File.Delete(path);

            if (string.IsNullOrWhiteSpace(text) || !IsNewer(text, CurrentVersion)) return false;

            version = text.TrimStart('v', 'V');
            return true;
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Pending update read failed: {ex.Message}");
            return false;
        }
    }

    public static void OpenReleasePage()
    {
        try
        {
            Process.Start(new ProcessStartInfo(LatestReleaseUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Opening release page failed: {ex.Message}");
        }
    }

    private static void PersistResult(UpdateCheckResult result)
    {
        try
        {
            Directory.CreateDirectory(WidgetPaths.DataDirectory);
            File.WriteAllText(
                Path.Combine(WidgetPaths.DataDirectory, ResultCacheFile),
                JsonSerializer.Serialize(result, SettingsService.JsonOptions));
            File.WriteAllText(
                Path.Combine(WidgetPaths.DataDirectory, CheckedStateFile),
                result.CheckedAt ?? DateTimeOffset.UtcNow.ToString("O"));
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Update cache write failed: {ex.Message}");
        }
    }

    private static UpdateCheckResult LoadCachedResult()
    {
        try
        {
            var path = Path.Combine(WidgetPaths.DataDirectory, ResultCacheFile);
            if (!File.Exists(path)) return UpdateCheckResult.Unknown;

            var result = JsonSerializer.Deserialize<UpdateCheckResult>(
                File.ReadAllText(path),
                SettingsService.JsonOptions);
            return result ?? UpdateCheckResult.Unknown;
        }
        catch
        {
            return UpdateCheckResult.Unknown;
        }
    }

    private static DateTimeOffset? LoadLastCheck()
    {
        try
        {
            var path = Path.Combine(WidgetPaths.DataDirectory, CheckedStateFile);
            if (!File.Exists(path)) return null;

            return DateTimeOffset.TryParse(File.ReadAllText(path).Trim(), out var stamp) ? stamp : null;
        }
        catch
        {
            return null;
        }
    }

    private static void TryWriteText(string path, string content)
    {
        try
        {
            Directory.CreateDirectory(WidgetPaths.DataDirectory);
            File.WriteAllText(path, content);
        }
        catch (Exception ex)
        {
            WidgetLog.Write($"Writing {Path.GetFileName(path)} failed: {ex.Message}");
        }
    }
}
