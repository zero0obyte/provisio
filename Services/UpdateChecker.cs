using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Provisio.Models;

namespace Provisio.Services;

public class UpdateInfo
{
    [JsonPropertyName("appVersion")] public string AppVersion { get; set; } = "";
    [JsonPropertyName("downloadUrl")] public string DownloadUrl { get; set; } = "";
    [JsonPropertyName("notes")] public string Notes { get; set; } = "";
    /// <summary>Optional updated kit definitions shipped with the update feed.</summary>
    [JsonPropertyName("kits")] public List<Kit> Kits { get; set; } = new();
}

/// <summary>
/// On launch, checks a configurable URL for a newer app version and updated
/// kit definitions. Silently does nothing when no URL is configured or offline.
/// </summary>
public static class UpdateChecker
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public static UpdateInfo? LastResult { get; private set; }

    public static event EventHandler<UpdateInfo>? UpdateAvailable;

    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public static async Task CheckAsync()
    {
        if (string.IsNullOrWhiteSpace(SettingsService.UpdateUrl)) return;
        try
        {
            var json = await Http.GetStringAsync(SettingsService.UpdateUrl);
            var info = JsonSerializer.Deserialize<UpdateInfo>(json);
            if (info is null) return;

            // Updated kit definitions are dropped into the custom kits folder,
            // where they override the bundled ones on next load.
            var kitsUpdated = false;
            foreach (var kit in info.Kits)
            {
                if (string.IsNullOrWhiteSpace(kit.Id)) continue;
                KitService.SaveKit(kit);
                kitsUpdated = true;
            }
            if (kitsUpdated) KitService.LoadAll();

            if (Version.TryParse(info.AppVersion, out var remote) &&
                Version.TryParse(CurrentVersion, out var local) &&
                remote > local)
            {
                LastResult = info;
                UpdateAvailable?.Invoke(null, info);
            }
        }
        catch { /* offline or bad feed — ignore */ }
    }
}
