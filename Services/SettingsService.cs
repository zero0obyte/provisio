using System.Text.Json;

namespace Provisio.Services;

/// <summary>
/// App settings persisted to %LOCALAPPDATA%\Provisio\settings.json.
/// (File-based so it also works in the unpackaged single-exe build.)
/// New settings only need a property here plus a line in <see cref="SettingsDto"/>.
/// </summary>
public static class SettingsService
{
    /// <summary>"system" | "light" | "dark".</summary>
    public static string Theme { get; set; } = "dark";

    /// <summary>Interface language code, see <see cref="Loc.Languages"/>.</summary>
    public static string Language { get; set; } = "";

    public static string UpdateUrl { get; set; } = "";
    public static bool CacheInstallers { get; set; } = true;

    /// <summary>Look for newer versions of installed packages on launch.</summary>
    public static bool CheckPackageUpdates { get; set; } = true;

    /// <summary>Install those updates without asking (off by default — it changes the machine).</summary>
    public static bool AutoInstallPackageUpdates { get; set; }

    /// <summary>One-time pop-ups that have already been acknowledged.</summary>
    public static bool HasSeenWelcome { get; set; }
    public static bool HasSeenImportWarning { get; set; }

    /// <summary>Back-compat helper for the pane toggle: light vs. dark.</summary>
    public static bool LightMode
    {
        get => Theme == "light";
        set => Theme = value ? "light" : "dark";
    }

    public static string DataFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Provisio");

    public static string CustomKitsFolder => Path.Combine(DataFolder, "Kits");
    public static string ProfilesFolder => Path.Combine(DataFolder, "Profiles");
    public static string InstallerCacheFolder => Path.Combine(DataFolder, "Cache");

    private static string SettingsFile => Path.Combine(DataFolder, "settings.json");

    private class SettingsDto
    {
        public string? Theme { get; set; }
        public string? Language { get; set; }
        public bool LightMode { get; set; }            // pre-1.1 setting, migrated on load
        public string UpdateUrl { get; set; } = "";
        public bool CacheInstallers { get; set; } = true;
        public bool CheckPackageUpdates { get; set; } = true;
        public bool AutoInstallPackageUpdates { get; set; }
        public bool HasSeenWelcome { get; set; }
        public bool HasSeenImportWarning { get; set; }
    }

    /// <summary>True when no settings file existed yet — i.e. this is a fresh install.</summary>
    public static bool IsFirstRun { get; private set; }

    public static void Load()
    {
        Directory.CreateDirectory(DataFolder);
        Directory.CreateDirectory(CustomKitsFolder);
        Directory.CreateDirectory(ProfilesFolder);
        Directory.CreateDirectory(InstallerCacheFolder);

        try
        {
            if (File.Exists(SettingsFile))
            {
                var dto = JsonSerializer.Deserialize<SettingsDto>(File.ReadAllText(SettingsFile));
                if (dto is not null)
                {
                    Theme = string.IsNullOrWhiteSpace(dto.Theme)
                        ? (dto.LightMode ? "light" : "dark")   // migrate the old bool
                        : dto.Theme!;
                    Language = dto.Language ?? "";
                    UpdateUrl = dto.UpdateUrl ?? "";
                    CacheInstallers = dto.CacheInstallers;
                    CheckPackageUpdates = dto.CheckPackageUpdates;
                    AutoInstallPackageUpdates = dto.AutoInstallPackageUpdates;
                    HasSeenWelcome = dto.HasSeenWelcome;
                    HasSeenImportWarning = dto.HasSeenImportWarning;
                }
            }
            else
            {
                IsFirstRun = true;
            }
        }
        catch { /* corrupt settings — use defaults */ }

        Loc.SetLanguage(string.IsNullOrWhiteSpace(Language) ? Loc.SystemLanguage() : Language, notify: false);
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(DataFolder);
            var dto = new SettingsDto
            {
                Theme = Theme,
                Language = Language,
                LightMode = Theme == "light",
                UpdateUrl = UpdateUrl,
                CacheInstallers = CacheInstallers,
                CheckPackageUpdates = CheckPackageUpdates,
                AutoInstallPackageUpdates = AutoInstallPackageUpdates,
                HasSeenWelcome = HasSeenWelcome,
                HasSeenImportWarning = HasSeenImportWarning
            };
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* non-fatal */ }
    }
}
