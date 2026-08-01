using System.Text.Json;
using Provisio.Models;

namespace Provisio.Services;

/// <summary>
/// Loads built-in kit definitions (Assets/Kits/*.json) plus user kits from
/// %LOCALAPPDATA%/Provisio/Kits (*.provisio-kit). Handles import/export/save.
/// </summary>
public static class KitService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static List<Kit> Kits { get; } = new();

    public static event EventHandler? KitsChanged;

    public static void LoadAll()
    {
        Kits.Clear();

        // Built-in kits are compiled into the assembly so the single-file exe works on its
        // own; an Assets\Kits folder next to the exe overrides them when present.
        var assembly = typeof(KitService).Assembly;
        foreach (var name in assembly.GetManifestResourceNames()
                     .Where(n => n.Contains(".Assets.Kits.") && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(n => n))
        {
            try
            {
                using var stream = assembly.GetManifestResourceStream(name);
                if (stream is null) continue;
                using var reader = new StreamReader(stream);
                var kit = JsonSerializer.Deserialize<Kit>(reader.ReadToEnd());
                if (kit is null || string.IsNullOrWhiteSpace(kit.Id)) continue;
                kit.Builtin = true;
                Kits.Add(kit);
            }
            catch { /* skip broken embedded kit */ }
        }

        var kitDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Kits");
        if (Directory.Exists(kitDir))
        {
            foreach (var file in Directory.GetFiles(kitDir, "*.json").OrderBy(f => f))
            {
                var kit = ReadKit(file);
                if (kit is null) continue;
                kit.Builtin = true;
                var existing = Kits.FindIndex(k => k.Id == kit.Id);
                if (existing >= 0) Kits[existing] = kit;
                else Kits.Add(kit);
            }
        }

        // User / imported kits — same id overrides a built-in one
        if (Directory.Exists(SettingsService.CustomKitsFolder))
        {
            foreach (var file in Directory.GetFiles(SettingsService.CustomKitsFolder, "*.provisio-kit").OrderBy(f => f))
            {
                var kit = ReadKit(file);
                if (kit is null) continue;
                kit.Builtin = false;
                var existing = Kits.FindIndex(k => k.Id == kit.Id);
                if (existing >= 0) Kits[existing] = kit;
                else Kits.Add(kit);
            }
        }

        KitsChanged?.Invoke(null, EventArgs.Empty);
    }

    private static Kit? ReadKit(string path)
    {
        try
        {
            var kit = JsonSerializer.Deserialize<Kit>(File.ReadAllText(path));
            return kit is not null && !string.IsNullOrWhiteSpace(kit.Id) ? kit : null;
        }
        catch
        {
            return null; // skip broken kit files instead of crashing
        }
    }

    public static Kit? Find(string id) => Kits.FirstOrDefault(k => k.Id == id);

    /// <summary>Persist a (custom) kit to the user kits folder.</summary>
    public static void SaveKit(Kit kit)
    {
        Directory.CreateDirectory(SettingsService.CustomKitsFolder);
        kit.Builtin = false;
        var path = Path.Combine(SettingsService.CustomKitsFolder, $"{Sanitize(kit.Id)}.provisio-kit");
        File.WriteAllText(path, JsonSerializer.Serialize(kit, JsonOpts));
        LoadAll();
    }

    public static void DeleteKit(Kit kit)
    {
        var path = Path.Combine(SettingsService.CustomKitsFolder, $"{Sanitize(kit.Id)}.provisio-kit");
        if (File.Exists(path)) File.Delete(path);
        LoadAll();
    }

    public static string ExportKit(Kit kit, string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(kit, JsonOpts));
        return path;
    }

    public static Kit? ImportKit(string path)
    {
        var kit = ReadKit(path);
        if (kit is null) return null;
        SaveKit(kit);
        return kit;
    }

    public static Kit DuplicateKit(Kit source)
    {
        var json = JsonSerializer.Serialize(source);
        var copy = JsonSerializer.Deserialize<Kit>(json)!;
        copy.Id = source.Id + "-copy-" + DateTime.Now.ToString("HHmmss");
        copy.Name = source.Name + " (Copy)";
        copy.Builtin = false;
        return copy;
    }

    private static string Sanitize(string id)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) id = id.Replace(c, '-');
        return id;
    }
}
