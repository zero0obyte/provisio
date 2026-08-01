using System.Text.Json;
using System.Text.Json.Serialization;
using Provisio.Services;
using Provisio.State;

namespace Provisio.Services;

/// <summary>A saved multi-kit selection that can be re-applied in one click.</summary>
public class Profile
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("created")] public DateTime Created { get; set; } = DateTime.Now;
    /// <summary>kitId -> pageId -> choice ids</summary>
    [JsonPropertyName("selections")] public Dictionary<string, Dictionary<string, List<string>>> Selections { get; set; } = new();
}

public static class ProfileService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static List<Profile> LoadAll()
    {
        var list = new List<Profile>();
        if (!Directory.Exists(SettingsService.ProfilesFolder)) return list;
        foreach (var file in Directory.GetFiles(SettingsService.ProfilesFolder, "*.json"))
        {
            try
            {
                var p = JsonSerializer.Deserialize<Profile>(File.ReadAllText(file));
                if (p is not null && !string.IsNullOrWhiteSpace(p.Name)) list.Add(p);
            }
            catch { /* skip broken profile */ }
        }
        return list.OrderByDescending(p => p.Created).ToList();
    }

    /// <summary>Capture the current AppState selections as a named profile.</summary>
    public static void SaveCurrent(string name)
    {
        Directory.CreateDirectory(SettingsService.ProfilesFolder);
        var profile = new Profile { Name = name };
        foreach (var (kitId, pages) in AppState.Selections)
        {
            var pd = new Dictionary<string, List<string>>();
            foreach (var (pageId, choices) in pages)
            {
                if (choices.Count > 0) pd[pageId] = choices.ToList();
            }
            if (pd.Count > 0) profile.Selections[kitId] = pd;
        }
        var path = Path.Combine(SettingsService.ProfilesFolder, Sanitize(name) + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(profile, JsonOpts));
    }

    /// <summary>Apply a profile: activates its kits and restores all selections.</summary>
    public static void Apply(Profile profile)
    {
        AppState.Reset();
        foreach (var (kitId, pages) in profile.Selections)
        {
            var kit = KitService.Find(kitId);
            if (kit is null) continue; // kit no longer exists — skip
            if (!AppState.ActiveKits.Any(k => k.Id == kit.Id)) AppState.ActiveKits.Add(kit);
            foreach (var (pageId, choices) in pages)
                AppState.SetSelection(kitId, pageId, choices);
        }
    }

    public static void Delete(Profile profile)
    {
        var path = Path.Combine(SettingsService.ProfilesFolder, Sanitize(profile.Name) + ".json");
        if (File.Exists(path)) File.Delete(path);
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '-');
        return name;
    }
}
